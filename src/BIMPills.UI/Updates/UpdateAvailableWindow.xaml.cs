using BIMPills.Core.Updates;
using System;
using System.Text.RegularExpressions;
using System.Windows;

namespace BIMPills.UI.Updates
{
    public partial class UpdateAvailableWindow : Window
    {
        private readonly UpdateInfo _update;
        private readonly Func<UpdateInfo, System.Threading.Tasks.Task<string?>>? _downloadCallback;

        public bool UserAccepted { get; private set; }

        public UpdateAvailableWindow(
            UpdateInfo update,
            string currentVersion,
            Func<UpdateInfo, System.Threading.Tasks.Task<string?>>? downloadCallback = null)
        {
            InitializeComponent();
            _update           = update;
            _downloadCallback = downloadCallback;

            CurrentVersionText.Text = currentVersion;
            NewVersionText.Text     = update.DisplayVersion;
            SubtitleText.Text       = $"BIM Pills {update.DisplayVersion} está lista para instalar";

            ReleaseNotesText.Text = string.IsNullOrWhiteSpace(update.ReleaseNotes)
                ? "Sin notas de versión disponibles."
                : CleanMarkdown(update.ReleaseNotes);

            if (string.IsNullOrEmpty(update.InstallerDownloadUrl))
                UpdateBtn.Content = "Ver en GitHub";
        }

        private static string CleanMarkdown(string md)
        {
            // Remove heading markers (##, ###, etc.)
            md = Regex.Replace(md, @"^#{1,6}\s+", string.Empty, RegexOptions.Multiline);
            // Remove bold/italic markers (**text** → text)
            md = Regex.Replace(md, @"\*\*(.+?)\*\*", "$1");
            md = Regex.Replace(md, @"\*(.+?)\*", "$1");
            // Convert - bullets to •
            md = Regex.Replace(md, @"^- ", "• ", RegexOptions.Multiline);
            // Collapse triple+ newlines to double
            md = Regex.Replace(md, @"\n{3,}", "\n\n");
            return md.Trim();
        }

        private async void Update_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_update.InstallerDownloadUrl))
            {
                UserAccepted = true;
                Close();
                return;
            }

            // Mostrar overlay de descarga
            MainContent.IsEnabled = false;
            DownloadOverlay.Visibility = Visibility.Visible;
            OverlayStatusText.Text = "Descargando actualización…";
            OverlayProgress.IsIndeterminate = true;

            try
            {
                if (_downloadCallback != null)
                {
                    var installerPath = await _downloadCallback(_update);
                    if (!IsLoaded) return; // ventana cerrada durante descarga
                    if (!string.IsNullOrEmpty(installerPath))
                    {
                        OverlayProgress.IsIndeterminate = false;
                        OverlayProgress.Value = 100;
                        OverlayStatusText.Text    = "Descarga completada.";
                        OverlaySubText.Text       = "La actualización se instalará al cerrar Revit.";
                        OverlaySubText.Visibility = Visibility.Visible;
                        OverlayIconText.Text      = "✓";
                        OverlayWarning.Visibility = Visibility.Collapsed;
                        OverlayCloseBtn.Visibility = Visibility.Visible;
                        UserAccepted = true;
                        return;
                    }
                }

                if (!IsLoaded) return;
                ShowDownloadError("No se pudo descargar. Intenta más tarde.");
            }
            catch
            {
                if (!IsLoaded) return;
                ShowDownloadError("Error al descargar la actualización.");
            }
        }

        private void ShowDownloadError(string message)
        {
            DownloadOverlay.Visibility = Visibility.Collapsed;
            MainContent.IsEnabled      = true;
            StatusText.Text            = message;
            StatusText.Visibility      = Visibility.Visible;
            UpdateBtn.IsEnabled        = true;
            RemindLaterBtn.IsEnabled   = true;
        }

        private void OverlayClose_Click(object sender, RoutedEventArgs e) => Close();

        private void RemindLater_Click(object sender, RoutedEventArgs e)
        {
            UserAccepted = false;
            Close();
        }
    }
}
