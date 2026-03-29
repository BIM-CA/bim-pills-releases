using BIMPills.Core.Models;
using BIMPills.Core.Services;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace BIMPills.UI.CustomDimensionSchemes
{
    public partial class EditDimensionSchemeWindow : Window
    {
        private readonly CustomDimensionScheme _scheme;
        private readonly bool _isNew;

        public EditDimensionSchemeWindow(CustomDimensionScheme scheme, bool isNew = false)
        {
            _scheme = scheme;
            _isNew = isNew;
            InitializeComponent();
            LoadScheme();
        }

        private void LoadScheme()
        {
            NameTextBox.Text = _scheme.Name ?? "";
            DescriptionTextBox.Text = _scheme.Description ?? "";

            // Load element type checkboxes
            LoadElementTypeCheckboxes();

            // Load rules
            RulesList.ItemsSource = _scheme.Rules;
        }

        private void LoadElementTypeCheckboxes()
        {
            var elementTypes = new[]
            {
                DimensionSchemeElementTypes.Door,
                DimensionSchemeElementTypes.Window,
                DimensionSchemeElementTypes.Wall,
                DimensionSchemeElementTypes.Floor,
                DimensionSchemeElementTypes.Roof,
                DimensionSchemeElementTypes.Room
            };

            ElementTypesPanel.Children.Clear();

            foreach (var type in elementTypes)
            {
                var checkBox = new CheckBox
                {
                    Content = type,
                    IsChecked = _scheme.ApplicableElementTypes.Contains(type),
                    Margin = new Thickness(0, 0, 0, 6),
                    FontSize = 12,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x21, 0x2B, 0x37))
                };

                checkBox.Checked += (s, e) =>
                {
                    if (!_scheme.ApplicableElementTypes.Contains(type))
                        _scheme.ApplicableElementTypes.Add(type);
                };

                checkBox.Unchecked += (s, e) =>
                {
                    _scheme.ApplicableElementTypes.Remove(type);
                };

                ElementTypesPanel.Children.Add(checkBox);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show("El nombre del esquema no puede estar vacío.", "BIMPills — Validación");
                return;
            }

            _scheme.Name = NameTextBox.Text.Trim();
            _scheme.Description = DescriptionTextBox.Text.Trim();

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
