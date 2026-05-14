using BIMPills.Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace BIMPills.UI.Export.Sheets
{
    public partial class MixSetsDialog : Window
    {
        private readonly List<PublicationSet> _sets;
        public HashSet<string> SelectedSetIds { get; } = new HashSet<string>();

        public MixSetsDialog(IEnumerable<PublicationSet> sets, IEnumerable<string> initiallySelected)
        {
            InitializeComponent();
            _sets = sets.ToList();
            var initial = new HashSet<string>(initiallySelected);
            BuildCheckboxes(initial);
        }

        private void BuildCheckboxes(HashSet<string> initial)
        {
            SetCheckPanel.Children.Clear();
            foreach (var set in _sets)
            {
                var cb = new CheckBox
                {
                    Content = $"{set.Name}  ({set.Items.Count} items)",
                    Tag = set.Id,
                    Padding = new Thickness(4, 5, 4, 5),
                    FontSize = 12,
                    IsChecked = initial.Contains(set.Id),
                };
                cb.Checked   += OnCheckChanged;
                cb.Unchecked += OnCheckChanged;
                SetCheckPanel.Children.Add(cb);
            }
            UpdateState();
        }

        private void OnCheckChanged(object sender, RoutedEventArgs e) => UpdateState();

        private void UpdateState()
        {
            int count = SetCheckPanel.Children
                .OfType<CheckBox>()
                .Count(cb => cb.IsChecked == true);

            SelectionHint.Text = count == 1 ? "1 conjunto seleccionado"
                                             : $"{count} conjuntos seleccionados";
            ApplyBtn.IsEnabled = count >= 2;
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            SelectedSetIds.Clear();
            foreach (CheckBox cb in SetCheckPanel.Children.OfType<CheckBox>())
            {
                if (cb.IsChecked == true && cb.Tag is string id)
                    SelectedSetIds.Add(id);
            }
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
