using BIMPills.Core.Models;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.Persistence;
using BIMPills.UI.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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

        // Publication sets
        private JsonPublicationSetRepository? _publicationSetRepo;
        private List<PublicationSet> _publicationSets = new List<PublicationSet>();

        // PDF engine (global printer selector, persisted)
        private JsonPdfEngineSettingsRepository? _pdfEngineRepo;
        private PdfEngineSettings _pdfEngine = new PdfEngineSettings();
        // Cached at load time to avoid re-enumerating Windows printers on every
        // UpdatePdfEngineUi() call (SelectionChanged fires it repeatedly and
        // PrinterSettings.InstalledPrinters is slow on machines with network printers).
        private bool _pdf24Installed;
        // IMPORTANT: starts true so early SelectionChanged events fired during
        // XAML parsing (because <ComboBoxItem IsSelected="True"/>) don't touch
        // fields that InitializeComponent() has not yet assigned. Cleared by
        // LoadPdfEngineSettings() once the UI is fully ready.
        private bool _suppressPdfEngineEvents = true;

        private string _exportLabel = "Exportar";

        /// <summary>Export queue built by Export_Click for non-blocking processing.</summary>
        public List<ExportQueueItem>? PendingExportQueue { get; private set; }

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
            LoadPdfEngineSettings();
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
            // Count based on visible (filtered) rows for summary
            var visibleSelected = _filteredRows.Count(r => r.IsSelected);
            var visibleTotal = _filteredRows.Count;

            // Total across all rows for export
            var totalSelected = _rows.Count(r => r.IsSelected);

            var sheetCount = _filteredRows.Count(r => r.IsSelected && r.Item.ItemType == ExportableItemType.Sheet);
            var viewCount = visibleSelected - sheetCount;

            string label;
            if (sheetCount > 0 && viewCount > 0)
                label = $"{sheetCount} planos + {viewCount} vistas";
            else if (sheetCount > 0)
                label = $"{sheetCount} planos";
            else if (viewCount > 0)
                label = $"{viewCount} vistas";
            else
                label = "0 items";

            SelectionSummary.Text = $"{visibleSelected} de {visibleTotal} seleccionados ({label})";

            if (totalSelected != visibleSelected)
                StatusText.Text = $"{visibleSelected} visibles + {totalSelected - visibleSelected} ocultos seleccionados";
            else
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
            var allSelected = _rows.Where(r => r.IsSelected).ToList();
            var sample = allSelected.Take(4).ToList();

            bool exportPdf = PdfCheck.IsChecked == true;
            bool exportDwg = DwgCheck.IsChecked == true;

            // Group files by their sub-folder path so the tree renders each file
            // nested under the folder it actually belongs to. Uses an ordered list
            // so sub-folders appear in their first-seen order (Dictionary insertion
            // order is implementation-defined on .NET Framework 4.8).
            var tree = new List<KeyValuePair<string, List<string>>>();
            void AddFile(string subPath, string fileName)
            {
                var bucket = tree.FirstOrDefault(kv => kv.Key == subPath);
                if (bucket.Value == null)
                {
                    bucket = new KeyValuePair<string, List<string>>(subPath, new List<string>());
                    tree.Add(bucket);
                }
                bucket.Value.Add(fileName);
            }

            foreach (var row in sample)
            {
                var sheetProxy = new SheetExportInfo(
                    row.Item.Id, row.Item.SheetNumber, row.Item.Name,
                    row.Item.Revision, row.Item.Discipline);
                var name = convention.GenerateFileName(sheetProxy, _projectName, DateTime.Now, row.Item.ParameterValues);

                if (exportPdf)
                {
                    var subPath = GetSubFolder(folderOrg, "PDF", row.Discipline);
                    AddFile(subPath, $"{name}.pdf");
                }

                if (exportDwg)
                {
                    var subPath = GetSubFolder(folderOrg, "DWG", row.Discipline);
                    AddFile(subPath, $"{name}.dwg");
                }
            }

            // Render the tree — base folder first, then each sub-folder with its files.
            var lines = new List<string>();
            var baseName = Path.GetFileName(_selectedFolder.TrimEnd('\\', '/'));
            if (string.IsNullOrEmpty(baseName)) baseName = _selectedFolder;
            lines.Add($"\U0001F4C1 {baseName}/");

            foreach (var kv in tree)
            {
                var subPath = kv.Key;
                var files = kv.Value;

                if (string.IsNullOrEmpty(subPath))
                {
                    // Flat: files hang directly off the base folder.
                    foreach (var file in files)
                        lines.Add($"  \U0001F4C4 {file}");
                }
                else
                {
                    // Sub-path may contain nested segments (e.g. "PDF/Arquitectura").
                    var segments = subPath.Split('/', '\\');
                    for (int i = 0; i < segments.Length; i++)
                    {
                        var indent = new string(' ', (i + 1) * 2);
                        lines.Add($"{indent}\U0001F4C1 {segments[i]}/");
                    }
                    var fileIndent = new string(' ', (segments.Length + 1) * 2);
                    foreach (var file in files)
                        lines.Add($"{fileIndent}\U0001F4C4 {file}");
                }
            }

            if (allSelected.Count > sample.Count)
                lines.Add($"  \u2026 y {allSelected.Count - sample.Count} items m\u00e1s");

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

        // ── Publication Sets ──

        /// <summary>Captures current export settings from UI controls into a PublicationSet.</summary>
        private void CaptureExportSettings(PublicationSet set)
        {
            set.ExportPdf = PdfCheck.IsChecked == true;
            set.ExportDwg = DwgCheck.IsChecked == true;
            set.NamingPattern = NamingPatternBox?.Text ?? "{SheetNumber}-{SheetName}";
            set.FolderOrganization = GetSelectedFolderOrganization();
            set.PdfSettings = GetPdfSettings();
            set.DwgPresetName = (DwgSetupCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            set.DwgExportLinkedAsXrefs = DwgExportLinkedCheck?.IsChecked == true;
            set.DwgCleanPcpFiles = DwgCleanPcpCheck?.IsChecked == true;
        }

        /// <summary>Restores export settings from a PublicationSet into UI controls.</summary>
        private void RestoreExportSettings(PublicationSet set)
        {
            // Format checkboxes
            PdfCheck.IsChecked = set.ExportPdf;
            DwgCheck.IsChecked = set.ExportDwg;

            // Naming pattern
            if (!string.IsNullOrEmpty(set.NamingPattern) && NamingPatternBox != null)
                NamingPatternBox.Text = set.NamingPattern;

            // Folder organization
            foreach (ComboBoxItem item in FolderOrgCombo.Items)
            {
                if (item.Tag?.ToString() == set.FolderOrganization.ToString())
                {
                    FolderOrgCombo.SelectedItem = item;
                    break;
                }
            }

            // PDF settings
            if (set.PdfSettings != null)
                ApplyPdfSettings(set.PdfSettings);

            // DWG preset
            if (!string.IsNullOrEmpty(set.DwgPresetName))
            {
                foreach (ComboBoxItem item in DwgSetupCombo.Items)
                {
                    if (item.Content?.ToString() == set.DwgPresetName)
                    {
                        DwgSetupCombo.SelectedItem = item;
                        break;
                    }
                }
            }
            DwgExportLinkedCheck.IsChecked = set.DwgExportLinkedAsXrefs;
            DwgCleanPcpCheck.IsChecked = set.DwgCleanPcpFiles;

            // Trigger visibility updates for PDF/DWG sections
            FormatCheck_Changed(this, new RoutedEventArgs());
        }

        private void ApplyPdfSettings(PdfExportSettings s)
        {
            // Paper placement
            foreach (ComboBoxItem item in PdfPaperPlacementCombo.Items)
                if (item.Tag?.ToString() == s.PaperPlacement.ToString()) { PdfPaperPlacementCombo.SelectedItem = item; break; }
            PdfOffsetXBox.Text = s.OffsetX.ToString();
            PdfOffsetYBox.Text = s.OffsetY.ToString();

            // Zoom
            foreach (ComboBoxItem item in PdfZoomTypeCombo.Items)
                if (item.Tag?.ToString() == s.ZoomType.ToString()) { PdfZoomTypeCombo.SelectedItem = item; break; }
            PdfZoomFactorBox.Text = s.ZoomPercent.ToString();

            // Hidden view processing
            foreach (ComboBoxItem item in PdfHiddenViewsCombo.Items)
                if (item.Tag?.ToString() == s.HiddenViewProcessing.ToString()) { PdfHiddenViewsCombo.SelectedItem = item; break; }

            // Color
            foreach (ComboBoxItem item in PdfColorCombo.Items)
                if (item.Tag?.ToString() == s.ColorDepth.ToString()) { PdfColorCombo.SelectedItem = item; break; }

            // Raster quality
            foreach (ComboBoxItem item in PdfDpiCombo.Items)
                if (item.Tag?.ToString() == s.RasterQuality.ToString()) { PdfDpiCombo.SelectedItem = item; break; }

            // Checkboxes
            PdfHideScopeBoxes.IsChecked = s.HideScopeBoxes;
            PdfHideCropBoundaries.IsChecked = s.HideCropBoundaries;
            PdfHideRefPlanes.IsChecked = s.HideRefWorkPlanes;
            PdfHideUnrefTags.IsChecked = s.HideUnreferencedViewTags;
            PdfViewLinksBlue.IsChecked = s.ViewLinksInBlue;
            PdfMaskCoincidentLines.IsChecked = s.MaskCoincidentLines;
            PdfReplaceHalftones.IsChecked = s.ReplaceHalftoneWithThinLines;
            PdfAlwaysUseRaster.IsChecked = s.AlwaysUseRaster;
            PdfCombineCheck.IsChecked = s.CombineIntoPdf;
            PdfCombinedNameBox.Text = s.CombinedFileName ?? "Planos_Combinados";
        }

        private void LoadPublicationSets()
        {
            try
            {
                var repoPath = string.IsNullOrEmpty(_projectName)
                    ? null
                    : JsonPublicationSetRepository.GetDirectoryForModel(_projectName);
                _publicationSetRepo = new JsonPublicationSetRepository(repoPath);
                _publicationSets = _publicationSetRepo.GetAll();

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
                bool hasActiveSet = !string.IsNullOrEmpty(setId);
                DeleteSetBtn.IsEnabled = hasActiveSet;
                RenameSetBtn.IsEnabled = hasActiveSet;

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

                // Restore export settings saved with the set
                RestoreExportSettings(set);

                int notFound = set.Items.Count - found;
                if (notFound > 0)
                {
                    BimPillsDialog.Info(
                        header: "Conjunto de publicaci\u00f3n cargado",
                        message: $"Se seleccionaron {found} de {set.Items.Count} items.",
                        detail: $"{notFound} items no se encontraron en el modelo actual. Es posible que hayan sido eliminados o que el conjunto provenga de otra versi\u00f3n del proyecto.",
                        owner: Window.GetWindow(this));
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
                    BimPillsDialog.Warning(
                        header: "Nada para guardar",
                        message: "Selecciona al menos un item para guardar el conjunto.",
                        owner: Window.GetWindow(this));
                    return;
                }

                var activeSetId = (PublicationSetCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
                var activeSet = string.IsNullOrEmpty(activeSetId) ? null : _publicationSets.FirstOrDefault(s => s.Id == activeSetId);

                var items = selectedItems.Select(r => new PublicationSetItem
                {
                    UniqueId = r.Item.UniqueId,
                    DisplayName = r.DisplayName,
                    ItemType = r.Item.ItemType
                }).ToList();

                if (activeSet != null)
                {
                    // Overwrite active set (items + export settings)
                    activeSet.Items = items;
                    CaptureExportSettings(activeSet);
                    _publicationSetRepo?.Update(activeSet);
                    LoadPublicationSets();
                    SelectSetInCombo(activeSet.Id);
                    BimPillsDialog.Success(
                        header: "Conjunto actualizado",
                        message: $"\u00ab{activeSet.Name}\u00bb actualizado con {items.Count} items.",
                        owner: Window.GetWindow(this));
                }
                else
                {
                    // No active set — create new
                    SaveNewSet(items);
                }
            }
            catch (Exception ex)
            {
                _logger?.Error("Error en SaveSet_Click", ex);
                BimPillsDialog.Error(
                    header: "No se pudo guardar",
                    message: "Ocurri\u00f3 un error al guardar el conjunto de publicaci\u00f3n.",
                    detail: ex.Message,
                    owner: Window.GetWindow(this));
            }
        }

        private void SaveAsSet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItems = _rows.Where(r => r.IsSelected).ToList();
                if (selectedItems.Count == 0)
                {
                    BimPillsDialog.Warning(
                        header: "Nada para guardar",
                        message: "Selecciona al menos un item para guardar el conjunto.",
                        owner: Window.GetWindow(this));
                    return;
                }

                var items = selectedItems.Select(r => new PublicationSetItem
                {
                    UniqueId = r.Item.UniqueId,
                    DisplayName = r.DisplayName,
                    ItemType = r.Item.ItemType
                }).ToList();

                SaveNewSet(items);
            }
            catch (Exception ex)
            {
                _logger?.Error("Error en SaveAsSet_Click", ex);
                BimPillsDialog.Error(
                    header: "No se pudo guardar",
                    message: "Ocurri\u00f3 un error al guardar el conjunto de publicaci\u00f3n.",
                    detail: ex.Message,
                    owner: Window.GetWindow(this));
            }
        }

        private void SaveNewSet(List<PublicationSetItem> items)
        {
            var name = PromptForSetName();
            if (string.IsNullOrWhiteSpace(name)) return;

            var set = new PublicationSet
            {
                Name = name.Trim(),
                Items = items
            };
            CaptureExportSettings(set);

            _publicationSetRepo?.Create(set);
            LoadPublicationSets();
            SelectSetInCombo(set.Id);

            BimPillsDialog.Success(
                header: "Conjunto creado",
                message: $"\u00ab{set.Name}\u00bb guardado con {items.Count} items.",
                owner: Window.GetWindow(this));
        }

        private void SelectSetInCombo(string setId)
        {
            for (int i = 0; i < PublicationSetCombo.Items.Count; i++)
            {
                if (PublicationSetCombo.Items[i] is ComboBoxItem ci && ci.Tag?.ToString() == setId)
                {
                    PublicationSetCombo.SelectedIndex = i;
                    break;
                }
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

                var confirm = BimPillsDialog.Confirm(
                    header: "\u00bfEliminar conjunto?",
                    message: $"El conjunto \u00ab{set.Name}\u00bb se eliminar\u00e1 permanentemente.",
                    detail: "Esta acci\u00f3n no se puede deshacer.",
                    owner: Window.GetWindow(this),
                    yesText: "Eliminar",
                    noText: "Cancelar",
                    kind: BimPillsDialog.DialogKind.Warning);
                if (!confirm) return;

                _publicationSetRepo?.Delete(setId);
                LoadPublicationSets();
            }
            catch (Exception ex)
            {
                _logger?.Error("Error en DeleteSet_Click", ex);
                BimPillsDialog.Error(
                    header: "No se pudo eliminar",
                    message: "Ocurri\u00f3 un error al eliminar el conjunto.",
                    detail: ex.Message,
                    owner: Window.GetWindow(this));
            }
        }

        private void RenameSet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = PublicationSetCombo.SelectedItem as ComboBoxItem;
                var setId = selectedItem?.Tag?.ToString() ?? "";
                if (string.IsNullOrEmpty(setId)) return;

                var set = _publicationSets.FirstOrDefault(s => s.Id == setId);
                if (set == null) return;

                var newName = PromptForSetName(set.Name);
                if (string.IsNullOrWhiteSpace(newName)) return;

                set.Name = newName.Trim();
                _publicationSetRepo?.Update(set);
                LoadPublicationSets();
                SelectSetInCombo(set.Id);
            }
            catch (Exception ex)
            {
                _logger?.Error("Error en RenameSet_Click", ex);
                BimPillsDialog.Error(
                    header: "No se pudo renombrar",
                    message: "Ocurri\u00f3 un error al renombrar el conjunto.",
                    detail: ex.Message,
                    owner: Window.GetWindow(this));
            }
        }

        private string? PromptForSetName(string currentName = "")
        {
            var dlg = new Window
            {
                Title               = "BIM Pills \u2014 Guardar conjunto",
                Width               = 380,
                Height              = 150,
                Owner               = Window.GetWindow(this),
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
                Text         = string.IsNullOrEmpty(currentName) ? $"Conjunto {DateTime.Now:yyyy-MM-dd}" : currentName
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

        private void RowCheckBox_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var cb = sender as CheckBox;
                var row = cb?.DataContext as ExportableViewRow;
                if (row == null) return;

                bool state = cb.IsChecked == true;

                // Apply to all highlighted (DataGrid-selected) rows
                var highlighted = SheetsGrid.SelectedItems.Cast<ExportableViewRow>().ToList();
                if (highlighted.Count > 1 && highlighted.Contains(row))
                {
                    foreach (var r in highlighted)
                        r.IsSelected = state;
                    SheetsGrid.Items.Refresh();
                }

                UpdateSelection();
            }
            catch (Exception ex) { _logger?.Error("Error en RowCheckBox_Click", ex); }
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
                HideScopeBoxes              = PdfHideScopeBoxes.IsChecked == true,
                HideCropBoundaries          = PdfHideCropBoundaries.IsChecked == true,
                HideRefWorkPlanes           = PdfHideRefPlanes.IsChecked == true,
                HideUnreferencedViewTags    = PdfHideUnrefTags.IsChecked == true,
                ViewLinksInBlue             = PdfViewLinksBlue.IsChecked == true,
                MaskCoincidentLines         = PdfMaskCoincidentLines.IsChecked == true,
                ReplaceHalftoneWithThinLines = PdfReplaceHalftones.IsChecked == true,
                AlwaysUseRaster             = PdfAlwaysUseRaster.IsChecked == true,
                CombineIntoPdf              = PdfCombineCheck.IsChecked == true,
                CombinedFileName            = PdfCombinedNameBox.Text?.Trim() ?? "Planos_Combinados"
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
                BimPillsDialog.Error(
                    header: "Error inesperado",
                    message: "No se pudo abrir el selector de carpeta.",
                    detail: ex.Message,
                    owner: Window.GetWindow(this));
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
                repo.Create(profile);

                BimPillsDialog.Success(
                    header: "Perfil guardado",
                    message: $"\u00ab{profile.Name}\u00bb guardado correctamente.",
                    owner: Window.GetWindow(this));
            }
            catch (Exception ex)
            {
                BimPillsDialog.Error(
                    header: "No se pudo guardar el perfil",
                    message: "Ocurri\u00f3 un error al persistir el perfil de exportaci\u00f3n.",
                    detail: ex.Message,
                    owner: Window.GetWindow(this));
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

        private string? PromptForProfileName()
        {
            var dlg = new Window
            {
                Title               = "BIM Pills \u2014 Guardar perfil",
                Width               = 380,
                Height              = 150,
                Owner               = Window.GetWindow(this),
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

                bool exportPdf = PdfCheck.IsChecked == true;
                bool exportDwg = DwgCheck.IsChecked == true;
                if (!exportPdf && !exportDwg) return;

                var selected = _rows.Where(r => r.IsSelected).ToList();
                if (selected.Count == 0) return;

                var convention = new SheetNamingConvention { Pattern = NamingPatternBox.Text };
                var folderOrg = GetSelectedFolderOrganization();
                var now = DateTime.Now;
                var pdfSettings = exportPdf ? GetPdfSettings() : null;
                var dwgConfig = exportDwg ? GetSelectedDwgConfig() : null;

                // Build export queue
                var queue = new List<ExportQueueItem>();
                foreach (var row in selected)
                {
                    var sheetProxy = new SheetExportInfo(
                        row.Item.Id, row.Item.SheetNumber, row.Item.Name,
                        row.Item.Revision, row.Item.Discipline);
                    var fileName = SanitizeFileName(convention.GenerateFileName(sheetProxy, _projectName, now, row.Item.ParameterValues));

                    if (exportPdf)
                    {
                        var folder = GetExportFolder(_selectedFolder, folderOrg, "PDF", row.Discipline);
                        Directory.CreateDirectory(folder);
                        queue.Add(new ExportQueueItem
                        {
                            ViewId = row.Item.Id,
                            Folder = folder,
                            FileName = fileName,
                            DisplayName = row.DisplayName,
                            Format = ExportFormat.Pdf,
                            PdfSettings = pdfSettings
                        });
                    }

                    if (exportDwg)
                    {
                        var folder = GetExportFolder(_selectedFolder, folderOrg, "DWG", row.Discipline);
                        Directory.CreateDirectory(folder);
                        queue.Add(new ExportQueueItem
                        {
                            ViewId = row.Item.Id,
                            Folder = folder,
                            FileName = fileName,
                            DisplayName = row.DisplayName,
                            Format = ExportFormat.Dwg,
                            DwgConfig = dwgConfig
                        });
                    }
                }

                var formatLabel = exportPdf && exportDwg ? "PDF y DWG" : exportPdf ? "PDF" : "DWG";
                var confirmMessage =
                    $"Se exportar\u00e1n {selected.Count} items a {formatLabel} " +
                    $"(total: {queue.Count} archivos).";
                var confirmDetail =
                    $"Destino: {_selectedFolder}\n\n" +
                    "Durante la exportaci\u00f3n Revit quedar\u00e1 ocupado y no podr\u00e1s usarlo. " +
                    "Mantendremos abierta una ventana de progreso que podr\u00e1s cancelar en cualquier momento.";

                var confirmed = BimPillsDialog.Confirm(
                    header: "\u00bfIniciar exportaci\u00f3n?",
                    message: confirmMessage,
                    detail: confirmDetail,
                    owner: Window.GetWindow(this),
                    yesText: "Exportar",
                    noText: "Cancelar");

                if (!confirmed) return;

                PendingExportQueue = queue;

                // Close the parent window — export will be processed by the command via Idling
                var win = Window.GetWindow(this);
                if (win != null) try { win.DialogResult = true; } catch (InvalidOperationException) { }
                win?.Close();
            }
            catch (Exception ex)
            {
                _logger?.Error("Error no controlado en Export_Click", ex);
                BimPillsDialog.Error(
                    header: "Error inesperado",
                    message: "Ocurri\u00f3 un error al iniciar la exportaci\u00f3n.",
                    detail: ex.Message,
                    owner: Window.GetWindow(this));
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

        // ── PDF Engine ─────────────────────────────────────────────────────

        /// <summary>
        /// Loads the persisted PDF engine settings, populates the ComboBoxes
        /// with installed PDF printers, and applies the saved selection.
        /// Safe to call multiple times (idempotent).
        /// </summary>
        private void LoadPdfEngineSettings()
        {
            try
            {
                _pdfEngineRepo = new JsonPdfEngineSettingsRepository();
                _pdfEngine = _pdfEngineRepo.Load();
            }
            catch (Exception ex)
            {
                _logger?.Warning($"[PdfEngine] No se pudieron cargar settings: {ex.Message}");
                _pdfEngine = new PdfEngineSettings();
            }

            // Guard against event feedback while we're populating.
            _suppressPdfEngineEvents = true;
            try
            {
                // Populate installed PDF printers and cache the PDF24 detection
                // derived from the same enumeration — avoids a second full sweep
                // of PrinterSettings.InstalledPrinters later.
                PdfPrinterCombo.Items.Clear();
                var printers = BIMPills.Infrastructure.Services.PdfPrinterService.GetInstalledPdfPrinters();
                _pdf24Installed = false;
                foreach (var p in printers)
                {
                    if (!_pdf24Installed &&
                        p.SystemName.IndexOf("pdf24", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _pdf24Installed = true;
                    }
                    var label = p.SupportsSilent
                        ? $"{p.DisplayName} (silencioso)"
                        : $"{p.DisplayName}";
                    PdfPrinterCombo.Items.Add(new ComboBoxItem
                    {
                        Content = label,
                        Tag     = p.SystemName,
                        ToolTip = $"Impresora: {p.SystemName}"
                    });
                }

                // Engine combo initial value
                foreach (ComboBoxItem item in PdfEngineCombo.Items)
                {
                    if (item.Tag?.ToString() == _pdfEngine.Engine.ToString())
                    {
                        PdfEngineCombo.SelectedItem = item;
                        break;
                    }
                }

                // Printer combo initial value — prefer saved printer, else the first.
                if (PdfPrinterCombo.Items.Count > 0)
                {
                    int matchIdx = -1;
                    if (!string.IsNullOrWhiteSpace(_pdfEngine.PrinterName))
                    {
                        for (int i = 0; i < PdfPrinterCombo.Items.Count; i++)
                        {
                            if (PdfPrinterCombo.Items[i] is ComboBoxItem ci &&
                                string.Equals(ci.Tag?.ToString(), _pdfEngine.PrinterName, StringComparison.OrdinalIgnoreCase))
                            {
                                matchIdx = i;
                                break;
                            }
                        }
                    }
                    PdfPrinterCombo.SelectedIndex = matchIdx >= 0 ? matchIdx : 0;
                }
            }
            finally
            {
                _suppressPdfEngineEvents = false;
            }

            UpdatePdfEngineUi();
        }

        /// <summary>
        /// Shows/hides the printer row based on engine choice, and warns if
        /// SystemPrinter is selected but no PDF printer is installed. When
        /// PDF24 isn't detected, it also exposes a "Download PDF24" hyperlink
        /// so users on a fresh machine can get it in one click.
        /// </summary>
        private void UpdatePdfEngineUi()
        {
            bool systemPrinter = _pdfEngine.Engine == PdfEngineKind.SystemPrinter;
            PdfPrinterRow.Visibility = systemPrinter ? Visibility.Visible : Visibility.Collapsed;

            if (systemPrinter && PdfPrinterCombo.Items.Count == 0)
            {
                PdfEngineWarning.Text = "⚠ No se detectó ninguna impresora PDF instalada. " +
                                        "Instalá PDF24 (recomendado) o cambiá el motor a «Revit nativo».";
                PdfEngineWarning.Visibility = Visibility.Visible;
            }
            else
            {
                PdfEngineWarning.Visibility = Visibility.Collapsed;
            }

            // Download hint: visible whenever SystemPrinter is selected AND PDF24
            // isn't installed. Uses the cached _pdf24Installed flag populated by
            // LoadPdfEngineSettings() instead of re-enumerating Windows printers.
            Pdf24DownloadHint.Visibility = (systemPrinter && !_pdf24Installed)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// Opens the PDF24 download page in the user's default browser.
        /// </summary>
        private void Pdf24DownloadLink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri)
                {
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                _logger?.Warning($"[PdfEngine] No se pudo abrir el navegador: {ex.Message}");
                try
                {
                    System.Windows.MessageBox.Show(
                        "No se pudo abrir el navegador. Copiá y pegá esta dirección:\n\n" + e.Uri.AbsoluteUri,
                        "BIM Pills — Descargar PDF24",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                catch { }
            }
        }

        private void PdfEngine_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressPdfEngineEvents) return;
            var tag = (PdfEngineCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            _pdfEngine.Engine = tag == "SystemPrinter" ? PdfEngineKind.SystemPrinter : PdfEngineKind.Native;
            UpdatePdfEngineUi();
            SavePdfEngineSettings();
        }

        private void PdfPrinter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressPdfEngineEvents) return;
            var tag = (PdfPrinterCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            _pdfEngine.PrinterName = tag ?? "";
            SavePdfEngineSettings();
        }

        private void SavePdfEngineSettings()
        {
            try { _pdfEngineRepo?.Save(_pdfEngine); }
            catch (Exception ex) { _logger?.Warning($"[PdfEngine] No se pudo guardar: {ex.Message}"); }
        }

        /// <summary>
        /// Exposed for the host (Revit command) so it can route the export
        /// through the native engine or a system printer. Field initializer
        /// guarantees a non-null default (Native) if the panel was never loaded.
        /// </summary>
        public PdfEngineSettings GetPdfEngineSettings() => _pdfEngine;
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
