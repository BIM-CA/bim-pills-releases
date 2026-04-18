using BIMPills.Core.About;
using BIMPills.Core.Licensing;
using BIMPills.Infrastructure.DI;
using BIMPills.UI.Helpers;
using BIMPills.UI.Licensing;
using BIMPills.UI.Shared;
using System;
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
            BIMPills.UI.Shared.ThemeHelper.Apply(this);
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
            // Reset
            LicenseHolderText.Visibility = Visibility.Collapsed;
            LicenseExpiryText.Visibility  = Visibility.Collapsed;
            PanelGetLicense.Visibility    = Visibility.Collapsed;
            LicenseButtonSeparator.Visibility = Visibility.Collapsed;
            BtnActivateLicense.Visibility  = Visibility.Collapsed;
            BtnRenewLicense.Visibility     = Visibility.Collapsed;
            BtnDeactivateLicense.Visibility = Visibility.Collapsed;
            BtnDeactivateLicense.IsEnabled  = true;
            BtnDeactivateLicense.Content    = "Desactivar equipo";

            if (!ServiceLocator.IsRegistered<ILicenseService>() ||
                !ServiceLocator.Get<ILicenseService>().IsActivated)
            {
                SetBadge("Sin licencia", "#86868B", "#F0F0F0");
                LicensePlanText.Text = "";
                PanelGetLicense.Visibility = Visibility.Visible;
                LicenseButtonSeparator.Visibility = Visibility.Visible;
                BtnActivateLicense.Visibility = Visibility.Visible;
                return;
            }

            var service = ServiceLocator.Get<ILicenseService>();
            var cached  = service.GetCachedLicense()!;

            _info.LicensePlan   = cached.Plan;
            _info.LicenseHolder = cached.HolderName;
            _info.LicenseExpiry = cached.ExpiresAt;

            if (!string.IsNullOrEmpty(cached.HolderName))
            {
                LicenseHolderText.Text       = cached.HolderName;
                LicenseHolderText.Visibility = Visibility.Visible;
            }

            LicensePlanText.Text = cached.Plan;

            LicenseButtonSeparator.Visibility = Visibility.Visible;

            if (service.IsValid && !service.IsGracePeriod)
            {
                SetBadge("Activa", "#27AE60", "#EBF8F1");
                if (cached.ExpiresAt.HasValue)
                {
                    LicenseExpiryText.Text       = $"Vence el {cached.ExpiresAt.Value:dd MMM yyyy}";
                    LicenseExpiryText.Foreground  = System.Windows.Media.Brushes.Gray;
                    LicenseExpiryText.Visibility  = Visibility.Visible;
                }
                BtnDeactivateLicense.Visibility = Visibility.Visible;
                _info.LicenseStatus = "Activa";
            }
            else if (service.IsGracePeriod)
            {
                SetBadge("Periodo de gracia", "#E67E22", "#FEF3E2");
                if (cached.ExpiresAt.HasValue)
                {
                    var daysLeft = Math.Max(0, 7 - (int)(DateTime.UtcNow - cached.ExpiresAt.Value).TotalDays);
                    LicenseExpiryText.Text       = $"Venció hace {(int)(DateTime.UtcNow - cached.ExpiresAt.Value).TotalDays} día(s) · {daysLeft} día(s) restantes";
                    LicenseExpiryText.Foreground  = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0xE6, 0x7E, 0x22));
                    LicenseExpiryText.Visibility  = Visibility.Visible;
                }
                BtnRenewLicense.Visibility      = Visibility.Visible;
                BtnDeactivateLicense.Visibility = Visibility.Visible;
                _info.LicenseStatus = "Periodo de gracia";
            }
            else
            {
                SetBadge("Expirada", "#C0392B", "#FDECEA");
                BtnActivateLicense.Visibility   = Visibility.Visible;
                _info.LicenseStatus = "Expirada";
            }
        }

        private void SetBadge(string text, string fg, string bg)
        {
            LicenseStatusText.Text     = text;
            LicenseStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(fg));
            LicenseStatusBadge.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(bg));
        }

        private void ActivateLicense_Click(object sender, RoutedEventArgs e)
        {
            var window = new LicenseActivationWindow { Owner = this };
            if (window.ShowDialog() == true)
                PopulateLicense();
        }

        private void RenewLicense_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => ProcessHelper.OpenUrl("https://app.recurrente.com/s/prototype-s-a?category=ca_heoljscz");

        private async void DeactivateLicense_Click(object sender, RoutedEventArgs e)
        {
            var confirm = BimPillsDialog.Confirm(
                "Desactivar licencia",
                "¿Desactivar BIMPills en este equipo?\n\nPodrás volver a activar la licencia en otro equipo o en este mismo.");

            if (!confirm) return;

            BtnDeactivateLicense.IsEnabled = false;
            BtnDeactivateLicense.Content = "Desactivando...";

            var service = ServiceLocator.Get<ILicenseService>();
            var ok = await service.DeactivateAsync();

            if (ok)
            {
                PopulateLicense();
                BimPillsDialog.Info(
                    "Desactivado",
                    "Licencia desactivada. Ya puedes activarla en otro equipo.");
            }
            else
            {
                BtnDeactivateLicense.IsEnabled = true;
                BtnDeactivateLicense.Content = "Desactivar equipo";
                BimPillsDialog.Warning(
                    "Error",
                    "No se pudo desactivar la licencia remotamente. Intenta de nuevo.");
            }
        }

        private void GetLicense_Click(object sender, MouseButtonEventArgs e)
            => ProcessHelper.OpenUrl("https://bim-ca.com");

        private void Website_Click(object sender, MouseButtonEventArgs e)
            => ProcessHelper.OpenUrl(_info.Website);

        private void SupportEmail_Click(object sender, MouseButtonEventArgs e)
            => ProcessHelper.OpenUrl($"mailto:{_info.SupportEmail}");

        private void Feedback_Click(object sender, MouseButtonEventArgs e)
            => ProcessHelper.OpenUrl("https://bimca.notion.site/33bd89d548c2802a83d6f01c013c6e41?pvs=105");

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
