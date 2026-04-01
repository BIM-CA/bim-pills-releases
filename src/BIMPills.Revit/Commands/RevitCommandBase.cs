using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPills.Core.Commands;
using BIMPills.Core.Licensing;
using BIMPills.Infrastructure.DI;
using BIMPills.Core.Services;
using BIMPills.Revit.Context;
using System;

namespace BIMPills.Revit.Commands
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]
    public abstract class RevitCommandBase : IExternalCommand
    {
        protected abstract IPluginCommand CreateCommand();

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
            ILogger? logger = null;
            try
            {
                logger = ServiceLocator.IsRegistered<ILogger>()
                    ? ServiceLocator.Get<ILogger>()
                    : null;

                logger?.Info($"Iniciando comando: {GetType().Name}");

                // License gate — block commands when license is fully expired
                if (ServiceLocator.IsRegistered<ILicenseService>())
                {
                    var license = ServiceLocator.Get<ILicenseService>();
                    if (license.IsGracePeriod)
                    {
                        var cached = license.GetCachedLicense();
                        var daysLeft = cached?.ExpiresAt.HasValue == true
                            ? Math.Max(0, 7 - (int)(DateTime.UtcNow - cached.ExpiresAt.Value).TotalDays)
                            : 0;
                        TaskDialog.Show("BIMPills",
                            $"Tu licencia est\u00E1 en periodo de gracia ({daysLeft} d\u00EDas restantes).\n" +
                            "Contacta soporte@bim-ca.com para renovar.");
                    }
                    else if (license.IsExpired)
                    {
                        TaskDialog.Show("BIMPills",
                            "Tu licencia ha expirado.\n\n" +
                            "Contacta soporte@bim-ca.com para renovar\n" +
                            "o activa una nueva licencia desde Acerca de.");
                        message = "Licencia expirada.";
                        return Result.Cancelled;
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
                    TaskDialog.Show("BIMPills — Error de interfaz",
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
