using BIMPills.Core.Commands;
using BIMPills.Core.Models;

namespace BIMPills.Commands.Ordering
{
    /// <summary>
    /// Entry point for the Ordenar / Numerador feature.
    /// Only verifies document access — the config is collected interactively
    /// by the UI layer (OrdenarWindow) before the session starts.
    /// </summary>
    public class OrderingCommand : IPluginCommand
    {
        public CommandResult Execute(ICommandContext context)
        {
            if (context?.Document == null)
                return CommandResult.Fail("No se pudo acceder al documento activo.");

            return CommandResult.Ok("Documento listo para ordenar.");
        }
    }
}
