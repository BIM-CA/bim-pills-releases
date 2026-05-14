using BIMPills.Commands.ModelAudit;
using BIMPills.Core.Audit;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace BIMPills.UI.ModelAudit
{
    public partial class ModelAuditWindow : Window
    {
        public string WarningsHeader   => $"Advertencias ({_result.Warnings.Count})";
        public string FamiliesHeader   => $"Familias ({_result.Families.Count})";
        public string ViewsHeader      => $"Vistas sin colocar ({_result.UnplacedViews.Count})";
        public string OrphansHeader    => $"Elementos huérfanos ({_result.OrphanElements.Count})";
        public string PurgeableHeader  => $"Purgables ({_result.PurgeableItems.Count})";

        private readonly ModelAuditResult _result;
        /// <summary>Recibe IDs a eliminar, retorna los IDs que realmente se eliminaron.</summary>
        private readonly Func<IReadOnlyList<long>, IReadOnlyList<long>>? _purgeCallback;
        private List<PurgeableItemViewModel> _allPurgeableViewModels = new List<PurgeableItemViewModel>();
        private List<PurgeableItemViewModel> _purgeableViewModels = new List<PurgeableItemViewModel>();
        private string? _activeTypeFilter = null;
        private string _purgeSearchText = "";
        private List<OrphanElementViewModel> _orphanViewModels = new List<OrphanElementViewModel>();

        public ModelAuditWindow(ModelAuditResult result, Func<IReadOnlyList<long>, IReadOnlyList<long>>? purgeCallback = null)
        {
            _result = result;
            _purgeCallback = purgeCallback;
            InitializeComponent();
            BIMPills.UI.Shared.ThemeHelper.Apply(this);
            DataContext = this;
            Populate();
        }

        private void Populate()
        {
            // Encabezado
            DocumentName.Text = _result.DocumentTitle;
            SubtitleText.Text = $"Modelo {(_result.IsWorkshared ? "colaborativo" : "local")}";

            // Dashboard de salud
            var health = _result.HealthScore;
            HealthScoreText.Text = health.TotalScore.ToString();
            HealthLevelText.Text = health.LevelLabel;

            var healthColor = health.Level switch
            {
                HealthLevel.Excelente => Color.FromRgb(52, 199, 89),    // Verde
                HealthLevel.Bueno     => Color.FromRgb(254, 202, 41),   // Amarillo BIM-CA
                HealthLevel.Regular   => Color.FromRgb(239, 99, 55),    // Naranja BIM-CA
                HealthLevel.Crítico   => Color.FromRgb(255, 59, 48),    // Rojo
                _ => Color.FromRgb(142, 142, 147)
            };

            HealthLevelText.Foreground = new SolidColorBrush(healthColor);
            HealthArcBrush.Color = healthColor;
            HealthFullRingBrush.Color = healthColor;
            DrawHealthArc(health.TotalScore);

            // Tarjetas de métricas
            FileSizeText.Text = _result.FileSizeLabel;
            TotalElementsText.Text = _result.TotalElements.ToString("N0");
            WarningsCountText.Text = _result.Warnings.Count.ToString("N0");
            WarningsCountText.Foreground = new SolidColorBrush(
                _result.Warnings.Count > 300 ? Color.FromRgb(255, 59, 48) :
                _result.Warnings.Count > 100 ? Color.FromRgb(239, 99, 55) :
                Color.FromRgb(33, 43, 55));
            PurgeableCountText.Text = _result.PurgeableItems.Count.ToString("N0");

            // Datos en grids
            WarningsGrid.ItemsSource = _result.Warnings;
            ViewsGrid.ItemsSource    = _result.UnplacedViews;

            // Huérfanos — wrap en ViewModels (solo informativo; los borrables se gestionan en Purgables)
            _orphanViewModels = _result.OrphanElements
                .Select(e => new OrphanElementViewModel(e))
                .ToList();
            OrphansGrid.ItemsSource = _orphanViewModels;

            // Los botones de eliminación ya no existen en esta pestaña;
            // los huérfanos borrables aparecen en la pestaña Purgables.
            DeleteOrphansButton.Visibility   = Visibility.Collapsed;
            OrphanSelectAllButton.Visibility = Visibility.Collapsed;

            UpdateOrphanSelection();

            // Scroll hints en todos los DataGrids
            Shared.ScrollHintHelper.Attach(WarningsGrid, WarningsScrollUp, WarningsScrollDown);
            Shared.ScrollHintHelper.Attach(ViewsGrid, ViewsScrollUp, ViewsScrollDown);
            Shared.ScrollHintHelper.Attach(OrphansGrid, OrphansScrollUp, OrphansScrollDown);
            Shared.ScrollHintHelper.Attach(PurgeableGrid, ScrollUpHint, ScrollDownHint);

            // Familias agrupadas por categoría
            var categoryCount = _result.Families.Select(f => f.Category).Distinct().Count();
            double totalSizeMB = _result.Families.Sum(f => f.SizeMB);
            FamiliesSummaryText.Text = $"{_result.Families.Count} familias en {categoryCount} categor\u00EDas \u2014 Tama\u00F1o total: {totalSizeMB:F1} MB";

            var familyGroups = _result.Families
                .GroupBy(f => f.Category)
                .OrderByDescending(g => g.Sum(f => f.SizeBytes))
                .Select(g => new FamilyCategoryGroup(g.Key, g.ToList()))
                .ToList();
            FamiliesGrouped.ItemsSource = familyGroups;

            // Purgables — wrap en ViewModels
            _allPurgeableViewModels = _result.PurgeableItems
                .Select(p => new PurgeableItemViewModel(p))
                .ToList();
            _purgeableViewModels = _allPurgeableViewModels;
            PurgeableGrid.ItemsSource = _purgeableViewModels;

            // Poblar ComboBox de filtro con los tipos presentes en los datos
            PopulatePurgeTypeFilter();

            // Deshabilitar purga si no hay callback
            if (_purgeCallback == null)
            {
                PurgeSelectedButton.Visibility = Visibility.Collapsed;
                SelectAllButton.Visibility = Visibility.Collapsed;
            }

            UpdatePurgeSelection();
        }

        /// <summary>
        /// Llena el ComboBox de tipo con "Todos los tipos" + los ItemType distintos
        /// presentes en los datos, ordenados alfabéticamente.
        /// </summary>
        private void PopulatePurgeTypeFilter()
        {
            PurgeTypeFilter.Items.Clear();
            PurgeTypeFilter.Items.Add("Todos los tipos");

            var types = _allPurgeableViewModels
                .Select(vm => vm.ItemType)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            foreach (var t in types)
                PurgeTypeFilter.Items.Add(t);

            PurgeTypeFilter.SelectedIndex = 0;
        }

        // ── Purge selection management ─────────────────────────────────

        private void UpdatePurgeSelection()
        {
            int selected = _purgeableViewModels.Count(vm => vm.IsSelected);
            int total = _purgeableViewModels.Count;
            long recoverableBytes = _purgeableViewModels
                .Where(vm => vm.IsSelected)
                .Sum(vm => vm.SizeBytes);
            double recoverableMB = recoverableBytes / 1_048_576.0;

            string sizeText = recoverableMB >= 0.1
                ? $" · ~{recoverableMB:F1} MB recuperables"
                : "";

            PurgeSelectionText.Text = $"{selected} de {total} seleccionados{sizeText}";
            PurgeSelectedButton.IsEnabled = selected > 0;

            // Update select all button text
            SelectAllButton.Content = selected == total && total > 0
                ? "Deseleccionar todo"
                : "Seleccionar todo";
        }

        private void ApplyPurgeFilters()
        {
            if (PurgeableGrid == null) return; // Called during XAML init before controls exist

            var filtered = _allPurgeableViewModels.AsEnumerable();

            if (!string.IsNullOrEmpty(_activeTypeFilter))
                filtered = filtered.Where(vm => vm.ItemType == _activeTypeFilter);

            if (!string.IsNullOrEmpty(_purgeSearchText))
                filtered = filtered.Where(vm =>
                    vm.Name.IndexOf(_purgeSearchText, StringComparison.OrdinalIgnoreCase) >= 0);

            _purgeableViewModels = filtered.ToList();
            PurgeableGrid.ItemsSource = _purgeableViewModels;
            UpdatePurgeSelection();
        }

        private void PurgeTypeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PurgeTypeFilter.SelectedItem is string selected && selected != "Todos los tipos")
                _activeTypeFilter = selected;
            else
                _activeTypeFilter = null;

            ApplyPurgeFilters();
        }

        private void PurgeSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _purgeSearchText = PurgeSearchBox.Text;
            ApplyPurgeFilters();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool allSelected = _purgeableViewModels.All(vm => vm.IsSelected);
            bool newValue = !allSelected;
            foreach (var vm in _purgeableViewModels)
                vm.IsSelected = newValue;

            PurgeableGrid.Items.Refresh();
            UpdatePurgeSelection();
        }

        private void PurgeCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is PurgeableItemViewModel clicked)
            {
                bool newState = cb.IsChecked == true;

                // Si hay múltiples filas seleccionadas en el grid y la fila clickeada
                // es una de ellas, aplica el mismo estado a todas (Shift+Click nativo del DataGrid).
                var highlighted = PurgeableGrid.SelectedItems
                    .OfType<PurgeableItemViewModel>()
                    .ToList();

                if (highlighted.Count > 1 && highlighted.Contains(clicked))
                {
                    foreach (var vm in highlighted)
                        vm.IsSelected = newState;
                    PurgeableGrid.Items.Refresh();
                }
            }
            UpdatePurgeSelection();
        }

        private void PurgeSelected_Click(object sender, RoutedEventArgs e)
        {
            if (_purgeCallback == null) return;

            var selectedItems = _purgeableViewModels
                .Where(vm => vm.IsSelected)
                .ToList();

            if (selectedItems.Count == 0) return;

            long totalBytes = selectedItems.Sum(vm => vm.SizeBytes);
            double totalMB = totalBytes / 1_048_576.0;
            string sizeInfo = totalMB >= 0.1 ? $" (~{totalMB:F1} MB)" : "";

            bool confirmed = Shared.BimPillsDialog.Confirm(
                "Confirmar purga",
                $"Se eliminarán {selectedItems.Count} elementos del modelo{sizeInfo}.",
                detail: "Esta acción no se puede deshacer.",
                owner: this,
                yesText: "Eliminar",
                noText: "Cancelar",
                kind: Shared.BimPillsDialog.DialogKind.Warning);

            if (!confirmed) return;

            try
            {
                var ids = selectedItems.Select(vm => vm.Id).ToList();
                var deletedIds = _purgeCallback(ids).ToHashSet();

                // Solo quitar los que Revit confirmó como eliminados
                var purged = selectedItems.Where(vm => deletedIds.Contains(vm.Id)).ToList();
                foreach (var item in purged)
                {
                    _purgeableViewModels.Remove(item);
                    _allPurgeableViewModels.Remove(item);
                }

                PurgeableGrid.ItemsSource = null;
                PurgeableGrid.ItemsSource = _purgeableViewModels;
                UpdatePurgeSelection();

                int failed = selectedItems.Count - purged.Count;
                if (failed == 0)
                {
                    Shared.BimPillsDialog.Success(
                        "Purga completada",
                        $"Se purgaron {purged.Count} elementos exitosamente.",
                        owner: this);
                }
                else
                {
                    Shared.BimPillsDialog.Info(
                        header: "Purga parcial",
                        message: $"{purged.Count} elementos eliminados, {failed} no se pudieron eliminar.",
                        detail: "Los elementos que no se pudieron eliminar pueden tener dependencias en el modelo.",
                        owner: this);
                }
            }
            catch (Exception ex)
            {
                Shared.BimPillsDialog.Error(
                    "Error al purgar",
                    "No se pudieron eliminar los elementos seleccionados.",
                    detail: ex.Message,
                    owner: this);
            }
        }

        // ── Orphan filter / display ───────────────────────────────────

        private void OrphanFilterDeletable_Changed(object sender, RoutedEventArgs e)
        {
            ApplyOrphanFilter();
        }

        private void ApplyOrphanFilter()
        {
            bool onlyDeletable = OrphanFilterDeletableBtn?.IsChecked == true;
            var visible = onlyDeletable
                ? _orphanViewModels.Where(vm => vm.CanDelete).ToList()
                : _orphanViewModels;

            OrphansGrid.ItemsSource = visible;
            UpdateOrphanSelection();
        }

        private void UpdateOrphanSelection()
        {
            int total     = _orphanViewModels.Count;
            int deletable = _orphanViewModels.Count(vm => vm.CanDelete);

            bool filtering = OrphanFilterDeletableBtn?.IsChecked == true;
            int shown = filtering ? deletable : total;

            OrphanSelectionText.Text = deletable > 0
                ? $"{shown} mostrados · {deletable} borrables (ver pestaña Purgables)"
                : $"{total} elementos del sistema (no borrables desde aquí)";
        }

        private void OrphanSelectAll_Click(object sender, RoutedEventArgs e)
        {
            // Solo considera los purgables; los no-purgables se ignoran (el setter los rechaza).
            var deletable = _orphanViewModels.Where(vm => vm.CanDelete).ToList();
            bool allSelected = deletable.Count > 0 && deletable.All(vm => vm.IsSelected);
            bool newValue = !allSelected;
            foreach (var vm in deletable)
                vm.IsSelected = newValue;
            OrphansGrid.Items.Refresh();
            UpdateOrphanSelection();
        }

        private void OrphanCheckBox_Click(object sender, RoutedEventArgs e)
        {
            UpdateOrphanSelection();
        }

        private void DeleteOrphans_Click(object sender, RoutedEventArgs e)
        {
            if (_purgeCallback == null) return;

            var selected = _orphanViewModels.Where(vm => vm.IsSelected && vm.CanDelete).ToList();
            if (selected.Count == 0) return;

            // Advertencia extra si hay mezcla de tipos, para que el usuario confirme
            // con información concreta sobre qué va a eliminar.
            var tipos = selected
                .GroupBy(vm => vm.ClassName)
                .OrderByDescending(g => g.Count())
                .Select(g => $"• {g.Count()} × {g.Key}")
                .Take(5);
            var detalle = string.Join("\n", tipos);

            bool confirmed = Shared.BimPillsDialog.Confirm(
                "Confirmar eliminación",
                $"Se eliminarán {selected.Count} elementos sin categoría del modelo.",
                detail:
                    $"{detalle}\n\nEsta acción no se puede deshacer. " +
                    "Solo se permiten eliminar elementos marcados como purgables.",
                owner: this,
                yesText: "Eliminar",
                noText: "Cancelar",
                kind: Shared.BimPillsDialog.DialogKind.Warning);

            if (!confirmed) return;

            try
            {
                var ids = selected.Select(vm => (long)vm.Id).ToList();
                var deletedIds = _purgeCallback(ids).ToHashSet();

                var removed = selected.Where(vm => deletedIds.Contains((long)vm.Id)).ToList();
                foreach (var vm in removed)
                    _orphanViewModels.Remove(vm);

                OrphansGrid.ItemsSource = null;
                OrphansGrid.ItemsSource = _orphanViewModels;
                UpdateOrphanSelection();

                int failed = selected.Count - removed.Count;
                if (failed == 0)
                {
                    Shared.BimPillsDialog.Success(
                        "Eliminación completada",
                        $"Se eliminaron {removed.Count} elementos exitosamente.",
                        owner: this);
                }
                else
                {
                    Shared.BimPillsDialog.Info(
                        header: "Eliminación parcial",
                        message: $"{removed.Count} elementos eliminados, {failed} no se pudieron eliminar.",
                        detail: "Los elementos que no se pudieron eliminar pueden tener dependencias en el modelo.",
                        owner: this);
                }
            }
            catch (Exception ex)
            {
                Shared.BimPillsDialog.Error(
                    "Error al eliminar",
                    "No se pudieron eliminar todos los elementos seleccionados.",
                    detail: ex.Message,
                    owner: this);
            }
        }

        // ── Export ─────────────────────────────────────────────────────

        private void OpenExportAudit_Click(object sender, RoutedEventArgs e)
        {
            var exportWindow = new ExportAudit.ExportAuditWindow(_result);
            try { exportWindow.Owner = this; } catch { }
            exportWindow.ShowDialog();
        }

        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Exportar informe de auditoría",
                Filter = "Archivo HTML (*.html)|*.html",
                FileName = $"Auditoria_{_result.DocumentTitle}_{DateTime.Now:yyyyMMdd}",
                DefaultExt = ".html"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                var html = GenerateHtmlReport();
                File.WriteAllText(dialog.FileName, html, Encoding.UTF8);

                bool openInBrowser = Shared.BimPillsDialog.Confirm(
                    "Exportación completada",
                    "Informe de auditoría exportado correctamente.",
                    detail: $"Archivo: {dialog.FileName}\n\n¿Desea abrirlo en el navegador?",
                    owner: this,
                    yesText: "Abrir",
                    noText: "Cerrar",
                    kind: Shared.BimPillsDialog.DialogKind.Success);

                if (openInBrowser)
                {
                    Helpers.ProcessHelper.OpenDocument(dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                Shared.BimPillsDialog.Error(
                    "Error al exportar",
                    "No se pudo exportar el informe.",
                    detail: ex.Message,
                    owner: this);
            }
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Exportar datos de auditoría a CSV",
                Filter = "Archivo CSV (*.csv)|*.csv",
                FileName = $"Auditoria_{_result.DocumentTitle}_{DateTime.Now:yyyyMMdd}",
                DefaultExt = ".csv"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                var sb = new StringBuilder();

                // Resumen
                sb.AppendLine("=== RESUMEN ===");
                sb.AppendLine($"Modelo,{CsvEscape(_result.DocumentTitle)}");
                sb.AppendLine($"Salud,{_result.HealthScore.TotalScore}/100 ({_result.HealthScore.LevelLabel})");
                sb.AppendLine($"Tamaño,{_result.FileSizeLabel}");
                sb.AppendLine($"Elementos,{_result.TotalElements}");
                sb.AppendLine($"Advertencias,{_result.Warnings.Count}");
                sb.AppendLine($"Purgables,{_result.PurgeableItems.Count}");
                sb.AppendLine();

                // Advertencias
                sb.AppendLine("=== ADVERTENCIAS ===");
                sb.AppendLine("Severidad,Descripción,Elementos");
                foreach (var w in _result.Warnings)
                    sb.AppendLine($"{CsvEscape(w.Severity)},{CsvEscape(w.Description)},{w.ElementCount}");
                sb.AppendLine();

                // Familias
                sb.AppendLine("=== FAMILIAS ===");
                sb.AppendLine("Familia,Categoría,Tamaño,Instancias");
                foreach (var f in _result.Families)
                    sb.AppendLine($"{CsvEscape(f.Name)},{CsvEscape(f.Category)},{CsvEscape(f.SizeLabel)},{f.InstanceCount}");
                sb.AppendLine();

                // Vistas sin colocar
                sb.AppendLine("=== VISTAS SIN COLOCAR ===");
                sb.AppendLine("Vista,Tipo");
                foreach (var v in _result.UnplacedViews)
                    sb.AppendLine($"{CsvEscape(v.Name)},{CsvEscape(v.ViewType)}");
                sb.AppendLine();

                // Elementos huérfanos
                sb.AppendLine("=== ELEMENTOS HUÉRFANOS ===");
                sb.AppendLine("ID,Nombre");
                foreach (var o in _result.OrphanElements)
                    sb.AppendLine($"{o.Id},{CsvEscape(o.Name)}");
                sb.AppendLine();

                // Purgables
                sb.AppendLine("=== ELEMENTOS PURGABLES ===");
                sb.AppendLine("ID,Nombre,Tipo,Categoría,Tamaño");
                foreach (var p in _result.PurgeableItems)
                    sb.AppendLine($"{p.Id},{CsvEscape(p.Name)},{CsvEscape(p.ItemType)},{CsvEscape(p.Category)},{CsvEscape(p.SizeLabel)}");

                File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);

                Shared.BimPillsDialog.Success(
                    "CSV exportado",
                    "Datos de auditoría exportados correctamente.",
                    detail: $"Archivo: {dialog.FileName}",
                    owner: this);
            }
            catch (Exception ex)
            {
                Shared.BimPillsDialog.Error(
                    "Error al exportar CSV",
                    "No se pudo exportar el archivo CSV.",
                    detail: ex.Message,
                    owner: this);
            }
        }

        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }

        // ── HTML Report Generation ────────────────────────────────────

        private string GenerateHtmlReport()
        {
            var health = _result.HealthScore;
            string healthColorHex = health.Level switch
            {
                HealthLevel.Excelente => "#34C759",
                HealthLevel.Bueno     => "#FECA29",
                HealthLevel.Regular   => "#EF6337",
                HealthLevel.Crítico   => "#FF3B30",
                _ => "#8E8E93"
            };

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"es\">");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset=\"UTF-8\">");
            sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine($"<title>Auditoría — {HtmlEscape(_result.DocumentTitle)}</title>");
            sb.AppendLine("<style>");
            sb.AppendLine(@"
                * { margin: 0; padding: 0; box-sizing: border-box; }
                body { font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, sans-serif; background: #F0F0F0; color: #212B37; line-height: 1.5; }
                .container { max-width: 900px; margin: 0 auto; padding: 32px 24px; }
                .header { background: white; border-radius: 10px; padding: 24px; margin-bottom: 20px; box-shadow: 0 1px 8px rgba(0,0,0,0.06); display: flex; align-items: center; gap: 20px; }
                .header-text h1 { font-size: 20px; font-weight: 600; color: #212B37; }
                .header-text .subtitle { font-size: 13px; color: #EF6337; font-weight: 500; }
                .header-text .meta { font-size: 12px; color: #86868B; margin-top: 4px; }
                .dashboard { background: white; border-radius: 10px; padding: 24px; margin-bottom: 20px; box-shadow: 0 1px 8px rgba(0,0,0,0.06); display: flex; align-items: center; gap: 24px; }
                .health-circle { width: 90px; height: 90px; position: relative; flex-shrink: 0; }
                .health-circle svg { width: 90px; height: 90px; }
                .health-score { position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%); text-align: center; }
                .health-score .number { font-size: 24px; font-weight: 700; color: #212B37; display: block; }
                .health-score .label { font-size: 10px; font-weight: 500; }
                .metrics { display: flex; gap: 12px; flex: 1; }
                .metric-card { background: #F9F9FB; border-radius: 8px; padding: 12px 16px; flex: 1; }
                .metric-card .metric-label { font-size: 11px; color: #86868B; }
                .metric-card .metric-value { font-size: 18px; font-weight: 600; margin-top: 4px; }
                .section { background: white; border-radius: 10px; padding: 20px; margin-bottom: 16px; box-shadow: 0 1px 8px rgba(0,0,0,0.06); }
                .section h2 { font-size: 15px; font-weight: 600; margin-bottom: 12px; padding-bottom: 8px; border-bottom: 1px solid #E5E5EA; }
                table { width: 100%; border-collapse: collapse; font-size: 12px; }
                th { background: #F5F5F7; color: #86868B; font-weight: 500; text-align: left; padding: 8px 12px; border-bottom: 1px solid #E5E5EA; }
                td { padding: 8px 12px; border-bottom: 1px solid #F0F0F0; }
                tr:nth-child(even) td { background: #FAFAFA; }
                .badge { display: inline-block; padding: 2px 8px; border-radius: 4px; font-size: 11px; color: white; }
                .badge-familia { background: #EF6337; }
                .badge-vista { background: #007AFF; }
                .badge-material { background: #34C759; }
                .badge-other { background: #86868B; }
                .footer { text-align: center; font-size: 11px; color: #86868B; margin-top: 24px; padding-top: 16px; border-top: 1px solid #E5E5EA; }
                @media print { body { background: white; } .container { padding: 0; } }
            ");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<div class=\"container\">");

            // Header
            sb.AppendLine("<div class=\"header\">");
            sb.AppendLine("<div class=\"header-text\">");
            sb.AppendLine("<div class=\"subtitle\">BIMPills — Auditoría de Modelo</div>");
            sb.AppendLine($"<h1>{HtmlEscape(_result.DocumentTitle)}</h1>");
            sb.AppendLine($"<div class=\"meta\">Modelo {(_result.IsWorkshared ? "colaborativo" : "local")} · Generado el {DateTime.Now:dd/MM/yyyy HH:mm}</div>");
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");

            // Dashboard with SVG health circle
            double pct = health.TotalScore / 100.0;
            double r = 36;
            double circ = 2 * Math.PI * r;
            double dashOffset = circ * (1 - pct);

            sb.AppendLine("<div class=\"dashboard\">");
            sb.AppendLine("<div class=\"health-circle\">");
            sb.AppendLine("<svg viewBox=\"0 0 90 90\">");
            sb.AppendLine($"<circle cx=\"45\" cy=\"45\" r=\"{r}\" fill=\"none\" stroke=\"#E5E5EA\" stroke-width=\"6\"/>");
            sb.AppendLine($"<circle cx=\"45\" cy=\"45\" r=\"{r}\" fill=\"none\" stroke=\"{healthColorHex}\" stroke-width=\"6\" " +
                $"stroke-dasharray=\"{circ.ToString("F1", CultureInfo.InvariantCulture)}\" " +
                $"stroke-dashoffset=\"{dashOffset.ToString("F1", CultureInfo.InvariantCulture)}\" " +
                $"stroke-linecap=\"round\" transform=\"rotate(-90 45 45)\"/>");
            sb.AppendLine("</svg>");
            sb.AppendLine($"<div class=\"health-score\"><span class=\"number\">{health.TotalScore}</span><span class=\"label\" style=\"color:{healthColorHex}\">{HtmlEscape(health.LevelLabel)}</span></div>");
            sb.AppendLine("</div>");

            sb.AppendLine("<div class=\"metrics\">");
            sb.AppendLine($"<div class=\"metric-card\"><div class=\"metric-label\">Peso</div><div class=\"metric-value\">{HtmlEscape(_result.FileSizeLabel)}</div></div>");
            sb.AppendLine($"<div class=\"metric-card\"><div class=\"metric-label\">Elementos</div><div class=\"metric-value\">{_result.TotalElements:N0}</div></div>");
            sb.AppendLine($"<div class=\"metric-card\"><div class=\"metric-label\">Advertencias</div><div class=\"metric-value\">{_result.Warnings.Count:N0}</div></div>");
            sb.AppendLine($"<div class=\"metric-card\"><div class=\"metric-label\">Purgables</div><div class=\"metric-value\" style=\"color:#EF6337\">{_result.PurgeableItems.Count:N0}</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");

            // Warnings table
            if (_result.Warnings.Count > 0)
            {
                sb.AppendLine("<div class=\"section\">");
                sb.AppendLine($"<h2>Advertencias ({_result.Warnings.Count})</h2>");
                sb.AppendLine("<table><tr><th>Severidad</th><th>Descripción</th><th>Elementos</th></tr>");
                foreach (var w in _result.Warnings)
                    sb.AppendLine($"<tr><td>{HtmlEscape(w.Severity)}</td><td>{HtmlEscape(w.Description)}</td><td>{w.ElementCount}</td></tr>");
                sb.AppendLine("</table></div>");
            }

            // Families grouped by category
            if (_result.Families.Count > 0)
            {
                var groups = _result.Families
                    .GroupBy(f => f.Category)
                    .OrderByDescending(g => g.Sum(f => f.SizeBytes));

                sb.AppendLine("<div class=\"section\">");
                sb.AppendLine($"<h2>Familias ({_result.Families.Count})</h2>");
                foreach (var group in groups)
                {
                    double groupSizeMB = group.Sum(f => f.SizeBytes) / 1_048_576.0;
                    sb.AppendLine($"<h3 style=\"font-size:13px;margin:16px 0 8px;padding:6px 10px;background:#F5F5F7;border-radius:6px;\">{HtmlEscape(group.Key)} — {group.Count()} familias — {groupSizeMB:F1} MB</h3>");
                    sb.AppendLine("<table><tr><th>Familia</th><th>Tama\u00F1o</th><th>Instancias</th></tr>");
                    foreach (var f in group.OrderByDescending(f => f.SizeBytes))
                        sb.AppendLine($"<tr><td>{HtmlEscape(f.Name)}</td><td>{HtmlEscape(f.SizeLabel)}</td><td>{f.InstanceCount}</td></tr>");
                    sb.AppendLine("</table>");
                }
                sb.AppendLine("</div>");
            }

            // Unplaced views
            if (_result.UnplacedViews.Count > 0)
            {
                sb.AppendLine("<div class=\"section\">");
                sb.AppendLine($"<h2>Vistas sin colocar ({_result.UnplacedViews.Count})</h2>");
                sb.AppendLine("<table><tr><th>Vista</th><th>Tipo</th></tr>");
                foreach (var v in _result.UnplacedViews)
                    sb.AppendLine($"<tr><td>{HtmlEscape(v.Name)}</td><td>{HtmlEscape(v.ViewType)}</td></tr>");
                sb.AppendLine("</table></div>");
            }

            // Orphan elements
            if (_result.OrphanElements.Count > 0)
            {
                sb.AppendLine("<div class=\"section\">");
                sb.AppendLine($"<h2>Elementos huérfanos ({_result.OrphanElements.Count})</h2>");
                sb.AppendLine("<table><tr><th>ID</th><th>Nombre</th></tr>");
                foreach (var o in _result.OrphanElements)
                    sb.AppendLine($"<tr><td>{o.Id}</td><td>{HtmlEscape(o.Name)}</td></tr>");
                sb.AppendLine("</table></div>");
            }

            // Purgeable items
            if (_result.PurgeableItems.Count > 0)
            {
                sb.AppendLine("<div class=\"section\">");
                sb.AppendLine($"<h2>Elementos purgables ({_result.PurgeableItems.Count})</h2>");
                sb.AppendLine("<table><tr><th>Nombre</th><th>Tipo</th><th>Categoría</th><th>Tamaño</th></tr>");
                foreach (var p in _result.PurgeableItems)
                {
                    string badgeClass = p.ItemType.ToLowerInvariant() switch
                    {
                        "familia"  => "badge-familia",
                        "vista"    => "badge-vista",
                        "material" => "badge-material",
                        _          => "badge-other"
                    };
                    sb.AppendLine($"<tr><td>{HtmlEscape(p.Name)}</td><td><span class=\"badge {badgeClass}\">{HtmlEscape(p.ItemType)}</span></td><td>{HtmlEscape(p.Category)}</td><td>{HtmlEscape(p.SizeLabel)}</td></tr>");
                }
                sb.AppendLine("</table></div>");
            }

            // Footer
            sb.AppendLine($"<div class=\"footer\">Generado por BIMPills v1.0 · BIM-CA · {DateTime.Now:yyyy}</div>");
            sb.AppendLine("</div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        private static string HtmlEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        // ── Health Arc drawing ────────────────────────────────────────

        private void DrawHealthArc(int score)
        {
            double radius = 37; // 80/2 - strokeThickness/2
            double centerX = 40;
            double centerY = 40;
            double angle = (score / 100.0) * 360.0;

            if (angle <= 0) return;

            // Para 100% usar un Ellipse completo en lugar de arc (evita el bug de arco 360°)
            if (score >= 100)
            {
                HealthArc.Data = null;
                HealthFullRing.Visibility = Visibility.Visible;
                return;
            }

            HealthFullRing.Visibility = Visibility.Collapsed;

            double startAngle = -90; // Empezar arriba
            double endAngle = startAngle + angle;

            double startRad = startAngle * Math.PI / 180;
            double endRad = endAngle * Math.PI / 180;

            double startX = centerX + radius * Math.Cos(startRad);
            double startY = centerY + radius * Math.Sin(startRad);
            double endX = centerX + radius * Math.Cos(endRad);
            double endY = centerY + radius * Math.Sin(endRad);

            bool isLargeArc = angle > 180;

            var geometry = new PathGeometry();
            var figure = new PathFigure { StartPoint = new Point(startX, startY) };
            figure.Segments.Add(new ArcSegment(
                new Point(endX, endY),
                new Size(radius, radius),
                0,
                isLargeArc,
                SweepDirection.Clockwise,
                true));
            geometry.Figures.Add(figure);

            HealthArc.Data = geometry;
        }

        private void HealthInfo_Click(object sender, RoutedEventArgs e)
        {
            ShowMethodologyWindow();
        }

        private void ShowMethodologyWindow()
        {
            var hs = _result.HealthScore;

            // ── Criterios ──────────────────────────────────────────────────────
            var criteria = new[]
            {
                ("Advertencias",          hs.WarningsScore,      30,  $"{hs.WarningsCount} detectadas",     "≤50: 30 · ≤100: 24 · ≤200: 18 · ≤400: 10 · ≤600: 5 · >600: 0"),
                ("Tamaño del archivo",    hs.FileSizeScore,      20,  hs.FileSizeMB > 0 ? $"{hs.FileSizeMB:F1} MB" : "—",  "<150 MB: 20 · <300 MB: 15 · <500 MB: 10 · <1 GB: 5 · ≥1 GB: 0"),
                ("Familia más pesada",    hs.FamilySizeScore,    15,  $"{hs.LargestFamilyMB:F2} MB",        "<0.5 MB: 15 · <1 MB: 12 · <2 MB: 6 · <5 MB: 3 · ≥5 MB: 0"),
                ("Cantidad de elementos", hs.ElementsScore,      10,  $"{hs.TotalElements:N0} elementos",   "<300K: 10 · <500K: 8 · <1M: 5 · <2M: 2 · ≥2M: 0"),
                ("Vistas sin colocar",    hs.UnplacedViewsScore, 10,  $"{hs.UnplacedViewsCount} vistas",    "≤5: 10 · ≤15: 7 · ≤30: 4 · ≤50: 2 · >50: 0"),
                ("Elementos purgables",   hs.PurgeableScore,     15,  $"{hs.PurgeableCount} elementos",     "≤10: 15 · ≤30: 11 · ≤60: 7 · ≤100: 3 · >100: 0"),
            };

            // ── Ventana ─────────────────────────────────────────────────────────
            // Colores del design system (mismos valores que Styles.xaml)
            var bgBrush      = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));
            var whiteBrush   = new SolidColorBrush(Color.FromRgb(0xFB, 0xFA, 0xF8));
            var primaryBrush = new SolidColorBrush(Color.FromRgb(0x21, 0x2B, 0x37));
            var accentBrush  = new SolidColorBrush(Color.FromRgb(0xEF, 0x63, 0x37));
            var secBrush     = new SolidColorBrush(Color.FromRgb(0x86, 0x86, 0x8B));
            var sepBrush     = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xEA));

            var win = new Window
            {
                Title                 = "BIM Pills — Metodología de evaluación",
                Width                 = 620,
                Height                = 540,
                MinWidth              = 480,
                MinHeight             = 400,
                ResizeMode            = ResizeMode.CanResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner                 = this,
                Background            = bgBrush,
                FontFamily            = new FontFamily("Segoe UI"),
                UseLayoutRounding     = true,
                SnapsToDevicePixels   = true,
            };
            Shared.ThemeHelper.Apply(win);

            // ── Layout principal ─────────────────────────────────────────────────
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // ── Header (patrón idéntico al resto de ventanas BIM Pills) ──────────
            var header = new Border
            {
                Background      = whiteBrush,
                Padding         = new Thickness(24, 16, 24, 14),
                BorderBrush     = sepBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
            };
            var headerStack = new StackPanel();
            headerStack.Children.Add(new TextBlock
            {
                Text       = "Metodología",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize   = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = accentBrush,
                Margin     = new Thickness(0, 0, 0, 2),
            });
            headerStack.Children.Add(new TextBlock
            {
                Text       = "Evaluación del Modelo",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize   = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = primaryBrush,
            });
            headerStack.Children.Add(new TextBlock
            {
                Text       = $"Puntaje actual: {hs.TotalScore}/100 — {hs.LevelLabel}",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize   = 12,
                Foreground = secBrush,
                Margin     = new Thickness(0, 3, 0, 0),
            });
            header.Child = headerStack;
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // ── Tabla de criterios ───────────────────────────────────────────────
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(20, 12, 20, 12),
            };
            var itemsPanel = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };

            foreach (var (name, score, max, current, scale) in criteria)
            {
                double pct = max > 0 ? (double)score / max : 0;
                var accentColor = pct >= 0.8
                    ? Color.FromRgb(0x34, 0xC7, 0x59)   // verde
                    : pct >= 0.5
                        ? Color.FromRgb(0xFE, 0xCA, 0x29) // amarillo
                        : Color.FromRgb(0xFF, 0x3B, 0x30); // rojo

                var card = new Border
                {
                    Background      = whiteBrush,
                    CornerRadius    = new CornerRadius(8),
                    Padding         = new Thickness(14, 11, 14, 11),
                    Margin          = new Thickness(0, 0, 0, 6),
                    BorderBrush     = sepBrush,
                    BorderThickness = new Thickness(1),
                };
                var cardGrid = new Grid();
                cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var left = new StackPanel();
                left.Children.Add(new TextBlock
                {
                    Text       = name,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize   = 13,
                    FontWeight = FontWeights.Medium,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x21, 0x2B, 0x37)),
                });
                left.Children.Add(new TextBlock
                {
                    Text       = current,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize   = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x86, 0x86, 0x8B)),
                    Margin     = new Thickness(0, 1, 0, 4),
                });
                // Barra de progreso
                var barBg = new Border
                {
                    Height          = 4,
                    CornerRadius    = new CornerRadius(2),
                    Background      = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF5)),
                    Margin          = new Thickness(0, 0, 8, 0),
                };
                var barFill = new Border
                {
                    Height          = 4,
                    CornerRadius    = new CornerRadius(2),
                    Background      = new SolidColorBrush(accentColor),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                // Use a Grid to layer fill over background
                var barGrid = new Grid { Margin = new Thickness(0, 0, 8, 0) };
                barGrid.Children.Add(barBg);
                var fillWrapper = new Grid();
                fillWrapper.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(pct, GridUnitType.Star) });
                fillWrapper.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - pct, GridUnitType.Star) });
                barFill.SetValue(Grid.ColumnProperty, 0);
                fillWrapper.Children.Add(barFill);
                barGrid.Children.Add(fillWrapper);
                left.Children.Add(barGrid);

                left.Children.Add(new TextBlock
                {
                    Text            = scale,
                    FontFamily      = new FontFamily("Segoe UI"),
                    FontSize        = 10,
                    Foreground      = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xB0)),
                    TextWrapping    = TextWrapping.Wrap,
                    Margin          = new Thickness(0, 3, 0, 0),
                });

                Grid.SetColumn(left, 0);
                cardGrid.Children.Add(left);

                // Score badge
                var badge = new Border
                {
                    Background      = new SolidColorBrush(Color.FromArgb(0x22,
                        accentColor.R, accentColor.G, accentColor.B)),
                    CornerRadius    = new CornerRadius(6),
                    Padding         = new Thickness(10, 4, 10, 4),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin          = new Thickness(10, 0, 0, 0),
                };
                badge.Child = new TextBlock
                {
                    Text       = $"{score}/{max}",
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize   = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(accentColor),
                };
                Grid.SetColumn(badge, 1);
                cardGrid.Children.Add(badge);

                card.Child = cardGrid;
                itemsPanel.Children.Add(card);
            }

            // Niveles de salud
            itemsPanel.Children.Add(new TextBlock
            {
                Text            = "NIVELES DE SALUD  •  80–100: Excelente  •  60–79: Bueno  •  40–59: Regular  •  0–39: Crítico",
                FontFamily      = new FontFamily("Segoe UI"),
                FontSize        = 11,
                Foreground      = new SolidColorBrush(Color.FromRgb(0x86, 0x86, 0x8B)),
                TextWrapping    = TextWrapping.Wrap,
                Margin          = new Thickness(0, 6, 0, 16),
            });

            // Fuentes y referencias
            var refsCard = new Border
            {
                Background      = bgBrush,
                CornerRadius    = new CornerRadius(8),
                Padding         = new Thickness(14, 12, 14, 12),
                BorderBrush     = sepBrush,
                BorderThickness = new Thickness(1),
            };
            var refsStack = new StackPanel();
            refsStack.Children.Add(new TextBlock
            {
                Text       = "FUENTES Y REFERENCIAS",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize   = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = secBrush,
                Margin     = new Thickness(0, 0, 0, 8),
            });

            var refs = new[]
            {
                ("ISO 19650-1:2018",           "Conceptos y principios de gestión de información BIM (§11.1, §8.1)"),
                ("ISO 19650-2:2018",           "Fase de desarrollo de activos (§5.2.2 Intercambio de información)"),
                ("Autodesk AU IT20549",        "Health Check for Your Revit Project Models (umbrales de advertencias)"),
                ("Autodesk Knowledge Network", "Model Performance Best Practices & File Maintenance"),
                ("AEC (UK) BIM Protocol v2.0", "Model Validation Checklist (purga, vistas, limpieza de worksets)"),
                ("BIM Forum",                  "LOD Specification (gestión de contenido y peso de familias)"),
            };

            foreach (var (refName, refDesc) in refs)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
                row.Children.Add(new TextBlock
                {
                    Text       = "• ",
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize   = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x21, 0x2B, 0x37)),
                });
                var refText = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily   = new FontFamily("Segoe UI"),
                    FontSize     = 11,
                };
                refText.Inlines.Add(new System.Windows.Documents.Run(refName + " — ")
                {
                    FontWeight = FontWeights.SemiBold,
                    Foreground = primaryBrush,
                });
                refText.Inlines.Add(new System.Windows.Documents.Run(refDesc)
                {
                    Foreground = secBrush,
                });
                row.Children.Add(refText);
                refsStack.Children.Add(row);
            }

            refsCard.Child = refsStack;
            itemsPanel.Children.Add(refsCard);

            scroll.Content = itemsPanel;
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            // Footer
            var footer = new Border
            {
                Background      = whiteBrush,
                Padding         = new Thickness(24, 12, 24, 12),
                BorderBrush     = sepBrush,
                BorderThickness = new Thickness(0, 1, 0, 0),
            };
            var closeBtn = new Button
            {
                Content     = "Cerrar",
                FontFamily  = new FontFamily("Segoe UI"),
                FontSize    = 13,
                FontWeight  = FontWeights.Medium,
                Padding     = new Thickness(24, 8, 24, 8),
                Background  = new SolidColorBrush(Color.FromRgb(0x21, 0x2B, 0x37)),
                Foreground  = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                Cursor      = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            closeBtn.Click += (_, _) => win.Close();
            footer.Child = closeBtn;
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            win.Content = root;
            win.ShowDialog();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

    // ── Family category group for grouped display ────────────────────

    public sealed class FamilyCategoryGroup
    {
        private static readonly Dictionary<string, (string bg, string fg)> CategoryColors = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Mobiliario"]    = ("#FFF3E0", "#E65100"),
            ["Puertas"]       = ("#E3F2FD", "#1565C0"),
            ["Ventanas"]      = ("#E8F5E9", "#2E7D32"),
            ["Columnas"]      = ("#F3E5F5", "#7B1FA2"),
            ["Fontaner\u00EDa"]    = ("#E0F7FA", "#00695C"),
            ["Iluminaci\u00F3n"]   = ("#FFFDE7", "#F57F17"),
            ["Muros"]         = ("#EFEBE9", "#4E342E"),
            ["Estructura"]    = ("#ECEFF1", "#37474F"),
            ["Electricidad"]  = ("#FCE4EC", "#AD1457"),
            ["Escaleras"]     = ("#EDE7F6", "#4527A0"),
        };

        public string Category { get; }
        public IReadOnlyList<FamilyInfo> Families { get; }
        public string CountLabel { get; }
        public string SizeLabel { get; }
        public SolidColorBrush BadgeBackground { get; }
        public SolidColorBrush BadgeForeground { get; }

        public FamilyCategoryGroup(string category, IReadOnlyList<FamilyInfo> families)
        {
            Category = category;
            Families = families;
            CountLabel = $"{families.Count} familias";
            double sizeMB = families.Sum(f => f.SizeBytes) / 1_048_576.0;
            SizeLabel = $"{sizeMB:F1} MB";

            if (CategoryColors.TryGetValue(category, out var colors))
            {
                BadgeBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors.bg));
                BadgeForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors.fg));
            }
            else
            {
                BadgeBackground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF7));
                BadgeForeground = new SolidColorBrush(Color.FromRgb(0x21, 0x2B, 0x37));
            }
        }
    }

    // ── Width converter for Expander header ────────────────────────

    public sealed class SubtractConverter : IValueConverter
    {
        public static readonly SubtractConverter Instance = new SubtractConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d) return Math.Max(0, d - 32); // subtract expander toggle width
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // ── OrphanElementViewModel ────────────────────────────────────────

    public sealed class OrphanElementViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;

        public ElementInfo Item { get; }

        public int    Id          => Item.Id;
        public string Name        => Item.Name;
        public string ClassName   => Item.ClassName ?? "—";
        public bool   CanDelete   => Item.CanDelete;
        public string Description => Item.Description
            ?? (CanDelete
                ? "Elemento sin categoría. Revisar antes de eliminar."
                : "Elemento del sistema o anclado — no es seguro eliminarlo desde aquí.");

        public string CanDeleteLabel => CanDelete ? "Sí" : "No";

        public Brush CanDeleteBrush => CanDelete
            ? new SolidColorBrush(Color.FromRgb(52, 199, 89))    // verde
            : new SolidColorBrush(Color.FromRgb(142, 142, 147));  // gris

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (!CanDelete && value) return; // no seleccionar lo que no se puede borrar
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public OrphanElementViewModel(ElementInfo item) => Item = item;

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    // ── Value Converter for type badges ───────────────────────────────

    /// <summary>
    /// Converts ItemType string to a colored brush for type badges in the purgables grid.
    /// </summary>
    public sealed class TypeToBrushConverter : IValueConverter
    {
        public static readonly TypeToBrushConverter Instance = new TypeToBrushConverter();

        private static readonly SolidColorBrush FamiliaBrush      = new SolidColorBrush(Color.FromRgb(239, 99, 55));   // #EF6337 orange
        private static readonly SolidColorBrush VistaBrush        = new SolidColorBrush(Color.FromRgb(0, 122, 255));    // #007AFF blue
        private static readonly SolidColorBrush MaterialBrush     = new SolidColorBrush(Color.FromRgb(52, 199, 89));    // #34C759 green
        private static readonly SolidColorBrush EstiloTextoBrush  = new SolidColorBrush(Color.FromRgb(175, 82, 222));   // #AF52DE purple
        private static readonly SolidColorBrush TipoCotaBrush     = new SolidColorBrush(Color.FromRgb(88, 86, 214));    // #5856D6 indigo
        private static readonly SolidColorBrush PatronRellenoBrush= new SolidColorBrush(Color.FromRgb(255, 45, 85));    // #FF2D55 pink
        private static readonly SolidColorBrush DefaultBrush      = new SolidColorBrush(Color.FromRgb(142, 142, 147));  // #8E8E93 grey

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var type = value as string;
            if (string.IsNullOrEmpty(type)) return DefaultBrush;

            return type!.ToLowerInvariant() switch
            {
                "familia"        => FamiliaBrush,
                "vista"          => VistaBrush,
                "material"       => MaterialBrush,
                "estilo texto"   => EstiloTextoBrush,
                "tipo cota"      => TipoCotaBrush,
                "patron relleno" => PatronRellenoBrush,
                _                => DefaultBrush
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
