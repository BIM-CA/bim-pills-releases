using BIMPills.Core.Commands;

namespace BIMPills.Commands.Documentacion
{
    public sealed class DocumentacionCommand : IPluginCommand
    {
        public CommandResult Execute(ICommandContext context)
        {
            context.Logger.Info("Abriendo ventana de Documentación (placeholder).");
            return CommandResult.Ok("Documentación abierta.");
        }
    }
}
