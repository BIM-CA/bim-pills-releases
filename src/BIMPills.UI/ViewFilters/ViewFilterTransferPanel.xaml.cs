using BIMPills.Core.Models;
using BIMPills.UI.ViewTemplates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace BIMPills.UI.ViewFilters
{
    public partial class ViewFilterTransferPanel : UserControl
    {
        private List<TransferableFilterInfo>             _filters          = new();
        private Func<string, IReadOnlyList<TransferableFilterInfo>>? _getFiltersCallback;
        private Func<string, long, FilterDetail?>?       _getDetailCallback;
        private Func<string, IReadOnlyList<long>, ConflictResolution, TransferResult>? _transferCallback;

        private bool   _canTransfer;
        private string _transferLabel = "Importar";

        public event EventHandler<bool>? TransferEnabledChanged;
        public string TransferLabel => _transferLabel;

        public ViewFilterTransferPanel()
        {
            InitializeComponent();
        }

        public void Initialize(
            IReadOnlyList<OpenDocumentInfo> openDocs,
            Func<string, IReadOnlyList<TransferableFilterInfo>>? getFiltersCallback = null,
            Func<string, long, FilterDetail?>? getDetailCallback = null,
            Func<string, IReadOnlyList<long>, ConflictResolution, TransferResult>? transferCallback = null)
        {
            _getFiltersCallback = getFiltersCallback;
            _getDetailCallback  = getDetailCallback;
            _transferCallback   = transferCallback;

            var sourceDocs = openDocs.Where(d => !d.IsCurrent).ToList();

            if (sourceDocs.Count == 0)
            {
                SingleDocState.Visibility   = Visibility.Visible;
                DetailEmptyState.Visibility = Visibility.Collapsed;
                return;
            }

            SourceDocCombo.Items.Clear();
            foreach (var doc in sourceDocs)
                SourceDocCombo.Items.Add(new ComboBoxItem { Content = doc.Title, Tag = doc.Title });

            if (SourceDocCombo.Items.Count > 0)
                SourceDocCombo.SelectedIndex = 0;
        }

        private void SourceDoc_Changed(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var docTitle = (SourceDocCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
                if (string.IsNullOrEmpty(docTitle)) return;

                _filters = _getFiltersCallback != null
                    ? _getFiltersCallback(docTitle).ToList()
                    : GenerateMockFilters();

                FilterListBox.ItemsSource = _filters;
                UpdateSelection();
                ClearDetail();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error cargando filtros: {ex.Message}",
                    "BIM Pills \u2014 Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── List interactions ─────────────────────────────────────────────

        private void FilterList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FilterListBox.SelectedItem is not TransferableFilterInfo info) return;
            LoadDetail(info);
        }

        private void FilterCheckBox_Click(object sender, RoutedEventArgs e)
        {
            UpdateSelection();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool all = _filters.All(f => f.IsSelected);
            foreach (var f in _filters) f.IsSelected = !all;
            RefreshList();
            UpdateSelection();
        }

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            var q = SearchBox.Text.Trim().ToLowerInvariant();
            FilterListBox.ItemsSource = string.IsNullOrEmpty(q)
                ? _filters
                : _filters.Where(f => f.Name.ToLowerInvariant().Contains(q)).ToList();
        }

        private void UpdateSelection()
        {
            int sel   = _filters.Count(f => f.IsSelected);
            int total = _filters.Count;
            SelectionSummary.Text = $"{sel} de {total} seleccionados";
            StatusText.Text = sel > 0
                ? $"{sel} filtros listos para importar"
                : "Selecciona filtros para importar";

            _canTransfer   = sel > 0;
            _transferLabel = sel > 0 ? $"Importar ({sel} filtros)" : "Importar";
            TransferEnabledChanged?.Invoke(this, _canTransfer);
        }

        // ── Detail ────────────────────────────────────────────────────────

        private void LoadDetail(TransferableFilterInfo info)
        {
            try
            {
                var docTitle = (SourceDocCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
                FilterDetail? detail = _getDetailCallback?.Invoke(docTitle, info.Id);

                DetailName.Text = detail?.Name ?? info.Name;
                DetailType.Text = detail?.FilterType ?? info.FilterType;

                CategoriesList.ItemsSource = detail?.Categories ?? new List<string>();
                RulesList.ItemsSource      = detail?.Rules ?? new List<string>();
                NoRulesText.Visibility     = (detail?.Rules?.Count ?? 0) == 0
                    ? Visibility.Visible : Visibility.Collapsed;

                DetailEmptyState.Visibility = Visibility.Collapsed;
                DetailPanel.Visibility      = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error cargando detalle: {ex.Message}",
                    "BIM Pills \u2014 Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearDetail()
        {
            DetailPanel.Visibility      = Visibility.Collapsed;
            DetailEmptyState.Visibility = Visibility.Visible;
        }

        // ── Transfer ──────────────────────────────────────────────────────

        public void TriggerTransfer()
        {
            try
            {
                var selected = _filters.Where(f => f.IsSelected).ToList();
                if (selected.Count == 0)
                {
                    BIMPills.UI.Shared.BimPillsDialog.Warning(
                        header: "Nada seleccionado",
                        message: "Selecciona al menos un filtro para importar.",
                        owner: Window.GetWindow(this));
                    return;
                }

                var docTitle    = (SourceDocCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
                var conflictTag = (ConflictCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Replace";

                if (conflictTag == "Individual")
                {
                    var templateItems = selected
                        .Select(f => new ViewTemplateInfo { Id = f.Id, Name = f.Name, ViewType = f.FilterType })
                        .ToList();
                    var dlg = new ConflictResolutionDialog(templateItems) { Owner = Window.GetWindow(this) };
                    if (dlg.ShowDialog() != true) return;

                    var replaceIds = dlg.Resolutions.Where(kv => kv.Value == ConflictResolution.Replace).Select(kv => kv.Key).ToList();
                    var skipIds    = dlg.Resolutions.Where(kv => kv.Value == ConflictResolution.Skip).Select(kv => kv.Key).ToList();
                    var total = new TransferResult();

                    if (_transferCallback != null)
                    {
                        if (replaceIds.Count > 0) MergeResult(total, _transferCallback(docTitle, replaceIds, ConflictResolution.Replace));
                        if (skipIds.Count > 0)    MergeResult(total, _transferCallback(docTitle, skipIds,    ConflictResolution.Skip));
                    }
                    else { total.Transferred = replaceIds.Count; total.Skipped = skipIds.Count; }
                    ShowResult(total, Window.GetWindow(this));
                    return;
                }

                var conflict = conflictTag == "Skip" ? ConflictResolution.Skip : ConflictResolution.Replace;
                var confirmed = BIMPills.UI.Shared.BimPillsDialog.Confirm(
                    header: "\u00bfImportar filtros?",
                    message: $"Se importar\u00e1n {selected.Count} filtros desde \u00ab{docTitle}\u00bb.",
                    detail: conflict == ConflictResolution.Replace
                        ? "Los filtros con el mismo nombre ser\u00e1n reemplazados."
                        : "Los filtros con el mismo nombre se omitir\u00e1n.",
                    owner: Window.GetWindow(this),
                    yesText: "Importar",
                    noText: "Cancelar");

                if (!confirmed) return;

                TransferResult result;
                if (_transferCallback != null)
                    result = _transferCallback(docTitle, selected.Select(f => f.Id).ToList(), conflict);
                else
                    result = new TransferResult { Transferred = selected.Count };

                ShowResult(result, Window.GetWindow(this));
            }
            catch (Exception ex)
            {
                BIMPills.UI.Shared.BimPillsDialog.Error(
                    header: "Error al importar",
                    message: "Ocurri\u00f3 un error al importar los filtros.",
                    detail: ex.Message,
                    owner: Window.GetWindow(this));
            }
        }

        private static void MergeResult(TransferResult dest, TransferResult src)
        {
            dest.Transferred += src.Transferred;
            dest.Skipped     += src.Skipped;
            dest.Conflicts   += src.Conflicts;
            dest.Errors.AddRange(src.Errors);
        }

        private static void ShowResult(TransferResult r, Window? owner = null)
        {
            var message = $"Importados: {r.Transferred} \u00b7 Omitidos: {r.Skipped}";
            var detail = r.Errors.Count > 0
                ? $"Errores:\n{string.Join("\n", r.Errors.Take(5))}"
                : null;

            if (r.Errors.Count > 0)
                BIMPills.UI.Shared.BimPillsDialog.Warning(
                    header: "Importaci\u00f3n con avisos",
                    message: message, detail: detail, owner: owner);
            else
                BIMPills.UI.Shared.BimPillsDialog.Success(
                    header: "Importaci\u00f3n completada",
                    message: message, owner: owner);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private void RefreshList()
        {
            var src = FilterListBox.ItemsSource;
            FilterListBox.ItemsSource = null;
            FilterListBox.ItemsSource = src;
        }

        private static List<TransferableFilterInfo> GenerateMockFilters()
        {
            return new List<TransferableFilterInfo>
            {
                new TransferableFilterInfo { Id=1, Name="MUROS - Hormigón",     FilterType="Par\u00e1metro", CategoryCount=1, RuleCount=2 },
                new TransferableFilterInfo { Id=2, Name="ESTRUCTURA - Pilares", FilterType="Par\u00e1metro", CategoryCount=2, RuleCount=1 },
                new TransferableFilterInfo { Id=3, Name="MEP - Fontanería",     FilterType="Par\u00e1metro", CategoryCount=3, RuleCount=3 },
                new TransferableFilterInfo { Id=4, Name="FASE - Demolición",    FilterType="Par\u00e1metro", CategoryCount=5, RuleCount=1 },
                new TransferableFilterInfo { Id=5, Name="SELECCIÓN - Fachada",  FilterType="Selecci\u00f3n",CategoryCount=0, RuleCount=0 },
                new TransferableFilterInfo { Id=6, Name="DISCIPLINA - ARQ",     FilterType="Par\u00e1metro", CategoryCount=4, RuleCount=2 },
            };
        }
    }
}
