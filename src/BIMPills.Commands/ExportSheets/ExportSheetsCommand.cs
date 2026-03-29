using BIMPills.Core.Commands;
using BIMPills.Core.Models;
using System.Collections.Generic;

namespace BIMPills.Commands.ExportSheets
{
    /// <summary>
    /// Recopila los planos (ViewSheets) del modelo para exportación.
    /// Sin ninguna referencia a RevitAPI — completamente testeable.
    /// </summary>
    public sealed class ExportSheetsCommand : IPluginCommand
    {
        public CommandResult Execute(ICommandContext context)
        {
            var doc = context.Document;
            context.Logger.Info($"Recopilando planos para exportar: {doc.Title}");

            var sheets = doc.GetSheets();
            var projectName = doc.GetProjectName();

            context.Logger.Info($"Encontrados {sheets.Count} planos exportables.");

            LastResult = new ExportSheetsResult
            {
                DocumentTitle = doc.Title,
                ProjectName = projectName,
                Sheets = sheets
            };

            return CommandResult.Ok($"{sheets.Count} planos encontrados para exportar.");
        }

        public static ExportSheetsResult? LastResult { get; private set; }
    }

    public sealed class ExportSheetsResult
    {
        public string DocumentTitle { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public IReadOnlyList<SheetExportInfo> Sheets { get; set; } = new List<SheetExportInfo>();
    }
}
