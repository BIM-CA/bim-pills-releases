using BIMPills.Core.Updates;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

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

            RenderMarkdown(ReleaseNotesText, update.ReleaseNotes);

            if (string.IsNullOrEmpty(update.InstallerDownloadUrl))
                UpdateBtn.Content = "Ver en GitHub";
        }

        /// <summary>
        /// Parsea el markdown del changelog y lo renderiza como inlines WPF con
        /// formato real: H1/H2 en negrita y mayor tamaño, bullets con •, **bold**.
        /// </summary>
        private static void RenderMarkdown(System.Windows.Controls.TextBlock tb, string md)
        {
            tb.Inlines.Clear();

            if (string.IsNullOrWhiteSpace(md))
            {
                tb.Inlines.Add(new Run("Sin notas de versión disponibles."));
                return;
            }

            var lines     = md.Replace("\r\n", "\n").Split('\n');
            bool first    = true;
            bool lastEmpty = false;

            foreach (var rawLine in lines)
            {
                var line  = rawLine.TrimEnd();
                bool empty = string.IsNullOrWhiteSpace(line);

                if (empty) { lastEmpty = true; continue; }

                // Spacing between blocks
                if (!first)
                {
                    tb.Inlines.Add(new LineBreak());
                    if (lastEmpty) tb.Inlines.Add(new LineBreak()); // blank line between sections
                }
                lastEmpty = false;
                first     = false;

                if (line.StartsWith("# "))
                {
                    tb.Inlines.Add(new Run(line.Substring(2))
                    {
                        FontWeight = FontWeights.Bold,
                        FontSize   = 14,
                        Foreground = new SolidColorBrush(Color.FromRgb(33, 43, 55))
                    });
                }
                else if (line.StartsWith("## "))
                {
                    tb.Inlines.Add(new Run(line.Substring(3))
                    {
                        FontWeight = FontWeights.SemiBold,
                        FontSize   = 13,
                        Foreground = new SolidColorBrush(Color.FromRgb(33, 43, 55))
                    });
                }
                else if (line.StartsWith("- "))
                {
                    tb.Inlines.Add(new Run("• ") { Foreground = new SolidColorBrush(Color.FromRgb(0, 122, 255)) });
                    AddInlineRuns(tb, line.Substring(2));
                }
                else
                {
                    AddInlineRuns(tb, line);
                }
            }
        }

        /// <summary>
        /// Añade texto con soporte para **negrita** inline.
        /// </summary>
        private static void AddInlineRuns(System.Windows.Controls.TextBlock tb, string text)
        {
            var parts = Regex.Split(text, @"\*\*(.+?)\*\*");
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i])) continue;
                tb.Inlines.Add(i % 2 == 1
                    ? new Run(parts[i]) { FontWeight = FontWeights.SemiBold }
                    : new Run(parts[i]));
            }
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
