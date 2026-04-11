using System.Diagnostics;
using System.Windows;

namespace BIMPills.UI.Licensing
{
    /// <summary>
    /// Dialog shown when the license has expired AND the grace period is over.
    /// Result: true = user wants to renew (open activation window), false = cancel.
    /// </summary>
    public partial class LicenseExpiredWindow : Window
    {
        public LicenseExpiredWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Optional: name of the license holder (e.g. "Rodrigo Flores").
        /// When set, it is displayed below the title.
        /// </summary>
        public string? HolderName
        {
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    HolderNameText.Text       = value;
                    HolderNameText.Visibility = System.Windows.Visibility.Visible;
                }
            }
        }

        private void Renew_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Contact_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("https://bim-ca.com") { UseShellExecute = true }); }
            catch { /* ignore */ }
        }
    }
}
