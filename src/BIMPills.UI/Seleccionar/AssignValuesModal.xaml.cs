using BIMPills.Core.Gestion;
using BIMPills.Core.Seleccionar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace BIMPills.UI.Seleccionar
{
    public partial class AssignValuesModal : Window
    {
        private IReadOnlyList<WorksetInfo>             _worksets;
        private IReadOnlyList<CategoryElementSummary>  _selectionSummary;
        private readonly ObservableCollection<ParamEditRow>    _paramRows = new();
        private ICollectionView?                               _paramView;
        /// <summary>IDs snapshotteados en el momento de abrir el modal — no depende de la selección activa al aplicar.</summary>
        private List<long>                                     _snapshotElementIds = new();

        public SubprojectAssignRequest? ResultRequest { get; private set; }

        /// <summary>Raised when the user clicks Aplicar with a valid request.</summary>
        public event Action<SubprojectAssignRequest>? OnApply;


        public AssignValuesModal(
            IReadOnlyList<WorksetInfo>            worksets,
            IReadOnlyList<ParamInfo>              compatibleParams,
            IReadOnlyList<CategoryElementSummary> selectionSummary,
            List<long>?                           snapshotElementIds = null)
        {
            InitializeComponent();

            _worksets            = worksets;
            _selectionSummary    = selectionSummary;
            _snapshotElementIds  = snapshotElementIds ?? new List<long>();

            // ── Worksets ──────────────────────────────────────────────
            if (worksets.Count == 0)
            {
                NoWorksetsLabel.Visibility = Visibility.Visible;
                AssignWorksetCheck.IsEnabled = false;
            }
            else
            {
                foreach (var w in worksets)
                    WorksetCombo.Items.Add(w.Name);
                WorksetCombo.SelectedIndex = 0;
            }

            // ── Parameters ────────────────────────────────────────────
            foreach (var param in compatibleParams)
                _paramRows.Add(new ParamEditRow
                {
                    ParameterName = param.Name,
                    Group         = param.Group,
                    AllowedValues = param.AllowedValues,
                    IsTypeParam   = param.IsTypeParam
                });

            _paramView = CollectionViewSource.GetDefaultView(_paramRows);
            _paramView.Filter = o => o is ParamEditRow r &&
                (string.IsNullOrEmpty(_paramSearchText) ||
                 r.ParameterName.IndexOf(_paramSearchText, StringComparison.OrdinalIgnoreCase) >= 0);
            _paramView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ParamEditRow.Group)));
            ParamGrid.ItemsSource = _paramView;

            if (compatibleParams.Count == 0)
                NoParamsLabel.Visibility = Visibility.Visible;

            // ── Summary ───────────────────────────────────────────────
            SummaryList.ItemsSource = selectionSummary;
            UpdateSummaryTotal();

            // ── Collapse all sections by default ──────────────────────
            Loaded += (_, _) => InitCollapsibleSections();
        }

        private void UpdateSummaryTotal()
        {
            var total    = _selectionSummary.Sum(s => s.TotalCount);
            var editable = _selectionSummary.Sum(s => s.EditableCount);
            SummaryTotalLabel.Text   = $"{total} elemento{(total != 1 ? "s" : "")} seleccionado{(total != 1 ? "s" : "")}";
            FooterTotalLabel.Text    = $"{total} seleccionado{(total != 1 ? "s" : "")}";
            FooterEditableLabel.Text = $"{editable} editable{(editable != 1 ? "s" : "")}";
        }

        /// <summary>
        /// Actualiza los IDs snapshotteados cuando la selección de Revit cambia mientras el modal está abierto.
        /// </summary>
        public void UpdateElementIds(List<long> ids)
        {
            _snapshotElementIds = ids ?? new List<long>();
        }

        /// <summary>
        /// Reemplaza la lista de parámetros compatibles, el resumen y (opcionalmente) los worksets.
        /// Llamado desde AssignValuesOpenHandler cuando la selección de Revit cambia.
        /// </summary>
        public void UpdateParams(
            IReadOnlyList<ParamInfo>              newParams,
            IReadOnlyList<CategoryElementSummary> newSummary,
            IReadOnlyList<WorksetInfo>?           newWorksets = null)
        {
            if (newWorksets != null)
                UpdateWorksets(newWorksets);

            _selectionSummary = newSummary;

            _paramRows.Clear();
            foreach (var param in newParams)
                _paramRows.Add(new ParamEditRow
                {
                    ParameterName = param.Name,
                    Group         = param.Group,
                    AllowedValues = param.AllowedValues,
                    IsTypeParam   = param.IsTypeParam
                });

            NoParamsLabel.Visibility = newParams.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            SummaryList.ItemsSource = newSummary;
            UpdateSummaryTotal();

            _paramView?.Refresh();
        }

        /// <summary>Actualiza la lista de worksets en el ComboBox (recoge cambios post-apertura).</summary>
        public void UpdateWorksets(IReadOnlyList<WorksetInfo> worksets)
        {
            _worksets = worksets;
            WorksetCombo.Items.Clear();

            if (worksets.Count == 0)
            {
                NoWorksetsLabel.Visibility   = Visibility.Visible;
                AssignWorksetCheck.IsEnabled = false;
            }
            else
            {
                NoWorksetsLabel.Visibility   = Visibility.Collapsed;
                AssignWorksetCheck.IsEnabled = true;
                foreach (var w in worksets)
                    WorksetCombo.Items.Add(w.Name);
                if (WorksetCombo.SelectedIndex < 0)
                    WorksetCombo.SelectedIndex = 0;
            }
        }

        private void AssignWorksetCheck_Changed(object sender, RoutedEventArgs e)
        {
            WorksetCombo.IsEnabled = AssignWorksetCheck.IsChecked == true && _worksets.Count > 0;
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            var assignWorkset = AssignWorksetCheck.IsChecked == true && WorksetCombo.SelectedIndex >= 0;

            var assignments = _paramRows
                .Where(r => !string.IsNullOrWhiteSpace(r.NewValue))
                .Select(r => new ParameterAssignment
                {
                    ParameterName = r.ParameterName,
                    NewValue      = r.NewValue.Trim(),
                    IsTypeParam   = r.IsTypeParam
                })
                .ToList();

            // Si no hay nada que hacer simplemente cerrar — sin bloquear al usuario
            if (!assignWorkset && assignments.Count == 0)
            {
                Close();
                return;
            }

            ResultRequest = new SubprojectAssignRequest
            {
                WorksetId            = assignWorkset ? _worksets[WorksetCombo.SelectedIndex].Id : 0L,
                AssignWorkset        = assignWorkset,
                // Usar los IDs snapshotteados al abrir el modal (no re-leer la selección al
                // ejecutar — para ese momento el modal ya cerró y Revit puede haber limpiado
                // la selección al recuperar el foco).
                UseCurrentSelection  = false,
                ElementIds           = _snapshotElementIds,
                ParameterAssignments = assignments
            };

            OnApply?.Invoke(ResultRequest);
            Close();
        }

        // ── Buscador de parámetros ────────────────────────────────────────────

        private string _paramSearchText = string.Empty;

        private void ParamSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _paramSearchText = ((TextBox)sender).Text;
            _paramView?.Refresh();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

        // ── Collapsible sections (colapsadas por defecto) ─────────────────────

        private bool _sec1Expanded = false;
        private bool _sec2Expanded = false;
        private bool _sec3Expanded = false;

        private void InitCollapsibleSections()
        {
            Sec1Content.Visibility = Visibility.Collapsed;
            Sec1Chevron.Text       = "▶";
            Sec2Content.Visibility = Visibility.Collapsed;
            Sec2Chevron.Text       = "▶";
            Sec3Content.Visibility = Visibility.Collapsed;
            Sec3Chevron.Text       = "▶";
        }

        private void Sec1Header_Click(object sender, MouseButtonEventArgs e)
        {
            _sec1Expanded = !_sec1Expanded;
            Sec1Content.Visibility = _sec1Expanded ? Visibility.Visible : Visibility.Collapsed;
            Sec1Chevron.Text       = _sec1Expanded ? "▼" : "▶";
        }

        private void Sec2Header_Click(object sender, MouseButtonEventArgs e)
        {
            _sec2Expanded = !_sec2Expanded;
            Sec2Content.Visibility = _sec2Expanded ? Visibility.Visible : Visibility.Collapsed;
            Sec2Chevron.Text       = _sec2Expanded ? "▼" : "▶";
        }

        private void Sec3Header_Click(object sender, MouseButtonEventArgs e)
        {
            _sec3Expanded = !_sec3Expanded;
            Sec3Content.Visibility = _sec3Expanded ? Visibility.Visible : Visibility.Collapsed;
            Sec3Chevron.Text       = _sec3Expanded ? "▼" : "▶";
        }
    }

    // ── ParamEditRow ViewModel ────────────────────────────────────────

    internal sealed class ParamEditRow : INotifyPropertyChanged
    {
        public string ParameterName { get; set; } = string.Empty;
        public string Group         { get; set; } = "Otros";

        /// <summary>Valores posibles (e.g. fases). Null = texto libre.</summary>
        public IReadOnlyList<string>? AllowedValues { get; set; }
        public bool HasAllowedValues => AllowedValues != null && AllowedValues.Count > 0;

        public bool   IsTypeParam    { get; set; } = false;
        public string TypeLabel      => IsTypeParam ? "TIPO" : "EJEMPLAR";
        public string TypeLabelColor => IsTypeParam ? "#FFFFFF" : "#FFFFFF";
        public string TypeLabelBg    => IsTypeParam ? "#7B5EA7" : "#1565C0";

        private string _newValue = string.Empty;
        public string NewValue
        {
            get => _newValue;
            set { _newValue = value ?? string.Empty; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
