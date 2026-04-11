using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPills.Core.Commands;
using BIMPills.Core.Licensing;
using BIMPills.Infrastructure.DI;
using BIMPills.Core.Services;
using BIMPills.Revit.Context;
using BIMPills.UI.Shared;
using System;

namespace BIMPills.Revit.Commands
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]
    public abstract class RevitCommandBase : IExternalCommand
    {
        protected abstract IPluginCommand CreateCommand();

        /// <summary>
        /// Override to false in commands that must run regardless of license state (e.g. About).
        /// </summary>
        protected virtual bool RequiresLicense => true;

        /// <summary>
        /// The ExternalCommandData from the current execution.
        /// Available during and after Execute (including OnSuccess).
        /// </summary>
        protected ExternalCommandData? CommandData { get; private set; }

        /// <summary>
        /// Called after the command executes successfully.
        /// Override to show UI windows with the command results.
        /// </summary>
        protected virtual void OnSuccess(IPluginCommand command) { }

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            CommandData = commandData;

            // Anchor all BIM Pills WPF windows to the current Revit main window
            // so popups appear centered over Revit (important on multi-monitor setups).
            try { RevitOwnerHelper.SetCurrentRevitHandle(commandData.Application.MainWindowHandle); }
            catch { /* non-fatal — popups will fall back to center-screen */ }

            ILogger? logger = null;
            try
            {
                logger = ServiceLocator.IsRegistered<ILogger>()
                    ? ServiceLocator.Get<ILogger>()
                    : null;

                logger?.Info($"Iniciando comando: {GetType().Name}");

                // License gate — skip for commands that explicitly opt out (e.g. About)
                if (RequiresLicense && ServiceLocator.IsRegistered<ILicenseService>())
                {
                    var license = ServiceLocator.Get<ILicenseService>();

                    if (!license.IsActivated)
                    {
                        // Never activated — show activation window
                        var dlg = new BIMPills.UI.Licensing.LicenseActivationWindow();
                        dlg.ShowDialogOverRevit();
                        if (!dlg.LicenseActivated)
                        {
                            message = "Se requiere una licencia activa para usar BIM Pills.";
                            return Result.Cancelled;
                        }
                        // Re-read license after activation
                    }
                    else if (license.IsGracePeriod)
                    {
                        var cached = license.GetCachedLicense();
                        var daysLeft = cached?.ExpiresAt.HasValue == true
                            ? Math.Max(0, 7 - (int)(DateTime.UtcNow - cached.ExpiresAt.Value).TotalDays)
                            : 0;
                        TaskDialog.Show("BIM Pills — Licencia por vencer",
                            $"Tu licencia est\u00E1 en periodo de gracia.\n" +
                            $"Quedan {daysLeft} d\u00EDa(s) para el bloqueo.\n\n" +
                            "Renueva desde BIM Pills \u2192 Acerca de.");
                    }
                    else if (license.IsExpired)
                    {
                        var dlg = new BIMPills.UI.Licensing.LicenseActivationWindow();
                        dlg.ShowDialogOverRevit();
                        if (!dlg.LicenseActivated)
                        {
                            message = "Licencia expirada. Activa una licencia v\u00E1lida para continuar.";
                            return Result.Cancelled;
                        }
                    }
                }

                var context = new RevitCommandContext(commandData);
                var command = CreateCommand();
                var result  = command.Execute(context);

                if (!result.Success)
                {
                    var msg = result.Message ?? "El comando no pudo completarse.";
                    logger?.Warning($"Comando {GetType().Name} finalizó sin éxito: {msg}");
                    message = msg;
                    return Result.Failed;
                }

                logger?.Info($"Comando {GetType().Name} ejecutado correctamente. Abriendo UI...");

                try
                {
                    OnSuccess(command);
                }
                catch (Exception uiEx)
                {
                    logger?.Error($"Error al mostrar la ventana de {GetType().Name}", uiEx);
                    TaskDialog.Show("BIM Pills — Error de interfaz",
                        $"El comando se ejecutó correctamente, pero ocurrió un error al mostrar la ventana.\n\n" +
                        $"Detalle: {uiEx.Message}\n\n" +
                        $"Revisa el log para más información.");
                    return Result.Succeeded; // El comando sí funcionó
                }

                logger?.Info($"Comando {GetType().Name} completado.");
                return Result.Succeeded;
            }
            catch (OperationCanceledException)
            {
                logger?.Info($"Comando {GetType().Name} cancelado por el usuario.");
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                logger?.Error($"Excepción no controlada en {GetType().Name}", ex);
                return Result.Failed;
            }
        }
    }
}
