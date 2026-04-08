using BIMPills.Commands.Gestion;
using BIMPills.Core.Gestion;
using ClosedXML.Excel;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BIMPills.UI.Gestion
{
    public partial class GestionWindow : Window
    {
        private readonly GestionResult _result;
        private readonly Func<string, bool>? _createCallback;
        private readonly Func<long, string, bool>? _renameCallback;
        private readonly Func<View3DCreationConfig, View3DCreationResult>? _createViewsCallback;
        private readonly ObservableCollection<WorksetViewModel> _worksets;

        public GestionWindow(
            GestionResult result,
            Func<string, bool>? createCallback,
            Func<long, string, bool>? renameCallback,
            Func<View3DCreationConfig, View3DCreationResult>? createViewsCallback = null)
        {
            InitializeComponent();
            BIMPills.UI.Shared.ThemeHelper.Apply(this);

            _result = result;
            _createCallback = createCallback;
            _renameCallback = renameCallback;
            _createViewsCallback = createViewsCallback;

            TitleText.Text = result.DocumentTitle;
            SubtitleText.Text = result.IsWorkshared
                ? "Modelo central — Worksharing activo"
                : "Modelo local";

            if (!result.IsWorkshared)
            {
                WorksharingNotice.Visibility = Visibility.Collapsed;
                NoWorksharingNotice.Visibility = Visibility.Visible;
                BtnNewWorkset.IsEnabled = false;
            }

            _worksets = new ObservableCollection<WorksetViewModel>(
                result.Worksets.Select(w => new WorksetViewModel(w)));

            WorksetsGrid.ItemsSource = _worksets;
            Shared.ScrollHintHelper.Attach(WorksetsGrid, ScrollUpHint, ScrollDownHint);
            UpdateFooter();
        }

        // ── Footer ────────────────────────────────────────────────────────────

        private void UpdateFooter()
        {
            var total = _worksets.Sum(w => w.ElementCount);
            WorksetCountText.Text = $"{_worksets.Count} subproyectos — {total:N0} elementos en total";
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Subproyectos
        // ══════════════════════════════════════════════════════════════════════

        private void NewWorkset_Click(object sender, RoutedEventArgs e)
        {
            var name = PromptForName("Nuevo subproyecto", "Nombre del subproyecto:", "");
            if (string.IsNullOrWhiteSpace(name)) return;

            if (_createCallback != null && _createCallback(name))
            {
                _worksets.Add(new WorksetViewModel(new WorksetInfo
                {
                    Name = name,
                    IsOpen = true,
                    IsEditable = true,
                    Owner = "",
                    ElementCount = 0
                }));
                UpdateFooter();
            }
            else
            {
                MessageBox.Show(
                    "No se pudo crear el subproyecto. Verifica que el nombre no esté duplicado.",
                    "BIM Pills", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void WorksetsGrid_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (WorksetsGrid.SelectedItem is WorksetViewModel ws)
            {
                var newName = PromptForName("Renombrar subproyecto", "Nuevo nombre:", ws.Name);
                if (string.IsNullOrWhiteSpace(newName) || newName == ws.Name) return;

                if (_renameCallback != null && _renameCallback(ws.Id, newName))
                {
                    ws.Name = newName;
                }
                else
                {
                    MessageBox.Show(
                        "No se pudo renombrar el subproyecto.",
                        "BIM Pills", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void ImportExcel_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Excel (*.xlsx)|*.xlsx",
                Title = "Importar subproyectos"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                int created = 0;

                using (var workbook = new XLWorkbook(dlg.FileName))
                {
                    var sheet = workbook.Worksheets.First();
                    int lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;

                    for (int row = 2; row <= lastRow; row++)
                    {
                        var name = sheet.Cell(row, 1).GetString().Trim();
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        if (_createCallback != null && _createCallback(name))
                        {
                            _worksets.Add(new WorksetViewModel(new WorksetInfo
                            {
                                Name = name, IsOpen = true, IsEditable = true, Owner = "", ElementCount = 0
                            }));
                            created++;
                        }
                    }
                }

                UpdateFooter();
                MessageBox.Show(
                    $"Se crearon {created} subproyectos desde el archivo.",
                    "BIM Pills", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al importar: {ex.Message}",
                    "BIM Pills", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Excel (*.xlsx)|*.xlsx",
                Title = "Exportar subproyectos",
                FileName = $"Subproyectos_{_result.DocumentTitle}.xlsx"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                using (var workbook = new XLWorkbook())
                {
                    var sheet = workbook.Worksheets.Add("Subproyectos");
                    sheet.Cell(1, 1).Value = "Nombre";
                    sheet.Cell(1, 2).Value = "Estado";
                    sheet.Cell(1, 3).Value = "Elementos";
                    sheet.Cell(1, 4).Value = "Propietario";
                    sheet.Cell(1, 5).Value = "Editable";

                    var headerRange = sheet.Range(1, 1, 1, 5);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#217346");
                    headerRange.Style.Font.FontColor = XLColor.White;

                    for (int i = 0; i < _worksets.Count; i++)
                    {
                        var ws = _worksets[i];
                        int row = i + 2;
                        sheet.Cell(row, 1).Value = ws.Name;
                        sheet.Cell(row, 2).Value = ws.StatusLabel;
                        sheet.Cell(row, 3).Value = ws.ElementCount;
                        sheet.Cell(row, 4).Value = ws.Owner;
                        sheet.Cell(row, 5).Value = ws.EditableLabel;
                    }

                    sheet.Columns().AdjustToContents();
                    workbook.SaveAs(dlg.FileName);
                }

                MessageBox.Show($"Exportado a: {dlg.FileName}",
                    "BIM Pills", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar: {ex.Message}",
                    "BIM Pills", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DownloadTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Excel (*.xlsx)|*.xlsx",
                Title = "Guardar plantilla de subproyectos",
                FileName = "Plantilla_Subproyectos.xlsx"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                using (var workbook = new XLWorkbook())
                {
                    var sheet = workbook.Worksheets.Add("Subproyectos");
                    sheet.Cell(1, 1).Value = "Nombre";
                    sheet.Cell(1, 2).Value = "Estado";

                    var headerRange = sheet.Range(1, 1, 1, 2);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#217346");
                    headerRange.Style.Font.FontColor = XLColor.White;

                    sheet.Cell(2, 1).Value = "ARQ - Arquitectura";
                    sheet.Cell(2, 2).Value = "Abierto";
                    sheet.Cell(3, 1).Value = "EST - Estructura";
                    sheet.Cell(3, 2).Value = "Abierto";

                    var exampleRange = sheet.Range(2, 1, 3, 2);
                    exampleRange.Style.Font.Italic = true;
                    exampleRange.Style.Font.FontColor = XLColor.FromHtml("#86868B");

                    sheet.Columns().AdjustToContents();
                    workbook.SaveAs(dlg.FileName);
                }

                var openResult = MessageBox.Show(
                    $"Plantilla guardada en:\n\n{dlg.FileName}\n\n¿Desea abrirla?",
                    "BIM Pills", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (openResult == MessageBoxResult.Yes)
                    Helpers.ProcessHelper.OpenDocument(dlg.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al crear la plantilla: {ex.Message}",
                    "BIM Pills", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WorksetCheckBox_Click(object sender, RoutedEventArgs e)
        {
            int count = _worksets.Count(w => w.IsSelected);
            BtnCreateViews3D.IsEnabled = count > 0;
        }

        private void BimCa_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new BimCaWindow(
                new HashSet<string>(_worksets.Select(w => w.Name), StringComparer.OrdinalIgnoreCase),
                _createCallback)
            {
                Owner = this
            };
            dlg.ShowDialog();
            foreach (var ws in dlg.CreatedWorksets)
            {
                if (!_worksets.Any(w => w.Name.Equals(ws.Name, StringComparison.OrdinalIgnoreCase)))
                    _worksets.Add(new WorksetViewModel(ws));
            }
            UpdateFooter();
        }

        private void CreateViews3D_Click(object sender, RoutedEventArgs e)
        {
            var selected = _worksets.Where(w => w.IsSelected).ToList();
            var dlg = new Create3DViewsWindow(_worksets, selected, _createViewsCallback)
            {
                Owner = this
            };
            dlg.ShowDialog();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private string? PromptForName(string title, string prompt, string defaultValue)
        {
            var win = new Window
            {
                Title = title,
                Width = 380, Height = 160,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = Brushes.White
            };

            var panel = new StackPanel { Margin = new Thickness(16) };
            var label = new TextBlock { Text = prompt, FontSize = 12, Margin = new Thickness(0, 0, 0, 8) };
            var textBox = new TextBox { Text = defaultValue, FontSize = 13, Padding = new Thickness(6, 4, 6, 4) };
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            var btnOk = new Button { Content = "Aceptar", Width = 80, Padding = new Thickness(0, 4, 0, 4), IsDefault = true };
            var btnCancel = new Button { Content = "Cancelar", Width = 80, Padding = new Thickness(0, 4, 0, 4), Margin = new Thickness(6, 0, 0, 0), IsCancel = true };

            string? result = null;
            btnOk.Click += (s, ev) => { result = textBox.Text; win.Close(); };
            btnCancel.Click += (s, ev) => win.Close();

            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            panel.Children.Add(label);
            panel.Children.Add(textBox);
            panel.Children.Add(btnPanel);
            win.Content = panel;
            win.ShowDialog();

            return result;
        }
    }

    // ── View Models ───────────────────────────────────────────────────────────

    public sealed class WorksetViewModel : INotifyPropertyChanged
    {
        private string _name;

        public WorksetViewModel(WorksetInfo info)
        {
            Id = info.Id;
            _name = info.Name;
            IsOpen = info.IsOpen;
            IsDefault = info.IsDefault;
            IsEditable = info.IsEditable;
            Owner = string.IsNullOrEmpty(info.Owner) ? "—" : info.Owner;
            ElementCount = info.ElementCount;
        }

        public long Id { get; }
        public string Name
        {
            get => _name;
            set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); }
        }
        public bool IsOpen { get; }
        public bool IsDefault { get; }
        public bool IsEditable { get; }
        public string Owner { get; }
        public int ElementCount { get; }
        public string StatusLabel => IsOpen ? "Abierto" : "Cerrado";
        public string EditableLabel => IsEditable ? "Sí" : "No";

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
