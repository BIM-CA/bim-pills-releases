using BIMPills.Core.Commands;

namespace BIMPills.Commands.Support
{
    /// <summary>
    /// Comando que abre la ventana de soporte (Intercom via WebView2).
    /// No requiere acceso al documento de Revit.
    /// </summary>
    public sealed class SupportCommand : IPluginCommand
    {
        public CommandResult Execute(ICommandContext context)
        {
            context.Logger.Info("Abriendo ventana de soporte BIM Pills.");
            return CommandResult.Ok("Soporte");
        }
    }
}
