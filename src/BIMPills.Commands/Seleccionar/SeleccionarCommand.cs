using BIMPills.Core.Commands;

namespace BIMPills.Commands.Seleccionar
{
    public sealed class SeleccionarCommand : IPluginCommand
    {
        public CommandResult Execute(ICommandContext context)
        {
            context.Logger.Info($"[Seleccionar] Abriendo galería de selección — documento: {context.Document.Title}");
            return CommandResult.Ok("Galería de selección lista.");
        }
    }
}
