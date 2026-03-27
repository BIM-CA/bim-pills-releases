using System.Windows;

namespace BIMPills.UI.Documentacion
{
    public partial class DocumentacionWindow : Window
    {
        public DocumentacionWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
