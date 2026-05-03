using BIMPills.Core.Gestion;
using BIMPills.Core.Seleccionar;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace BIMPills.UI.Seleccionar
{
    public partial class SubprojectAssignModal : Window
    {
        private readonly IReadOnlyList<WorksetInfo> _worksets;
        private readonly IFilterPresetRepository    _presetRepo;

        public SubprojectAssignRequest? ResultRequest { get; private set; }

        public SubprojectAssignModal(IReadOnlyList<WorksetInfo> worksets, IFilterPresetRepository presetRepo)
        {
            InitializeComponent();

            _worksets   = worksets;
            _presetRepo = presetRepo;

            if (worksets.Count == 0)
            {
                NoWorksetsWarning.Visibility = Visibility.Visible;
                AssignButton.IsEnabled       = false;
            }
            else
            {
                foreach (var w in worksets)
                    WorksetCombo.Items.Add(w.Name);
                WorksetCombo.SelectedIndex = 0;
            }

            // Populate filter presets for UseFilter mode
            foreach (var p in presetRepo.LoadAll())
                FilterPresetCombo.Items.Add(p);
            if (FilterPresetCombo.Items.Count > 0)
                FilterPresetCombo.SelectedIndex = 0;
        }

        private void SourceMode_Changed(object sender, RoutedEventArgs e)
        {
            if (FilterPresetRow == null) return;
            FilterPresetRow.Visibility = UseFilterRadio.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void Assign_Click(object sender, RoutedEventArgs e)
        {
            if (WorksetCombo.SelectedIndex < 0 || _worksets.Count == 0) return;

            var selectedWorkset = _worksets[WorksetCombo.SelectedIndex];

            // Build element IDs: when UseFilter, apply the filter preset
            // The actual filtering happens in the Revit thread via the handler.
            // We pass ElementIds=[] as signal that the handler should use current Revit selection,
            // OR we use a special WorksetId < 0 to indicate "filter mode" — but simpler approach:
            // We always pass the filter if UseFilter is checked; the handler receives it separately.
            // For now: UseSelection = pass empty list (handler reads UIDocument.Selection)
            //          UseFilter    = pass null (not yet wired; handled in future)

            var useFilter = UseFilterRadio.IsChecked == true;
            var selectedPreset = useFilter ? FilterPresetCombo.SelectedItem as FilterPreset : null;

            ResultRequest = new SubprojectAssignRequest
            {
                ElementIds = new List<long>(),   // empty = use current Revit selection
                WorksetId  = selectedWorkset.Id,
                UseCurrentSelection = !useFilter,
                FilterPreset = selectedPreset
            };

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
