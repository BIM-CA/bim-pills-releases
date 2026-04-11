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
        public bool LicenseActivated { get; private set; }

        public LicenseActivationWindow()
        {
            InitializeComponent();
            BIMPills.UI.Shared.ThemeHelper.Apply(this);
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

            BtnActivate.IsEnabled = false;
            BtnActivate.Content = "Validando...";
            ShowStatus("Conectando con el servidor de licencias...", isInfo: true);

            try
            {
                var machineId = MachineIdProvider.GetMachineId();
                var cache = new LicenseCache();
                var service = new AirtableLicenseService(cache);

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

                    ShowStatus(msg, isSuccess: true);

                    await Task.Delay(1500);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    ShowStatus(
                        "No se pudo activar la licencia. Verifica que la License Key sea correcta, " +
                        "que no esté vencida y que no esté asignada a otra máquina.",
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
            => ProcessHelper.OpenUrl("https://app.recurrente.com/s/prototype-s-a?category=ca_heoljscz");
    }
}
