using BIMPills.Core.Commands;
using BIMPills.Core.Services;
using System.Threading.Tasks;

namespace BIMPills.Commands.CustomDimensionSchemes
{
    /// <summary>
    /// Command to manage custom dimension schemes (create, edit, delete).
    /// Depends on IDimensionSchemeService from Infrastructure layer.
    /// No Revit API dependencies.
    /// </summary>
    public sealed class CustomDimensionSchemesCommand : IPluginCommand
    {
        private readonly IDimensionSchemeService _schemeService;

        public CustomDimensionSchemesCommand(IDimensionSchemeService schemeService)
        {
            _schemeService = schemeService;
        }

        public CommandResult Execute(ICommandContext context)
        {
            // This command is initiated from the UI window (CustomDimensionSchemesWindow)
            // which handles the CRUD operations asynchronously.
            // The command simply returns OK — actual logic is in the window.
            return CommandResult.Ok("Custom dimension schemes window opened");
        }
    }
}
