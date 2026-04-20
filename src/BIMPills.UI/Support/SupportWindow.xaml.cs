using BIMPills.Core.Services;
using BIMPills.Infrastructure.DI;
using BIMPills.Infrastructure.Licensing;
using BIMPills.UI.Shared;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;

namespace BIMPills.UI.Support
{
    public partial class SupportWindow : Window
    {
        private const string IntercomAppId = "le2ot70e";

        // Dominio virtual para WebView2 — listado en la whitelist de Intercom
        // (Messenger → Security → Trusted Domains: app.bimpills.com).
        // WebView2 intercepta estas requests internamente: no requiere DNS ni servidor real.
        private const string VirtualHost  = "app.bimpills.com";
        private const string HtmlFileName = "support_chat.html";

        private const double WindowMargin  = 24;

        // Color clave para transparencia Win32 (LWA_COLORKEY).
        // #010203 = RGB(1,2,3) — casi negro, prácticamente invisible en cualquier UI.
        // DWM quita del composite todos los píxeles con este color exacto → fondo transparente.
        // Al ser manual (no AllowsTransparency), WebView2 sigue recibiendo input normalmente.
        private const uint TransparentKey  = 0x00030201; // COLORREF = 0x00BBGGRR

        private readonly ILogger? _logger;
        private bool _webViewShown = false;

        // ── Win32 P/Invoke ────────────────────────────────────────────────────

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] private static extern int  GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern int  SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

        private const int  GWL_EXSTYLE    = -20;
        private const int  WS_EX_LAYERED  = 0x80000;
        private const uint LWA_COLORKEY   = 0x1;

        // ─────────────────────────────────────────────────────────────────────

        public SupportWindow()
        {
            InitializeComponent();
            if (ServiceLocator.IsRegistered<ILogger>())
                _logger = ServiceLocator.Get<ILogger>();

            // Aplicar color-key transparency después de que el HWND esté listo.
            // A diferencia de AllowsTransparency="True", este método NO usa UpdateLayeredWindow,
            // por lo que WebView2 (HwndHost) sigue recibiendo input de mouse y teclado.
            SourceInitialized += (_, _) => ApplyColorKeyTransparency();
        }

        // ── Transparencia Win32 ───────────────────────────────────────────────

        // ESTRATEGIA ACTIVA: Opción A — LWA_COLORKEY + WebView2 DefaultBackgroundColor alpha=0
        //
        // Cómo funciona la cadena completa:
        //   1. WPF window Background="#010203" (casi negro, indistinguible visualmente)
        //   2. WebView2.DefaultBackgroundColor = Color.FromArgb(0,1,2,3) → alpha=0 → WebView2
        //      composita transparentemente, dejando ver el fondo WPF #010203 donde no hay HTML
        //   3. SetLayeredWindowAttributes(LWA_COLORKEY, #010203) → DWM elimina esos píxeles
        //      del composite → el contenido de Revit detrás queda visible
        //   4. WebView2 renderiza por DComp en su propio HWND hijo — NO usa UpdateLayeredWindow
        //      → recibe input de mouse/teclado normalmente (a diferencia de AllowsTransparency="True")
        //
        // ALTERNATIVA: Opción C — DWM Acrylic Blur (sin colorkey, sin AllowsTransparency)
        //   Ventaja: no requiere color clave; da efecto blur real del contenido detrás
        //   Desventaja: el contenido de Revit se ve borroso (efecto acrílico), no nítido
        //   Para activar: descomentar ApplyAcrylicEffect(), llamarla en SourceInitialized
        //   y cambiar Background="Transparent" en XAML (sin AllowsTransparency).

        private void ApplyColorKeyTransparency()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            var ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_LAYERED);
            SetLayeredWindowAttributes(hwnd, TransparentKey, 255, LWA_COLORKEY);
        }

        // ── Opción C: DWM Acrylic Blur ────────────────────────────────────────
        // Descomentar para activar. Requiere: Background="Transparent" en XAML (sin AllowsTransparency).
        // WebView2 funciona con input porque no se usa AllowsTransparency/UpdateLayeredWindow.
        //
        // [StructLayout(LayoutKind.Sequential)]
        // private struct AccentPolicy { public int AccentState; public int AccentFlags; public int GradientColor; public int AnimationId; }
        // [StructLayout(LayoutKind.Sequential)]
        // private struct WindowCompositionAttributeData { public int Attribute; public IntPtr Data; public int SizeOfData; }
        // [DllImport("user32.dll")] static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);
        //
        // private void ApplyAcrylicEffect()
        // {
        //     var hwnd = new WindowInteropHelper(this).Handle;
        //     if (hwnd == IntPtr.Zero) return;
        //     // AccentState: 3 = ACCENT_ENABLE_BLURBEHIND (blur puro)
        //     //              4 = ACCENT_ENABLE_ACRYLICBLURBEHIND (acrylic con tinte)
        //     // GradientColor: ARGB donde A controla opacidad del tinte
        //     //   0x40000000 = negro con 25% opacidad (tinte oscuro sutil)
        //     //   0x00000000 = completamente transparente (máxima visibilidad de Revit)
        //     var accent = new AccentPolicy { AccentState = 4, GradientColor = 0x40000000 };
        //     var accentSize = Marshal.SizeOf(accent);
        //     var accentPtr  = Marshal.AllocHGlobal(accentSize);
        //     Marshal.StructureToPtr(accent, accentPtr, false);
        //     var data = new WindowCompositionAttributeData { Attribute = 19, Data = accentPtr, SizeOfData = accentSize };
        //     SetWindowCompositionAttribute(hwnd, ref data);
        //     Marshal.FreeHGlobal(accentPtr);
        // }

        // ── Posición ──────────────────────────────────────────────────────────

        public void PositionBottomRight()
        {
            var hwnd = RevitOwnerHelper.CurrentRevitHandle;
            if (hwnd != IntPtr.Zero && GetWindowRect(hwnd, out RECT rect))
            {
                Left = rect.Right  - Width  - WindowMargin;
                Top  = rect.Bottom - Height - WindowMargin;
            }
            else
            {
                Left = SystemParameters.PrimaryScreenWidth  - Width  - WindowMargin;
                Top  = SystemParameters.PrimaryScreenHeight - Height - WindowMargin;
            }
        }

        // ── Inicialización ────────────────────────────────────────────────────

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            PositionBottomRight();

            try
            {
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BIMPills", "WebView2");

                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(
                    null, userDataFolder);

                await WebView.EnsureCoreWebView2Async(env);

                // Fondo WebView2 transparente (Opción A):
                // alpha=0 → WebView2 composita transparentemente contra el WPF parent.
                // Los píxeles sin contenido HTML heredan el color clave #010203 (RGB 1,2,3)
                // del fondo WPF, que DWM elimina via LWA_COLORKEY → transparencia real.
                // IMPORTANTE: FromArgb(alpha, r, g, b) — alpha=0 es imprescindible.
                // FromArgb(1,2,3) sin alpha explícito asigna alpha=255 (opaco), que es el bug anterior.
                WebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0, 1, 2, 3);

                WebView.CoreWebView2.Settings.IsScriptEnabled              = true;
                WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                WebView.CoreWebView2.Settings.AreDevToolsEnabled           = false;
                WebView.CoreWebView2.Settings.IsStatusBarEnabled           = false;
                WebView.CoreWebView2.Settings.IsZoomControlEnabled         = false;

                WebView.CoreWebView2.WebMessageReceived  += OnWebMessage;
                WebView.CoreWebView2.NavigationCompleted += OnNavCompleted;

                var (name, email) = GetUserIdentity();
                var htmlFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BIMPills");
                Directory.CreateDirectory(htmlFolder);
                File.WriteAllText(
                    Path.Combine(htmlFolder, HtmlFileName),
                    BuildHtml(name, email),
                    Encoding.UTF8);

                WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    VirtualHost, htmlFolder,
                    Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);

                WebView.CoreWebView2.Navigate($"https://{VirtualHost}/{HtmlFileName}");
                Log("Navegación iniciada");
            }
            catch (Exception ex) when (IsWebView2Missing(ex))
            {
                Log($"WebView2 faltante: {ex.Message}");
                ShowFallback();
            }
            catch (Exception ex)
            {
                Log($"Error init: {ex.GetType().Name} — {ex.Message}");
                ShowFallback();
            }
        }

        // ── Navegación ────────────────────────────────────────────────────────

        private void OnNavCompleted(object? sender,
            Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                Log("Nav OK");
                RevealWebView();
            }
            else
            {
                Log($"Nav FAIL — {e.WebErrorStatus}");
            }
        }

        private void OnWebMessage(object? sender,
            Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var msg = e.TryGetWebMessageAsString();
                if (msg == "[CLOSE]")  Dispatcher.InvokeAsync(AnimateAndHide);
                if (msg == "[SHOWN]")  Dispatcher.InvokeAsync(HideLoadingPanel);
            }
            catch { }
        }

        // ── Revelar WebView ───────────────────────────────────────────────────

        private void RevealWebView()
        {
            if (_webViewShown) return;
            _webViewShown = true;
            Dispatcher.InvokeAsync(() =>
            {
                WebView.Visibility = Visibility.Visible;
                var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(400));
                fade.Completed += (_, _) => LoadingPanel.Visibility = Visibility.Collapsed;
                LoadingPanel.BeginAnimation(OpacityProperty, fade);
            });
        }

        /// <summary>
        /// Oculta el LoadingPanel con fade. Llamado cuando Intercom dispara onShow
        /// (mensaje [SHOWN]) al re-abrir la ventana. También sirve como fallback
        /// tras timeout si Intercom no responde (CDN offline, etc.).
        /// </summary>
        private void HideLoadingPanel()
        {
            if (LoadingPanel.Visibility != Visibility.Visible) return;
            LoadingPanel.BeginAnimation(OpacityProperty, null); // cancelar animación previa
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fade.Completed += (_, _) => LoadingPanel.Visibility = Visibility.Collapsed;
            LoadingPanel.BeginAnimation(OpacityProperty, fade);
        }

        // ── Animaciones / toggle ──────────────────────────────────────────────

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape) AnimateAndHide();
        }

        private void AnimateAndHide()
        {
            // Solo slide (sin animación de Opacity) — la animación de Opacity en una
            // ventana layered sobreescribiría SetLayeredWindowAttributes y perdería
            // el color key, dejando el fondo opaco brevemente.
            var slide = new DoubleAnimation(Top, Top + 20, TimeSpan.FromMilliseconds(200))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            slide.Completed += (_, _) =>
            {
                Hide();
                BeginAnimation(TopProperty, null);
            };
            BeginAnimation(TopProperty, slide);
        }

        public void ShowAnimated()
        {
            PositionBottomRight();

            // Si la ventana ya está visible (Intercom en modo launcher),
            // solo re-abrir el chat sin reanimar la ventana.
            if (IsVisible)
            {
                Activate();
                try { _ = WebView.CoreWebView2?.ExecuteScriptAsync("try { window.Intercom('show'); } catch(_) {}"); }
                catch { }
                return;
            }

            // WebView ya cargado pero ventana oculta: re-mostrar LoadingPanel para tapar
            // el fondo negro de la superficie DComp mientras Intercom se expande.
            // onShow en el HTML dispara [SHOWN] → HideLoadingPanel().
            // Fallback: ocultar LoadingPanel tras 3 s si onShow nunca llega (CDN offline, etc.).
            if (_webViewShown)
            {
                LoadingPanel.BeginAnimation(OpacityProperty, null);
                LoadingPanel.Opacity    = 1;
                LoadingPanel.Visibility = Visibility.Visible;

                _ = System.Threading.Tasks.Task.Delay(3000)
                    .ContinueWith(_ => Dispatcher.InvokeAsync(HideLoadingPanel));
            }

            double targetTop = Top;
            Top = targetTop + 20;
            Show();
            // Re-aplicar color key tras Show() (puede haberse perdido al ocultar la ventana)
            ApplyColorKeyTransparency();
            Activate();

            // Llamar Intercom('show') para expandir desde estado launcher.
            // onShow callback en HTML enviará [SHOWN] para ocultar LoadingPanel.
            if (_webViewShown)
            {
                try { _ = WebView.CoreWebView2?.ExecuteScriptAsync("try { window.Intercom('show'); } catch(_) {}"); }
                catch { }
            }

            BeginAnimation(TopProperty, new DoubleAnimation(targetTop, TimeSpan.FromMilliseconds(260))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }

        // ── Fallback (WebView2 Runtime no instalado) ──────────────────────────

        private void ShowFallback()
        {
            LoadingPanel.Visibility  = Visibility.Collapsed;
            WebView.Visibility       = Visibility.Collapsed;
            FallbackPanel.Visibility = Visibility.Visible;
        }

        private void DownloadWebView2_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("https://developer.microsoft.com/en-us/microsoft-edge/webview2/") { UseShellExecute = true }); }
            catch { }
        }

        private static bool IsWebView2Missing(Exception ex)
        {
            // string.Contains(string, StringComparison) no existe en .NET Framework 4.8 (Revit 2024).
            // Usamos ToUpperInvariant() + Contains(string) para compatibilidad cross-framework.
            var m    = (ex.Message        ?? "").ToUpperInvariant();
            var type = ex.GetType().Name.ToUpperInvariant();
            return m.Contains("WEBVIEW2") || m.Contains("EDGE") || type.Contains("WEBVIEW2");
        }

        // ── HTML ──────────────────────────────────────────────────────────────

        private static string BuildHtml(string name, string email)
        {
            var safeName  = Js(name);
            var safeEmail = Js(email);
            var nameAttr  = string.IsNullOrEmpty(safeName)  ? "" : $"  window.intercomSettings.name  = '{safeName}';";
            var emailAttr = string.IsNullOrEmpty(safeEmail) ? "" : $"  window.intercomSettings.email = '{safeEmail}';";

            return $@"<!DOCTYPE html>
<html lang=""es"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width,initial-scale=1"">
  <style>
    * {{ margin:0; padding:0; box-sizing:border-box; }}
    html, body {{ height:100%; background:transparent; }}
  </style>
</head>
<body>
<script>
(function() {{
  window.intercomSettings = {{ app_id: '{IntercomAppId}', hide_default_launcher: false }};
{nameAttr}
{emailAttr}

  (function(){{var w=window;var ic=w.Intercom;if(typeof ic==='function'){{ic('reattach_activator');ic('update',w.intercomSettings);}}else{{var d=document;var i=function(){{i.c(arguments);}};i.q=[];i.c=function(args){{i.q.push(args);}};w.Intercom=i;var l=function(){{var s=d.createElement('script');s.type='text/javascript';s.async=true;s.src='https://widget.intercom.io/widget/{IntercomAppId}';var x=d.getElementsByTagName('script')[0];x.parentNode.insertBefore(s,x);}};if(d.readyState==='complete'){{l();}}else{{w.addEventListener('load',l,false);}}}}}})();

  window.Intercom('onHide', function() {{
    try {{ window.chrome.webview.postMessage('[CLOSE]'); }} catch(_) {{}}
  }});

  window.Intercom('onShow', function() {{
    try {{ window.chrome.webview.postMessage('[SHOWN]'); }} catch(_) {{}}
  }});

  setTimeout(function() {{
    try {{ window.Intercom('show'); }} catch(_) {{}}
  }}, 1500);
}})();
</script>
</body>
</html>";
        }

        private static string Js(string v)
        {
            if (string.IsNullOrEmpty(v)) return "";
            return v.Replace("\\","\\\\").Replace("'","\\'")
                    .Replace("\"","\\\"").Replace("\n","\\n").Replace("\r","\\r");
        }

        // ── Identidad del usuario ─────────────────────────────────────────────

        private static (string name, string email) GetUserIdentity()
        {
            try
            {
                var lic = new LicenseCache().Load();
                if (lic != null) return (lic.HolderName ?? "", lic.Email ?? "");
            }
            catch { }
            return ("", "");
        }

        // ── Logger ────────────────────────────────────────────────────────────

        private void Log(string msg)
        {
            _logger?.Info($"SupportWindow: {msg}");
            Debug.WriteLine($"[SupportWindow] {msg}");
        }
    }
}
