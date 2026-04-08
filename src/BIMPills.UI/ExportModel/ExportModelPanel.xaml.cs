using BIMPills.Core.Models;
using BIMPills.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;

namespace BIMPills.UI.ExportModel
{
    public partial class ExportModelPanel : UserControl
    {
        private string _modelTitle = "";
        private string? _activeViewName;
        private string? _selectedFolder;
        private bool _nwcAvailable = true;
        private ILogger? _logger;
        private Func<NwcExportConfig, bool>? _nwcExportCallback;

        private List<string> _availableParameters = new();
        private Dictionary<string, string> _parameterValues = new();
        private List<NwcExportPreset> _presets = new();
        private List<NwcViewInfo> _availableViews = new();
        private List<NwcViewInfo> _filteredViews = new();

        private string _exportLabel = "Exportar NWC";

        /// <summary>Raised when export availability changes.</summary>
        public event EventHandler<bool>? ExportEnabledChanged;

        public ExportModelPanel()
        {
            InitializeComponent();
        }

        /// <summary>Trigger export from external button.</summary>
        public void TriggerExport() => DoExport();

        /// <summary>Get export button label.</summary>
        public string ExportLabel => _exportLabel;

        /// <summary>Initializes the panel.</summary>
        public void Initialize(
            string modelTitle,
            string? activeViewName = null,
            bool nwcAvailable = true,
            Func<NwcExportConfig, bool>? nwcExportCallback = null,
            ILogger? logger = null,
            IReadOnlyList<string>? availableParameters = null,
            IReadOnlyDictionary<string, string>? parameterValues = null,
            IReadOnlyList<NwcExportPreset>? presets = null,
            IReadOnlyList<NwcViewInfo>? availableViews = null)
        {
            _modelTitle = modelTitle;
            _activeViewName = activeViewName;
            _nwcAvailable = nwcAvailable;
            _nwcExportCallback = nwcExportCallback;
            _logger = logger;

            if (availableParameters != null)
                _availableParameters = new List<string>(availableParameters);
            if (parameterValues != null)
                _parameterValues = parameterValues.ToDictionary(kv => kv.Key, kv => kv.Value);
            if (availableViews != null)
                _availableViews = new List<NwcViewInfo>(availableViews);

            FileNameBox.Text = System.IO.Path.GetFileNameWithoutExtension(modelTitle);
            BuildParamUI();
            LoadPresets(presets);
            PopulateViewsList(_availableViews);

            if (!_nwcAvailable)
            {
                NwcOptionsPanel.IsEnabled = false;
                NwcOptionsPanel.Opacity = 0.5;
                NwcUnavailableOverlay.Visibility = Visibility.Visible;
            }

            UpdateExportState();
        }

        // ── Param chips ──────────────────────────────────────────────────────────

        private void BuildParamUI()
        {
            ParamChipsPanel.Children.Clear();
            ParamSelectorCombo.Items.Clear();

            if (_availableParameters.Count == 0) return;

            int chipCount = Math.Min(_availableParameters.Count, 5);
            for (int i = 0; i < chipCount; i++)
            {
                var param = _availableParameters[i];
                var chip = new Button
                {
                    Content = param,
                    Tag = param,
                    Margin = new Thickness(0, 0, 6, 4),
                    Padding = new Thickness(8, 3, 8, 3),
                    FontSize = 11,
                    FontFamily = new FontFamily("Segoe UI"),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xF0, 0xFE)),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0xBB, 0xD2, 0xF5)),
                    BorderThickness = new Thickness(1)
                };
                chip.Click += Chip_Click;
                ParamChipsPanel.Children.Add(chip);
            }

            foreach (var param in _availableParameters)
                ParamSelectorCombo.Items.Add(new ComboBoxItem { Content = param, Tag = param });
            if (ParamSelectorCombo.Items.Count > 0)
                ParamSelectorCombo.SelectedIndex = 0;

            ParamSelectorRow.Visibility = Visibility.Visible;
        }

        private void Chip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
                InsertToken(btn.Tag?.ToString() ?? "");
        }

        private void AddParam_Click(object sender, RoutedEventArgs e)
        {
            var param = (ParamSelectorCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (!string.IsNullOrEmpty(param)) InsertToken(param);
        }

        private void InsertToken(string paramName)
        {
            if (string.IsNullOrEmpty(paramName)) return;
            var token = $"{{{paramName}}}";
            var pos = FileNameBox.SelectionStart;
            var text = FileNameBox.Text ?? "";
            FileNameBox.Text = text.Insert(pos, token);
            FileNameBox.SelectionStart = pos + token.Length;
            FileNameBox.Focus();
        }

        // ── Views picker ─────────────────────────────────────────────────────────

        private void PopulateViewsList(IEnumerable<NwcViewInfo> views)
        {
            _filteredViews = new List<NwcViewInfo>(views);
            RebuildViewsListBox();
        }

        private void RebuildViewsListBox()
        {
            ViewsListBox.Items.Clear();
            foreach (var v in _filteredViews)
                ViewsListBox.Items.Add(new ListBoxItem { Content = v.Name, Tag = v.ElementId });

            bool empty = ViewsListBox.Items.Count == 0;
            ViewsEmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            if (!empty) ViewsListBox.SelectedIndex = 0;
        }

        private void Scope_Changed(object sender, RoutedEventArgs e)
        {
            if (ViewPickerPanel == null) return;
            ViewPickerPanel.Visibility = ScopeSpecificView?.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;
            UpdateExportState();
        }

        private void ViewSearch_Changed(object sender, TextChangedEventArgs e)
        {
            var q = ViewSearchBox.Text?.Trim() ?? "";
            _filteredViews = string.IsNullOrEmpty(q)
                ? new List<NwcViewInfo>(_availableViews)
                : _availableViews.FindAll(v => v.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            RebuildViewsListBox();
        }

        private void ViewsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateExportState();
        }

        private long? GetSelectedViewId()
        {
            if (ScopeSpecificView?.IsChecked != true) return null;
            return (ViewsListBox.SelectedItem as ListBoxItem)?.Tag is long id ? id : (long?)null;
        }

        // ── Presets ──────────────────────────────────────────────────────────────

        private void LoadPresets(IReadOnlyList<NwcExportPreset>? presets)
        {
            if (presets != null)
                _presets = new List<NwcExportPreset>(presets);

            PresetCombo.Items.Clear();
            PresetCombo.Items.Add(new ComboBoxItem { Content = "(ninguno)", Tag = "" });
            foreach (var p in _presets)
                PresetCombo.Items.Add(new ComboBoxItem { Content = p.Name, Tag = p.Id });
            PresetCombo.SelectedIndex = 0;
            DeletePresetBtn.IsEnabled = false;
        }

        private void Preset_Changed(object sender, SelectionChangedEventArgs e)
        {
            var id = (PresetCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            DeletePresetBtn.IsEnabled = !string.IsNullOrEmpty(id);

            if (string.IsNullOrEmpty(id)) return;
            var preset = _presets.Find(p => p.Id == id);
            if (preset != null) ApplyConfig(preset.Config);
        }

        private void SavePreset_Click(object sender, RoutedEventArgs e)
        {
            var name = PromptForPresetName();
            if (string.IsNullOrWhiteSpace(name)) return;

            var config = GetConfig();
            var preset = new NwcExportPreset { Name = name, Config = config };
            _presets.Add(preset);

            PresetCombo.Items.Add(new ComboBoxItem { Content = name, Tag = preset.Id });
            PresetCombo.SelectedIndex = PresetCombo.Items.Count - 1;
        }

        private void DeletePreset_Click(object sender, RoutedEventArgs e)
        {
            var id = (PresetCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (string.IsNullOrEmpty(id)) return;

            var msg = MessageBox.Show("&#x00BF;Eliminar el conjunto seleccionado?",
                "BIM Pills \u2014 Eliminar conjunto",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (msg != MessageBoxResult.Yes) return;

            _presets.RemoveAll(p => p.Id == id);
            var idx = PresetCombo.SelectedIndex;
            PresetCombo.Items.RemoveAt(idx);
            PresetCombo.SelectedIndex = 0;
        }

        private string? PromptForPresetName()
        {
            var dlg = new Window
            {
                Title = "BIM Pills \u2014 Guardar conjunto",
                Width = 340, SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Owner = Window.GetWindow(this)
            };
            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock
            {
                Text = "Nombre del conjunto:",
                FontSize = 12, FontFamily = new FontFamily("Segoe UI"),
                Margin = new Thickness(0, 0, 0, 8)
            });
            var tb = new TextBox
            {
                FontSize = 12, FontFamily = new FontFamily("Segoe UI"),
                Padding = new Thickness(6, 5, 6, 5),
                Margin = new Thickness(0, 0, 0, 16)
            };
            panel.Children.Add(tb);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnOk = new Button
            {
                Content = "Guardar", Width = 80, Height = 30,
                FontFamily = new FontFamily("Segoe UI"), FontSize = 12,
                Margin = new Thickness(0, 0, 8, 0), IsDefault = true
            };
            var btnCancel = new Button
            {
                Content = "Cancelar", Width = 80, Height = 30,
                FontFamily = new FontFamily("Segoe UI"), FontSize = 12,
                IsCancel = true
            };
            string? result = null;
            btnOk.Click += (_, __) => { result = tb.Text.Trim(); dlg.DialogResult = true; };
            btnCancel.Click += (_, __) => { dlg.DialogResult = false; };
            btnRow.Children.Add(btnOk);
            btnRow.Children.Add(btnCancel);
            panel.Children.Add(btnRow);
            dlg.Content = panel;

            tb.Focus();
            dlg.ShowDialog();
            return result;
        }

        // ── Config ───────────────────────────────────────────────────────────────

        /// <summary>Builds NwcExportConfig from current UI state.</summary>
        public NwcExportConfig GetConfig()
        {
            var template = FileNameBox?.Text ?? "";
            return new NwcExportConfig
            {
                Scope = ScopeSpecificView?.IsChecked == true ? NwcExportScope.SpecificView
                      : ScopeSelection.IsChecked == true  ? NwcExportScope.Selection
                      : NwcExportScope.Model,
                ViewId = GetSelectedViewId(),
                ExportLinks = ExportLinksCheck.IsChecked == true,
                Coordinates = (CoordinatesCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "Internal"
                    ? NwcCoordinates.Internal : NwcCoordinates.Shared,
                Parameters = (ParametersCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() switch
                {
                    "Elements" => NwcParameters.Elements,
                    "None"     => NwcParameters.None,
                    _          => NwcParameters.All
                },
                FacetingPrecision = (FacetingCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() switch
                {
                    "Low"  => NwcFacetingPrecision.Low,
                    "High" => NwcFacetingPrecision.High,
                    _      => NwcFacetingPrecision.Medium
                },
                ConvertElementProperties = ConvertPropertiesCheck.IsChecked == true,
                ExportRoomAsAttribute = RoomAttributeCheck.IsChecked == true,
                ExportRoomGeometry = RoomGeometryCheck.IsChecked == true,
                DivideFileIntoLevels = DivideByLevelsCheck.IsChecked == true,
                ExportUrls = ExportUrlsCheck.IsChecked == true,
                FindMissingMaterials = FindMaterialsCheck.IsChecked == true,
                DestinationFolder = _selectedFolder ?? "",
                FileNameTemplate = template,
                FileName = GetResolvedFileName()
            };
        }

        /// <summary>Restores UI controls from a saved config.</summary>
        private void ApplyConfig(NwcExportConfig c)
        {
            ScopeModel.IsChecked        = c.Scope == NwcExportScope.Model;
            ScopeSpecificView.IsChecked = c.Scope == NwcExportScope.SpecificView;
            ScopeSelection.IsChecked    = c.Scope == NwcExportScope.Selection;

            if (c.Scope == NwcExportScope.SpecificView && c.ViewId.HasValue)
            {
                ViewPickerPanel.Visibility = Visibility.Visible;
                foreach (ListBoxItem item in ViewsListBox.Items)
                    if (item.Tag is long id && id == c.ViewId.Value)
                    { ViewsListBox.SelectedItem = item; break; }
            }

            ExportLinksCheck.IsChecked = c.ExportLinks;

            SelectComboByTag(CoordinatesCombo, c.Coordinates == NwcCoordinates.Internal ? "Internal" : "Shared");
            SelectComboByTag(ParametersCombo, c.Parameters switch
            {
                NwcParameters.Elements => "Elements",
                NwcParameters.None => "None",
                _ => "All"
            });
            SelectComboByTag(FacetingCombo, c.FacetingPrecision switch
            {
                NwcFacetingPrecision.Low  => "Low",
                NwcFacetingPrecision.High => "High",
                _ => "Medium"
            });

            ConvertPropertiesCheck.IsChecked = c.ConvertElementProperties;
            RoomAttributeCheck.IsChecked     = c.ExportRoomAsAttribute;
            RoomGeometryCheck.IsChecked      = c.ExportRoomGeometry;
            DivideByLevelsCheck.IsChecked    = c.DivideFileIntoLevels;
            ExportUrlsCheck.IsChecked        = c.ExportUrls;
            FindMaterialsCheck.IsChecked     = c.FindMissingMaterials;

            if (!string.IsNullOrEmpty(c.DestinationFolder))
            {
                _selectedFolder = c.DestinationFolder;
                DestinationBox.Text = c.DestinationFolder;
            }

            FileNameBox.Text = !string.IsNullOrEmpty(c.FileNameTemplate)
                ? c.FileNameTemplate : c.FileName;

            UpdateExportState();
        }

        private static void SelectComboByTag(ComboBox combo, string tag)
        {
            foreach (ComboBoxItem item in combo.Items)
                if (item.Tag?.ToString() == tag) { combo.SelectedItem = item; return; }
        }

        private string GetResolvedFileName()
        {
            var template = FileNameBox?.Text ?? "";
            if (string.IsNullOrWhiteSpace(template)) return "";
            foreach (var kvp in _parameterValues)
                template = template.Replace($"{{{kvp.Key}}}", kvp.Value);
            return template.Trim();
        }

        private void UpdateExportState()
        {
            if (FilePreview == null || StatusText == null) return;

            var fileName = GetResolvedFileName();
            bool viewOk = ScopeSpecificView?.IsChecked != true || GetSelectedViewId().HasValue;
            bool canExport = _nwcAvailable
                && !string.IsNullOrEmpty(_selectedFolder)
                && !string.IsNullOrWhiteSpace(fileName)
                && viewOk;

            ExportEnabledChanged?.Invoke(this, canExport);

            if (!string.IsNullOrEmpty(fileName) && !string.IsNullOrEmpty(_selectedFolder))
                FilePreview.Text = $"{System.IO.Path.GetFileName(_selectedFolder)}\\{fileName}";
            else if (!string.IsNullOrEmpty(fileName))
                FilePreview.Text = fileName;
            else
                FilePreview.Text = "(sin nombre)";

            StatusText.Text = canExport ? "Listo para exportar" : "Selecciona carpeta y nombre de archivo";
        }

        // ── Export ───────────────────────────────────────────────────────────────

        private void DoExport()
        {
            try
            {
                if (!_nwcAvailable)
                {
                    MessageBox.Show(
                        "Navisworks NWC Export Utility no est\u00e1 instalado.",
                        "BIM Pills \u2014 NWC no disponible",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var config = GetConfig();
                if (string.IsNullOrEmpty(config.DestinationFolder) || string.IsNullOrEmpty(config.FileName))
                {
                    MessageBox.Show("Selecciona una carpeta destino y define el nombre de archivo.",
                        "BIM Pills \u2014 Exportar Modelo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var viewName = config.ViewId.HasValue
                    ? _availableViews.Find(v => v.ElementId == config.ViewId.Value)?.Name ?? "Vista 3D"
                    : "Vista 3D";
                var scopeLabel = config.Scope switch
                {
                    NwcExportScope.SpecificView => viewName,
                    NwcExportScope.Selection    => "Selecci\u00f3n",
                    _                           => "Modelo completo"
                };

                var confirm = MessageBox.Show(
                    $"Se exportar\u00e1 el modelo a NWC:\n\n" +
                    $"\U0001F4C1 {config.DestinationFolder}\\{config.FileName}.nwc\n\n" +
                    $"Alcance: {scopeLabel}\n\n" +
                    "\u00bfDesea continuar?",
                    "BIM Pills \u2014 Confirmar exportaci\u00f3n",
                    MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);

                if (confirm != MessageBoxResult.Yes) return;

                if (_nwcExportCallback != null)
                {
                    ProgressOverlay.Visibility = Visibility.Visible;
                    try
                    {
                        bool ok = _nwcExportCallback(config);
                        ProgressOverlay.Visibility = Visibility.Collapsed;
                        if (ok)
                            MessageBox.Show("Modelo exportado correctamente a NWC.",
                                "BIM Pills \u2014 Exportaci\u00f3n completada",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        else
                            MessageBox.Show("La exportaci\u00f3n NWC fall\u00f3.",
                                "BIM Pills \u2014 Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    catch { ProgressOverlay.Visibility = Visibility.Collapsed; throw; }
                }
                else
                {
                    MessageBox.Show("Callback de exportaci\u00f3n NWC no configurado (sandbox).",
                        "BIM Pills \u2014 Sandbox", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger?.Error("Error en NWC export", ex);
                ProgressOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"Error inesperado:\n{ex.Message}",
                    "BIM Pills \u2014 Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Event handlers ────────────────────────────────────────────────────────

        private void FormatCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (NwcOptionsPanel == null) return;
            var tag = (FormatCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "NWC";
            NwcOptionsPanel.Visibility = tag == "NWC" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Seleccionar carpeta destino para NWC",
                    ShowNewFolderButton = true
                };
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _selectedFolder = dialog.SelectedPath;
                    DestinationBox.Text = _selectedFolder;
                    UpdateExportState();
                }
            }
            catch (Exception ex) { _logger?.Error("BrowseFolder_Click", ex); }
        }

        private void DestinationBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _selectedFolder = DestinationBox.Text;
            UpdateExportState();
        }

        private void FileName_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateExportState();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); e.Handled = true; }
            catch { }
        }
    }
}
