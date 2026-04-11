using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BIMPills.UI.Shared
{
    /// <summary>
    /// Reusable branded dialog for BIM Pills.
    /// Replaces <c>System.Windows.MessageBox.Show</c> with a dialog that matches
    /// the BIM Pills visual identity (pill icon, BIM-CA colors, macOS-inspired
    /// typography). Supports Info, Success, Warning, Error, Question variants.
    ///
    /// Use the static helpers <see cref="Info"/>, <see cref="Success"/>,
    /// <see cref="Warning"/>, <see cref="Error"/>, <see cref="Confirm"/>.
    /// </summary>
    public partial class BimPillsDialog : Window
    {
        public enum DialogKind
        {
            Info,
            Success,
            Warning,
            Error,
            Question
        }

        public enum DialogResultKind
        {
            None,
            Ok,
            Yes,
            No,
            Cancel
        }

        public DialogResultKind ResultKind { get; private set; } = DialogResultKind.None;

        public BimPillsDialog()
        {
            InitializeComponent();
        }

        // ─────────────────────────────────────────────────────────────
        //  Public static API (drop-in replacement for MessageBox.Show)
        // ─────────────────────────────────────────────────────────────

        /// <summary>Show an informational dialog with a single OK button.</summary>
        public static void Info(string header, string message, string? detail = null, Window? owner = null)
            => ShowDialog(DialogKind.Info, header, message, detail, owner,
                          buttons: new[] { (ButtonRole.Primary, "Entendido", DialogResultKind.Ok) });

        /// <summary>Show a success dialog with a single OK button.</summary>
        public static void Success(string header, string message, string? detail = null, Window? owner = null)
            => ShowDialog(DialogKind.Success, header, message, detail, owner,
                          buttons: new[] { (ButtonRole.Primary, "Listo", DialogResultKind.Ok) });

        /// <summary>Show a warning dialog with a single OK button.</summary>
        public static void Warning(string header, string message, string? detail = null, Window? owner = null)
            => ShowDialog(DialogKind.Warning, header, message, detail, owner,
                          buttons: new[] { (ButtonRole.Primary, "Entendido", DialogResultKind.Ok) });

        /// <summary>Show an error dialog with a single OK button.</summary>
        public static void Error(string header, string message, string? detail = null, Window? owner = null)
            => ShowDialog(DialogKind.Error, header, message, detail, owner,
                          buttons: new[] { (ButtonRole.Primary, "Cerrar", DialogResultKind.Ok) });

        /// <summary>
        /// Show a confirmation dialog with Yes/No buttons.
        /// Returns true if user confirmed, false if cancelled or closed.
        /// </summary>
        public static bool Confirm(
            string header,
            string message,
            string? detail = null,
            Window? owner = null,
            string yesText = "Continuar",
            string noText = "Cancelar",
            DialogKind kind = DialogKind.Question)
        {
            var result = ShowDialog(kind, header, message, detail, owner, buttons: new[]
            {
                (ButtonRole.Secondary, noText,  DialogResultKind.No),
                (ButtonRole.Primary,   yesText, DialogResultKind.Yes)
            });
            return result == DialogResultKind.Yes;
        }

        /// <summary>
        /// Ask user a question with Yes/No/Cancel buttons.
        /// Returns Yes, No, or Cancel.
        /// </summary>
        public static DialogResultKind YesNoCancel(
            string header,
            string message,
            string? detail = null,
            Window? owner = null,
            string yesText = "Sí",
            string noText = "No",
            string cancelText = "Cancelar")
        {
            return ShowDialog(DialogKind.Question, header, message, detail, owner, buttons: new[]
            {
                (ButtonRole.Secondary, cancelText, DialogResultKind.Cancel),
                (ButtonRole.Secondary, noText,     DialogResultKind.No),
                (ButtonRole.Primary,   yesText,    DialogResultKind.Yes)
            });
        }

        // ─────────────────────────────────────────────────────────────
        //  Core
        // ─────────────────────────────────────────────────────────────

        private enum ButtonRole { Primary, Secondary }

        private static DialogResultKind ShowDialog(
            DialogKind kind,
            string header,
            string message,
            string? detail,
            Window? owner,
            (ButtonRole Role, string Label, DialogResultKind Result)[] buttons)
        {
            var dlg = new BimPillsDialog();

            // Resolve owner — fall back to the currently active window
            if (owner == null)
            {
                foreach (Window w in Application.Current?.Windows ?? new WindowCollection())
                {
                    if (w.IsActive) { owner = w; break; }
                }
            }
            if (owner != null)
            {
                try { dlg.Owner = owner; } catch { /* may fail if owner is not shown */ }
            }
            else
            {
                dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            dlg.ConfigureContent(kind, header, message, detail);
            dlg.ConfigureButtons(buttons);

            dlg.ShowDialog();
            return dlg.ResultKind;
        }

        private void ConfigureContent(DialogKind kind, string header, string message, string? detail)
        {
            HeaderText.Text = header;
            MessageText.Text = message;

            if (!string.IsNullOrWhiteSpace(detail))
            {
                DetailText.Text = detail;
                DetailBorder.Visibility = Visibility.Visible;
            }

            // Configure status badge (color + MDL2 icon) based on kind
            (string icon, Color color) = kind switch
            {
                DialogKind.Success  => ("\uE930", (Color)ColorConverter.ConvertFromString("#27AE60")),  // Checkmark
                DialogKind.Warning  => ("\uE7BA", (Color)ColorConverter.ConvertFromString("#F57F17")),  // Warning triangle
                DialogKind.Error    => ("\uE783", (Color)ColorConverter.ConvertFromString("#D32F2F")),  // Error X
                DialogKind.Question => ("\uE9CE", (Color)ColorConverter.ConvertFromString("#1565C0")),  // Question mark
                _                   => ("\uE946", (Color)ColorConverter.ConvertFromString("#1565C0")),  // Info "i"
            };

            StatusBadgeIcon.Text = icon;
            StatusBadge.Background = new SolidColorBrush(color);
            StatusBadge.Visibility = Visibility.Visible;

            // Window title reflects kind for accessibility
            WindowTitleText.Text = kind switch
            {
                DialogKind.Success  => "BIM Pills — Éxito",
                DialogKind.Warning  => "BIM Pills — Atención",
                DialogKind.Error    => "BIM Pills — Error",
                DialogKind.Question => "BIM Pills — Confirmar",
                _                   => "BIM Pills",
            };
            Title = WindowTitleText.Text;
        }

        private void ConfigureButtons((ButtonRole Role, string Label, DialogResultKind Result)[] buttons)
        {
            ButtonsPanel.Children.Clear();

            for (int i = 0; i < buttons.Length; i++)
            {
                var (role, label, result) = buttons[i];
                var btn = new Button
                {
                    Content = label,
                    MinWidth = 100,
                    Height = 34,
                    Padding = new Thickness(16, 0, 16, 0),
                    Margin = i == 0 ? new Thickness(0) : new Thickness(8, 0, 0, 0),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 13,
                    Cursor = Cursors.Hand,
                };

                if (role == ButtonRole.Primary)
                {
                    try { btn.Style = (Style)FindResource("PrimaryButton"); } catch { }
                    btn.IsDefault = true;
                }
                else
                {
                    try { btn.Style = (Style)FindResource("SecondaryButton"); } catch { }
                    if (result == DialogResultKind.Cancel || result == DialogResultKind.No)
                        btn.IsCancel = true;
                }

                btn.Click += (_, __) =>
                {
                    ResultKind = result;
                    Close();
                };

                ButtonsPanel.Children.Add(btn);
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Window chrome — drag + close
        // ─────────────────────────────────────────────────────────────

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { /* not draggable if not shown */ }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (ResultKind == DialogResultKind.None)
                ResultKind = DialogResultKind.Cancel;
            Close();
        }
    }
}
