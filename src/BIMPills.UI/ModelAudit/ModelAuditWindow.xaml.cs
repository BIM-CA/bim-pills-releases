using BIMPills.Commands.ModelAudit;
using BIMPills.Core.Audit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
        private readonly Action<IReadOnlyList<long>>? _purgeCallback;
        private List<PurgeableItemViewModel> _purgeableViewModels = new List<PurgeableItemViewModel>();

        public ModelAuditWindow(ModelAuditResult result, Action<IReadOnlyList<long>>? purgeCallback = null)
        {
            _result = result;
            _purgeCallback = purgeCallback;
            InitializeComponent();
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
            WarningsGrid.ItemsSource   = _result.Warnings;
            ViewsGrid.ItemsSource      = _result.UnplacedViews;
            OrphansGrid.ItemsSource    = _result.OrphanElements;

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
            _purgeableViewModels = _result.PurgeableItems
                .Select(p => new PurgeableItemViewModel(p))
                .ToList();
            PurgeableGrid.ItemsSource = _purgeableViewModels;

            // Deshabilitar purga si no hay callback
            if (_purgeCallback == null)
            {
                PurgeSelectedButton.Visibility = Visibility.Collapsed;
                SelectAllButton.Visibility = Visibility.Collapsed;
            }

            UpdatePurgeSelection();
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

            var confirm = MessageBox.Show(
                $"Se eliminarán {selectedItems.Count} elementos del modelo{sizeInfo}.\n\n" +
                "Esta acción no se puede deshacer. ¿Desea continuar?",
                "BIMPills — Confirmar purga",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                var ids = selectedItems.Select(vm => vm.Id).ToList();
                _purgeCallback(ids);

                // Remove purged items from the list
                foreach (var item in selectedItems)
                    _purgeableViewModels.Remove(item);

                PurgeableGrid.ItemsSource = null;
                PurgeableGrid.ItemsSource = _purgeableViewModels;
                UpdatePurgeSelection();

                MessageBox.Show(
                    $"Se purgaron {selectedItems.Count} elementos exitosamente.",
                    "BIMPills — Purga completada",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al purgar elementos:\n\n{ex.Message}",
                    "BIMPills — Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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

                var openResult = MessageBox.Show(
                    $"Informe exportado exitosamente.\n\n{dialog.FileName}\n\n¿Desea abrirlo en el navegador?",
                    "BIMPills — Exportación completada",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (openResult == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = dialog.FileName,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al exportar el informe:\n\n{ex.Message}",
                    "BIMPills — Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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

                MessageBox.Show(
                    $"CSV exportado exitosamente.\n\n{dialog.FileName}",
                    "BIMPills — Exportación completada",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al exportar CSV:\n\n{ex.Message}",
                    "BIMPills — Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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
            var methodology = _result.HealthScore.GetMethodologyText();
            MessageBox.Show(
                methodology,
                "BIMPills \u2014 Metodolog\u00EDa de evaluaci\u00F3n",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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

    // ── Value Converter for type badges ───────────────────────────────

    /// <summary>
    /// Converts ItemType string to a colored brush for type badges in the purgables grid.
    /// </summary>
    public sealed class TypeToBrushConverter : IValueConverter
    {
        public static readonly TypeToBrushConverter Instance = new TypeToBrushConverter();

        private static readonly SolidColorBrush FamiliaBrush  = new SolidColorBrush(Color.FromRgb(239, 99, 55));   // #EF6337
        private static readonly SolidColorBrush VistaBrush    = new SolidColorBrush(Color.FromRgb(0, 122, 255));    // #007AFF
        private static readonly SolidColorBrush MaterialBrush = new SolidColorBrush(Color.FromRgb(52, 199, 89));    // #34C759
        private static readonly SolidColorBrush DefaultBrush  = new SolidColorBrush(Color.FromRgb(142, 142, 147));  // #8E8E93

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var type = value as string;
            if (string.IsNullOrEmpty(type)) return DefaultBrush;

            return type.ToLowerInvariant() switch
            {
                "familia"  => FamiliaBrush,
                "vista"    => VistaBrush,
                "material" => MaterialBrush,
                _          => DefaultBrush
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
