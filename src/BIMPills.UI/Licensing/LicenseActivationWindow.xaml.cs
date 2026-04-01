using BIMPills.Core.Licensing;
using BIMPills.Infrastructure.DI;
using BIMPills.Infrastructure.Licensing;
using BIMPills.UI.Helpers;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace BIMPills.UI.Licensing
{
    public partial class LicenseActivationWindow : Window
    {
        private bool _apiKeyAlreadyConfigured;

        public bool LicenseActivated { get; private set; }

        public LicenseActivationWindow()
        {
            InitializeComponent();
            ConfigureApiKeyVisibility();
        }

        private void ConfigureApiKeyVisibility()
        {
            _apiKeyAlreadyConfigured = AirtableConfig.HasApiKey();

            if (_apiKeyAlreadyConfigured)
            {
                ApiKeyPanel.Visibility = Visibility.Collapsed;
                ApiKeyConfiguredBadge.Visibility = Visibility.Visible;
            }
            else
            {
                ApiKeyPanel.Visibility = Visibility.Visible;
                ApiKeyConfiguredBadge.Visibility = Visibility.Collapsed;
            }
        }

        private void ChangeApiKey_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _apiKeyAlreadyConfigured = false;
            TxtApiKey.Password = "";
            ApiKeyPanel.Visibility = Visibility.Visible;
            ApiKeyConfiguredBadge.Visibility = Visibility.Collapsed;
            TxtApiKey.Focus();
        }

        private async void Activate_Click(object sender, RoutedEventArgs e)
        {
            var licenseKey = TxtLicenseKey.Text.Trim();

            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                ShowStatus("Ingresa tu License Key.", isError: true);
                TxtLicenseKey.Focus();
                return;
            }

            string apiKey;
            if (_apiKeyAlreadyConfigured)
            {
                apiKey = AirtableConfig.LoadApiKey() ?? "";
            }
            else
            {
                apiKey = TxtApiKey.Password.Trim();
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    ShowStatus("Ingresa tu Airtable API Key.", isError: true);
                    TxtApiKey.Focus();
                    return;
                }
            }

            BtnActivate.IsEnabled = false;
            BtnActivate.Content = "Validando...";
            ShowStatus("Conectando con el servidor de licencias...", isError: false, isInfo: true);

            try
            {
                if (!_apiKeyAlreadyConfigured)
                    AirtableConfig.SaveApiKey(apiKey);

                var machineId = MachineIdProvider.GetMachineId();
                var cache = new LicenseCache();
                var service = new AirtableLicenseService(apiKey, cache);

                var success = await Task.Run(() => service.ActivateAsync(licenseKey, machineId));

                if (success)
                {
                    ServiceLocator.Register<ILicenseService>(service);
                    LicenseActivated = true;

                    var cached = service.GetCachedLicense();
                    var holderName = cached?.HolderName ?? "";
                    var plan = cached?.Plan ?? "";
                    var expiry = cached?.ExpiresAt?.ToString("yyyy-MM-dd") ?? "";

                    var msg = string.IsNullOrEmpty(holderName)
                        ? $"Licencia activada. Plan: {plan}"
                        : $"Bienvenido, {holderName}. Plan: {plan}";
                    if (!string.IsNullOrEmpty(expiry))
                        msg += $" · Vence: {expiry}";

                    ShowStatus(msg, isError: false, isSuccess: true);

                    await Task.Delay(1500);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    ShowStatus(
                        "No se pudo activar la licencia. Verifica que la License Key sea correcta " +
                        "y que no esté asignada a otra máquina.",
                        isError: true);
                    BtnActivate.IsEnabled = true;
                    BtnActivate.Content = "Activar licencia";
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Error de conexión: {ex.Message}", isError: true);
                BtnActivate.IsEnabled = true;
                BtnActivate.Content = "Activar licencia";
            }
        }

        private void ShowStatus(string message, bool isError = false, bool isSuccess = false, bool isInfo = false)
        {
            StatusText.Text = message;
            StatusBorder.Visibility = Visibility.Visible;

            if (isError)
            {
                StatusBorder.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xEB, 0xEB));
                StatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xCC, 0xCC));
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
            }
            else if (isSuccess)
            {
                StatusBorder.Background = new SolidColorBrush(Color.FromRgb(0xEB, 0xF5, 0xEB));
                StatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xC3, 0xE6, 0xC3));
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
            }
            else
            {
                StatusBorder.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xF0, 0xFE));
                StatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xBB, 0xCF, 0xF8));
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x56, 0xDB));
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private void Buy_Click(object sender, RoutedEventArgs e)
            => ProcessHelper.OpenUrl("https://bim-ca.com");
    }
}
