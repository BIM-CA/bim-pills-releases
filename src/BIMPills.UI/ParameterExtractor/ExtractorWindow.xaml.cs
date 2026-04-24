using System;
using System.Collections.Generic;
using System.Windows;
using BIMPills.Core.ParameterExtractor;
using BIMPills.Infrastructure.Persistence;

namespace BIMPills.UI.ParameterExtractor
{
    public partial class ExtractorWindow : Window
    {
        public ExtractorWindow()
        {
            InitializeComponent();
            BIMPills.UI.Shared.ThemeHelper.Apply(this);

            Panel.ExportEnabledChanged += (_, enabled) => ApplyButton.IsEnabled = enabled;
        }

        public void SetModelName(string modelName) => ModelNameLabel.Text = modelName;

        public void Initialize(
            int selectedElementCount,
            Func<ExtractionConfig, bool>? applyCallback = null,
            JsonExtractionPresetRepository? presetRepository = null,
            IReadOnlyList<string>? availableCategories = null,
            IReadOnlyDictionary<string, IReadOnlyList<string>>? paramsByCategory = null)
        {
            Panel.Initialize(
                selectedElementCount,
                applyCallback,
                presetRepository,
                availableCategories,
                paramsByCategory);
        }

        private void Apply_Click(object sender, RoutedEventArgs e) => Panel.TriggerExport();

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
