using BIMPills.Core.Audit;
using BIMPills.Core.Commands;
using System.Collections.Generic;

namespace BIMPills.Commands.ExportFamilies
{
    /// <summary>
    /// Recopila las familias cargadas en el modelo para exportación.
    /// Sin ninguna referencia a RevitAPI — completamente testeable.
    /// </summary>
    public sealed class ExportFamiliesCommand : IPluginCommand
    {
        public CommandResult Execute(ICommandContext context)
        {
            var doc = context.Document;
            context.Logger.Info($"Recopilando familias para exportar: {doc.Title}");

            var families = doc.GetLoadedFamilies();

            context.Logger.Info($"Encontradas {families.Count} familias exportables.");

            LastResult = new ExportFamiliesResult
            {
                DocumentTitle = doc.Title,
                IsWorkshared = doc.IsWorkshared,
                Families = families
            };

            return CommandResult.Ok($"{families.Count} familias encontradas para exportar.");
        }

        public static ExportFamiliesResult? LastResult { get; private set; }
    }

    public sealed class ExportFamiliesResult
    {
        public string DocumentTitle { get; set; } = string.Empty;
        public bool IsWorkshared { get; set; }
        public IReadOnlyList<FamilyExportInfo> Families { get; set; } = new List<FamilyExportInfo>();
    }
}
