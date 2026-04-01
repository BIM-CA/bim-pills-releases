using BIMPills.Core.Licensing;
using BIMPills.Infrastructure.DI;
using BIMPills.Infrastructure.Licensing;
using BIMPills.UI.Helpers;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace BIMPills.UI.Licensing
{
    public partial class LicenseActivationWindow : Window
    {
        private readonly string? _preloadedApiKey;

        public bool LicenseActivated { get; private set; }

        public LicenseActivationWindow()
        {
            InitializeComponent();

            // If an API key is already stored, pre-fill it
            _preloadedApiKey = AirtableConfig.LoadApiKey();
            if (_preloadedApiKey != null)
            {
                TxtApiKey.Password = _preloadedApiKey;
            }
        }

        private async void Activate_Click(object sender, RoutedEventArgs e)
        {
            var licenseKey = TxtLicenseKey.Text.Trim();
            var apiKey = TxtApiKey.Password.Trim();

            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                StatusText.Text = "Ingresa tu License Key.";
                StatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
                return;
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                StatusText.Text = "Ingresa tu Airtable API Key.";
                StatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
                return;
            }

            BtnActivate.IsEnabled = false;
            StatusText.Text = "Validando licencia...";
            StatusText.Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");

            try
            {
                // Save API key first (encrypted with DPAPI)
                AirtableConfig.SaveApiKey(apiKey);

                var machineId = MachineIdProvider.GetMachineId();
                var cache = new LicenseCache();
                var service = new AirtableLicenseService(apiKey, cache);

                var success = await Task.Run(() => service.ActivateAsync(licenseKey, machineId));

                if (success)
                {
                    // Re-register the service in ServiceLocator with the real API key
                    ServiceLocator.Register<ILicenseService>(service);

                    StatusText.Text = "Licencia activada correctamente.";
                    StatusText.Foreground = System.Windows.Media.Brushes.Green;
                    LicenseActivated = true;

                    await Task.Delay(1000);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    StatusText.Text = "No se pudo activar. Verifica la key o que no est\u00E9 asignada a otra m\u00E1quina.";
                    StatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
                    BtnActivate.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
                StatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
                BtnActivate.IsEnabled = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private void Buy_Click(object sender, RoutedEventArgs e)
            => ProcessHelper.OpenUrl("https://bim-ca.com");
    }
}
