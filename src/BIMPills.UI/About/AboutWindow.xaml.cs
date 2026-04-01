using BIMPills.Core.About;
using BIMPills.Core.Licensing;
using BIMPills.Infrastructure.DI;
using BIMPills.UI.Helpers;
using BIMPills.UI.Licensing;
using System.Windows;
using System.Windows.Input;

namespace BIMPills.UI.About
{
    public partial class AboutWindow : Window
    {
        private readonly AboutInfo _info;

        public AboutWindow(AboutInfo info)
        {
            _info = info;
            InitializeComponent();
            Populate();
            PopulateLicense();
        }

        private void Populate()
        {
            DescriptionText.Text = _info.Description;
            VersionText.Text = _info.Version;
            DeveloperText.Text = _info.Developer;
            CompanyText.Text = _info.Company;
            WebsiteText.Text = _info.Website;
            SupportEmailText.Text = _info.SupportEmail;
            CopyrightText.Text = _info.Copyright;
        }

        private void PopulateLicense()
        {
            if (!ServiceLocator.IsRegistered<ILicenseService>())
            {
                LicensePlanText.Text = "No configurada";
                LicenseStatusText.Text = "Sin licencia";
                BtnActivateLicense.Visibility = Visibility.Visible;
                return;
            }

            var service = ServiceLocator.Get<ILicenseService>();
            var cached = service.GetCachedLicense();

            if (cached == null)
            {
                LicensePlanText.Text = "No activada";
                LicenseStatusText.Text = "Sin licencia";
                BtnActivateLicense.Visibility = Visibility.Visible;
                return;
            }

            LicensePlanText.Text = cached.Plan;
            _info.LicensePlan = cached.Plan;
            _info.LicenseHolder = cached.HolderName;
            _info.LicenseExpiry = cached.ExpiresAt;

            if (service.IsValid && !service.IsGracePeriod)
            {
                var expiryLabel = cached.ExpiresAt.HasValue
                    ? $"Activa hasta {cached.ExpiresAt.Value:yyyy-MM-dd}"
                    : "Activa";
                LicenseStatusText.Text = expiryLabel;
                LicenseStatusText.Foreground = System.Windows.Media.Brushes.Green;
                _info.LicenseStatus = expiryLabel;
            }
            else if (service.IsGracePeriod)
            {
                LicenseStatusText.Text = "Periodo de gracia";
                LicenseStatusText.Foreground = System.Windows.Media.Brushes.Orange;
                BtnActivateLicense.Visibility = Visibility.Visible;
                BtnActivateLicense.Content = "Renovar licencia";
                _info.LicenseStatus = "Periodo de gracia";
            }
            else
            {
                LicenseStatusText.Text = "Expirada";
                LicenseStatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
                BtnActivateLicense.Visibility = Visibility.Visible;
                _info.LicenseStatus = "Expirada";
            }
        }

        private void ActivateLicense_Click(object sender, RoutedEventArgs e)
        {
            var window = new LicenseActivationWindow { Owner = this };
            if (window.ShowDialog() == true)
            {
                PopulateLicense(); // Refresh license display
            }
        }

        private void Website_Click(object sender, MouseButtonEventArgs e)
            => ProcessHelper.OpenUrl(_info.Website);

        private void SupportEmail_Click(object sender, MouseButtonEventArgs e)
            => ProcessHelper.OpenUrl($"mailto:{_info.SupportEmail}");

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
