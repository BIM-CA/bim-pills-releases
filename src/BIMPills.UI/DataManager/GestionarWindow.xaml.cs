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

        public GestionarWindow(IDocumentServices documentServices, string modelName = "Modelo")
        {
            InitializeComponent();
            _documentServices = documentServices;
            _modelName = Path.GetFileNameWithoutExtension(modelName);

            ModelNameLabel.Text = modelName;

            // Default export path
            var defaultFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "BIMPills", "Exports");
            ExportPathBox.Text = defaultFolder;

            LoadSchedules();
        }

        private void LoadSchedules()
        {
            try
            {
                _allSchedules = _documentServices.GetSchedules()?.ToList()
                                ?? new List<ScheduleInfo>();
                ScheduleListBox.ItemsSource = _allSchedules;
                UpdateSelectionCount();
                StatusLabel.Text = $"{_allSchedules.Count} tablas disponibles";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error cargando tablas: {ex.Message}", "BIMPills — Error");
            }
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
                MessageBox.Show($"Error cargando datos: {ex.Message}", "BIMPills — Error");
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
                MessageBox.Show("Selecciona al menos una tabla para exportar.", "BIMPills — Gestionar");
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
                MessageBox.Show($"Error al exportar: {ex.Message}", "BIMPills — Error");
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
                MessageBox.Show($"Error al exportar: {ex.Message}", "BIMPills — Error");
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
                    MessageBox.Show("Las tablas seleccionadas no contienen datos.", "BIMPills — Gestionar");
                    return;
                }

                var path = _exporter.ExportMultiple(allData, folder, fileName);
                StatusLabel.Text = $"Exportadas {allData.Count} tablas → {Path.GetFileName(path)}";
                AskOpenFile(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar: {ex.Message}", "BIMPills — Error");
            }
        }

        private void AskOpenFile(string path)
        {
            var result = MessageBox.Show(
                $"Archivo exportado:\n{path}\n\n¿Abrir en Excel?",
                "BIMPills — Exportar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
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
                        "BIMPills — Importar");
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
                    MessageBox.Show("No se detectaron cambios en el archivo.", "BIMPills — Importar");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al leer el archivo: {ex.Message}", "BIMPills — Error");
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
                $"¿Aplicar {_pendingUpdates.Count} cambios al modelo Revit?",
                "BIMPills — Gestionar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                var result = _documentServices.ApplyParameterUpdates(_pendingUpdates);

                StatusLabel.Text = $"Aplicados: {result.Updated} · Omitidos: {result.Skipped}";

                var msg = $"Operación completada.\n\n• Actualizados: {result.Updated}\n• Omitidos: {result.Skipped}";
                if (result.Errors.Count > 0)
                    msg += $"\n• Errores: {result.Errors.Count}\n{string.Join("\n", result.Errors.Take(5))}";

                MessageBox.Show(msg, "BIMPills — Importar", MessageBoxButton.OK,
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
                MessageBox.Show($"Error al aplicar cambios: {ex.Message}", "BIMPills — Error");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
