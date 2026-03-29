using BIMPills.Core.Commands;
using BIMPills.Core.Documentacion;

namespace BIMPills.Commands.Documentacion
{
    /// <summary>
    /// Recopila datos del modelo necesarios para la herramienta de Acotado de Vanos.
    /// La creación real de cotas se ejecuta desde el Revit bridge via callback.
    /// </summary>
    public sealed class AcotadoVanosCommand : IPluginCommand
    {
        /// <summary>Último resultado para pasar al UI.</summary>
        public static AcotadoVanosData? LastResult { get; private set; }

        public CommandResult Execute(ICommandContext context)
        {
            context.Logger.Info("Acotado de Vanos: recopilando datos del modelo...");

            var dimensionTypes = context.Document.GetDimensionTypes();
            if (dimensionTypes.Count == 0)
            {
                return CommandResult.Fail(
                    "No se encontraron tipos de cota en el proyecto.\n" +
                    "Verifica que el proyecto contenga al menos un DimensionType.");
            }

            var doorCount  = context.Document.GetDoorCountInActiveView();
            var gridCount  = context.Document.GetGridCountInActiveView();
            var wallCount  = context.Document.GetWallCountInActiveView();
            var levelCount = context.Document.GetArqLevelCount();
            var viewName   = context.Document.GetActiveViewName();

            LastResult = new AcotadoVanosData(doorCount, dimensionTypes, viewName, gridCount, wallCount, levelCount);

            context.Logger.Info($"Acotado de Vanos: {doorCount} puertas, {gridCount} ejes, {wallCount} muros, {levelCount} niveles ARQ en '{viewName}', {dimensionTypes.Count} tipos de cota.");
            return CommandResult.Ok($"{doorCount} puertas, {gridCount} ejes, {wallCount} muros, {levelCount} niveles ARQ detectados.");
        }
    }
}
