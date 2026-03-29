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
        private readonly ObservableCollection<WorksetViewModel> _worksets;

        // BIM-CA template data
        private List<TemplateWorksetItem> _templateItems = new();
        private Dictionary<string, DisciplineGroup> _disciplineGroups = new();

        public GestionWindow(
            GestionResult result,
            Func<string, bool>? createCallback,
            Func<long, string, bool>? renameCallback)
        {
            InitializeComponent();

            _result = result;
            _createCallback = createCallback;
            _renameCallback = renameCallback;

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

            // Load built-in BIM-CA template
            LoadBimCaTemplate();
        }

        // ── Tab switching ─────────────────────────────────────────────────────

        private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source != MainTabs) return;

            bool isTemplateTab = MainTabs.SelectedIndex == 1;
            if (WorksetFooterButtons != null)
                WorksetFooterButtons.Visibility = isTemplateTab ? Visibility.Collapsed : Visibility.Visible;
            if (TemplateFooterButtons != null)
                TemplateFooterButtons.Visibility = isTemplateTab ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Footer ────────────────────────────────────────────────────────────

        private void UpdateFooter()
        {
            var total = _worksets.Sum(w => w.ElementCount);
            WorksetCountText.Text = $"{_worksets.Count} subproyectos — {total:N0} elementos en total";
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Tab 1: Subproyectos
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
                    "BIMPills", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                        "BIMPills", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    "BIMPills", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al importar: {ex.Message}",
                    "BIMPills", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    "BIMPills", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar: {ex.Message}",
                    "BIMPills", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    "BIMPills", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (openResult == MessageBoxResult.Yes)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = dlg.FileName, UseShellExecute = true
                    });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al crear la plantilla: {ex.Message}",
                    "BIMPills", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Tab 2: Plantillas BIM-CA
        // ══════════════════════════════════════════════════════════════════════

        // Discipline metadata: code → (full name, color hex)
        private static readonly Dictionary<string, (string Name, string Color)> DisciplineInfo = new()
        {
            { "00-GEN", ("General", "#78909C") },
            { "01-ARQ", ("Arquitectura", "#EF6337") },
            { "02-OCV", ("Obra Civil", "#8D6E63") },
            { "03-EST", ("Estructura", "#5C6BC0") },
            { "04-IHS", ("Instalaciones Hidrosanitarias", "#26A69A") },
            { "04-HID", ("Hidráulica", "#00ACC1") },
            { "04-SAN", ("Sanitaria", "#26A69A") },
            { "05-ELE", ("Electricidad", "#FFA726") },
            { "06-CLI", ("Climatización", "#42A5F5") },
            { "06-VEN", ("Ventilación", "#66BB6A") },
            { "07-PCI", ("Protección Contra Incendio", "#EF5350") },
            { "08-IND", ("Industrial", "#AB47BC") },
            { "09-EQU", ("Equipos", "#7E57C2") },
        };

        private void LoadBimCaTemplate()
        {
            LoadTemplateFromLines(GetBuiltInBimCaLines());
        }

        private void LoadTemplateFromLines(IEnumerable<string> lines)
        {
            _templateItems.Clear();
            _disciplineGroups.Clear();
            TemplateGroupsPanel.Children.Clear();

            // Existing workset names for duplicate detection
            var existingNames = new HashSet<string>(
                _worksets.Select(w => w.Name), StringComparer.OrdinalIgnoreCase);

            foreach (var line in lines)
            {
                var name = line.Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;

                // Extract discipline prefix (first two parts: "01-ARQ", "05-ELE", etc.)
                var parts = name.Split('-');
                string disciplineKey = parts.Length >= 2 ? $"{parts[0]}-{parts[1]}" : "00-GEN";

                if (!_disciplineGroups.ContainsKey(disciplineKey))
                    _disciplineGroups[disciplineKey] = new DisciplineGroup(disciplineKey);

                var item = new TemplateWorksetItem
                {
                    Name = name,
                    DisciplineKey = disciplineKey,
                    IsSelected = false,
                    AlreadyExists = existingNames.Contains(name)
                };

                _templateItems.Add(item);
                _disciplineGroups[disciplineKey].Items.Add(item);
            }

            // Build UI for each discipline group
            foreach (var kvp in _disciplineGroups.OrderBy(k => k.Key))
            {
                var group = kvp.Value;
                var hasDi = DisciplineInfo.TryGetValue(kvp.Key, out var di);
                var disciplineName = hasDi ? di.Name : kvp.Key;
                var disciplineColorHex = hasDi ? di.Color : "#78909C";
                var color = (Color)ColorConverter.ConvertFromString(disciplineColorHex);

                // ── Group header ──
                var headerBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(20, color.R, color.G, color.B)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 7, 10, 7),
                    Margin = new Thickness(0, 10, 0, 4),
                    Cursor = Cursors.Hand,
                    BorderBrush = new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B)),
                    BorderThickness = new Thickness(1)
                };

                var headerGrid = new Grid();
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Group checkbox
                var groupCheckBox = new CheckBox
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0),
                    Tag = kvp.Key
                };
                groupCheckBox.Click += GroupCheckBox_Click;
                group.GroupCheckBox = groupCheckBox;
                Grid.SetColumn(groupCheckBox, 0);

                // Discipline color dot
                var dot = new Border
                {
                    Width = 10, Height = 10,
                    CornerRadius = new CornerRadius(5),
                    Background = new SolidColorBrush(color),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };

                var namePanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                namePanel.Children.Add(dot);
                namePanel.Children.Add(new TextBlock
                {
                    Text = disciplineName,
                    FontSize = 12, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)),
                    FontFamily = new FontFamily("Segoe UI"),
                    VerticalAlignment = VerticalAlignment.Center
                });
                namePanel.Children.Add(new TextBlock
                {
                    Text = $"  ({kvp.Key})",
                    FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0x86, 0x86, 0x8B)),
                    FontFamily = new FontFamily("Segoe UI"),
                    VerticalAlignment = VerticalAlignment.Center
                });
                Grid.SetColumn(namePanel, 1);

                // Count badge
                var countBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(30, color.R, color.G, color.B)),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(8, 2, 8, 2),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                countBadge.Child = new TextBlock
                {
                    Text = $"{group.Items.Count}",
                    FontSize = 10, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(color),
                    FontFamily = new FontFamily("Segoe UI")
                };
                Grid.SetColumn(countBadge, 2);

                // Expand/collapse chevron
                var chevron = new TextBlock
                {
                    Text = "\u25B6", FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x86, 0x86, 0x8B)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                Grid.SetColumn(chevron, 3);

                headerGrid.Children.Add(groupCheckBox);
                headerGrid.Children.Add(namePanel);
                headerGrid.Children.Add(countBadge);
                headerGrid.Children.Add(chevron);
                headerBorder.Child = headerGrid;

                // ── Items panel (collapsible — collapsed by default) ──
                var itemsPanel = new StackPanel { Margin = new Thickness(32, 0, 0, 0), Visibility = Visibility.Collapsed };
                group.ItemsPanel = itemsPanel;

                foreach (var item in group.Items)
                {
                    var itemBorder = new Border
                    {
                        Padding = new Thickness(8, 5, 8, 5),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7)),
                        BorderThickness = new Thickness(0, 0, 0, 1)
                    };

                    var itemGrid = new Grid();
                    itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var cb = new CheckBox
                    {
                        IsChecked = item.IsSelected,
                        IsEnabled = !item.AlreadyExists,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 8, 0),
                        Tag = item
                    };
                    cb.Click += ItemCheckBox_Click;
                    item.CheckBox = cb;
                    Grid.SetColumn(cb, 0);

                    // Display: code bold + description readable
                    // e.g. "01-ARQ-MUE-Mobiliario" → code="MUE" description="Mobiliario"
                    var displayParts = item.Name.Split('-');
                    string codeTag = displayParts.Length >= 3 ? displayParts[2] : "";
                    string description = displayParts.Length >= 4 ? displayParts[3] : item.Name;

                    // Insert spaces before capitals for readability: "AcabadosInteriores" → "Acabados Interiores"
                    var readableDesc = System.Text.RegularExpressions.Regex.Replace(
                        description, "(?<=[a-z])(?=[A-Z])", " ");

                    var disabledColor = new SolidColorBrush(Color.FromRgb(0xAE, 0xAE, 0xB2));
                    var normalColor = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E));
                    var codeColor = new SolidColorBrush(Color.FromRgb(0x86, 0x86, 0x8B));

                    var namePanel2 = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    namePanel2.Children.Add(new TextBlock
                    {
                        Text = codeTag,
                        FontSize = 10,
                        FontWeight = FontWeights.Bold,
                        FontFamily = new FontFamily("Consolas"),
                        Foreground = item.AlreadyExists ? disabledColor : codeColor,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 8, 0),
                        MinWidth = 30
                    });
                    namePanel2.Children.Add(new TextBlock
                    {
                        Text = readableDesc,
                        FontSize = 11.5,
                        FontFamily = new FontFamily("Segoe UI"),
                        Foreground = item.AlreadyExists ? disabledColor : normalColor,
                        VerticalAlignment = VerticalAlignment.Center,
                        ToolTip = item.Name
                    });
                    Grid.SetColumn(namePanel2, 1);

                    itemGrid.Children.Add(cb);
                    itemGrid.Children.Add(namePanel2);

                    if (item.AlreadyExists)
                    {
                        var existsBadge = new Border
                        {
                            Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xF5, 0xE9)),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(6, 1, 6, 1),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        existsBadge.Child = new TextBlock
                        {
                            Text = "Ya existe",
                            FontSize = 9,
                            Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)),
                            FontFamily = new FontFamily("Segoe UI")
                        };
                        Grid.SetColumn(existsBadge, 2);
                        itemGrid.Children.Add(existsBadge);
                    }

                    itemBorder.Child = itemGrid;
                    itemsPanel.Children.Add(itemBorder);
                }

                // Toggle collapse on header click
                headerBorder.MouseLeftButtonDown += (s, ev) =>
                {
                    if (ev.Source is CheckBox) return; // Don't toggle when clicking checkbox
                    itemsPanel.Visibility = itemsPanel.Visibility == Visibility.Visible
                        ? Visibility.Collapsed : Visibility.Visible;
                    chevron.Text = itemsPanel.Visibility == Visibility.Visible ? "\u25BC" : "\u25B6";
                };

                TemplateGroupsPanel.Children.Add(headerBorder);
                TemplateGroupsPanel.Children.Add(itemsPanel);
            }

            UpdateTemplateSelectionCount();
        }

        private void GroupCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb || cb.Tag is not string key) return;
            if (!_disciplineGroups.TryGetValue(key, out var group)) return;

            bool check = cb.IsChecked == true;
            foreach (var item in group.Items.Where(i => !i.AlreadyExists))
            {
                item.IsSelected = check;
                if (item.CheckBox != null)
                    item.CheckBox.IsChecked = check;
            }

            UpdateTemplateSelectionCount();
        }

        private void ItemCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is TemplateWorksetItem item)
            {
                item.IsSelected = cb.IsChecked == true;

                // Update group checkbox state
                if (_disciplineGroups.TryGetValue(item.DisciplineKey, out var group) && group.GroupCheckBox != null)
                {
                    var selectableItems = group.Items.Where(i => !i.AlreadyExists).ToList();
                    if (selectableItems.All(i => i.IsSelected))
                        group.GroupCheckBox.IsChecked = true;
                    else if (selectableItems.Any(i => i.IsSelected))
                        group.GroupCheckBox.IsChecked = null;
                    else
                        group.GroupCheckBox.IsChecked = false;
                }
            }
            UpdateTemplateSelectionCount();
        }

        private void SelectAllTemplates_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _templateItems.Where(i => !i.AlreadyExists))
            {
                item.IsSelected = true;
                if (item.CheckBox != null) item.CheckBox.IsChecked = true;
            }
            foreach (var group in _disciplineGroups.Values)
                if (group.GroupCheckBox != null) group.GroupCheckBox.IsChecked = true;
            UpdateTemplateSelectionCount();
        }

        private void DeselectAllTemplates_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _templateItems)
            {
                item.IsSelected = false;
                if (item.CheckBox != null) item.CheckBox.IsChecked = false;
            }
            foreach (var group in _disciplineGroups.Values)
                if (group.GroupCheckBox != null) group.GroupCheckBox.IsChecked = false;
            UpdateTemplateSelectionCount();
        }

        private void UpdateTemplateSelectionCount()
        {
            int count = _templateItems.Count(i => i.IsSelected);
            TemplateSelectionText.Text = count > 0
                ? $"{count} de {_templateItems.Count(i => !i.AlreadyExists)} seleccionados"
                : "";
            if (CreateBtnLabel != null)
                CreateBtnLabel.Text = count > 0 ? $"Crear {count} subproyectos" : "Crear seleccionados";
            if (BtnCreateSelected != null)
                BtnCreateSelected.IsEnabled = count > 0;
        }

        private void CreateSelectedWorksets_Click(object sender, RoutedEventArgs e)
        {
            var selected = _templateItems.Where(i => i.IsSelected && !i.AlreadyExists).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Selecciona al menos un subproyecto para crear.", "BIMPills");
                return;
            }

            var confirm = MessageBox.Show(
                $"¿Crear {selected.Count} subproyectos en el modelo?\n\nEsta acción no se puede deshacer.",
                "BIMPills — Estandarizar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            int created = 0, failed = 0;
            foreach (var item in selected)
            {
                if (_createCallback != null && _createCallback(item.Name))
                {
                    _worksets.Add(new WorksetViewModel(new WorksetInfo
                    {
                        Name = item.Name, IsOpen = true, IsEditable = true, Owner = "", ElementCount = 0
                    }));
                    item.AlreadyExists = true;
                    item.IsSelected = false;
                    if (item.CheckBox != null)
                    {
                        item.CheckBox.IsChecked = false;
                        item.CheckBox.IsEnabled = false;
                    }
                    created++;
                }
                else
                {
                    failed++;
                }
            }

            UpdateFooter();
            UpdateTemplateSelectionCount();

            var msg = $"Se crearon {created} subproyectos.";
            if (failed > 0) msg += $"\n{failed} no se pudieron crear (posiblemente duplicados).";
            MessageBox.Show(msg, "BIMPills — Estandarizar", MessageBoxButton.OK,
                failed > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }

        private void LoadCustomTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Texto (*.txt)|*.txt|Todos los archivos (*.*)|*.*",
                Title = "Cargar plantilla de subproyectos"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var lines = File.ReadAllLines(dlg.FileName);
                // Refresh existing names in case worksets were created
                LoadTemplateFromLines(lines);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar el archivo: {ex.Message}", "BIMPills — Error");
            }
        }

        // ── Built-in BIM-CA standard ──────────────────────────────────────────

        private static IEnumerable<string> GetBuiltInBimCaLines()
        {
            return new[]
            {
                "01-ARQ-MUE-Mobiliario",
                "01-ARQ-FIN-AcabadosInteriores",
                "01-ARQ-MAM-MurosMamposteria",
                "01-ARQ-SUE-AcabadosSuelosExteriores",
                "01-ARQ-SUI-AcabadosSuelosInteriores",
                "01-ARQ-TYS-TabiquesTablayeso",
                "01-ARQ-FEX-AcabadosExteriores",
                "01-ARQ-INT-Interiorismo",
                "01-ARQ-RUT-RutasEvacuacion",
                "01-ARQ-CIE-CielosExteriores",
                "01-ARQ-CII-CielosInteriores",
                "01-ARQ-ENT-Entorno",
                "01-ARQ-BAS-ExtraccionBasura",
                "01-ARQ-URB-MobiliarioUrbano",
                "01-ARQ-CAR-Carpinteria",
                "01-ARQ-PAV-Pavimentos",
                "01-ARQ-BCL-BasuraResiduosClinicos",
                "01-ARQ-PAI-PaisajismoVegetacion",
                "01-ARQ-ACU-Acustica",
                "01-ARQ-HER-Herreria",
                "01-ARQ-PUE-Puertas",
                "01-ARQ-VEN-Ventanas",
                "01-ARQ-MCO-MurosCortina",
                "01-ARQ-SEV-SenaleticaVial",
                "01-ARQ-SEI-SenaleticaInstitucional",
                "01-ARQ-SES-SenaleticaSeguridad",
                "05-ELE-ACO-AcometidaElectricidad",
                "05-ELE-SAC-SubAcometidaElectricidad",
                "05-ELE-CEL-CanalizacionElectrica",
                "05-ELE-TIE-TierraFisica",
                "05-ELE-PRY-Pararrayos",
                "05-ELE-ALU-Alumbrado",
                "05-ELE-DEL-DispositivosElectricidad",
                "05-ELE-REG-FuerzaRegulada",
                "05-ELE-UPS-FuerzaRespaldo",
                "05-ELE-ATL-AcometidaTelecomunicaciones",
                "05-ELE-SAT-SubAcometidaTelecomunicaciones",
                "05-ELE-CTL-CanalizacionTelecomunicaciones",
                "05-ELE-TEL-DispositivosTelecomunicaciones",
                "05-ELE-SEG-Seguridad",
                "05-ELE-CTV-CircuitoCerradoTV",
                "05-ELE-VOD-VozDatos",
                "05-ELE-AUD-Audio",
                "05-ELE-COD-CorrientesDebiles",
                "05-ELE-RAD-Radiocomunicacion",
                "05-ELE-DOM-Domotica",
                "00-GEN-OPE-OperacionMantenimiento",
                "00-GEN-PEL-MaterialesPeligrososNocivos",
                "00-GEN-PRO-Procesos",
                "00-GEN-NIV-NivelesYEjesCompartidos",
                "00-GEN-SCP-CajasYPlanosReferencia",
                "00-GEN-ARE-Areas",
                "00-GEN-ESP-Espacios",
                "00-GEN-HAB-Habitaciones",
                "00-GEN-ARQ-VinculoArquitectura",
                "00-GEN-EST-VinculoEstructura",
                "00-GEN-ELE-VinculoElectricidad",
                "00-GEN-IHS-VinculoHidrosanitarias",
                "00-GEN-MEC-VinculoMecanicas",
                "00-GEN-ESP-VinculoEspeciales",
                "00-GEN-EMP-VinculoEmplazamiento",
                "02-OCV-TOP-Topografia",
                "02-OCV-ACT-LevantamientoEstadoActual",
                "02-OCV-GEO-Geotecnia",
                "02-OCV-MOV-PlataformasMovimientoTierra",
                "03-EST-CIM-Cimentacion",
                "03-EST-MCC-MurosColumnasInSitu",
                "03-EST-MCP-MurosColumnasPrefabricados",
                "03-EST-MCA-MurosColumnasAcero",
                "03-EST-FYC-FachadasCubiertas",
                "03-EST-VEC-VigasEntrepisosInSitu",
                "03-EST-VEP-VigasEntrepisosPrefabricados",
                "03-EST-VEA-VigasEntrepisosAcero",
                "03-EST-EDS-EstabilizacionSuelo",
                "03-EST-RBR-Armadura",
                "06-CLI-INY-InyeccionAireClimatizado",
                "06-CLI-RET-RetornoAireClimatizado",
                "06-CLI-EXA-ExtraccionAireViciadoClimatizado",
                "06-CLI-FRE-RenovacionAireFrescoClimatizacion",
                "06-CLI-SRF-SuministroAguaHeladaRefrigeracion",
                "06-CLI-RRF-RetornoAguaHeladaRefrigeracion",
                "06-CLI-SCL-SuministroAguaCalienteCalefaccion",
                "06-CLI-RCL-RetornoAguaCalienteCalefaccion",
                "06-VEN-FPA-SuministroVentilacionParqueos",
                "06-VEN-EPA-ExtraccionForzadaParqueos",
                "06-VEN-SVV-SuministroVentilacionVivienda",
                "06-VEN-EVV-ExtraccionForzadaVentilacionVivienda",
                "06-VEN-RVV-RetornoVentilacionVivienda",
                "06-VEN-FVV-AportacionVentilacionVivienda",
                "06-VEN-ECO-ExtraccionCocinas",
                "06-VEN-EEG-ExtraccionEscapeGases",
                "06-VEN-FSA-SuministroAireFresco",
                "06-VEN-EAV-ExtraccionAireViciado",
                "06-VEN-SSP-SistemaSobrepresion",
                "04-IHS-ASA-AparatosSanitarios",
                "04-HID-AFA-AcometidaAguaFria",
                "04-HID-AFR-SuministroAguaFria",
                "04-HID-ACS-SuministroAguaCaliente",
                "04-HID-ACR-RetornoAguaCaliente",
                "04-HID-RIE-Riego",
                "04-HID-APO-AguaPotable",
                "04-HID-ANP-AguaNoPotable",
                "04-SAN-VES-VentilacionSanitaria",
                "04-SAN-EXS-ExtraccionSecadoras",
                "04-SAN-DAN-DrenajeAguasNegras",
                "04-SAN-DAG-DrenajeAguasGrises",
                "04-SAN-DPL-DrenajePluvial",
                "04-SAN-DIN-DrenajeInundacion",
                "04-SAN-DCN-DrenajeCondensacion",
                "04-SAN-BIO-DesechosBiologicos",
                "04-SAN-ATR-AguasTratadas",
                "04-SAN-DPR-DrenajePresion",
                "04-SAN-DRD-DrenajeDescarga",
                "07-PCI-DET-DeteccionIncendio",
                "07-PCI-RHU-RedHumedaBIES",
                "07-PCI-RHS-RedHumedaSprinklers",
                "07-PCI-RSE-RedSeca",
                "07-PCI-EXH-ExtincionIncendioHalon",
                "07-PCI-EXG-ExtincionIncendioGasInerte",
                "07-PCI-EXT-ExtincionIncendioCO2",
                "08-IND-RFI-TuberiaDesconocida",
                "08-IND-GLP-GasPropano",
                "08-IND-VCB-VentilacionCombustible",
                "08-IND-CCB-CargaCombustible",
                "08-IND-GVP-GasolinaExtraSuperior",
                "08-IND-GSP-GasolinaSuperior",
                "08-IND-GRG-GasolinaRegular",
                "08-IND-DSP-DieselSuperior",
                "08-IND-DRG-DieselRegular",
                "08-IND-ACE-Aceite",
                "08-IND-AIR-AireComprimido",
                "08-IND-NIT-Nitrogeno",
                "08-IND-VAP-VaporAltaPresion",
                "08-IND-VMP-VaporMediaPresion",
                "08-IND-VBP-VaporBajaPresion",
                "08-IND-VAC-Vacio",
                "08-IND-GLB-GasLaboratorio",
                "08-IND-GMD-GasMedico",
                "08-IND-OXI-Oxigeno",
                "09-EQU-EQU-Equipos"
            };
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
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    internal class TemplateWorksetItem
    {
        public string Name { get; set; } = "";
        public string DisciplineKey { get; set; } = "";
        public bool IsSelected { get; set; }
        public bool AlreadyExists { get; set; }
        public CheckBox? CheckBox { get; set; }
    }

    internal class DisciplineGroup
    {
        public string Key { get; }
        public List<TemplateWorksetItem> Items { get; } = new();
        public CheckBox? GroupCheckBox { get; set; }
        public StackPanel? ItemsPanel { get; set; }

        public DisciplineGroup(string key) => Key = key;
    }
}
