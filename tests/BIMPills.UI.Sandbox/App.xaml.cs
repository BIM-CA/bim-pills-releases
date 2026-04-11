using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace BIMPills.UI.Sandbox
{
    public partial class App : Application
    {
        private static readonly string LogPath = Path.Combine(
            Path.GetTempPath(), "bimpills-sandbox-errors.log");

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Clear previous log
            try { File.WriteAllText(LogPath, $"[{DateTime.Now:O}] Sandbox started\r\n"); } catch { }

            DispatcherUnhandledException += (_, args) =>
            {
                LogException("Dispatcher", args.Exception);
                MessageBox.Show(
                    $"Excepción no controlada:\n\n{args.Exception.GetType().Name}\n{args.Exception.Message}\n\n" +
                    $"Stack (log en {LogPath}):\n{args.Exception.StackTrace}",
                    "Sandbox — Error");
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                    LogException("AppDomain", ex);
            };
        }

        private static void LogException(string source, Exception ex)
        {
            try
            {
                var line = $"[{DateTime.Now:O}] [{source}] {ex.GetType().FullName}: {ex.Message}\r\n{ex.StackTrace}\r\n";
                if (ex.InnerException != null)
                    line += $"  INNER: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}\r\n{ex.InnerException.StackTrace}\r\n";
                File.AppendAllText(LogPath, line + "\r\n");
            }
            catch { }
        }
    }
}
