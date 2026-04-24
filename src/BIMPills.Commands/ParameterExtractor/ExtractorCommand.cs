using BIMPills.Core.Commands;

namespace BIMPills.Commands.ParameterExtractor
{
    /// <summary>
    /// Marcador para el Extractor de Parámetros. La lógica real de extracción
    /// vive en la capa Revit (ExtractorApplier) porque necesita RevitAPI.
    /// </summary>
    public sealed class ExtractorCommand : IPluginCommand
    {
        public CommandResult Execute(ICommandContext context)
        {
            context.Logger.Info("Abriendo Extractor de Parámetros.");
            return CommandResult.Ok("Extractor");
        }
    }
}
