using BIMPills.Core.About;
using BIMPills.Core.Commands;

namespace BIMPills.Commands.About
{
    /// <summary>
    /// Comando que expone la información "Acerca de" del plugin.
    /// No requiere acceso al documento de Revit.
    /// </summary>
    public sealed class AboutCommand : IPluginCommand
    {
        public CommandResult Execute(ICommandContext context)
        {
            context.Logger.Info("Mostrando información Acerca de BIMPills.");

            var info = new AboutInfo();
            LastResult = info;

            return CommandResult.Ok($"{info.PluginName} v{info.Version} — {info.Company}");
        }

        /// <summary>
        /// Holds the result so the Revit bridge can pass it to the UI window.
        /// </summary>
        public static AboutInfo? LastResult { get; private set; }
    }
}
