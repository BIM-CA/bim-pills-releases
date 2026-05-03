using System;
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

        /// <summary>
        /// Show a success dialog with an "Abrir carpeta" button that opens Windows Explorer
        /// without closing the dialog, plus a "Listo" button to close it.
        /// </summary>
        public static void SuccessWithFolder(string header, string message, string? detail, string folderPath, Window? owner = null)
        {
            var dlg = new BimPillsDialog();

            if (owner == null)
                foreach (Window w in Application.Current?.Windows ?? new WindowCollection())
                    if (w.IsActive) { owner = w; break; }
            if (owner != null)
                try { dlg.Owner = owner; } catch { }
            else
            {
                RevitOwnerHelper.SetRevitAsOwner(dlg);
                if (RevitOwnerHelper.CurrentRevitHandle == IntPtr.Zero)
                    dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            dlg.ConfigureContent(DialogKind.Success, header, message, detail);
            dlg.ButtonsPanel.Children.Clear();

            var openBtn = dlg.BuildButton("Abrir carpeta", ButtonRole.Secondary, margin: new Thickness(0));
            openBtn.Click += (_, __) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName        = "explorer.exe",
                        Arguments       = folderPath,
                        UseShellExecute = true,
                    });
                }
                catch { }
            };
            dlg.ButtonsPanel.Children.Add(openBtn);

            var doneBtn = dlg.BuildButton("Listo", ButtonRole.Primary, margin: new Thickness(8, 0, 0, 0));
            doneBtn.IsDefault = true;
            doneBtn.Click += (_, __) => { dlg.ResultKind = DialogResultKind.Ok; dlg.Close(); };
            dlg.ButtonsPanel.Children.Add(doneBtn);

            dlg.ShowDialog();
        }

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

            // Resolve owner — fall back to the currently active WPF window,
            // then fall back to RevitOwnerHelper (anchors to Revit's main window
            // so dialogs stay on the correct monitor in multi-monitor setups).
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
                // Use Revit's main window handle as Win32 owner so the dialog
                // appears on the same monitor as Revit, even after all WPF
                // windows have been closed (e.g. end-of-batch notification).
                RevitOwnerHelper.SetRevitAsOwner(dlg);
                if (RevitOwnerHelper.CurrentRevitHandle == IntPtr.Zero)
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

            // Los íconos ya traen su propio color/forma. Warning lleva fondo blanco
            // ajustado al interior del borde azul del ícono.
            StatusBadgeIcon.Slug = kind switch
            {
                DialogKind.Success  => "accept",
                DialogKind.Warning  => "reportwarning",
                DialogKind.Error    => "delete-x",
                DialogKind.Question => "statuscircleinfo",
                _                   => "statuscircleinfo",
            };
            StatusBadge.Background = kind == DialogKind.Warning ? Brushes.White : Brushes.Transparent;
            StatusBadgeContainer.Visibility = Visibility.Visible;

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

        private Button BuildButton(string label, ButtonRole role, Thickness margin)
        {
            var btn = new Button
            {
                Content     = label,
                MinWidth    = 100,
                Height      = 34,
                Padding     = new Thickness(16, 0, 16, 0),
                Margin      = margin,
                FontFamily  = new FontFamily("Segoe UI"),
                FontSize    = 13,
                Cursor      = Cursors.Hand,
            };
            if (role == ButtonRole.Primary)
                try { btn.Style = (Style)FindResource("PrimaryButton"); } catch { }
            else
                try { btn.Style = (Style)FindResource("SecondaryButton"); } catch { }
            return btn;
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
        //  Input prompt (replaces Microsoft.VisualBasic.Interaction.InputBox)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Shows a simple text-input dialog. Returns the entered text or
        /// <c>null</c> if the user cancelled.
        /// </summary>
        public static string? Prompt(
            string prompt,
            string title        = "BIM Pills",
            string defaultValue = "",
            Window? owner       = null)
        {
            // ── build window in code ──────────────────────────────────
            var win = new Window
            {
                Title                 = title,
                Width                 = 380,
                SizeToContent         = SizeToContent.Height,
                ResizeMode            = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                FontFamily            = new FontFamily("Segoe UI"),
                Background            = Brushes.White,
                BorderThickness       = new Thickness(1),
                BorderBrush           = new SolidColorBrush(Color.FromRgb(220, 220, 224)),
                AllowsTransparency    = false,
                WindowStyle           = WindowStyle.SingleBorderWindow,
            };

            // resolve owner
            if (owner == null)
                foreach (Window w in Application.Current?.Windows ?? new WindowCollection())
                    if (w.IsActive) { owner = w; break; }
            if (owner != null)
                try { win.Owner = owner; } catch { }

            // ── layout ───────────────────────────────────────────────
            var root = new StackPanel { Margin = new Thickness(20) };

            var promptTb = new TextBlock
            {
                Text         = prompt,
                FontSize     = 13,
                Foreground   = new SolidColorBrush(Color.FromRgb(51, 51, 51)),
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 0, 0, 10),
            };
            root.Children.Add(promptTb);

            var input = new TextBox
            {
                Text              = defaultValue,
                FontSize          = 13,
                Padding           = new Thickness(8, 6, 8, 6),
                BorderBrush       = new SolidColorBrush(Color.FromRgb(180, 180, 188)),
                BorderThickness   = new Thickness(1),
                Margin            = new Thickness(0, 0, 0, 16),
                SelectionStart    = 0,
                SelectionLength   = defaultValue.Length,
            };
            root.Children.Add(input);

            var btnsPanel = new StackPanel
            {
                Orientation       = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
            };

            string? result = null;

            var cancelBtn = new Button
            {
                Content    = "Cancelar",
                MinWidth   = 90,
                Height     = 32,
                Padding    = new Thickness(14, 0, 14, 0),
                FontSize   = 13,
                Margin     = new Thickness(0, 0, 8, 0),
                IsCancel   = true,
                Cursor     = Cursors.Hand,
            };
            cancelBtn.Click += (_, __) => win.Close();
            btnsPanel.Children.Add(cancelBtn);

            var okBtn = new Button
            {
                Content    = "Guardar",
                MinWidth   = 90,
                Height     = 32,
                Padding    = new Thickness(14, 0, 14, 0),
                FontSize   = 13,
                IsDefault  = true,
                Cursor     = Cursors.Hand,
            };
            okBtn.Click += (_, __) =>
            {
                result = input.Text;
                win.Close();
            };
            btnsPanel.Children.Add(okBtn);

            root.Children.Add(btnsPanel);
            win.Content = root;

            // apply primary button style if resources available
            win.Loaded += (_, __) =>
            {
                try
                {
                    var primaryStyle = Application.Current?.TryFindResource("PrimaryButton") as Style;
                    if (primaryStyle != null) okBtn.Style = primaryStyle;
                    var secondaryStyle = Application.Current?.TryFindResource("SecondaryButton") as Style;
                    if (secondaryStyle != null) cancelBtn.Style = secondaryStyle;
                }
                catch { }
                input.Focus();
                input.SelectAll();
            };

            win.ShowDialog();
            return result;
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
