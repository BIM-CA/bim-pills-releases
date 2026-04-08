using BIMPills.Core.Models;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BIMPills.UI.Ordering
{
    public partial class OrdenarWindow : Window
    {
        private readonly Func<string, IReadOnlyList<string>> _getCategoriesByType;
        private readonly Func<string, IReadOnlyList<string>>? _getParameters;
        private string       _selectedType         = "Modelo";
        private SequenceType _selectedSequenceType = SequenceType.Numeric;

        public OrderingConfig Config { get; private set; } = new OrderingConfig();

        public OrdenarWindow(
            Func<string, IReadOnlyList<string>> getCategoriesByType,
            Func<string, IReadOnlyList<string>>? getParametersForCategory = null)
        {
            InitializeComponent();
            BIMPills.UI.Shared.ThemeHelper.Apply(this);
            _getCategoriesByType = getCategoriesByType;
            _getParameters       = getParametersForCategory;

            RefreshCategories();
            UpdatePreview(null, null);
        }

        // ── Paso 1: Tipo ────────────────────────────────────────────────────────

        private void TypeChip_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border chip) return;
            _selectedType = chip.Tag as string ?? "Modelo";
            UpdateTypeChips();
            RefreshCategories();
        }

        private void UpdateTypeChips()
        {
            var accentBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x63, 0x37)); // #EF6337
            var greyBrush   = new SolidColorBrush(Color.FromRgb(0x86, 0x86, 0x8B));
            var greyBorder  = new SolidColorBrush(Color.FromRgb(0xD1, 0xD1, 0xD6));
            var lightOrange = new SolidColorBrush(Color.FromRgb(0xFF, 0xF0, 0xEB));

            bool isModelo = _selectedType == "Modelo";

            // Modelo chip
            ModeloChip.Background   = isModelo ? lightOrange : Brushes.White;
            ModeloChip.BorderBrush  = isModelo ? accentBrush : greyBorder;
            var mt = (TextBlock)ModeloChip.Child;
            mt.Foreground  = isModelo ? accentBrush : greyBrush;
            mt.FontWeight  = isModelo ? FontWeights.Medium : FontWeights.Normal;

            // Anotación chip
            AnotacionChip.Background  = isModelo ? Brushes.White : lightOrange;
            AnotacionChip.BorderBrush = isModelo ? greyBorder : accentBrush;
            AnotacionChipText.Foreground = isModelo ? greyBrush : accentBrush;
            AnotacionChipText.FontWeight = isModelo ? FontWeights.Normal : FontWeights.Medium;
        }

        // ── Paso 2: Categoría ───────────────────────────────────────────────────

        private void RefreshCategories()
        {
            CategoryCombo.Items.Clear();
            ParameterCombo.Items.Clear();

            var cats = _getCategoriesByType(_selectedType);
            foreach (var c in cats)
                CategoryCombo.Items.Add(c);

            if (CategoryCombo.Items.Count > 0)
                CategoryCombo.SelectedIndex = 0;

            UpdatePreview(null, null);
        }

        private void CategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_getParameters == null) return;
            var cat = CategoryCombo.SelectedItem as string;
            if (string.IsNullOrEmpty(cat)) return;

            ParameterCombo.Items.Clear();
            var parameters = _getParameters(cat);
            foreach (var p in parameters)
                ParameterCombo.Items.Add(p);

            if (ParameterCombo.Items.Count > 0)
                ParameterCombo.SelectedIndex = 0;

            UpdatePreview(null, null);
        }

        // ── Paso 4: Tipo de secuencia ────────────────────────────────────────────

        private void SeqTypeChip_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not Border chip) return;
            _selectedSequenceType = chip.Tag as string == "Alphabetic"
                ? SequenceType.Alphabetic
                : SequenceType.Numeric;
            UpdateSeqTypeChips();
            UpdatePreview(null, null);
        }

        private void UpdateSeqTypeChips()
        {
            var accentBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x63, 0x37));
            var greyBrush   = new SolidColorBrush(Color.FromRgb(0x86, 0x86, 0x8B));
            var greyBorder  = new SolidColorBrush(Color.FromRgb(0xD1, 0xD1, 0xD6));
            var lightOrange = new SolidColorBrush(Color.FromRgb(0xFF, 0xF0, 0xEB));

            bool isNumeric = _selectedSequenceType == SequenceType.Numeric;

            NumericChip.Background  = isNumeric ? lightOrange : Brushes.White;
            NumericChip.BorderBrush = isNumeric ? accentBrush : greyBorder;
            NumericChipBold.Foreground  = isNumeric ? accentBrush : greyBrush;
            NumericChipText.Foreground  = isNumeric ? accentBrush : greyBrush;
            NumericChipText.FontWeight  = isNumeric ? FontWeights.Medium : FontWeights.Normal;

            AlphabeticChip.Background  = isNumeric ? Brushes.White : lightOrange;
            AlphabeticChip.BorderBrush = isNumeric ? greyBorder : accentBrush;
            AlphabeticChipBold.Foreground  = isNumeric ? greyBrush : accentBrush;
            AlphabeticChipText.Foreground  = isNumeric ? greyBrush : accentBrush;
            AlphabeticChipText.FontWeight  = isNumeric ? FontWeights.Normal : FontWeights.Medium;
        }

        // ── Paso 4: Vista previa ────────────────────────────────────────────────

        private void UpdatePreview(object? sender, object? e)
        {
            if (Pill1 == null) return;

            var prefix = PrefixBox?.Text ?? "";
            var suffix = SuffixBox?.Text ?? "";
            int step   = int.TryParse(StepBox?.Text, out int st) ? st : 1;

            if (_selectedSequenceType == SequenceType.Alphabetic)
            {
                int startIdx = OrderingConfig.LettersToIndex(StartBox?.Text ?? "A");
                Pill1.Text = $"{prefix}{OrderingConfig.IndexToLetters(startIdx)}{suffix}";
                Pill2.Text = $"{prefix}{OrderingConfig.IndexToLetters(startIdx + step)}{suffix}";
                Pill3.Text = $"{prefix}{OrderingConfig.IndexToLetters(startIdx + step * 2)}{suffix}";
                Pill4.Text = $"{prefix}{OrderingConfig.IndexToLetters(startIdx + step * 3)}{suffix}";
            }
            else
            {
                int start = int.TryParse(StartBox?.Text, out int s) ? s : 1;
                Pill1.Text = $"{prefix}{start}{suffix}";
                Pill2.Text = $"{prefix}{start + step}{suffix}";
                Pill3.Text = $"{prefix}{start + step * 2}{suffix}";
                Pill4.Text = $"{prefix}{start + step * 3}{suffix}";
            }
        }

        // ── Iniciar ─────────────────────────────────────────────────────────────

        private void Iniciar_Click(object sender, RoutedEventArgs e)
        {
            var categoryName  = CategoryCombo.SelectedItem as string ?? CategoryCombo.Text;
            var parameterName = ParameterCombo.SelectedItem as string ?? ParameterCombo.Text;

            if (string.IsNullOrWhiteSpace(categoryName))
            {
                MessageBox.Show("Selecciona una categoría.", "BIMPills — Ordenar");
                return;
            }
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                MessageBox.Show("Selecciona o escribe el nombre del parámetro.", "BIMPills — Ordenar");
                return;
            }
            int start;
            if (_selectedSequenceType == SequenceType.Alphabetic)
            {
                var startText = StartBox.Text?.Trim() ?? "";
                if (string.IsNullOrEmpty(startText) ||
                    !System.Text.RegularExpressions.Regex.IsMatch(startText, @"^[A-Za-z]+$"))
                {
                    MessageBox.Show("Para modo alfabético, el inicio debe ser una letra (A, B, Z, AA…).",
                        "BIMPills — Ordenar");
                    return;
                }
                start = OrderingConfig.LettersToIndex(startText.ToUpperInvariant());
            }
            else
            {
                if (!int.TryParse(StartBox.Text, out start))
                {
                    MessageBox.Show("El valor de inicio debe ser un número entero.", "BIMPills — Ordenar");
                    return;
                }
            }

            if (!int.TryParse(StepBox.Text, out int step) || step == 0)
            {
                MessageBox.Show("El paso debe ser un número entero distinto de cero.", "BIMPills — Ordenar");
                return;
            }

            Config = new OrderingConfig
            {
                CategoryName  = categoryName,
                ParameterName = parameterName,
                Prefix        = PrefixBox.Text ?? "",
                StartValue    = start,
                Step          = step,
                Suffix        = SuffixBox.Text ?? "",
                SequenceType  = _selectedSequenceType
            };

            try { DialogResult = true; } catch (InvalidOperationException) { }
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            try { DialogResult = false; } catch (InvalidOperationException) { }
            Close();
        }
    }
}
