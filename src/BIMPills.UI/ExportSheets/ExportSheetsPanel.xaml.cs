using BIMPills.Core.Models;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace BIMPills.UI.ExportSheets
{
    public partial class ExportSheetsPanel : UserControl
    {
        private IReadOnlyList<ExportableViewInfo> _allItems = Array.Empty<ExportableViewInfo>();
        private List<ExportableViewRow> _rows = new List<ExportableViewRow>();
        private List<ExportableViewRow> _filteredRows = new List<ExportableViewRow>();
        private Func<long, string, string, PdfExportSettings, bool>? _pdfExportCallback;
        private Func<long, string, string, DwgExportConfig?, bool>? _dwgExportCallback;
        private string _projectName = "";
        private string? _selectedFolder;
        private ILogger? _logger;
        private List<string> _availableParameters = new List<string>();

        // Publication sets (S6-C)
        private JsonPublicationSetRepository? _publicationSetRepo;
        private List<PublicationSet> _publicationSets = new List<PublicationSet>();

        private string _exportLabel = "Exportar";

        /// <summary>Raised when export availability changes. Arg = canExport.</summary>
        public event EventHandler<bool>? ExportEnabledChanged;

        public ExportSheetsPanel()
        {
            InitializeComponent();
        }

        /// <summary>Trigger export from external button.</summary>
        public void TriggerExport() => Export_Click(this, new RoutedEventArgs());

        /// <summary>Get export button label.</summary>
        public string ExportLabel => _exportLabel;

        /// <summary>
        /// Initializes the panel with unified exportable views (sheets + views).
        /// </summary>
        public void InitializeViews(
            IReadOnlyList<ExportableViewInfo> items,
            Func<long, string, string, PdfExportSettings, bool>? pdfExportCallback = null,
            Func<long, string, string, DwgExportConfig?, bool>? dwgExportCallback = null,
            string projectName = "",
            ILogger? logger = null,
            IReadOnlyList<string>? availableParameters = null,
            IReadOnlyList<string>? dwgPresetNames = null)
        {
            _allItems = items;
            _pdfExportCallback = pdfExportCallback;
            _dwgExportCallback = dwgExportCallback;
            _projectName = projectName;
            _logger = logger;

            // Populate Revit parameter ComboBox
            if (availableParameters != null)
            {
                _availableParameters = new List<string>(availableParameters);
                RevitParamsCombo.Items.Clear();
                foreach (var param in _availableParameters)
                    RevitParamsCombo.Items.Add(new ComboBoxItem { Content = param });
            }

            // Populate DWG preset ComboBox with Revit's native presets
            if (dwgPresetNames != null && dwgPresetNames.Count > 0)
            {
                DwgSetupCombo.Items.Clear();
                foreach (var name in dwgPresetNames)
                {
                    var cfg = new DwgExportConfig
                    {
                        Id = name,
                        Name = name,
                        IsRevitPreset = true,
                        RevitPresetName = name
                    };
                    DwgSetupCombo.Items.Add(new ComboBoxItem { Content = name, Tag = cfg });
                }
                if (DwgSetupCombo.Items.Count > 0)
                    DwgSetupCombo.SelectedIndex = 0;
            }

            Populate();
            UpdateAllFileNames();
            UpdateNamingPreview();
            LoadPublicationSets();
        }

        /// <summary>
        /// Backward-compatible initializer using SheetExportInfo (converts to ExportableViewInfo).
        /// </summary>
        public void Initialize(
            IReadOnlyList<SheetExportInfo> sheets,
            Func<long, string, string, PdfExportSettings, bool>? pdfExportCallback = null,
            Func<long, string, string, DwgExportConfig?, bool>? dwgExportCallback = null,
            string projectName = "",
            ILogger? logger = null,
            IReadOnlyList<string>? availableParameters = null,
            IReadOnlyList<string>? dwgPresetNames = null)
        {
            var items = sheets.Select(s => new ExportableViewInfo(
                s.Id, "", s.SheetName, ExportableItemType.Sheet,
                s.SheetNumber, s.Revision, s.Discipline, s.ParameterValues
            )).ToList();

            InitializeViews(items, pdfExportCallback, dwgExportCallback, projectName, logger, availableParameters, dwgPresetNames);
        }

        private void Populate()
        {
            _rows = _allItems.Select(v => new ExportableViewRow(v)).ToList();
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            var typeTag = (TypeFilterCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "All";
            var query = SearchBox?.Text?.Trim().ToLowerInvariant() ?? "";

            _filteredRows = _rows.Where(r =>
            {
                // Type filter
                if (typeTag != "All" && r.Item.ItemType.ToString() != typeTag)
                    return false;

                // Search filter
                if (!string.IsNullOrEmpty(query))
                {
                    return (r.DisplayName?.ToLowerInvariant().Contains(query) ?? false)
                        || (r.SheetNumber?.ToLowerInvariant().Contains(query) ?? false)
                        || (r.TypeLabel?.ToLowerInvariant().Contains(query) ?? false)
                        || (r.Discipline?.ToLowerInvariant().Contains(query) ?? false);
                }
                return true;
            }).ToList();

            SheetsGrid.ItemsSource = _filteredRows;
            UpdateSelection();
        }

        private void UpdateAllFileNames()
        {
            var convention = new SheetNamingConvention { Pattern = NamingPatternBox?.Text ?? "{SheetNumber}-{SheetName}" };
            foreach (var row in _rows)
                row.UpdateFileName(convention, _projectName);
        }

        private void UpdateSelection()
        {
            var totalSelected = _rows.Count(r => r.IsSelected);
            var sheetCount = _rows.Count(r => r.IsSelected && r.Item.ItemType == ExportableItemType.Sheet);
            var viewCount = totalSelected - sheetCount;

            string label;
            if (sheetCount > 0 && viewCount > 0)
                label = $"{sheetCount} planos + {viewCount} vistas";
            else if (sheetCount > 0)
                label = $"{sheetCount} planos";
            else if (viewCount > 0)
                label = $"{viewCount} vistas";
            else
                label = "0 items";

            SelectionSummary.Text = $"{totalSelected} de {_rows.Count} seleccionados ({label})";
            StatusText.Text = $"{totalSelected} items seleccionados";

            bool canExport = totalSelected > 0
                && !string.IsNullOrEmpty(_selectedFolder)
                && (PdfCheck.IsChecked == true || DwgCheck.IsChecked == true);
            ExportEnabledChanged?.Invoke(this, canExport);
            _exportLabel = $"Exportar {totalSelected} items";
        }

        private void UpdateNamingPreview()
        {
            var convention = new SheetNamingConvention { Pattern = NamingPatternBox.Text };
            var firstItem = _rows.FirstOrDefault(r => r.IsSelected) ?? _rows.FirstOrDefault();
            if (firstItem != null)
            {
                // Build a SheetExportInfo for naming compatibility
                var sheetProxy = new SheetExportInfo(
                    firstItem.Item.Id,
                    firstItem.Item.SheetNumber,
                    firstItem.Item.Name,
                    firstItem.Item.Revision,
                    firstItem.Item.Discipline);

                var name = convention.GenerateFileName(sheetProxy, _projectName, DateTime.Now, firstItem.Item.ParameterValues);
                var ext = PdfCheck.IsChecked == true ? ".pdf" : ".dwg";
                NamingPreview.Text = name + ext;
            }
            else
            {
                NamingPreview.Text = "(sin items)";
            }
        }

        private void UpdateFolderPreview()
        {
            if (string.IsNullOrEmpty(_selectedFolder))
            {
                FolderPreview.Text = "";
                return;
            }

            var convention = new SheetNamingConvention { Pattern = NamingPatternBox.Text };
            var folderOrg = GetSelectedFolderOrganization();
            var selected = _rows.Where(r => r.IsSelected).Take(4).ToList();
            var lines = new List<string>();

            var baseName = Path.GetFileName(_selectedFolder);
            lines.Add($"\U0001F4C1 {baseName}/");

            bool exportPdf = PdfCheck.IsChecked == true;
            bool exportDwg = DwgCheck.IsChecked == true;

            foreach (var row in selected)
            {
                var sheetProxy = new SheetExportInfo(
                    row.Item.Id, row.Item.SheetNumber, row.Item.Name,
                    row.Item.Revision, row.Item.Discipline);
                var name = convention.GenerateFileName(sheetProxy, _projectName, DateTime.Now, row.Item.ParameterValues);

                if (exportPdf)
                {
                    var subPath = GetSubFolder(folderOrg, "PDF", row.Discipline);
                    if (!string.IsNullOrEmpty(subPath) && !lines.Any(l => l.Contains(subPath)))
                        lines.Add($"  \U0001F4C1 {subPath}/");
                    var indent = string.IsNullOrEmpty(subPath) ? "  " : "    ";
                    lines.Add($"{indent}\U0001F4C4 {name}.pdf");
                }

                if (exportDwg)
                {
                    var dwgSub = GetSubFolder(folderOrg, "DWG", row.Discipline);
                    if (!string.IsNullOrEmpty(dwgSub) && !lines.Any(l => l.Contains(dwgSub)))
                        lines.Add($"  \U0001F4C1 {dwgSub}/");
                    var indent = string.IsNullOrEmpty(dwgSub) ? "  " : "    ";
                    lines.Add($"{indent}\U0001F4C4 {name}.dwg");
                }
            }

            if (_rows.Count(r => r.IsSelected) > 4)
                lines.Add($"  \u2026 y {_rows.Count(r => r.IsSelected) - 4} archivos m\u00e1s");

            FolderPreview.Text = string.Join("\n", lines);
        }

        private static string GetSubFolder(FolderOrganization org, string format, string discipline)
        {
            switch (org)
            {
                case FolderOrganization.ByFormat:
                    return format;
                case FolderOrganization.ByDiscipline:
                    return string.IsNullOrEmpty(discipline) ? "General" : discipline;
                case FolderOrganization.ByFormatAndDiscipline:
                    var disc = string.IsNullOrEmpty(discipline) ? "General" : discipline;
                    return $"{format}/{disc}";
                default: // Flat
                    return "";
            }
        }

        private FolderOrganization GetSelectedFolderOrganization()
        {
            var item = FolderOrgCombo.SelectedItem as ComboBoxItem;
            var tag = item?.Tag?.ToString() ?? "ByFormat";
            switch (tag)
            {
                case "Flat": return FolderOrganization.Flat;
                case "ByDiscipline": return FolderOrganization.ByDiscipline;
                case "ByFormatAndDiscipline": return FolderOrganization.ByFormatAndDiscipline;
                default: return FolderOrganization.ByFormat;
            }
        }

        // ── Publication Sets (S6-C) ──

        private void LoadPublicationSets()
        {
            try
            {
                _publicationSetRepo = new JsonPublicationSetRepository();
                _publicationSets = _publicationSetRepo.GetAllAsync().GetAwaiter().GetResult();

                PublicationSetCombo.Items.Clear();
                PublicationSetCombo.Items.Add(new ComboBoxItem { Content = "(ninguno)", Tag = "" });
                foreach (var set in _publicationSets)
                    PublicationSetCombo.Items.Add(new ComboBoxItem { Content = $"{set.Name} ({set.Items.Count} items)", Tag = set.Id });

                PublicationSetCombo.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                _logger?.Error("Error loading publication sets", ex);
            }
        }

        private void PublicationSet_Changed(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var selectedItem = PublicationSetCombo.SelectedItem as ComboBoxItem;
                var setId = selectedItem?.Tag?.ToString() ?? "";
                DeleteSetBtn.IsEnabled = !string.IsNullOrEmpty(setId);

                if (string.IsNullOrEmpty(setId)) return;

                var set = _publicationSets.FirstOrDefault(s => s.Id == setId);
                if (set == null) return;

                // Deselect all first
                foreach (var row in _rows)
                    row.IsSelected = false;

                // Select items by UniqueId
                int found = 0;
                foreach (var item in set.Items)
                {
                    var row = _rows.FirstOrDefault(r => r.Item.UniqueId == item.UniqueId);
                    if (row != null)
                    {
                        row.IsSelected = true;
                        found++;
                    }
                }

                SheetsGrid.Items.Refresh();
                UpdateSelection();

                int notFound = set.Items.Count - found;
                if (notFound > 0)
                {
                    MessageBox.Show(
                        $"Se seleccionaron {found} de {set.Items.Count} items.\n{notFound} no se encontraron en el modelo actual.",
                        "BIM Pills \u2014 Conjunto de publicaci\u00f3n",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex) { _logger?.Error("Error en PublicationSet_Changed", ex); }
        }

        private void SaveSet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItems = _rows.Where(r => r.IsSelected).ToList();
                if (selectedItems.Count == 0)
                {
                    MessageBox.Show("Selecciona al menos un item para guardar el conjunto.",
                        "BIM Pills \u2014 Conjunto de publicaci\u00f3n", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var name = PromptForSetName();
                if (string.IsNullOrWhiteSpace(name)) return;

                var set = new PublicationSet
                {
                    Name = name.Trim(),
                    Items = selectedItems.Select(r => new PublicationSetItem
                    {
                        UniqueId = r.Item.UniqueId,
                        DisplayName = r.DisplayName,
                        ItemType = r.Item.ItemType
                    }).ToList()
                };

                _publicationSetRepo?.CreateAsync(set).GetAwaiter().GetResult();
                LoadPublicationSets();

                // Select the newly created set
                for (int i = 0; i < PublicationSetCombo.Items.Count; i++)
                {
                    if (PublicationSetCombo.Items[i] is ComboBoxItem ci && ci.Tag?.ToString() == set.Id)
                    {
                        PublicationSetCombo.SelectedIndex = i;
                        break;
                    }
                }

                MessageBox.Show($"Conjunto \u00ab{set.Name}\u00bb guardado con {set.Items.Count} items.",
                    "BIM Pills \u2014 Conjunto de publicaci\u00f3n", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger?.Error("Error en SaveSet_Click", ex);
                MessageBox.Show($"Error al guardar: {ex.Message}",
                    "BIM Pills \u2014 Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteSet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = PublicationSetCombo.SelectedItem as ComboBoxItem;
                var setId = selectedItem?.Tag?.ToString() ?? "";
                if (string.IsNullOrEmpty(setId)) return;

                var set = _publicationSets.FirstOrDefault(s => s.Id == setId);
                if (set == null) return;

                var confirm = MessageBox.Show(
                    $"\u00bfEliminar el conjunto \u00ab{set.Name}\u00bb?",
                    "BIM Pills \u2014 Confirmar",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes) return;

                _publicationSetRepo?.DeleteAsync(setId).GetAwaiter().GetResult();
                LoadPublicationSets();
            }
            catch (Exception ex)
            {
                _logger?.Error("Error en DeleteSet_Click", ex);
                MessageBox.Show($"Error al eliminar: {ex.Message}",
                    "BIM Pills \u2014 Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string? PromptForSetName()
        {
            var dlg = new Window
            {
                Title               = "BIM Pills \u2014 Guardar conjunto",
                Width               = 380,
                Height              = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode          = ResizeMode.NoResize,
                Background          = System.Windows.Media.Brushes.White,
                WindowStyle         = WindowStyle.ToolWindow
            };

            var grid = new Grid { Margin = new Thickness(18) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lbl = new TextBlock
            {
                Text       = "Nombre del conjunto:",
                FontSize   = 12,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                Margin     = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(lbl, 0);

            var tb = new TextBox
            {
                Height       = 30,
                FontSize     = 12,
                FontFamily   = new System.Windows.Media.FontFamily("Segoe UI"),
                Padding      = new Thickness(6, 4, 6, 4),
                BorderBrush  = System.Windows.Media.Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Text         = $"Conjunto {DateTime.Now:yyyy-MM-dd}"
            };
            Grid.SetRow(tb, 1);

            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetRow(btns, 3);

            string? result = null;
            var ok = new Button
            {
                Content     = "Guardar",
                Width       = 80,
                Height      = 28,
                Margin      = new Thickness(0, 0, 8, 0),
                IsDefault   = true,
                FontFamily  = new System.Windows.Media.FontFamily("Segoe UI"),
                Background  = System.Windows.Media.Brushes.White
            };
            ok.Click += (_, __) => { result = tb.Text; dlg.DialogResult = true; };

            var cancel = new Button
            {
                Content    = "Cancelar",
                Width      = 80,
                Height     = 28,
                IsCancel   = true,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
            };

            btns.Children.Add(ok);
            btns.Children.Add(cancel);
            grid.Children.Add(lbl);
            grid.Children.Add(tb);
            grid.Children.Add(btns);
            dlg.Content = grid;

            tb.Loaded += (_, __) => { tb.SelectAll(); tb.Focus(); };

            dlg.ShowDialog();
            return result;
        }

        // ── Event handlers ──

        private void TypeFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            try { ApplyFilters(); }
            catch (Exception ex) { _logger?.Error("Error en TypeFilter_Changed", ex); }
        }

        private void WizardTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateFormatScrollHint();
        }

        private void FormatScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            UpdateFormatScrollHint();
        }

        private void UpdateFormatScrollHint()
        {
            if (FormatScrollHint == null || FormatScroll == null) return;
            bool hasMore = FormatScroll.VerticalOffset + FormatScroll.ViewportHeight < FormatScroll.ExtentHeight - 1;
            FormatScrollHint.Visibility = hasMore ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try { ApplyFilters(); }
            catch (Exception ex) { _logger?.Error("Error en SearchBox_TextChanged", ex); }
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (var row in _filteredRows)
                    row.IsSelected = true;
                SheetsGrid.Items.Refresh();
                UpdateSelection();
            }
            catch (Exception ex) { _logger?.Error("Error en SelectAll_Click", ex); }
        }

        private void DeselectAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (var row in _filteredRows)
                    row.IsSelected = false;
                SheetsGrid.Items.Refresh();
                UpdateSelection();
            }
            catch (Exception ex) { _logger?.Error("Error en DeselectAll_Click", ex); }
        }

        private void FormatCheck_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                if (PdfSection != null)
                    PdfSection.Visibility = PdfCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                if (DwgSection != null)
                    DwgSection.Visibility = DwgCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                UpdateSelection();
                UpdateNamingPreview();
                UpdateFolderPreview();
            }
            catch (Exception ex) { _logger?.Error("Error en FormatCheck_Changed", ex); }
        }

        private void PdfPlacement_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (PdfOffsetPanel == null) return;
            var tag = (PdfPaperPlacementCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            PdfOffsetPanel.Visibility = tag == "OffsetFromCorner" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PdfZoom_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (PdfZoomFactorPanel == null) return;
            var tag = (PdfZoomTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            PdfZoomFactorPanel.Visibility = tag == "Custom" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PdfCombine_Changed(object sender, RoutedEventArgs e)
        {
            if (PdfCombineNamePanel == null) return;
            PdfCombineNamePanel.Visibility = PdfCombineCheck.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private PdfExportSettings GetPdfSettings()
        {
            return new PdfExportSettings
            {
                PaperPlacement = (PdfPaperPlacementCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "OffsetFromCorner"
                    ? PdfPaperPlacement.OffsetFromCorner : PdfPaperPlacement.Center,
                OffsetX = double.TryParse(PdfOffsetXBox.Text, out var ox) ? ox : 0,
                OffsetY = double.TryParse(PdfOffsetYBox.Text, out var oy) ? oy : 0,
                ZoomType = (PdfZoomTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "Custom"
                    ? PdfZoomType.Custom : PdfZoomType.FitToPage,
                ZoomPercent = int.TryParse(PdfZoomFactorBox.Text, out var zp) ? zp : 100,
                HiddenViewProcessing = (PdfHiddenViewsCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "Raster"
                    ? PdfHiddenViewProcessing.Raster : PdfHiddenViewProcessing.Vector,
                ColorDepth = (PdfColorCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() switch
                {
                    "Grayscale"  => PdfColorDepth.Grayscale,
                    "BlackLines" => PdfColorDepth.BlackLines,
                    _            => PdfColorDepth.Color
                },
                RasterQuality = (PdfDpiCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() switch
                {
                    "Low"          => PdfRasterQuality.Low,
                    "High"         => PdfRasterQuality.High,
                    "Presentation" => PdfRasterQuality.Presentation,
                    _              => PdfRasterQuality.Medium
                },
                HideScopeBoxes           = PdfHideScopeBoxes.IsChecked == true,
                HideCropBoundaries       = PdfHideCropBoundaries.IsChecked == true,
                HideRefWorkPlanes        = PdfHideRefPlanes.IsChecked == true,
                HideUnreferencedViewTags = PdfHideUnrefTags.IsChecked == true,
                ViewLinksInBlue          = PdfViewLinksBlue.IsChecked == true,
                CombineIntoPdf           = PdfCombineCheck.IsChecked == true,
                CombinedFileName         = PdfCombinedNameBox.Text?.Trim() ?? "Planos_Combinados"
            };
        }

        private void NamingPattern_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                UpdateAllFileNames();
                UpdateNamingPreview();
                UpdateFolderPreview();
            }
            catch (Exception ex) { _logger?.Error("Error en NamingPattern_TextChanged", ex); }
        }

        private void InsertToken_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is string token)
                {
                    var caret = NamingPatternBox.CaretIndex;
                    NamingPatternBox.Text = NamingPatternBox.Text.Insert(caret, token);
                    NamingPatternBox.CaretIndex = caret + token.Length;
                    NamingPatternBox.Focus();
                }
            }
            catch (Exception ex) { _logger?.Error("Error en InsertToken_Click", ex); }
        }

        private void AddParameterToken_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selected = RevitParamsCombo.SelectedItem as ComboBoxItem;
                if (selected == null) return;

                var paramName = selected.Content?.ToString();
                if (string.IsNullOrEmpty(paramName)) return;

                var token = $"{{Param:{paramName}}}";

                var btn = new Button
                {
                    Content = paramName,
                    Style = (Style)FindResource("TokenButton"),
                    Tag = token,
                    Margin = new Thickness(0, 0, 4, 4)
                };
                btn.Click += InsertToken_Click;
                DynamicTokensPanel.Children.Add(btn);

                var caret = NamingPatternBox.CaretIndex;
                NamingPatternBox.Text = NamingPatternBox.Text.Insert(caret, token);
                NamingPatternBox.CaretIndex = caret + token.Length;
                NamingPatternBox.Focus();

                RevitParamsCombo.Items.Remove(selected);
                RevitParamsCombo.SelectedIndex = -1;
            }
            catch (Exception ex) { _logger?.Error("Error en AddParameterToken_Click", ex); }
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Seleccionar carpeta de destino",
                    ShowNewFolderButton = true
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _selectedFolder = dialog.SelectedPath;
                    DestinationBox.Text = _selectedFolder;
                    UpdateSelection();
                    UpdateFolderPreview();
                }
            }
            catch (Exception ex)
            {
                _logger?.Error("Error en BrowseFolder_Click", ex);
                MessageBox.Show($"Error inesperado:\n{ex.Message}",
                    "BIM Pills \u2014 Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DestinationBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _selectedFolder = DestinationBox.Text;
                UpdateSelection();
                UpdateFolderPreview();
            }
            catch (Exception ex) { _logger?.Error("Error en DestinationBox_TextChanged", ex); }
        }

        private void FolderOrg_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try { UpdateFolderPreview(); }
            catch (Exception ex) { _logger?.Error("Error en FolderOrg_SelectionChanged", ex); }
        }

        private void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            var name = PromptForProfileName();
            if (string.IsNullOrWhiteSpace(name)) return;

            try
            {
                var profile = new SheetExportProfile
                {
                    Name               = name.Trim(),
                    Format             = GetSelectedFormat(),
                    NamingConvention   = new SheetNamingConvention { Pattern = NamingPatternBox?.Text ?? "{SheetNumber}-{SheetName}" },
                    FolderOrganization = GetSelectedFolderOrganization(),
                    DwgExportConfigId  = (DwgSetupCombo.SelectedItem as ComboBoxItem)?.Tag is DwgExportConfig cfg ? cfg.Id : ""
                };

                var repo = new JsonSheetExportProfileRepository();
                repo.CreateAsync(profile).GetAwaiter().GetResult();

                MessageBox.Show($"Perfil \u00ab{profile.Name}\u00bb guardado correctamente.",
                    "BIM Pills \u2014 Exportar",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar el perfil: {ex.Message}",
                    "BIM Pills \u2014 Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private SheetExportFormat GetSelectedFormat()
        {
            bool pdf = PdfCheck.IsChecked == true;
            bool dwg = DwgCheck.IsChecked == true;
            if (pdf && dwg) return SheetExportFormat.Both;
            if (dwg)         return SheetExportFormat.DWG;
            return SheetExportFormat.PDF;
        }

        private static string? PromptForProfileName()
        {
            var dlg = new Window
            {
                Title               = "BIM Pills \u2014 Guardar perfil",
                Width               = 380,
                Height              = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode          = ResizeMode.NoResize,
                Background          = System.Windows.Media.Brushes.White,
                WindowStyle         = WindowStyle.ToolWindow
            };

            var grid = new Grid { Margin = new Thickness(18) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lbl = new TextBlock
            {
                Text       = "Nombre del perfil:",
                FontSize   = 12,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                Margin     = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(lbl, 0);

            var tb = new TextBox
            {
                Height       = 30,
                FontSize     = 12,
                FontFamily   = new System.Windows.Media.FontFamily("Segoe UI"),
                Padding      = new Thickness(6, 4, 6, 4),
                BorderBrush  = System.Windows.Media.Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Text         = $"Perfil {DateTime.Now:yyyy-MM-dd}"
            };
            Grid.SetRow(tb, 1);

            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetRow(btns, 3);

            string? result = null;
            var ok = new Button
            {
                Content     = "Guardar",
                Width       = 80,
                Height      = 28,
                Margin      = new Thickness(0, 0, 8, 0),
                IsDefault   = true,
                FontFamily  = new System.Windows.Media.FontFamily("Segoe UI"),
                Background  = System.Windows.Media.Brushes.White
            };
            ok.Click += (_, __) => { result = tb.Text; dlg.DialogResult = true; };

            var cancel = new Button
            {
                Content    = "Cancelar",
                Width      = 80,
                Height     = 28,
                IsCancel   = true,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
            };

            btns.Children.Add(ok);
            btns.Children.Add(cancel);
            grid.Children.Add(lbl);
            grid.Children.Add(tb);
            grid.Children.Add(btns);
            dlg.Content = grid;

            tb.Loaded += (_, __) => { tb.SelectAll(); tb.Focus(); };

            dlg.ShowDialog();
            return result;
        }

        // ── DWG Config ──

        private DwgExportConfig? GetSelectedDwgConfig()
        {
            DwgExportConfig? cfg = null;
            if (DwgSetupCombo.SelectedItem is ComboBoxItem item && item.Tag is DwgExportConfig c)
                cfg = c;

            if (cfg == null) return null;

            return new DwgExportConfig
            {
                Id                = cfg.Id,
                Name              = cfg.Name,
                IsRevitPreset     = cfg.IsRevitPreset,
                RevitPresetName   = cfg.RevitPresetName,
                FileVersion       = cfg.FileVersion,
                ExportOfSolids    = cfg.ExportOfSolids,
                SharedCoords      = cfg.SharedCoords,
                ExportLinkedAsXrefs = DwgExportLinkedCheck?.IsChecked == true,
                CleanPcpFiles       = DwgCleanPcpCheck?.IsChecked == true,
            };
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_selectedFolder)) return;

                bool exportPdf = PdfCheck.IsChecked == true && _pdfExportCallback != null;
                bool exportDwg = DwgCheck.IsChecked == true && _dwgExportCallback != null;
                if (!exportPdf && !exportDwg) return;

                var selected = _rows.Where(r => r.IsSelected).ToList();
                if (selected.Count == 0) return;

                var convention = new SheetNamingConvention { Pattern = NamingPatternBox.Text };
                var folderOrg = GetSelectedFolderOrganization();
                var now = DateTime.Now;

                var selectedDwgConfig = GetSelectedDwgConfig();

                int totalOps = selected.Count * ((exportPdf ? 1 : 0) + (exportDwg ? 1 : 0));
                var pdfSettings = exportPdf ? GetPdfSettings() : new PdfExportSettings();

                var confirm = MessageBox.Show(
                    $"Se exportar\u00e1n {selected.Count} items" +
                    (exportPdf && exportDwg ? " a PDF y DWG" : exportPdf ? " a PDF" : " a DWG") +
                    $" en:\n\n\U0001F4C1 {_selectedFolder}\n\n" +
                    $"Total de archivos: {totalOps}\n\n" +
                    "Este proceso puede tomar varios minutos. \u00bfDesea continuar?",
                    "BIM Pills \u2014 Confirmar exportaci\u00f3n",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.Yes);

                if (confirm != MessageBoxResult.Yes) return;

                ProgressOverlay.Visibility = Visibility.Visible;
                ProgressBar.Maximum = totalOps;
                ProgressBar.Value = 0;

                int exported = 0;
                int failed = 0;
                var errors = new List<string>();
                int step = 0;

                try
                {
                    foreach (var row in selected)
                    {
                        var sheetProxy = new SheetExportInfo(
                            row.Item.Id, row.Item.SheetNumber, row.Item.Name,
                            row.Item.Revision, row.Item.Discipline);
                        var fileName = SanitizeFileName(convention.GenerateFileName(sheetProxy, _projectName, now, row.Item.ParameterValues));

                        if (exportPdf)
                        {
                            step++;
                            ProgressText.Text = $"PDF: {step} de {totalOps}...";
                            ProgressDetail.Text = row.DisplayName;
                            ProgressBar.Value = step;
                            PumpDispatcher();

                            var folder = GetExportFolder(_selectedFolder, folderOrg, "PDF", row.Discipline);
                            Directory.CreateDirectory(folder);

                            bool ok = _pdfExportCallback!(row.Item.Id, folder, fileName, pdfSettings);
                            if (ok) exported++;
                            else { failed++; errors.Add($"[PDF] {row.DisplayName}"); }
                        }

                        if (exportDwg)
                        {
                            step++;
                            ProgressText.Text = $"DWG: {step} de {totalOps}...";
                            ProgressDetail.Text = row.DisplayName;
                            ProgressBar.Value = step;
                            PumpDispatcher();

                            var folder = GetExportFolder(_selectedFolder, folderOrg, "DWG", row.Discipline);
                            Directory.CreateDirectory(folder);

                            bool ok = _dwgExportCallback!(row.Item.Id, folder, fileName, selectedDwgConfig);
                            if (ok) exported++;
                            else { failed++; errors.Add($"[DWG] {row.DisplayName}"); }
                        }
                    }

                    ProgressBar.Value = totalOps;
                }
                finally
                {
                    ProgressOverlay.Visibility = Visibility.Collapsed;
                }

                string summary = $"Exportados {exported} de {totalOps} archivos.";
                if (failed > 0)
                {
                    summary += $"\n\n{failed} archivos no pudieron exportarse:";
                    foreach (var name in errors.Take(10))
                        summary += $"\n  - {name}";
                    if (errors.Count > 10)
                        summary += $"\n  ... y {errors.Count - 10} m\u00e1s";
                }

                MessageBox.Show(summary,
                    "BIM Pills \u2014 Exportaci\u00f3n completada",
                    MessageBoxButton.OK,
                    failed > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

                if (exported > 0 && failed == 0)
                {
                    var win = Window.GetWindow(this);
                    if (win != null) try { win.DialogResult = true; } catch (InvalidOperationException) { }
                    win?.Close();
                }
            }
            catch (Exception ex)
            {
                _logger?.Error("Error no controlado en Export_Click", ex);
                ProgressOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show(
                    $"Error inesperado durante la exportaci\u00f3n:\n{ex.Message}\n\nRevisa el log para m\u00e1s detalles.",
                    "BIM Pills \u2014 Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static string GetExportFolder(string basePath, FolderOrganization org, string format, string discipline)
        {
            switch (org)
            {
                case FolderOrganization.ByFormat:
                    return Path.Combine(basePath, format);
                case FolderOrganization.ByDiscipline:
                    return Path.Combine(basePath, string.IsNullOrEmpty(discipline) ? "General" : discipline);
                case FolderOrganization.ByFormatAndDiscipline:
                    return Path.Combine(basePath, format, string.IsNullOrEmpty(discipline) ? "General" : discipline);
                default:
                    return basePath;
            }
        }

        private static void PumpDispatcher()
        {
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                System.Windows.Threading.DispatcherPriority.Render,
                new Action(() => { }));
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var sb = new System.Text.StringBuilder();
            foreach (char c in name)
                sb.Append(invalid.Contains(c) ? '_' : c);
            return sb.ToString().Trim().TrimEnd('.');
        }
    }

    /// <summary>
    /// UI wrapper for ExportableViewInfo with live-computed DisplayFileName.
    /// </summary>
    internal class ExportableViewRow : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public ExportableViewInfo Item { get; }

        public bool IsSelected
        {
            get => Item.IsSelected;
            set { Item.IsSelected = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected))); }
        }

        public string SheetNumber  => Item.SheetNumber;
        public string DisplayName  => Item.DisplayName;
        public string TypeLabel    => Item.TypeLabel;
        public string Revision     => Item.Revision;
        public string Discipline   => Item.Discipline;

        public string DisciplineCategory
        {
            get
            {
                var d = (Item.Discipline ?? "").ToLowerInvariant();
                if (d.Contains("arq") || d.Contains("arch")) return "Arquitectura";
                if (d.Contains("est") || d.Contains("str"))  return "Estructura";
                if (d.Contains("mep") || d.Contains("mec") || d.Contains("ele") || d.Contains("plu") || d.Contains("san")) return "MEP";
                if (string.IsNullOrWhiteSpace(d)) return "";
                return "Other";
            }
        }

        private string _displayFileName = "";
        public string DisplayFileName
        {
            get => _displayFileName;
            set { _displayFileName = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(DisplayFileName))); }
        }

        public ExportableViewRow(ExportableViewInfo item) => Item = item;

        public void UpdateFileName(SheetNamingConvention convention, string projectName)
        {
            var sheetProxy = new SheetExportInfo(Item.Id, Item.SheetNumber, Item.Name, Item.Revision, Item.Discipline);
            DisplayFileName = convention.GenerateFileName(sheetProxy, projectName, DateTime.Now, Item.ParameterValues);
        }
    }
}
