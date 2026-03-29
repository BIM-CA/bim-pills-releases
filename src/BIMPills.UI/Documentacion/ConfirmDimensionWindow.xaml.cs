using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BIMPills.UI.Documentacion
{
    public partial class ConfirmDimensionWindow : Window
    {
        public ConfirmDimensionWindow(
            string schemeName,
            string viewName,
            int elementCount,
            string elementLabel,
            string dimTypeName,
            int offsetMm)
        {
            InitializeComponent();

            SubtitleText.Text = $"Se crearán cotas en \"{viewName}\".";

            AddRow("Esquema",       schemeName);
            AddRow("Vista",         viewName);
            AddRow("Elementos",     $"{elementCount} {elementLabel}");
            AddRow("Tipo de cota",  dimTypeName);
            AddRow("Desfase",       $"{offsetMm} mm");
        }

        private void AddRow(string label, string value)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lbl = new TextBlock
            {
                Text = label,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x73)),
                VerticalAlignment = VerticalAlignment.Top
            };
            var val = new TextBlock
            {
                Text = value,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x1D, 0x1D, 0x1F)),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(lbl, 0);
            Grid.SetColumn(val, 1);
            row.Children.Add(lbl);
            row.Children.Add(val);

            // Remove bottom margin from last row after all are added
            DetailsPanel.Children.Add(row);
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
