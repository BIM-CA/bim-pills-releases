using BIMPills.Commands.ModelAudit;
using BIMPills.Core.Audit;
using BIMPills.Core.Commands;
using BIMPills.Core.Documentacion;
using BIMPills.Core.Services;
using System.Collections.Generic;
using Xunit;

namespace BIMPills.Core.Tests.ModelAudit
{
    public class ModelAuditCommandTests
    {
        [Fact]
        public void Execute_WithWarnings_ReturnsSuccessAndPopulatesLastResult()
        {
            var context = new FakeCommandContext(new FakeDocumentServices
            {
                Warnings = new List<ModelWarningInfo>
                {
                    new ModelWarningInfo("Elementos superpuestos", "Error", 4),
                    new ModelWarningInfo("Advertencia de habitabilidad", "Warning", 1)
                }
            });

            var command = new ModelAuditCommand();
            var result  = command.Execute(context);

            Assert.True(result.Success);
            Assert.NotNull(ModelAuditCommand.LastResult);
            Assert.Equal(2, ModelAuditCommand.LastResult!.Warnings.Count);
        }

        [Fact]
        public void Execute_EmptyModel_ReturnsSuccessWithZeroCounts()
        {
            var context = new FakeCommandContext(new FakeDocumentServices());
            var result  = new ModelAuditCommand().Execute(context);

            Assert.True(result.Success);
            Assert.Empty(ModelAuditCommand.LastResult!.Warnings);
            Assert.Empty(ModelAuditCommand.LastResult!.UnplacedViews);
        }

        [Fact]
        public void Execute_PopulatesHealthScore()
        {
            var context = new FakeCommandContext(new FakeDocumentServices
            {
                Warnings = new List<ModelWarningInfo>
                {
                    new ModelWarningInfo("Test warning", "Warning", 1)
                },
                ModelFileSize = 100_000_000, // 100 MB
                ElementCount = 50_000
            });

            new ModelAuditCommand().Execute(context);
            var health = ModelAuditCommand.LastResult!.HealthScore;

            Assert.NotNull(health);
            Assert.True(health.TotalScore > 0);
            Assert.Equal(HealthLevel.Excelente, health.Level);
        }

        [Fact]
        public void Execute_UnhealthyModel_ScoresLow()
        {
            var warnings = new List<ModelWarningInfo>();
            for (int i = 0; i < 600; i++)
                warnings.Add(new ModelWarningInfo($"Warning {i}", "Error", 2));

            var views = new List<ViewInfo>();
            for (int i = 0; i < 80; i++)
                views.Add(new ViewInfo($"Vista {i}", "Floor Plan", false));

            var purgeables = new List<PurgeableItem>();
            for (int i = 0; i < 150; i++)
                purgeables.Add(new PurgeableItem(i, $"Purgeable {i}", "General", "Familia", 1000));

            var context = new FakeCommandContext(new FakeDocumentServices
            {
                Warnings = warnings,
                ModelFileSize = 1_500_000_000, // 1.5 GB
                ElementCount = 3_000_000,
                Views = views,
                Purgeables = purgeables
            });

            new ModelAuditCommand().Execute(context);
            var health = ModelAuditCommand.LastResult!.HealthScore;

            Assert.Equal(HealthLevel.Cr\u00EDtico, health.Level);
            Assert.True(health.TotalScore < 40);
        }
    }

    // ── Test doubles ────────────────────────────────────────────────────────────

    internal sealed class FakeCommandContext : ICommandContext
    {
        public IDocumentServices Document { get; }
        public ILogger Logger { get; } = new NullLogger();

        public FakeCommandContext(IDocumentServices doc) { Document = doc; }
    }

    internal sealed class FakeDocumentServices : IDocumentServices
    {
        public string Title { get; set; } = "TestModel.rvt";
        public bool IsWorkshared { get; set; } = false;
        public long ModelFileSize { get; set; } = 50_000_000; // 50 MB
        public int ElementCount { get; set; } = 10_000;

        public IReadOnlyList<ModelWarningInfo> Warnings    { get; set; } = new List<ModelWarningInfo>();
        public IReadOnlyList<FamilyInfo>       FamilyList  { get; set; } = new List<FamilyInfo>();
        public IReadOnlyList<ViewInfo>         Views       { get; set; } = new List<ViewInfo>();
        public IReadOnlyList<ElementInfo>      Orphans     { get; set; } = new List<ElementInfo>();
        public IReadOnlyList<PurgeableItem>    Purgeables  { get; set; } = new List<PurgeableItem>();
        public IReadOnlyList<FamilyExportInfo> LoadedFamilies { get; set; } = new List<FamilyExportInfo>();

        public long GetModelFileSize() => ModelFileSize;
        public int GetTotalElementCount() => ElementCount;
        public IReadOnlyList<ModelWarningInfo> GetWarnings()             => Warnings;
        public IReadOnlyList<FamilyInfo>       GetFamilySizes()          => FamilyList;
        public IReadOnlyList<ViewInfo>         GetUnplacedViews()        => Views;
        public IReadOnlyList<ElementInfo>      GetElementsWithoutCategory() => Orphans;
        public IReadOnlyList<PurgeableItem>    GetPurgeableElements()    => Purgeables;
        public int PurgeElements(IReadOnlyList<long> elementIds)           => elementIds?.Count ?? 0;
        public IReadOnlyList<FamilyExportInfo> GetLoadedFamilies()       => LoadedFamilies;
        public bool ExportFamily(long familyId, string destinationPath)  => true;
        public IReadOnlyList<Core.Gestion.WorksetInfo> GetWorksets()     => new List<Core.Gestion.WorksetInfo>();
        public bool CreateWorkset(string name)                           => true;
        public bool RenameWorkset(long worksetId, string newName)        => true;

        // Documentación
        public IReadOnlyList<DimensionTypeInfo> GetDimensionTypes()      => new List<DimensionTypeInfo>
        {
            new DimensionTypeInfo(1, "Linear - 2.5mm Arial")
        };
        public int GetDoorCountInActiveView()                            => 5;
        public string GetActiveViewName()                                => "Level 1";
        public int GetGridCountInActiveView()                            => 4;
        public int GetWallCountInActiveView()                            => 8;

        public int GetArqLevelCount()                                        => 3;

        // Export Sheets
        public IReadOnlyList<Models.SheetExportInfo> GetSheets()           => new List<Models.SheetExportInfo>();
        public IReadOnlyList<Models.ExportableViewInfo> GetExportableViews() => new List<Models.ExportableViewInfo>();
        public string GetProjectName()                                     => "TestProject";
        public string GetModelIdentifier()                                 => "TestModel.rvt";
        // SheetLink
        public IReadOnlyList<Models.ScheduleInfo> GetSchedules()                                               => new List<Models.ScheduleInfo>();
        public Models.ScheduleData GetScheduleData(long scheduleId)                                           => new Models.ScheduleData();
        public Models.ParameterUpdateResult ApplyParameterUpdates(IReadOnlyList<Models.ParameterUpdateRequest> u) => new Models.ParameterUpdateResult();
    }

    internal sealed class NullLogger : ILogger
    {
        public void Info(string message)    { }
        public void Warning(string message) { }
        public void Error(string message, System.Exception? exception = null) { }
    }
}
