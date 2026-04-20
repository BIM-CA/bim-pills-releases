using BIMPills.Commands.Support;
using BIMPills.Core.Commands;
using BIMPills.Revit.Commands;
using BIMPills.UI.Shared;
using BIMPills.UI.Support;
using System.Windows.Media.Animation;

namespace BIMPills.Revit.Commands.Support
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.ReadOnly)]
    public sealed class SupportRevitCommand : RevitCommandBase
    {
        // Soporte debe ser siempre accesible — no requiere licencia activa
        protected override bool RequiresLicense => false;

        // Singleton: reutilizamos la misma ventana para hacer toggle show/hide
        private static SupportWindow? _window;

        protected override IPluginCommand CreateCommand()
            => new SupportCommand();

        protected override void OnSuccess(IPluginCommand command)
        {
            // Ventana cerrada o nunca creada → crear y mostrar
            if (_window == null || !_window.IsLoaded)
            {
                _window = new SupportWindow();
                _window.ShowAnimated();
                return;
            }

            // Ventana visible → ocultar (toggle)
            if (_window.IsVisible)
            {
                var fadeOut  = new DoubleAnimation(0.0, System.TimeSpan.FromMilliseconds(200));
                var slideOut = new DoubleAnimation(
                    _window.Top, _window.Top + 20,
                    System.TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                slideOut.Completed += (_, _) => _window.Hide();
                _window.BeginAnimation(System.Windows.Window.OpacityProperty, fadeOut);
                _window.BeginAnimation(System.Windows.Window.TopProperty, slideOut);
                return;
            }

            // Ventana oculta → volver a mostrar (con repositionamiento por si Revit se movió)
            _window.ShowAnimated();
        }
    }
}
