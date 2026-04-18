using BIMPills.Core.Services;
using BIMPills.Infrastructure.DI;
using BIMPills.Infrastructure.Licensing;
using System;
using System.Diagnostics;
using System.Text;
using System.Windows;

namespace BIMPills.UI.Support
{
    public partial class SupportWindow : Window
    {
        private const string IntercomAppId = "le2ot70e";

        private readonly ILogger? _logger;

        public SupportWindow()
        {
            InitializeComponent();
            BIMPills.UI.Shared.ThemeHelper.Apply(this);

            if (ServiceLocator.IsRegistered<ILogger>())
                _logger = ServiceLocator.Get<ILogger>();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Inicializa WebView2 — lanza si el Runtime no está instalado
                await WebView.EnsureCoreWebView2Async(null);

                WebView.Visibility  = Visibility.Visible;
                LoadingPanel.Visibility = Visibility.Collapsed;

                // Obtener datos del usuario desde licencia (pre-identificar en Intercom)
                var (userName, userEmail) = GetUserIdentity();

                // Cargar HTML local con Intercom JS embebido
                var html = BuildIntercomHtml(userName, userEmail);
                WebView.NavigateToString(html);

                StatusText.Text = "Chat de soporte listo.";
                _logger?.Info("SupportWindow: WebView2 inicializado correctamente.");
            }
            catch (Exception ex) when (IsWebView2RuntimeMissing(ex))
            {
                _logger?.Warning($"SupportWindow: WebView2 Runtime no instalado — {ex.Message}");
                ShowFallback();
            }
            catch (Exception ex)
            {
                _logger?.Error("SupportWindow: error al inicializar WebView2", ex);
                ShowFallback();
            }
        }

        // ── Identity ─────────────────────────────────────────────────────────

        private static (string name, string email) GetUserIdentity()
        {
            try
            {
                var cache   = new LicenseCache();
                var license = cache.Load();
                if (license != null)
                    return (license.HolderName ?? "", "");
            }
            catch { /* non-critical */ }

            return ("", "");
        }

        // ── HTML builder ──────────────────────────────────────────────────────

        private static string BuildIntercomHtml(string userName, string userEmail)
        {
            var sb = new StringBuilder();

            // Sanitize inputs to prevent XSS inside the JS literal
            var safeName  = EscapeJsString(userName);
            var safeEmail = EscapeJsString(userEmail);

            sb.Append(@"<!DOCTYPE html>
<html lang=""es"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>Soporte BIM Pills</title>
  <style>
    * { margin: 0; padding: 0; box-sizing: border-box; }
    html, body { height: 100%; width: 100%; background: #f8f9fa; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; }
    .container { display: flex; flex-direction: column; align-items: center; justify-content: center; height: 100%; padding: 24px; text-align: center; }
    .icon { font-size: 48px; margin-bottom: 16px; }
    h2 { font-size: 18px; font-weight: 600; color: #1a1a2e; margin-bottom: 8px; }
    p  { font-size: 13px; color: #6c757d; line-height: 1.5; }
  </style>
</head>
<body>
  <div class=""container"">
    <div class=""icon"">💬</div>
    <h2>¿En qué te podemos ayudar?</h2>
    <p>El chat de soporte se abrirá en un momento.<br>Si no aparece, haz clic en el ícono en la esquina inferior derecha.</p>
  </div>

  <script>
    window.intercomSettings = {
      api_base: 'https://api-iam.intercom.io',
      app_id: '");
            sb.Append(IntercomAppId);
            sb.Append(@"'");

            if (!string.IsNullOrEmpty(safeEmail))
            {
                sb.Append($",\n      email: '{safeEmail}'");
            }
            if (!string.IsNullOrEmpty(safeName))
            {
                sb.Append($",\n      name: '{safeName}'");
            }

            sb.Append(@"
    };
    (function(){
      var w=window;
      var ic=w.Intercom;
      if(typeof ic==='function'){
        ic('reattach_activator');
        ic('update',w.intercomSettings);
      } else {
        var d=document;
        var i=function(){i.c(arguments);};
        i.q=[];
        i.c=function(args){i.q.push(args);};
        w.Intercom=i;
        var l=function(){
          var s=d.createElement('script');
          s.type='text/javascript';
          s.async=true;
          s.src='https://widget.intercom.io/widget/");
            sb.Append(IntercomAppId);
            sb.Append(@"';
          var x=d.getElementsByTagName('script')[0];
          x.parentNode.insertBefore(s,x);
        };
        if(document.readyState==='complete'){l();}
        else if(w.attachEvent){w.attachEvent('onload',l);}
        else{w.addEventListener('load',l,false);}
      }
    })();
  </script>
</body>
</html>");

            return sb.ToString();
        }

        private static string EscapeJsString(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace("\\", "\\\\")
                .Replace("'",  "\\'")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");
        }

        // ── Fallback ──────────────────────────────────────────────────────────

        private void ShowFallback()
        {
            LoadingPanel.Visibility  = Visibility.Collapsed;
            WebView.Visibility       = Visibility.Collapsed;
            FallbackPanel.Visibility = Visibility.Visible;
            StatusText.Text          = "WebView2 Runtime no disponible.";
        }

        private static bool IsWebView2RuntimeMissing(Exception ex)
        {
            // WebView2 lanza WebView2RuntimeNotFoundException o una excepción con este mensaje
            // cuando el Runtime no está instalado en el sistema.
            var msg = ex.Message ?? "";
            return msg.IndexOf("WebView2", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("Edge", StringComparison.OrdinalIgnoreCase) >= 0
                || ex.GetType().Name.IndexOf("WebView2", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void DownloadWebView2_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = "https://developer.microsoft.com/en-us/microsoft-edge/webview2/",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger?.Warning($"SupportWindow: no se pudo abrir link de WebView2 — {ex.Message}");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
            => Close();
    }
}
