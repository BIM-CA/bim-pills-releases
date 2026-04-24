using BIMPills.Core.Commands;

namespace BIMPills.Commands.LegendFromExcel
{
    /// <summary>
    /// Marcador delgado — la orquestación real ocurre en DibujarRevitCommand (capa Revit).
    /// </summary>
    public sealed class DrawExcelLegendCommand : IPluginCommand
    {
        public CommandResult Execute(ICommandContext context)
        {
            return CommandResult.Ok("Listo para dibujar leyenda desde Excel.");
        }
    }
}
