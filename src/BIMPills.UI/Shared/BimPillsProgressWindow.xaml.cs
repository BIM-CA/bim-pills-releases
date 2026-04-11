using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace BIMPills.UI.Shared
{
    /// <summary>
    /// Branded, BIM Pills–styled progress window.
    /// Replaces the plain WPF <see cref="Window"/> previously used for exports,
    /// audits, and other long-running actions. Matches the visual identity
    /// of <see cref="BimPillsDialog"/> (pill icon + badge + macOS-inspired chrome).
    ///
    /// Typical use (modeless, non-blocking):
    /// <code>
    ///   var progress = new BimPillsProgressWindow("Exportando planos", total);
    ///   progress.ShowOverRevit();
    ///   // …
    ///   progress.Report(i, total, "Planta_01.pdf");
    ///   // …
    ///   progress.Complete();
    /// </code>
    /// </summary>
    public partial class BimPillsProgressWindow : Window
    {
        private readonly DispatcherTimer _elapsedTimer;
        private readonly Stopwatch _stopwatch = new();

        /// <summary>
        /// True if the user pressed Cancel (or closed the window).
        /// Consumers should poll this on every iteration of the long-running loop.
        /// </summary>
        public bool IsCancelled { get; private set; }

        /// <summary>
        /// Raised when the user presses the Cancel button.
        /// </summary>
        public event EventHandler? Cancelled;

        public BimPillsProgressWindow() : this(header: "Procesando…", total: 0) { }

        public BimPillsProgressWindow(string header, int total = 0, string? message = null)
        {
            InitializeComponent();

            HeaderText.Text  = header;
            MessageText.Text = message ?? (total > 0 ? $"Preparando {total} elementos…" : "Preparando…");
            CounterText.Text = total > 0 ? $"0 / {total}" : string.Empty;
            WindowTitleText.Text = $"BIM Pills — {header}";
            Title = WindowTitleText.Text;

            _elapsedTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _elapsedTimer.Tick += (_, __) => UpdateElapsed();

            Loaded  += (_, __) => { _stopwatch.Start(); _elapsedTimer.Start(); };
            Closing += (_, e) =>
            {
                if (!IsCancelled)
                {
                    IsCancelled = true;
                    Cancelled?.Invoke(this, EventArgs.Empty);
                }
                _elapsedTimer.Stop();
                _stopwatch.Stop();
            };
        }

        // ── Public API ────────────────────────────────────────────────

        /// <summary>
        /// Update the progress window with the current step.
        /// Safe to call from the UI thread.
        /// </summary>
        public void Report(int current, int total, string? currentItem = null, string? message = null)
        {
            if (total <= 0) return;

            double ratio = Math.Max(0, Math.Min(1, (double)current / total));
            // Animate the fill width against the outer Border's width
            if (ProgressFill.Parent is FrameworkElement parent && parent.ActualWidth > 0)
                ProgressFill.Width = parent.ActualWidth * ratio;

            CounterText.Text = $"{current} / {total}";

            if (!string.IsNullOrWhiteSpace(currentItem))
                DetailText.Text = currentItem;

            if (!string.IsNullOrWhiteSpace(message))
                MessageText.Text = message;
        }

        /// <summary>
        /// Update the header text (e.g., phase name).
        /// </summary>
        public void SetHeader(string header)
        {
            HeaderText.Text = header;
            WindowTitleText.Text = $"BIM Pills — {header}";
            Title = WindowTitleText.Text;
        }

        /// <summary>
        /// Update the message text (secondary description under the header).
        /// </summary>
        public void SetMessage(string message) => MessageText.Text = message;

        /// <summary>
        /// Closes the window. Safe to call multiple times.
        /// </summary>
        public void Complete()
        {
            _elapsedTimer.Stop();
            _stopwatch.Stop();
            try { Close(); } catch { /* already closed */ }
        }

        // ── Internals ─────────────────────────────────────────────────

        private void UpdateElapsed()
        {
            var elapsed = _stopwatch.Elapsed;
            ElapsedText.Text = elapsed.TotalMinutes >= 1
                ? $"{(int)elapsed.TotalMinutes} min {elapsed.Seconds:D2} seg"
                : $"{elapsed.TotalSeconds:F0} seg";
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { /* not draggable if not shown */ }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsCancelled) return;
            IsCancelled = true;
            CancelButton.IsEnabled = false;
            CancelButton.Content = "Cancelando…";
            MessageText.Text = "Finalizando tarea en curso…";
            Cancelled?.Invoke(this, EventArgs.Empty);
        }
    }
}
