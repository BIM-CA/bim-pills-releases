using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace BIMPills.UI.Shared
{
    /// <summary>
    /// Helper to anchor WPF windows to the Revit main window.
    /// Ensures all BIM Pills pop-ups appear centered over the Revit window
    /// (the one the user is looking at), and not on a different monitor
    /// or at the center of the primary screen.
    /// </summary>
    public static class RevitOwnerHelper
    {
        /// <summary>
        /// Current Revit main window handle. Set once by
        /// <see cref="SetCurrentRevitHandle"/> during command execution.
        /// Used as a fallback when an explicit owner is not passed.
        /// </summary>
        public static IntPtr CurrentRevitHandle { get; private set; } = IntPtr.Zero;

        /// <summary>
        /// Stores the Revit main window handle for the running command.
        /// Called by <c>RevitCommandBase</c> before executing the command.
        /// </summary>
        public static void SetCurrentRevitHandle(IntPtr handle)
        {
            CurrentRevitHandle = handle;
        }

        /// <summary>
        /// Assigns the Revit main window as the Win32 owner of a WPF window.
        /// Must be called BEFORE the window is shown
        /// (<see cref="Window.Show"/> / <see cref="Window.ShowDialog"/>).
        /// </summary>
        public static void SetRevitAsOwner(Window window)
        {
            if (window == null) return;
            var handle = CurrentRevitHandle;
            if (handle == IntPtr.Zero) return;

            try
            {
                var helper = new WindowInteropHelper(window);
                helper.Owner = handle;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            catch
            {
                // If the window was already shown, WindowInteropHelper.Owner throws.
                // Fall back to a manual centering via Win32 rect below.
                TryCenterOnRevit(window);
            }
        }

        /// <summary>
        /// Shows <paramref name="window"/> modally after anchoring it to the
        /// Revit main window. Returns the dialog result.
        /// </summary>
        public static bool? ShowDialogOverRevit(this Window window)
        {
            SetRevitAsOwner(window);
            return window.ShowDialog();
        }

        /// <summary>
        /// Shows <paramref name="window"/> modelessly after anchoring it to
        /// the Revit main window.
        /// </summary>
        public static void ShowOverRevit(this Window window)
        {
            SetRevitAsOwner(window);
            window.Show();
        }

        // ── Manual fallback (Win32) ─────────────────────────────────────

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        private static void TryCenterOnRevit(Window window)
        {
            if (CurrentRevitHandle == IntPtr.Zero) return;
            if (!GetWindowRect(CurrentRevitHandle, out var rect)) return;

            double width  = window.ActualWidth  > 0 ? window.ActualWidth  : window.Width;
            double height = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
            if (double.IsNaN(width) || double.IsNaN(height)) return;

            double revitW = rect.Right - rect.Left;
            double revitH = rect.Bottom - rect.Top;

            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = rect.Left + (revitW - width)  / 2.0;
            window.Top  = rect.Top  + (revitH - height) / 2.0;
        }
    }
}
