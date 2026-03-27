using BIMPills.Core.Audit;
using BIMPills.Core.Commands;
using System.Collections.Generic;
using System.Linq;

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

            var families = doc.GetFamilySizes();
            var warnings = doc.GetWarnings();
            var fileSizeBytes = doc.GetModelFileSize();
            var totalElements = doc.GetTotalElementCount();
            var largestFamilyMB = families.Count > 0
                ? families.Max(f => f.SizeMB)
                : 0;

            var unplacedViews = doc.GetUnplacedViews();
            var purgeableItems = doc.GetPurgeableElements();

            var healthScore = new ModelHealthScore(
                warningsCount: warnings.Count,
                fileSizeMB: fileSizeBytes / 1_048_576.0,
                largestFamilyMB: largestFamilyMB,
                totalElements: totalElements,
                unplacedViewsCount: unplacedViews.Count,
                purgeableCount: purgeableItems.Count);

            var result = new ModelAuditResult
            {
                DocumentTitle   = doc.Title,
                IsWorkshared    = doc.IsWorkshared,
                Warnings        = warnings,
                Families        = families,
                UnplacedViews   = unplacedViews,
                OrphanElements  = doc.GetElementsWithoutCategory(),
                PurgeableItems  = purgeableItems,
                HealthScore     = healthScore,
                FileSizeBytes   = fileSizeBytes,
                TotalElements   = totalElements
            };

            context.Logger.Info(
                $"Auditoría completada — Salud: {healthScore.TotalScore}/100 ({healthScore.LevelLabel}), " +
                $"{warnings.Count} advertencias, {families.Count} familias, " +
                $"{result.PurgeableItems.Count} elementos purgables.");

            LastResult = result;
            return CommandResult.Ok($"Auditoría completada: {healthScore.LevelLabel} ({healthScore.TotalScore}/100)");
        }

        public static ModelAuditResult? LastResult { get; private set; }
    }

    public sealed class ModelAuditResult
    {
        public string DocumentTitle { get; set; } = string.Empty;
        public bool IsWorkshared { get; set; }
        public long FileSizeBytes { get; set; }
        public int TotalElements { get; set; }
        public ModelHealthScore HealthScore { get; set; } = null!;
        public IReadOnlyList<ModelWarningInfo> Warnings { get; set; } = new List<ModelWarningInfo>();
        public IReadOnlyList<FamilyInfo> Families { get; set; } = new List<FamilyInfo>();
        public IReadOnlyList<ViewInfo> UnplacedViews { get; set; } = new List<ViewInfo>();
        public IReadOnlyList<ElementInfo> OrphanElements { get; set; } = new List<ElementInfo>();
        public IReadOnlyList<PurgeableItem> PurgeableItems { get; set; } = new List<PurgeableItem>();

        public string FileSizeLabel =>
            FileSizeBytes >= 1_073_741_824 ? $"{FileSizeBytes / 1_073_741_824.0:F1} GB" :
            FileSizeBytes >= 1_048_576     ? $"{FileSizeBytes / 1_048_576.0:F1} MB" :
            FileSizeBytes >= 1_024         ? $"{FileSizeBytes / 1_024.0:F1} KB" :
                                              $"{FileSizeBytes} B";
    }
}
