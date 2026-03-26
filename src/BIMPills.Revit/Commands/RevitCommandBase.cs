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
        /// Called after the command executes successfully.
        /// Override to show UI windows with the command results.
        /// </summary>
        protected virtual void OnSuccess(IPluginCommand command) { }

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            ILogger? logger = null;
            try
            {
                logger = ServiceLocator.IsRegistered<ILogger>()
                    ? ServiceLocator.Get<ILogger>()
                    : null;

                var context = new RevitCommandContext(commandData);
                var command = CreateCommand();
                var result  = command.Execute(context);

                if (!result.Success)
                {
                    message = result.Message ?? "El comando no pudo completarse.";
                    return Result.Failed;
                }

                OnSuccess(command);
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
