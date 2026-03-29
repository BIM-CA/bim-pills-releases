using BIMPills.Core.Commands;

namespace BIMPills.Commands.DataManager
{
    /// <summary>
    /// Entry point for the Gestionar / SheetLink feature.
    /// Validates document access and passes schedule list to the UI layer.
    /// </summary>
    public class DataManagerCommand : IPluginCommand
    {
        public CommandResult Execute(ICommandContext context)
        {
            var schedules = context.Document.GetSchedules();
            if (schedules == null)
                return CommandResult.Fail("No se pudo acceder a las tablas del modelo.");

            return CommandResult.Ok($"Tablas disponibles: {schedules.Count}");
        }
    }
}
