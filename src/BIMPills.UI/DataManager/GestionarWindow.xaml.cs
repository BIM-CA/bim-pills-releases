using BIMPills.Core.Models;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.Export;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;

namespace BIMPills.UI.DataManager
{
    /// <summary>View model for one row in the diff preview grid.</summary>
    internal sealed class DiffItem
    {
        public string Tabla      { get; set; } = "";
        public long   ElementId  { get; set; }
        public string Parametro  { get; set; } = "";
        public string NuevoValor { get; set; } = "";
    }

    public partial class GestionarWindow : Window
    {
        private readonly IDocumentServices _documentServices;
        private List<ScheduleInfo>         _allSchedules   = new();
        private ScheduleData?              _currentData;
        private List<ParameterUpdateRequest> _pendingUpdates = new();

        private readonly ScheduleExcelExporter _exporter = new();
        private readonly ScheduleExcelImporter _importer = new();
        private readonly string _modelName;

        /// <summary>Track which columns are read-only by name for styling.</summary>
        private HashSet<string> _readOnlyColumnNames = new();

        private bool _tablasLoaded = false;
        private int  _activeTab   = 0;

        public GestionarWindow(IDocumentServices documentServices, string modelName = "Modelo")
        {
            InitializeComponent();
            BIMPills.UI.Shared.ThemeHelper.Apply(this);
            _documentServices = documentServices;
            _modelName = Path.GetFileNameWithoutExtension(modelName);

            ModelNameLabel.Text = modelName;

            // Default export path
            var defaultFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "BIMPills", "Exports");
            ExportPathBox.Text = defaultFolder;

            // Lazy-load tables on first show
            Loaded += (_, _) => { if (!_tablasLoaded) LoadSchedulesWithOverlay(); };
        }

        // ── Tab switching ─────────────────────────────────────────────────────

        public void InitializeKeynotes(
            string? keynoteFilePath = null,
            Func<string, bool>? reloadInRevitCallback = null)
        {
            KeynotesPanel.Initialize(keynoteFilePath, reloadInRevitCallback);
        }

        private void Tab0_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => SwitchTab(0);
        private void Tab1_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => SwitchTab(1);

        private void SwitchTab(int tab)
        {
            _activeTab = tab;

            // Content visibility
            TablasContent.Visibility  = tab == 0 ? Visibility.Visible : Visibility.Collapsed;
            KeynotesPanel.Visibility  = tab == 1 ? Visibility.Visible : Visibility.Collapsed;

            // Footer buttons
            FooterImportButton.Visibility = tab == 0 ? Visibility.Visible : Visibility.Collapsed;
            FooterExportButton.Visibility = tab == 0 ? Visibility.Visible : Visibility.Collapsed;

            // Tab border styles — use global TabHeader / TabHeaderActive resources
            var activeStyle   = (System.Windows.Style)FindResource("TabHeaderActive");
            var inactiveStyle = (System.Windows.Style)FindResource("TabHeader");
            var activeText    = (System.Windows.Style)FindResource("TabHeaderTextActive");
            var inactiveText  = (System.Windows.Style)FindResource("TabHeaderText");

            Tab0Border.Style = tab == 0 ? activeStyle   : inactiveStyle;
            Tab1Border.Style = tab == 1 ? activeStyle   : inactiveStyle;
            Tab0Text.Style   = tab == 0 ? activeText    : inactiveText;
            Tab1Text.Style   = tab == 1 ? activeText    : inactiveText;

            // Tool header
            if (tab == 0)
            {
                ToolHeaderIcon.Text       = "\uE9F9";
                ToolHeaderIcon.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1565C0"));
                ToolHeaderTitle.Text      = "Tablas de planificaci\u00f3n";
                ToolHeaderSubtitle.Text   = "Exporta e importa datos de tablas de planificaci\u00f3n al modelo";
                ToolHeaderIconBorder.Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E3F2FD"));
            }
            else
            {
                ToolHeaderIcon.Text       = "\uE8D2";
                ToolHeaderIcon.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2E7D32"));
                ToolHeaderTitle.Text      = "Notas Clave";
                ToolHeaderSubtitle.Text   = "Edita, importa y exporta el archivo de notas clave del proyecto";
                ToolHeaderIconBorder.Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E8F5E9"));
            }

            // Lazy-load
            if (tab == 0 && !_tablasLoaded) LoadSchedulesWithOverlay();
        }

        // ── Lazy loading ──────────────────────────────────────────────────────

        private void LoadSchedulesWithOverlay()
        {
            ShowLoading("Cargando tablas de planificaci\u00f3n...");
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try   { LoadSchedules(); _tablasLoaded = true; }
                catch (Exception ex)
                { MessageBox.Show($"Error cargando tablas: {ex.Message}", "BIM Pills \u2014 Error"); }
                finally { HideLoading(); }
            }), DispatcherPriority.Background);
        }

        private void ShowLoading(string message)
        {
            LoadingText.Text          = message;
            LoadingOverlay.Visibility = Visibility.Visible;
        }

        private void HideLoading()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private void LoadSchedules()
        {
            _allSchedules = _documentServices.GetSchedules()?.ToList()
                            ?? new List<ScheduleInfo>();
            ScheduleListBox.ItemsSource = _allSchedules;
            UpdateSelectionCount();
            StatusLabel.Text = $"{_allSchedules.Count} tablas disponibles";
        }

        private void ScheduleSearch_Changed(object sender, TextChangedEventArgs e)
        {
            var q = ScheduleSearchBox.Text.Trim().ToLower();
            ScheduleListBox.ItemsSource = string.IsNullOrEmpty(q)
                ? _allSchedules
                : _allSchedules.Where(s => s.Name.ToLower().Contains(q)).ToList();
        }

        private void ScheduleList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScheduleListBox.SelectedItem is not ScheduleInfo info) return;

            try
            {
                _currentData = _documentServices.GetScheduleData(info.Id);

                // Track read-only column names
                _readOnlyColumnNames = new HashSet<string>(
                    _currentData.Columns.Where(c => c.IsReadOnly).Select(c => c.Name));

                // Show preview area, hide empty state
                EmptyState.Visibility  = Visibility.Collapsed;
                PreviewArea.Visibility = Visibility.Visible;

                RefreshPreviewGrid();
                StatusLabel.Text = $"{info.Name}  ·  {_currentData.Rows.Count} elementos  ·  {_currentData.Columns.Count} columnas";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error cargando datos: {ex.Message}", "BIM Pills \u2014 Error");
            }
        }

        private void RefreshPreviewGrid()
        {
            if (_currentData == null) return;

            var dt = new DataTable();
            foreach (var col in _currentData.Columns)
                dt.Columns.Add(col.Name, typeof(string));

            foreach (var row in _currentData.Rows)
            {
                var dr = dt.NewRow();
                for (int i = 0; i < _currentData.Columns.Count && i < row.Count; i++)
                    dr[i] = row[i] ?? "";
                dt.Rows.Add(dr);
            }

            PreviewGrid.ItemsSource = dt.DefaultView;
        }

        /// <summary>
        /// Style read-only columns with yellow background + colored header.
        /// </summary>
        private void PreviewGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            e.Column.Header = e.PropertyName.ToUpperInvariant();

            if (_readOnlyColumnNames.Contains(e.PropertyName))
            {
                var cellStyle = new Style(typeof(DataGridCell));
                cellStyle.Setters.Add(new Setter(BackgroundProperty,
                    new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0xFF, 0xF9, 0xC4))));
                cellStyle.Setters.Add(new Setter(ForegroundProperty,
                    new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x9E, 0x9D, 0x24))));
                cellStyle.Setters.Add(new Setter(FontStyleProperty, FontStyles.Italic));

                var template = new ControlTemplate(typeof(DataGridCell));
                var borderFactory = new FrameworkElementFactory(typeof(Border));
                borderFactory.SetBinding(Border.BackgroundProperty,
                    new Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
                borderFactory.SetValue(Border.PaddingProperty, new Thickness(10, 0, 10, 0));
                var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
                contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                borderFactory.AppendChild(contentFactory);
                template.VisualTree = borderFactory;
                cellStyle.Setters.Add(new Setter(Control.TemplateProperty, template));

                if (e.Column is DataGridTextColumn textCol)
                    textCol.CellStyle = cellStyle;

                var headerStyle = new Style(typeof(DataGridColumnHeader));
                headerStyle.Setters.Add(new Setter(BackgroundProperty,
                    new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0xFF, 0xF9, 0xC4))));
                headerStyle.Setters.Add(new Setter(ForegroundProperty,
                    new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0xF5, 0x7F, 0x17))));
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.SemiBold));
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.FontSizeProperty, 10.0));
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(10, 7, 10, 7)));
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.FontFamilyProperty, new System.Windows.Media.FontFamily("Segoe UI")));
                e.Column.HeaderStyle = headerStyle;
            }
        }

        // ── Selection ─────────────────────────────────────────────────────────

        private void ScheduleCheckBox_Click(object sender, RoutedEventArgs e)
        {
            UpdateSelectionCount();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool allSelected = _allSchedules.All(s => s.IsSelected);
            foreach (var s in _allSchedules)
                s.IsSelected = !allSelected;

            // Refresh the list to update checkbox bindings
            var source = ScheduleListBox.ItemsSource;
            ScheduleListBox.ItemsSource = null;
            ScheduleListBox.ItemsSource = source;

            SelectAllButton.Content = allSelected ? "Seleccionar todo" : "Deseleccionar todo";
            UpdateSelectionCount();
        }

        private void UpdateSelectionCount()
        {
            int count = _allSchedules.Count(s => s.IsSelected);
            SelectionCountText.Text = count > 0 ? $"{count} seleccionadas" : "";
            FooterExportButton.IsEnabled = count > 0 || _currentData != null;
        }

        // ── Export ──────────────────────────────────────────────────────────────

        private void BrowseExportFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Seleccionar carpeta de exportación",
                SelectedPath = ExportPathBox.Text
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                ExportPathBox.Text = dlg.SelectedPath;
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var selected = _allSchedules.Where(s => s.IsSelected).ToList();

            // If none checked, fall back to the currently previewed table
            if (selected.Count == 0 && _currentData != null)
            {
                ExportSingle();
                return;
            }

            if (selected.Count == 0)
            {
                MessageBox.Show("Selecciona al menos una tabla para exportar.", "BIM Pills \u2014 Gestionar");
                return;
            }

            if (selected.Count == 1)
            {
                // Single selected — use SaveFileDialog for precise naming
                var info = selected[0];
                ExportSingleSchedule(info);
                return;
            }

            // Multiple selected — export all into one file
            ExportMultipleSchedules(selected);
        }

        private void ExportSingle()
        {
            if (_currentData == null) return;

            var safeName = string.Join("_", _currentData.Schedule.Name.Split(Path.GetInvalidFileNameChars()));
            var safeModel = string.Join("_", _modelName.Split(Path.GetInvalidFileNameChars()));
            var dlg = new SaveFileDialog
            {
                Title = "Exportar tabla a Excel",
                Filter = "Excel (*.xlsx)|*.xlsx",
                FileName = $"{safeModel}_{safeName}.xlsx",
                InitialDirectory = string.IsNullOrWhiteSpace(ExportPathBox.Text)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    : ExportPathBox.Text
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var folder = Path.GetDirectoryName(dlg.FileName)!;
                var fileName = Path.GetFileName(dlg.FileName);
                ExportPathBox.Text = folder;

                var path = _exporter.Export(_currentData, folder, fileName);
                StatusLabel.Text = $"Exportado: {Path.GetFileName(path)}";
                AskOpenFile(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar: {ex.Message}", "BIM Pills \u2014 Error");
            }
        }

        private void ExportSingleSchedule(ScheduleInfo info)
        {
            var safeName = string.Join("_", info.Name.Split(Path.GetInvalidFileNameChars()));
            var safeModel = string.Join("_", _modelName.Split(Path.GetInvalidFileNameChars()));
            var dlg = new SaveFileDialog
            {
                Title = "Exportar tabla a Excel",
                Filter = "Excel (*.xlsx)|*.xlsx",
                FileName = $"{safeModel}_{safeName}.xlsx",
                InitialDirectory = string.IsNullOrWhiteSpace(ExportPathBox.Text)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    : ExportPathBox.Text
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var data = _documentServices.GetScheduleData(info.Id);
                var folder = Path.GetDirectoryName(dlg.FileName)!;
                var fileName = Path.GetFileName(dlg.FileName);
                ExportPathBox.Text = folder;

                var path = _exporter.Export(data, folder, fileName);
                StatusLabel.Text = $"Exportado: {Path.GetFileName(path)}";
                AskOpenFile(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar: {ex.Message}", "BIM Pills \u2014 Error");
            }
        }

        private void ExportMultipleSchedules(List<ScheduleInfo> selected)
        {
            var safeModel = string.Join("_", _modelName.Split(Path.GetInvalidFileNameChars()));
            var dlg = new SaveFileDialog
            {
                Title = $"Exportar {selected.Count} tablas a Excel",
                Filter = "Excel (*.xlsx)|*.xlsx",
                FileName = $"{safeModel}_Tablas_{selected.Count}.xlsx",
                InitialDirectory = string.IsNullOrWhiteSpace(ExportPathBox.Text)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    : ExportPathBox.Text
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var folder = Path.GetDirectoryName(dlg.FileName)!;
                var fileName = Path.GetFileName(dlg.FileName);
                ExportPathBox.Text = folder;

                // Load data for all selected schedules
                var allData = new List<ScheduleData>();
                foreach (var info in selected)
                {
                    var data = _documentServices.GetScheduleData(info.Id);
                    if (data?.Rows.Count > 0)
                        allData.Add(data);
                }

                if (allData.Count == 0)
                {
                    MessageBox.Show("Las tablas seleccionadas no contienen datos.", "BIM Pills \u2014 Gestionar");
                    return;
                }

                var path = _exporter.ExportMultiple(allData, folder, fileName);
                StatusLabel.Text = $"Exportadas {allData.Count} tablas → {Path.GetFileName(path)}";
                AskOpenFile(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar: {ex.Message}", "BIM Pills \u2014 Error");
            }
        }

        private void AskOpenFile(string path)
        {
            var result = MessageBox.Show(
                $"Archivo exportado:\n{path}\n\n¿Abrir en Excel?",
                "BIM Pills \u2014 Exportar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
                Helpers.ProcessHelper.OpenDocument(path);
        }

        // ── Import ─────────────────────────────────────────────────────────────

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Excel (*.xlsx)|*.xlsx",
                Title  = "Seleccionar archivo Excel para importar"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var multiUpdates = _importer.ImportMultiple(dlg.FileName);

                if (multiUpdates.Count == 0)
                {
                    MessageBox.Show("No se detectaron datos importables en el archivo.",
                        "BIM Pills \u2014 Importar");
                    return;
                }

                // Flatten updates and build diff items
                _pendingUpdates = new List<ParameterUpdateRequest>();
                var diffItems   = new List<DiffItem>();

                foreach (var kvp in multiUpdates)
                {
                    _pendingUpdates.AddRange(kvp.Value);
                    foreach (var u in kvp.Value)
                    {
                        diffItems.Add(new DiffItem
                        {
                            Tabla      = kvp.Key,
                            ElementId  = u.ElementId,
                            Parametro  = u.ParameterName,
                            NuevoValor = u.NewValue
                        });
                    }
                }

                if (_pendingUpdates.Count > 0)
                {
                    var sheetCount = multiUpdates.Count;
                    ImportBarText.Text = sheetCount == 1
                        ? $"  {_pendingUpdates.Count} cambios desde {Path.GetFileName(dlg.FileName)}"
                        : $"  {_pendingUpdates.Count} cambios en {sheetCount} tablas desde {Path.GetFileName(dlg.FileName)}";
                    ImportBar.Visibility = Visibility.Visible;

                    // Show diff preview in right panel
                    DiffCountLabel.Text = $"{_pendingUpdates.Count} cambios pendientes";
                    DiffGrid.ItemsSource = diffItems;
                    EmptyState.Visibility   = Visibility.Collapsed;
                    PreviewArea.Visibility  = Visibility.Collapsed;
                    DiffArea.Visibility     = Visibility.Visible;

                    StatusLabel.Text = $"{_pendingUpdates.Count} cambios pendientes en {sheetCount} tabla(s)";
                }
                else
                {
                    MessageBox.Show("No se detectaron cambios en el archivo.", "BIM Pills \u2014 Importar");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al leer el archivo: {ex.Message}", "BIM Pills \u2014 Error");
            }
        }

        private void DiscardImport_Click(object sender, RoutedEventArgs e)
        {
            _pendingUpdates.Clear();
            DiffGrid.ItemsSource = null;
            ImportBar.Visibility = Visibility.Collapsed;
            DiffArea.Visibility  = Visibility.Collapsed;

            // Restore the panel that was visible before import
            if (_currentData != null)
                PreviewArea.Visibility = Visibility.Visible;
            else
                EmptyState.Visibility = Visibility.Visible;

            StatusLabel.Text = ScheduleListBox.SelectedItem is ScheduleInfo sel
                ? $"{sel.Name}  ·  {_currentData?.Rows.Count ?? 0} elementos  ·  {_currentData?.Columns.Count ?? 0} columnas"
                : $"{_allSchedules.Count} tablas disponibles";
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingUpdates.Count == 0) return;

            var confirm = MessageBox.Show(
                $"\u00bfAplicar {_pendingUpdates.Count} cambios al modelo Revit?",
                "BIM Pills \u2014 Gestionar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                var result = _documentServices.ApplyParameterUpdates(_pendingUpdates);

                StatusLabel.Text = $"Aplicados: {result.Updated} · Omitidos: {result.Skipped}";

                var msg = $"Operaci\u00f3n completada.\n\n\u2022 Actualizados: {result.Updated}\n\u2022 Omitidos: {result.Skipped}";
                if (result.Errors.Count > 0)
                    msg += $"\n\u2022 Errores: {result.Errors.Count}\n{string.Join("\n", result.Errors.Take(5))}";

                MessageBox.Show(msg, "BIM Pills \u2014 Importar", MessageBoxButton.OK,
                    result.Errors.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

                _pendingUpdates.Clear();
                DiffGrid.ItemsSource = null;
                ImportBar.Visibility = Visibility.Collapsed;
                DiffArea.Visibility  = Visibility.Collapsed;

                // Reload data and restore preview panel
                if (ScheduleListBox.SelectedItem is ScheduleInfo sel)
                {
                    _currentData = _documentServices.GetScheduleData(sel.Id);
                    RefreshPreviewGrid();
                    PreviewArea.Visibility = Visibility.Visible;
                }
                else
                {
                    EmptyState.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al aplicar cambios: {ex.Message}", "BIM Pills \u2014 Error");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
