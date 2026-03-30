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
        private IReadOnlyList<SheetExportInfo> _sheets = Array.Empty<SheetExportInfo>();
        private List<SheetExportRow> _rows = new List<SheetExportRow>();
        private Func<long, string, string, PdfExportSettings, bool>? _pdfExportCallback;
        private Func<long, string, string, DwgExportConfig?, bool>? _dwgExportCallback;
        private string _projectName = "";
        private string? _selectedFolder;
        private ILogger? _logger;
        private List<string> _availableParameters = new List<string>();

        private string _exportLabel = "Exportar planos";

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
        /// Initializes the panel with sheet data and export callbacks.
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
            _sheets = sheets;
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
        }

        private void Populate()
        {
            _rows = _sheets.Select(s => new SheetExportRow(s)).ToList();
            SheetsGrid.ItemsSource = _rows;
            UpdateAllFileNames();
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
            var selected = _rows.Count(r => r.IsSelected);
            SelectionSummary.Text = $"{selected} de {_rows.Count} planos seleccionados";
            StatusText.Text = $"{selected} planos seleccionados";

            bool canExport = selected > 0
                && !string.IsNullOrEmpty(_selectedFolder)
                && (PdfCheck.IsChecked == true || DwgCheck.IsChecked == true);
            ExportEnabledChanged?.Invoke(this, canExport);
            _exportLabel = $"Exportar {selected} planos";
        }

        private void UpdateNamingPreview()
        {
            var convention = new SheetNamingConvention { Pattern = NamingPatternBox.Text };
            var firstSheet = _rows.FirstOrDefault(r => r.IsSelected)?.Sheet ?? _rows.FirstOrDefault()?.Sheet;
            if (firstSheet != null)
            {
                var name = convention.GenerateFileName(firstSheet, _projectName, DateTime.Now, firstSheet.ParameterValues);
                var ext = PdfCheck.IsChecked == true ? ".pdf" : ".dwg";
                NamingPreview.Text = name + ext;
            }
            else
            {
                NamingPreview.Text = "(sin planos)";
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
            var selected = _rows.Where(r => r.IsSelected).Take(4).Select(r => r.Sheet).ToList();
            var lines = new List<string>();

            var baseName = Path.GetFileName(_selectedFolder);
            lines.Add($"\U0001F4C1 {baseName}/");

            bool exportPdf = PdfCheck.IsChecked == true;
            bool exportDwg = DwgCheck.IsChecked == true;

            foreach (var sheet in selected)
            {
                var name = convention.GenerateFileName(sheet, _projectName, DateTime.Now, sheet.ParameterValues);
                var subPath = GetSubFolder(folderOrg, "PDF", sheet.Discipline);

                if (exportPdf)
                {
                    var pdfPath = string.IsNullOrEmpty(subPath) ? "" : $"  \U0001F4C1 {subPath}/\n";
                    if (!string.IsNullOrEmpty(subPath) && !lines.Any(l => l.Contains(subPath)))
                        lines.Add($"  \U0001F4C1 {subPath}/");
                    var indent = string.IsNullOrEmpty(subPath) ? "  " : "    ";
                    lines.Add($"{indent}\U0001F4C4 {name}.pdf");
                }

                if (exportDwg)
                {
                    var dwgSub = GetSubFolder(folderOrg, "DWG", sheet.Discipline);
                    if (!string.IsNullOrEmpty(dwgSub) && !lines.Any(l => l.Contains(dwgSub)))
                        lines.Add($"  \U0001F4C1 {dwgSub}/");
                    var indent = string.IsNullOrEmpty(dwgSub) ? "  " : "    ";
                    lines.Add($"{indent}\U0001F4C4 {name}.dwg");
                }
            }

            if (_rows.Count(r => r.IsSelected) > 4)
                lines.Add($"  \u2026 y {_rows.Count(r => r.IsSelected) - 4} archivos más");

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

        // ── Event handlers ──

        private void WizardTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Refresh scroll hint when switching to Format tab
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
            try
            {
                var query = SearchBox.Text?.Trim().ToLowerInvariant() ?? "";
                if (string.IsNullOrEmpty(query))
                {
                    SheetsGrid.ItemsSource = _rows;
                }
                else
                {
                    SheetsGrid.ItemsSource = _rows.Where(r =>
                        (r.SheetNumber?.ToLowerInvariant().Contains(query) ?? false)
                        || (r.SheetName?.ToLowerInvariant().Contains(query) ?? false)
                        || (r.Discipline?.ToLowerInvariant().Contains(query) ?? false))
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                _logger?.Error("Error en SearchBox_TextChanged", ex);
            }
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (var row in _rows)
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
                foreach (var row in _rows)
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

                // Create dynamic token button
                var btn = new Button
                {
                    Content = paramName,
                    Style = (Style)FindResource("TokenButton"),
                    Tag = token,
                    Margin = new Thickness(0, 0, 4, 4)
                };
                btn.Click += InsertToken_Click;
                DynamicTokensPanel.Children.Add(btn);

                // Insert into pattern
                var caret = NamingPatternBox.CaretIndex;
                NamingPatternBox.Text = NamingPatternBox.Text.Insert(caret, token);
                NamingPatternBox.CaretIndex = caret + token.Length;
                NamingPatternBox.Focus();

                // Remove from ComboBox to prevent duplicates
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
                    Description = "Seleccionar carpeta de destino para planos",
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
                    "BIMPills \u2014 Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            try
            {
                UpdateFolderPreview();
            }
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

                MessageBox.Show($"Perfil «{profile.Name}» guardado correctamente.",
                    "BIMPills — Exportar planos",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar el perfil: {ex.Message}",
                    "BIMPills — Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        /// <summary>Shows a small inline dialog to enter a profile name. Returns null if cancelled.</summary>
        private static string? PromptForProfileName()
        {
            var dlg = new Window
            {
                Title               = "BIMPills — Guardar perfil",
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

        // ── DWG Config (resolved from DwgSetupCombo tag + extra checkboxes) ──

        private DwgExportConfig? GetSelectedDwgConfig()
        {
            DwgExportConfig? cfg = null;
            if (DwgSetupCombo.SelectedItem is ComboBoxItem item && item.Tag is DwgExportConfig c)
                cfg = c;

            if (cfg == null) return null;

            // Overlay the extra checkbox options (non-destructive clone)
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

                var selected = _rows.Where(r => r.IsSelected).Select(r => r.Sheet).ToList();
                if (selected.Count == 0) return;

                var convention = new SheetNamingConvention { Pattern = NamingPatternBox.Text };
                var folderOrg = GetSelectedFolderOrganization();
                var now = DateTime.Now;

                // Resolve selected DWG config from native preset
                var selectedDwgConfig = GetSelectedDwgConfig();

                int totalOps = selected.Count * ((exportPdf ? 1 : 0) + (exportDwg ? 1 : 0));
                var pdfSettings = exportPdf ? GetPdfSettings() : new PdfExportSettings();

                var confirm = MessageBox.Show(
                    $"Se exportarán {selected.Count} planos" +
                    (exportPdf && exportDwg ? " a PDF y DWG" : exportPdf ? " a PDF" : " a DWG") +
                    $" en:\n\n\U0001F4C1 {_selectedFolder}\n\n" +
                    $"Total de archivos: {totalOps}\n\n" +
                    "Este proceso puede tomar varios minutos. \u00bfDesea continuar?",
                    "BIMPills \u2014 Confirmar exportaci\u00f3n",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.Yes);

                if (confirm != MessageBoxResult.Yes) return;

                // Show progress
                ProgressOverlay.Visibility = Visibility.Visible;
                ProgressBar.Maximum = totalOps;
                ProgressBar.Value = 0;

                int exported = 0;
                int failed = 0;
                var errors = new List<string>();
                int step = 0;

                try
                {
                    foreach (var sheet in selected)
                    {
                        // Sanitize filename - remove chars invalid for filesystem
                        var fileName = SanitizeFileName(convention.GenerateFileName(sheet, _projectName, now, sheet.ParameterValues));

                        if (exportPdf)
                        {
                            step++;
                            ProgressText.Text = $"PDF: {step} de {totalOps}...";
                            ProgressDetail.Text = $"{sheet.SheetNumber} - {sheet.SheetName}";
                            ProgressBar.Value = step;
                            PumpDispatcher();

                            var folder = GetExportFolder(_selectedFolder, folderOrg, "PDF", sheet.Discipline);
                            Directory.CreateDirectory(folder);

                            bool ok = _pdfExportCallback!(sheet.Id, folder, fileName, pdfSettings);
                            if (ok) exported++;
                            else { failed++; errors.Add($"[PDF] {sheet.SheetNumber}"); }
                        }

                        if (exportDwg)
                        {
                            step++;
                            ProgressText.Text = $"DWG: {step} de {totalOps}...";
                            ProgressDetail.Text = $"{sheet.SheetNumber} - {sheet.SheetName}";
                            ProgressBar.Value = step;
                            PumpDispatcher();

                            var folder = GetExportFolder(_selectedFolder, folderOrg, "DWG", sheet.Discipline);
                            Directory.CreateDirectory(folder);

                            bool ok = _dwgExportCallback!(sheet.Id, folder, fileName, selectedDwgConfig);
                            if (ok) exported++;
                            else { failed++; errors.Add($"[DWG] {sheet.SheetNumber}"); }
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
                    "BIMPills \u2014 Exportaci\u00f3n completada",
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
                    "BIMPills \u2014 Error",
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
    /// UI wrapper for SheetExportInfo that adds a live-computed DisplayFileName.
    /// </summary>
    internal class SheetExportRow : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public SheetExportInfo Sheet { get; }

        public bool IsSelected
        {
            get => Sheet.IsSelected;
            set { Sheet.IsSelected = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected))); }
        }
        public string SheetNumber   => Sheet.SheetNumber;
        public string SheetName     => Sheet.SheetName;
        public string Revision      => Sheet.Revision;
        public string Discipline    => Sheet.Discipline;

        /// <summary>Normalized category for chip coloring: "Arquitectura", "Estructura", "MEP", or "Other"</summary>
        public string DisciplineCategory
        {
            get
            {
                var d = (Sheet.Discipline ?? "").ToLowerInvariant();
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

        public SheetExportRow(SheetExportInfo sheet) => Sheet = sheet;

        public void UpdateFileName(SheetNamingConvention convention, string projectName)
        {
            DisplayFileName = convention.GenerateFileName(Sheet, projectName, DateTime.Now, Sheet.ParameterValues);
        }
    }
}
