using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPills.Core.Commands;
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
            catch (Exception ex)
            {
                message = ex.Message;
                logger?.Error($"Excepción no controlada en {GetType().Name}", ex);
                return Result.Failed;
            }
        }
    }
}
