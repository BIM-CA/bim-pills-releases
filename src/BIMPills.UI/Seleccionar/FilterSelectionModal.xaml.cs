using BIMPills.Core.Seleccionar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace BIMPills.UI.Seleccionar
{
    public partial class FilterSelectionModal : Window
    {
        private readonly IReadOnlyList<string>   _categories;
        private readonly IFilterPresetRepository _presetRepo;
        private readonly List<ConditionRow>      _conditionRows = new();

        public SelectionFilterConfig? ResultFilter { get; private set; }

        public FilterSelectionModal(IReadOnlyList<string> categories, IFilterPresetRepository presetRepo)
        {
            InitializeComponent();

            _categories = categories;
            _presetRepo = presetRepo;

            // Categorías: primera opción vacía = todas
            CategoryCombo.Items.Add("(todas las categorías)");
            foreach (var cat in categories)
                CategoryCombo.Items.Add(cat);
            CategoryCombo.SelectedIndex = 0;

            RefreshPresets();
            AddConditionRow();
        }

        // ── Conditions ────────────────────────────────────────────────────────

        private void AddConditionRow(FilterCondition? existing = null)
        {
            var row = new ConditionRow(_categories, existing);
            row.RemoveRequested += () =>
            {
                _conditionRows.Remove(row);
                ConditionsPanel.Children.Remove(row.Container);
            };
            _conditionRows.Add(row);
            ConditionsPanel.Children.Add(row.Container);
        }

        private void AddCondition_Click(object sender, RoutedEventArgs e)
            => AddConditionRow();

        // ── Category change → refresh param suggestions ───────────────────────

        private void CategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Notify rows so they can update param suggestions if needed (future enhancement)
        }

        // ── Presets ───────────────────────────────────────────────────────────

        private void RefreshPresets()
        {
            PresetsCombo.Items.Clear();
            foreach (var p in _presetRepo.LoadAll())
                PresetsCombo.Items.Add(p);
        }

        private void PresetsCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void LoadPreset_Click(object sender, RoutedEventArgs e)
        {
            if (PresetsCombo.SelectedItem is not FilterPreset preset) return;

            // Apply preset to UI
            var catIndex = CategoryCombo.Items.IndexOf(preset.Filter.CategoryName);
            CategoryCombo.SelectedIndex = catIndex >= 0 ? catIndex : 0;

            AndRadio.IsChecked = preset.Filter.Logic == FilterLogic.And;
            OrRadio.IsChecked  = preset.Filter.Logic == FilterLogic.Or;

            _conditionRows.Clear();
            ConditionsPanel.Children.Clear();
            foreach (var cond in preset.Filter.Conditions)
                AddConditionRow(cond);

            if (_conditionRows.Count == 0) AddConditionRow();
        }

        private void SavePreset_Click(object sender, RoutedEventArgs e)
        {
            var filter = BuildFilter();
            if (filter == null) return;

            var name = BIMPills.UI.Shared.BimPillsDialog.Prompt(
                "Nombre del preset:", "Guardar preset", "Nuevo filtro", this);
            if (string.IsNullOrWhiteSpace(name)) return;

            var preset = new FilterPreset { Name = name!, Filter = filter };
            _presetRepo.Save(preset);
            RefreshPresets();
        }

        // ── Preview ───────────────────────────────────────────────────────────

        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            // Preview is count-only — actual filtering happens in Revit thread
            // We just build the filter and show feedback
            var filter = BuildFilter();
            if (filter == null) return;

            PreviewBadge.Visibility = Visibility.Visible;
            PreviewLabel.Text = $"Filtro listo — categoría: {(string.IsNullOrEmpty(filter.CategoryName) ? "todas" : filter.CategoryName)}, {filter.Conditions.Count} condición(es), lógica {filter.Logic}";
        }

        // ── Actions ───────────────────────────────────────────────────────────

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            ResultFilter = BuildFilter();
            if (ResultFilter == null) return;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

        // ── Build ─────────────────────────────────────────────────────────────

        private SelectionFilterConfig? BuildFilter()
        {
            var conditions = _conditionRows
                .Select(r => r.ToCondition())
                .Where(c => c != null && !string.IsNullOrEmpty(c.ParameterName))
                .ToList();

            var catText = CategoryCombo.SelectedItem?.ToString() ?? "";
            var category = catText == "(todas las categorías)" ? string.Empty : catText;

            return new SelectionFilterConfig
            {
                CategoryName = category,
                Logic        = OrRadio.IsChecked == true ? FilterLogic.Or : FilterLogic.And,
                Conditions   = conditions!
            };
        }
    }

    // ── ConditionRow helper ───────────────────────────────────────────────────

    internal sealed class ConditionRow
    {
        public UIElement Container { get; }
        public event Action? RemoveRequested;

        private readonly ComboBox _paramCombo;
        private readonly ComboBox _operatorCombo;
        private readonly TextBox  _valueBox;

        public ConditionRow(IReadOnlyList<string> categories, FilterCondition? existing = null)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });

            _paramCombo = new ComboBox { Height = 28, FontSize = 11, IsEditable = true };
            _paramCombo.Items.Add("(escribir parámetro)");
            _paramCombo.SelectedIndex = 0;
            Grid.SetColumn(_paramCombo, 0);

            _operatorCombo = new ComboBox { Height = 28, FontSize = 11 };
            foreach (var op in Enum.GetValues(typeof(FilterOperator)))
                _operatorCombo.Items.Add(OpLabel((FilterOperator)op));
            _operatorCombo.SelectedIndex = 0;
            Grid.SetColumn(_operatorCombo, 2);

            _valueBox = new TextBox { Height = 28, FontSize = 11, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(4, 0, 4, 0) };
            Grid.SetColumn(_valueBox, 4);

            var removeBtn = new Button
            {
                Content = "✕", Width = 28, Height = 28, FontSize = 11,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(229, 229, 234))
            };
            removeBtn.Click += (_, __) => RemoveRequested?.Invoke();
            Grid.SetColumn(removeBtn, 6);

            grid.Children.Add(_paramCombo);
            grid.Children.Add(_operatorCombo);
            grid.Children.Add(_valueBox);
            grid.Children.Add(removeBtn);

            Container = grid;

            if (existing != null)
            {
                _paramCombo.Text     = existing.ParameterName;
                _operatorCombo.SelectedIndex = (int)existing.Operator;
                _valueBox.Text       = existing.Value;
            }
        }

        public FilterCondition? ToCondition()
        {
            var param = _paramCombo.Text?.Trim();
            if (string.IsNullOrEmpty(param) || param == "(escribir parámetro)") return null;

            return new FilterCondition
            {
                ParameterName = param!,
                Operator      = (FilterOperator)_operatorCombo.SelectedIndex,
                Value         = _valueBox.Text?.Trim() ?? string.Empty
            };
        }

        private static string OpLabel(FilterOperator op) => op switch
        {
            FilterOperator.Equals      => "= Igual a",
            FilterOperator.NotEquals   => "≠ Distinto de",
            FilterOperator.Contains    => "∋ Contiene",
            FilterOperator.NotContains => "∌ No contiene",
            FilterOperator.StartsWith  => "⊂ Empieza con",
            FilterOperator.EndsWith    => "⊃ Termina con",
            FilterOperator.GreaterThan => "> Mayor que",
            FilterOperator.LessThan    => "< Menor que",
            FilterOperator.IsEmpty     => "∅ Está vacío",
            FilterOperator.IsNotEmpty  => "≠∅ No está vacío",
            _                          => op.ToString()
        };
    }
}
