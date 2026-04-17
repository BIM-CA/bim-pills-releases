using BIMPills.Core.Models;
using BIMPills.UI.ViewTemplates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace BIMPills.UI.ProjectStandards
{
    public partial class ProjectStandardsTransferPanel : UserControl
    {
        // ── Callbacks ────────────────────────────────────────────────────────

        private Func<string, string, IReadOnlyList<ProjectStandardItem>>? _getItemsCallback;
        private Func<string, IReadOnlyList<long>, ConflictResolution, Action<int, int, string>?, ProjectStandardTransferResult>? _transferCallback;

        // ── State ────────────────────────────────────────────────────────────

        private List<OpenDocumentInfo>            _openDocs    = new();
        private List<ProjectStandardCategoryInfo> _categories  = new();
        private List<ProjectStandardItem>         _currentItems = new();

        /// <summary>Selected item IDs per category key. Survives category switching.</summary>
        private readonly Dictionary<string, HashSet<long>> _selectedByCategory = new();

        private string? _currentCategoryKey;

        private bool   _canTransfer;
        private string _transferLabel = "Importar";

        public event EventHandler<bool>? TransferEnabledChanged;
        public string TransferLabel => _transferLabel;

        // ── Static category definitions ──────────────────────────────────────

        private static readonly List<ProjectStandardCategoryInfo> CategoryDefinitions
            = new List<ProjectStandardCategoryInfo>
        {
            // Anotación
            new ProjectStandardCategoryInfo { Key = ProjectStandardKeys.DimensionTypes, DisplayName = "Estilos de cota",        Icon = "\uE8EF", Group = "Anotaci\u00f3n" },
            new ProjectStandardCategoryInfo { Key = ProjectStandardKeys.SpotDimTypes,   DisplayName = "Cotas puntuales",        Icon = "\uE8EF", Group = "Anotaci\u00f3n" },
            new ProjectStandardCategoryInfo { Key = ProjectStandardKeys.TextNoteTypes,  DisplayName = "Estilos de texto",       Icon = "\uE8D2", Group = "Anotaci\u00f3n" },
            // Gráficos
            new ProjectStandardCategoryInfo { Key = ProjectStandardKeys.LineStyles,     DisplayName = "Estilos de l\u00ednea", Icon = "\uECC6", Group = "Gr\u00e1ficos" },
            new ProjectStandardCategoryInfo { Key = ProjectStandardKeys.FillPatterns,   DisplayName = "Patrones de relleno", Icon = "\uE9D9", Group = "Gr\u00e1ficos" },
            // Construcción
            new ProjectStandardCategoryInfo { Key = ProjectStandardKeys.WallTypes,      DisplayName = "Tipos de muros",     Icon = "\uE8CB", Group = "Construcci\u00f3n" },
            new ProjectStandardCategoryInfo { Key = ProjectStandardKeys.FloorTypes,     DisplayName = "Tipos de suelos",  Icon = "\uE81E", Group = "Construcci\u00f3n" },
            new ProjectStandardCategoryInfo { Key = ProjectStandardKeys.CeilingTypes,   DisplayName = "Tipos de techos",    Icon = "\uE8A1", Group = "Construcci\u00f3n" },
            new ProjectStandardCategoryInfo { Key = ProjectStandardKeys.RoofTypes,      DisplayName = "Tipos de cubierta",  Icon = "\uE80F", Group = "Construcci\u00f3n" },
        };

        // ── Constructor ──────────────────────────────────────────────────────

        public ProjectStandardsTransferPanel()
        {
            InitializeComponent();
        }

        // ── Public API ───────────────────────────────────────────────────────

        public void Initialize(
            IReadOnlyList<OpenDocumentInfo> openDocs,
            Func<string, string, IReadOnlyList<ProjectStandardItem>>? getItemsCallback = null,
            Func<string, IReadOnlyList<long>, ConflictResolution, Action<int, int, string>?, ProjectStandardTransferResult>? transferCallback = null)
        {
            _openDocs           = openDocs.ToList();
            _getItemsCallback   = getItemsCallback;
            _transferCallback   = transferCallback;

            var sourceDocs = _openDocs.Where(d => !d.IsCurrent).ToList();

            if (sourceDocs.Count == 0)
            {
                NoDocState.Visibility     = Visibility.Collapsed;
                SingleDocState.Visibility = Visibility.Visible;
                CategoryListBox.IsEnabled = false;
                return;
            }

            // Hide "no doc" state — we have valid source docs
            NoDocState.Visibility = Visibility.Collapsed;

            // Populate source doc combo
            SourceDocCombo.Items.Clear();
            foreach (var doc in sourceDocs)
                SourceDocCombo.Items.Add(new ComboBoxItem { Content = doc.Title, Tag = doc.Title });

            // Build category list (item counts start at 0; loaded per doc)
            _categories = CategoryDefinitions
                .Select(c => new ProjectStandardCategoryInfo
                {
                    Key         = c.Key,
                    DisplayName = c.DisplayName,
                    Icon        = c.Icon,
                    Group       = c.Group,
                    ItemCount   = 0
                }).ToList();

            RebuildCategoryView(_categories);

            NoCategoryState.Visibility = Visibility.Collapsed;

            if (SourceDocCombo.Items.Count > 0)
                SourceDocCombo.SelectedIndex = 0;
        }

        public void TriggerTransfer()
        {
            try
            {
                var allSelected = GetAllSelectedIds();
                if (allSelected.Count == 0)
                {
                    BIMPills.UI.Shared.BimPillsDialog.Warning(
                        header: "Nada seleccionado",
                        message: "Selecciona al menos un elemento para importar.",
                        owner: Window.GetWindow(this));
                    return;
                }

                var docTitle    = (SourceDocCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
                var conflictTag = (ConflictCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Replace";

                if (conflictTag == "Individual")
                {
                    ExecuteIndividualTransfer(allSelected, docTitle);
                    return;
                }

                var conflict = conflictTag == "Skip" ? ConflictResolution.Skip : ConflictResolution.Replace;

                int total = TotalSelectedCount();
                int catCount = _selectedByCategory.Count(kv => kv.Value.Count > 0);
                string catLabel = catCount == 1 ? "1 categor\u00eda" : $"{catCount} categor\u00edas";

                var confirmed = BIMPills.UI.Shared.BimPillsDialog.Confirm(
                    header: "\u00bfImportar est\u00e1ndares?",
                    message: $"Se importar\u00e1n {total} elementos ({catLabel}) desde \u00ab{docTitle}\u00bb.",
                    detail: conflict == ConflictResolution.Replace
                        ? "Los elementos con el mismo nombre ser\u00e1n reemplazados."
                        : "Los elementos con el mismo nombre se omitir\u00e1n.",
                    owner: Window.GetWindow(this),
                    yesText: "Importar",
                    noText: "Cancelar");

                if (!confirmed) return;

                ExecuteBulkTransfer(allSelected, docTitle, conflict);
            }
            catch (Exception ex)
            {
                BIMPills.UI.Shared.BimPillsDialog.Error(
                    header: "Error al importar",
                    message: "Ocurri\u00f3 un error al importar los est\u00e1ndares.",
                    detail: ex.Message,
                    owner: Window.GetWindow(this));
            }
        }

        // ── Source doc changed ───────────────────────────────────────────────

        private void SelectAllCategories_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentSelections();

            var docTitle = (SourceDocCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
            if (string.IsNullOrEmpty(docTitle)) return;

            // Load items for every category
            var allItemsPerCat = new Dictionary<string, List<ProjectStandardItem>>();
            foreach (var cat in _categories)
            {
                List<ProjectStandardItem> items;
                if (_getItemsCallback != null)
                {
                    items = _getItemsCallback(docTitle, cat.Key).ToList();
                }
                else
                {
                    items = Enumerable.Range(1, 5)
                        .Select(i => new ProjectStandardItem { Id = i, Name = $"{cat.DisplayName} {i}", Detail = "Detalle de ejemplo" })
                        .ToList();
                }
                allItemsPerCat[cat.Key] = items;
                cat.ItemCount = items.Count;
            }

            // Toggle: if every item in every category is already selected → deselect all, otherwise select all
            bool allSelected = allItemsPerCat.All(kv =>
                _selectedByCategory.TryGetValue(kv.Key, out var sel) &&
                kv.Value.All(i => sel.Contains(i.Id)));

            foreach (var kv in allItemsPerCat)
            {
                _selectedByCategory[kv.Key] = allSelected
                    ? new HashSet<long>()
                    : new HashSet<long>(kv.Value.Select(i => i.Id));
            }

            // Reload current category panel to reflect new selections
            if (_currentCategoryKey != null)
            {
                var cur = _categories.FirstOrDefault(c => c.Key == _currentCategoryKey);
                if (cur != null) LoadItemsForCategory(cur);
            }

            RebuildCategoryView(_categories);
            UpdateItemSummary();
            UpdateTransferButton();
        }

        private void ResetCategoryList()
        {
            RebuildCategoryView(_categories);
        }

        private void RebuildCategoryView(IEnumerable<ProjectStandardCategoryInfo> source)
        {
            var view = new System.Windows.Data.ListCollectionView(source.ToList());
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ProjectStandardCategoryInfo.Group)));
            CategoryListBox.ItemsSource = null;
            CategoryListBox.ItemsSource = view;
        }

        private void CategorySearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_categories.Count == 0) return;
            var q = CategorySearchBox.Text.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(q))
            {
                RebuildCategoryView(_categories);
                return;
            }
            var filtered = _categories.Where(c =>
                c.DisplayName.ToLowerInvariant().Contains(q) ||
                c.Group.ToLowerInvariant().Contains(q));
            RebuildCategoryView(filtered);
        }

        private void SourceDoc_Changed(object sender, SelectionChangedEventArgs e)
        {
            _selectedByCategory.Clear();
            _currentCategoryKey = null;
            _currentItems.Clear();
            ItemsPanel.Visibility = Visibility.Collapsed;
            NoCategoryState.Visibility = Visibility.Visible;

            // Reset counts
            foreach (var cat in _categories)
                cat.ItemCount = 0;
            RefreshCategoryList();

            UpdateTransferButton();

            if (CategoryListBox.SelectedItem != null)
            {
                CategoryListBox.SelectedItem = null;
                NoCategoryState.Visibility = Visibility.Visible;
                ItemsPanel.Visibility = Visibility.Collapsed;
            }

            StatusText.Text = "Elige una categor\u00eda para ver sus elementos";
        }

        // ── Category selected ────────────────────────────────────────────────

        private void Category_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryListBox.SelectedItem is not ProjectStandardCategoryInfo cat) return;

            // Save selections for current category before switching
            SaveCurrentSelections();

            _currentCategoryKey = cat.Key;
            LoadItemsForCategory(cat);
        }

        private void LoadItemsForCategory(ProjectStandardCategoryInfo cat)
        {
            try
            {
                var docTitle = (SourceDocCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
                if (string.IsNullOrEmpty(docTitle)) return;

                List<ProjectStandardItem> items;
                if (_getItemsCallback != null)
                {
                    items = _getItemsCallback(docTitle, cat.Key).ToList();
                }
                else
                {
                    // Sandbox mock
                    items = Enumerable.Range(1, 5)
                        .Select(i => new ProjectStandardItem { Id = i, Name = $"{cat.DisplayName} {i}", Detail = "Detalle de ejemplo" })
                        .ToList();
                }

                _currentItems = items;

                // Restore previously saved selections for this category
                if (_selectedByCategory.TryGetValue(cat.Key, out var savedIds))
                {
                    foreach (var item in _currentItems)
                        item.IsSelected = savedIds.Contains(item.Id);
                }

                // Update count badge
                cat.ItemCount = items.Count;
                RefreshCategoryList();

                // Bind to list
                ItemsListBox.ItemsSource = _currentItems;
                SearchBox.Text = "";
                CategoryTitle.Text = cat.DisplayName.ToUpperInvariant();
                UpdateItemSummary();

                NoCategoryState.Visibility = Visibility.Collapsed;
                ItemsPanel.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                BIMPills.UI.Shared.BimPillsDialog.Error(
                    header: "Error cargando categoría",
                    message: $"No se pudieron cargar los elementos de «{cat.DisplayName}».",
                    detail: ex.Message,
                    owner: Window.GetWindow(this));
            }
        }

        // ── Selection ────────────────────────────────────────────────────────

        private void ItemCheckBox_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentSelections();
            UpdateItemSummary();
            UpdateTransferButton();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool allSelected = _currentItems.All(i => i.IsSelected);
            foreach (var item in _currentItems)
                item.IsSelected = !allSelected;

            RefreshItemList();
            SaveCurrentSelections();
            UpdateItemSummary();
            UpdateTransferButton();
        }

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            var q = SearchBox.Text.Trim().ToLowerInvariant();
            ItemsListBox.ItemsSource = string.IsNullOrEmpty(q)
                ? _currentItems
                : _currentItems.Where(i => i.Name.ToLowerInvariant().Contains(q)
                    || (i.Detail ?? "").ToLowerInvariant().Contains(q)).ToList();
        }

        private void SaveCurrentSelections()
        {
            if (_currentCategoryKey == null) return;
            var selectedIds = _currentItems.Where(i => i.IsSelected).Select(i => i.Id);
            _selectedByCategory[_currentCategoryKey] = new HashSet<long>(selectedIds);
        }

        private List<long> GetAllSelectedIds()
        {
            SaveCurrentSelections();
            return _selectedByCategory.Values
                .SelectMany(ids => ids)
                .Distinct()
                .ToList();
        }

        private int TotalSelectedCount()
            => _selectedByCategory.Values.Sum(ids => ids.Count);

        private void UpdateItemSummary()
        {
            int selInCat = _currentItems.Count(i => i.IsSelected);
            int totalSel = TotalSelectedCount();

            SelectionSummary.Text = totalSel > 0
                ? $"{selInCat} aqu\u00ed · {totalSel} en total"
                : $"{selInCat} seleccionados";
        }

        private void UpdateTransferButton()
        {
            int total = TotalSelectedCount();
            int cats  = _selectedByCategory.Count(kv => kv.Value.Count > 0);

            _canTransfer = total > 0;

            if (total == 0)
            {
                _transferLabel = "Importar";
                StatusText.Text = "Selecciona elementos para importar";
            }
            else
            {
                string catLabel = cats == 1 ? "1 categor\u00eda" : $"{cats} categor\u00edas";
                _transferLabel = $"Importar ({total} de {catLabel})";
                StatusText.Text = $"{total} elementos seleccionados en {catLabel}";
            }

            TransferEnabledChanged?.Invoke(this, _canTransfer);
        }

        // ── Transfer helpers ─────────────────────────────────────────────────

        private void ShowProgress(int current, int total, string itemName)
        {
            ProgressOverlay.Visibility = Visibility.Visible;
            ProgressTitle.Text = "Importando\u2026";
            ProgressDetail.Text = itemName;
            ProgressBar.Maximum = total;
            ProgressBar.Value = current;
            ProgressCount.Text = $"{current} de {total}";
            // Force WPF to repaint so the overlay updates mid-operation
            Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() => { }));
        }

        private void HideProgress()
        {
            ProgressOverlay.Visibility = Visibility.Collapsed;
        }

        private Action<int, int, string> CreateProgressReporter()
        {
            ProgressOverlay.Visibility = Visibility.Visible;
            ProgressBar.Value = 0;
            ProgressTitle.Text = "Preparando importaci\u00f3n\u2026";
            ProgressDetail.Text = "";
            ProgressCount.Text = "";
            Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() => { }));
            return (current, total, name) => ShowProgress(current, total, name);
        }

        private void ExecuteIndividualTransfer(List<long> allIds, string docTitle)
        {
            // Build ViewTemplateInfo-compatible list for the reusable dialog
            var items = _selectedByCategory
                .SelectMany(kv =>
                {
                    var catName = _categories.FirstOrDefault(c => c.Key == kv.Key)?.DisplayName ?? kv.Key;
                    return kv.Value.Select(id =>
                    {
                        var item = _currentCategoryKey == kv.Key
                            ? _currentItems.FirstOrDefault(i => i.Id == id)
                            : null;
                        return new ViewTemplateInfo
                        {
                            Id       = id,
                            Name     = item?.Name ?? id.ToString(),
                            ViewType = catName
                        };
                    });
                })
                .ToList();

            var dlg = new ConflictResolutionDialog(items)
            {
                Owner = Window.GetWindow(this)
            };

            if (dlg.ShowDialog() != true) return;

            var replaceIds = dlg.Resolutions
                .Where(kv => kv.Value == ConflictResolution.Replace)
                .Select(kv => kv.Key).ToList();
            var skipIds = dlg.Resolutions
                .Where(kv => kv.Value == ConflictResolution.Skip)
                .Select(kv => kv.Key).ToList();

            var totalResult = new ProjectStandardTransferResult();

            if (_transferCallback != null)
            {
                var progress = CreateProgressReporter();
                if (replaceIds.Count > 0)
                {
                    var r = _transferCallback(docTitle, replaceIds, ConflictResolution.Replace, progress);
                    totalResult.Transferred += r.Transferred;
                    totalResult.Skipped     += r.Skipped;
                    totalResult.Conflicts   += r.Conflicts;
                    totalResult.Errors.AddRange(r.Errors);
                }
                if (skipIds.Count > 0)
                {
                    var r = _transferCallback(docTitle, skipIds, ConflictResolution.Skip, progress);
                    totalResult.Transferred += r.Transferred;
                    totalResult.Skipped     += r.Skipped;
                    totalResult.Conflicts   += r.Conflicts;
                    totalResult.Errors.AddRange(r.Errors);
                }
                HideProgress();
            }
            else
            {
                totalResult.Transferred = replaceIds.Count;
                totalResult.Skipped     = skipIds.Count;
            }

            ShowResult(totalResult, Window.GetWindow(this));
        }

        private void ExecuteBulkTransfer(List<long> ids, string docTitle, ConflictResolution conflict)
        {
            ProjectStandardTransferResult result;
            if (_transferCallback != null)
            {
                var progress = CreateProgressReporter();
                result = _transferCallback(docTitle, ids, conflict, progress);
                HideProgress();
            }
            else
            {
                result = new ProjectStandardTransferResult { Transferred = ids.Count };
            }
            ShowResult(result, Window.GetWindow(this));
        }

        private static void ShowResult(ProjectStandardTransferResult r, Window? owner = null)
        {
            var message = $"Importados: {r.Transferred} \u00b7 Omitidos: {r.Skipped} \u00b7 Conflictos: {r.Conflicts}";

            var detailLines = new List<string>();
            if (r.SkippedNames.Count > 0)
                detailLines.Add($"Omitidos (ya exist\u00edan):\n{string.Join("\n", r.SkippedNames.Select(n => $"\u2022 {n}"))}");
            if (r.Errors.Count > 0)
                detailLines.Add($"Errores:\n{string.Join("\n", r.Errors.Take(5))}");

            var detail = detailLines.Count > 0 ? string.Join("\n\n", detailLines) : null;

            if (r.Errors.Count > 0)
                BIMPills.UI.Shared.BimPillsDialog.Warning(
                    header: "Importaci\u00f3n con avisos",
                    message: message, detail: detail, owner: owner);
            else
                BIMPills.UI.Shared.BimPillsDialog.Success(
                    header: "Importaci\u00f3n completada",
                    message: message, detail: detail, owner: owner);
        }

        // ── UI helpers ───────────────────────────────────────────────────────

        private void RefreshCategoryList()
        {
            var src = CategoryListBox.ItemsSource;
            CategoryListBox.ItemsSource = null;
            CategoryListBox.ItemsSource = src;
        }

        private void RefreshItemList()
        {
            var src = ItemsListBox.ItemsSource;
            ItemsListBox.ItemsSource = null;
            ItemsListBox.ItemsSource = src;
        }
    }
}
