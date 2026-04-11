using BIMPills.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace BIMPills.UI.ViewTemplates
{
    public partial class ViewTemplateTransferPanel : UserControl
    {
        private List<OpenDocumentInfo> _openDocs = new();
        private List<ViewTemplateInfo> _templates = new();
        private Func<string, IReadOnlyList<ViewTemplateInfo>>? _getTemplatesCallback;
        private Func<string, long, ViewTemplateDetail?>? _getDetailCallback;
        private Func<string, IReadOnlyList<long>, ConflictResolution, TransferResult>? _transferCallback;

        private string _transferLabel = "Importar";
        private bool _canTransfer;

        /// <summary>Raised when transfer availability changes.</summary>
        public event EventHandler<bool>? TransferEnabledChanged;

        public string TransferLabel => _transferLabel;

        public ViewTemplateTransferPanel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Initializes the panel with callbacks for loading templates and transferring.
        /// </summary>
        public void Initialize(
            IReadOnlyList<OpenDocumentInfo> openDocs,
            Func<string, IReadOnlyList<ViewTemplateInfo>>? getTemplatesCallback = null,
            Func<string, long, ViewTemplateDetail?>? getDetailCallback = null,
            Func<string, IReadOnlyList<long>, ConflictResolution, TransferResult>? transferCallback = null)
        {
            _openDocs = openDocs.ToList();
            _getTemplatesCallback = getTemplatesCallback;
            _getDetailCallback = getDetailCallback;
            _transferCallback = transferCallback;

            // Filter to only non-current docs as sources
            var sourceDocs = _openDocs.Where(d => !d.IsCurrent).ToList();

            if (sourceDocs.Count == 0)
            {
                // Only one project open
                SingleDocState.Visibility = Visibility.Visible;
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
                var selectedItem = SourceDocCombo.SelectedItem as ComboBoxItem;
                var docTitle = selectedItem?.Tag?.ToString() ?? "";
                if (string.IsNullOrEmpty(docTitle)) return;

                if (_getTemplatesCallback != null)
                {
                    _templates = _getTemplatesCallback(docTitle).ToList();
                }

                TemplateListBox.ItemsSource = _templates;
                UpdateSelection();
                ClearDetail();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error cargando plantillas: {ex.Message}",
                    "BIM Pills \u2014 Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var query = SearchBox.Text?.Trim().ToLowerInvariant() ?? "";
                if (string.IsNullOrEmpty(query))
                    TemplateListBox.ItemsSource = _templates;
                else
                    TemplateListBox.ItemsSource = _templates.Where(t =>
                        t.Name.ToLowerInvariant().Contains(query) ||
                        t.ViewType.ToLowerInvariant().Contains(query)).ToList();
            }
            catch { }
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool allSelected = _templates.All(t => t.IsSelected);
            foreach (var t in _templates)
                t.IsSelected = !allSelected;

            var source = TemplateListBox.ItemsSource;
            TemplateListBox.ItemsSource = null;
            TemplateListBox.ItemsSource = source;
            UpdateSelection();
        }

        private void TemplateCheckBox_Click(object sender, RoutedEventArgs e)
        {
            UpdateSelection();
        }

        private void TemplateList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TemplateListBox.SelectedItem is not ViewTemplateInfo info) return;
            LoadDetail(info);
        }

        private void UpdateSelection()
        {
            int selected = _templates.Count(t => t.IsSelected);
            SelectionSummary.Text = $"{selected} de {_templates.Count} seleccionadas";
            StatusText.Text = selected > 0
                ? $"{selected} plantillas listas para importar"
                : "Selecciona plantillas para importar";

            _canTransfer = selected > 0;
            _transferLabel = selected > 0 ? $"Importar {selected} plantillas" : "Importar";
            TransferEnabledChanged?.Invoke(this, _canTransfer);
        }

        private void ClearDetail()
        {
            DetailPanel.Visibility = Visibility.Collapsed;
            DetailEmptyState.Visibility = Visibility.Visible;
            ParamsGrid.ItemsSource = null;
        }

        private void LoadDetail(ViewTemplateInfo info)
        {
            try
            {
                var docTitle = (SourceDocCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
                ViewTemplateDetail? detail = null;

                if (_getDetailCallback != null)
                    detail = _getDetailCallback(docTitle, info.Id);

                DetailName.Text = detail?.Name ?? info.Name;
                DetailViewType.Text = detail?.ViewType ?? info.ViewType;
                AssignedViewCount.Text = detail != null
                    ? $"N\u00famero de vistas con esta plantilla asignada:\u2009{detail.AssignedViewCount}"
                    : "";

                // Build parameter rows
                var rows = new List<ViewTemplateParamRow>();
                if (detail?.Parameters is { Count: > 0 })
                {
                    foreach (var p in detail.Parameters)
                        rows.Add(new ViewTemplateParamRow
                        {
                            Name = p.Name,
                            Value = p.Value,
                            IsComplex = p.IsComplex,
                            Include = p.Include
                        });
                }
                else
                {
                    // Fallback when no detail available
                    rows.Add(new ViewTemplateParamRow { Name = "(detalle no disponible)", IsComplex = false, Value = "" });
                }

                ParamsGrid.ItemsSource = rows;
                DetailEmptyState.Visibility = Visibility.Collapsed;
                DetailPanel.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error cargando detalle: {ex.Message}",
                    "BIM Pills \u2014 Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Inner model for DataGrid rows (INPC for opacity trigger) ─────────────

        private class ViewTemplateParamRow : System.ComponentModel.INotifyPropertyChanged
        {
            private bool _include = true;

            public string Name { get; set; } = "";
            public string Value { get; set; } = "";
            public bool IsComplex { get; set; }

            public bool Include
            {
                get => _include;
                set
                {
                    _include = value;
                    PropertyChanged?.Invoke(this,
                        new System.ComponentModel.PropertyChangedEventArgs(nameof(Include)));
                }
            }

            public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        }

        /// <summary>Trigger transfer from external button.</summary>
        public void TriggerTransfer()
        {
            try
            {
                var selected = _templates.Where(t => t.IsSelected).ToList();
                if (selected.Count == 0)
                {
                    BIMPills.UI.Shared.BimPillsDialog.Warning(
                        header: "Nada seleccionado",
                        message: "Selecciona al menos una plantilla para importar.",
                        owner: Window.GetWindow(this));
                    return;
                }

                var docTitle    = (SourceDocCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
                var conflictTag = (ConflictCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Replace";

                // ── Individual mode ─────────────────────────────────────────
                if (conflictTag == "Individual")
                {
                    ExecuteIndividualTransfer(selected, docTitle);
                    return;
                }

                // ── Bulk mode ────────────────────────────────────────────────
                var conflict = conflictTag == "Skip" ? ConflictResolution.Skip : ConflictResolution.Replace;

                var confirmed = BIMPills.UI.Shared.BimPillsDialog.Confirm(
                    header: "\u00bfImportar plantillas?",
                    message: $"Se importar\u00e1n {selected.Count} plantillas desde \u00ab{docTitle}\u00bb.",
                    detail: conflict == ConflictResolution.Replace
                        ? "Las plantillas con el mismo nombre que ya existan en el proyecto actual ser\u00e1n reemplazadas."
                        : "Las plantillas con el mismo nombre que ya existan se omitir\u00e1n.",
                    owner: Window.GetWindow(this),
                    yesText: "Importar",
                    noText: "Cancelar");

                if (!confirmed) return;

                ExecuteBulkTransfer(selected.Select(t => t.Id).ToList(), docTitle, conflict);
            }
            catch (Exception ex)
            {
                BIMPills.UI.Shared.BimPillsDialog.Error(
                    header: "Error al importar",
                    message: "Ocurri\u00f3 un error al importar las plantillas.",
                    detail: ex.Message,
                    owner: Window.GetWindow(this));
            }
        }

        private void ExecuteIndividualTransfer(List<ViewTemplateInfo> selected, string docTitle)
        {
            var dlg = new ConflictResolutionDialog(selected)
            {
                Owner = Window.GetWindow(this)
            };

            if (dlg.ShowDialog() != true) return;

            // Split into groups by chosen resolution
            var replaceIds = dlg.Resolutions
                .Where(kv => kv.Value == ConflictResolution.Replace)
                .Select(kv => kv.Key).ToList();
            var skipIds = dlg.Resolutions
                .Where(kv => kv.Value == ConflictResolution.Skip)
                .Select(kv => kv.Key).ToList();

            var totalResult = new TransferResult();

            if (_transferCallback != null)
            {
                if (replaceIds.Count > 0)
                {
                    var r = _transferCallback(docTitle, replaceIds, ConflictResolution.Replace);
                    totalResult.Transferred += r.Transferred;
                    totalResult.Skipped     += r.Skipped;
                    totalResult.Conflicts   += r.Conflicts;
                    totalResult.Errors.AddRange(r.Errors);
                }
                if (skipIds.Count > 0)
                {
                    var r = _transferCallback(docTitle, skipIds, ConflictResolution.Skip);
                    totalResult.Transferred += r.Transferred;
                    totalResult.Skipped     += r.Skipped;
                    totalResult.Conflicts   += r.Conflicts;
                    totalResult.Errors.AddRange(r.Errors);
                }
            }
            else
            {
                // Sandbox
                totalResult.Transferred = replaceIds.Count;
                totalResult.Skipped     = skipIds.Count;
            }

            ShowTransferResult(totalResult);
        }

        private void ExecuteBulkTransfer(List<long> ids, string docTitle, ConflictResolution conflict)
        {
            if (_transferCallback != null)
            {
                var result = _transferCallback(docTitle, ids, conflict);
                ShowTransferResult(result);
            }
            else
            {
                MessageBox.Show(
                    $"Callback de transferencia no configurado (sandbox).\n\n" +
                    $"Se transferir\u00edan {ids.Count} plantillas desde \u00ab{docTitle}\u00bb.",
                    "BIM Pills \u2014 Sandbox", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private static void ShowTransferResult(TransferResult result)
        {
            var msg = $"Transferencia completada.\n\n" +
                      $"\u2022 Transferidas: {result.Transferred}\n" +
                      $"\u2022 Omitidas: {result.Skipped}\n" +
                      $"\u2022 Conflictos resueltos: {result.Conflicts}";
            if (result.Errors.Count > 0)
                msg += $"\n\nErrores:\n{string.Join("\n", result.Errors.Take(5))}";

            MessageBox.Show(msg, "BIM Pills \u2014 Transferencia",
                MessageBoxButton.OK,
                result.Errors.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
    }
}
