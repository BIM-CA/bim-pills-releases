using BIMPills.Core.Models;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace BIMPills.UI.ViewTemplates
{
    public partial class ConflictResolutionDialog : Window
    {
        private readonly List<ConflictRow> _rows;

        /// <summary>Result: per-template action chosen by the user.</summary>
        public IReadOnlyDictionary<long, ConflictResolution> Resolutions { get; private set; }
            = new Dictionary<long, ConflictResolution>();

        public ConflictResolutionDialog(IReadOnlyList<ViewTemplateInfo> conflictingTemplates)
        {
            InitializeComponent();
            BIMPills.UI.Shared.ThemeHelper.Apply(this);

            _rows = conflictingTemplates
                .Select(t => new ConflictRow { Id = t.Id, Name = t.Name, ViewType = t.ViewType, Action = "Replace" })
                .ToList();

            ConflictGrid.ItemsSource = _rows;
            HeaderTitle.Text = $"{_rows.Count} plantillas ya existen en el documento destino";
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            int replace = _rows.Count(r => r.Action == "Replace");
            int skip    = _rows.Count(r => r.Action == "Skip");
            BulkSummary.Text = $"{replace} reemplazar · {skip} omitir";
        }

        private void BulkReplace_Click(object sender, RoutedEventArgs e)
        {
            foreach (var r in _rows) r.Action = "Replace";
            RefreshGrid();
            UpdateSummary();
        }

        private void BulkSkip_Click(object sender, RoutedEventArgs e)
        {
            foreach (var r in _rows) r.Action = "Skip";
            RefreshGrid();
            UpdateSummary();
        }

        private void ActionCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            UpdateSummary();
        }

        private void RefreshGrid()
        {
            var src = ConflictGrid.ItemsSource;
            ConflictGrid.ItemsSource = null;
            ConflictGrid.ItemsSource = src;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            Resolutions = _rows.ToDictionary(
                r => r.Id,
                r => r.Action == "Skip" ? ConflictResolution.Skip : ConflictResolution.Replace);
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        // ── Row model ────────────────────────────────────────────────────────

        internal sealed class ConflictRow : INotifyPropertyChanged
        {
            private string _action = "Replace";

            public long   Id       { get; set; }
            public string Name     { get; set; } = "";
            public string ViewType { get; set; } = "";

            public string Action
            {
                get => _action;
                set
                {
                    _action = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Action)));
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
        }
    }
}
