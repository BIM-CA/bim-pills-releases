using System.Windows;

namespace BIMPills.UI.CustomDimensionSchemes
{
    public partial class CustomDimensionSchemesWindow : Window
    {
        public CustomDimensionSchemesWindow()
        {
            InitializeComponent();
            BIMPills.UI.Shared.ThemeHelper.Apply(this);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
