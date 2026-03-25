using BIMPills.Core.Audit;
using BIMPills.Core.Commands;
using System.Collections.Generic;

namespace BIMPills.Commands.ModelAudit
{
    /// <summary>
    /// Recopila información de auditoría del modelo activo.
    /// Sin ninguna referencia a RevitAPI — completamente testeable.
    /// </summary>
    public sealed class ModelAuditCommand : IPluginCommand
    {
        public CommandResult Execute(ICommandContext context)
        {
            var doc = context.Document;
            context.Logger.Info($"Iniciando auditoría: {doc.Title}");

            var result = new ModelAuditResult
            {
                DocumentTitle  = doc.Title,
                IsWorkshared   = doc.IsWorkshared,
                Warnings       = doc.GetWarnings(),
                Families       = doc.GetFamilySizes(),
                UnplacedViews  = doc.GetUnplacedViews(),
                OrphanElements = doc.GetElementsWithoutCategory()
            };

            context.Logger.Info(
                $"Auditoría completada — {result.Warnings.Count} advertencias, " +
                $"{result.Families.Count} familias, " +
                $"{result.UnplacedViews.Count} vistas sin colocar.");

            // The UI layer reads the result via a shared property; commands are UI-agnostic
            LastResult = result;
            return CommandResult.Ok($"Auditoría completada: {result.Warnings.Count} advertencias encontradas.");
        }

        /// <summary>
        /// Holds the result so the Revit bridge can pass it to the UI window.
        /// In a more complex plugin this would use an event bus or dialog service.
        /// </summary>
        public static ModelAuditResult? LastResult { get; private set; }
    }

    public sealed class ModelAuditResult
    {
        public string DocumentTitle { get; set; } = string.Empty;
        public bool IsWorkshared { get; set; }
        public IReadOnlyList<ModelWarningInfo> Warnings { get; set; } = new List<ModelWarningInfo>();
        public IReadOnlyList<FamilyInfo> Families { get; set; } = new List<FamilyInfo>();
        public IReadOnlyList<ViewInfo> UnplacedViews { get; set; } = new List<ViewInfo>();
        public IReadOnlyList<ElementInfo> OrphanElements { get; set; } = new List<ElementInfo>();
    }
}
