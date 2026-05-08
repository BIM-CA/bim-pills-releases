using BIMPills.Commands.About;
using BIMPills.Core.Commands;
using BIMPills.Core.Services;
using System;
using Xunit;

namespace BIMPills.Core.Tests.About
{
    public class AboutCommandTests
    {
        [Fact]
        public void Execute_ReturnsSuccess()
        {
            var context = new FakeAboutContext();
            var command = new AboutCommand();
            var result  = command.Execute(context);

            Assert.True(result.Success);
        }

        [Fact]
        public void Execute_PopulatesLastResult()
        {
            var context = new FakeAboutContext();
            var command = new AboutCommand();
            command.Execute(context);

            Assert.NotNull(AboutCommand.LastResult);
            Assert.Equal("BIM Pills", AboutCommand.LastResult!.PluginName);
            Assert.NotEmpty(AboutCommand.LastResult.Version);
            Assert.NotEqual("0.0.0", AboutCommand.LastResult.Version);
            Assert.Equal("MBA Arq. Rodrigo Flores", AboutCommand.LastResult.Developer);
            Assert.Equal("BIM-CA (Prototype, S.A.)", AboutCommand.LastResult.Company);
            Assert.Equal("https://bim-ca.com", AboutCommand.LastResult.Website);
        }

        [Fact]
        public void Execute_MessageContainsPluginNameAndVersion()
        {
            var context = new FakeAboutContext();
            var command = new AboutCommand();
            var result  = command.Execute(context);

            Assert.Contains("BIM Pills", result.Message);
            Assert.Contains("beta", result.Message);
            Assert.Contains("BIM-CA", result.Message);
        }
    }

    // â”€â”€ Test doubles â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    internal sealed class FakeAboutContext : ICommandContext
    {
        public IDocumentServices Document { get; } = new FakeAboutDocumentServices();
        public ILogger Logger { get; } = new NullAboutLogger();
    }

    internal sealed class FakeAboutDocumentServices : IDocumentServices
    {
        public string Title => "TestModel.rvt";
        public bool IsWorkshared => false;

        public long GetModelFileSize() => 50_000_000;
        public int GetTotalElementCount() => 10_000;
        public System.Collections.Generic.IReadOnlyList<Audit.ModelWarningInfo> GetWarnings()
            => new System.Collections.Generic.List<Audit.ModelWarningInfo>();
        public System.Collections.Generic.IReadOnlyList<Audit.FamilyInfo> GetFamilySizes()
            => new System.Collections.Generic.List<Audit.FamilyInfo>();
        public System.Collections.Generic.IReadOnlyList<Audit.ViewInfo> GetUnplacedViews()
            => new System.Collections.Generic.List<Audit.ViewInfo>();
        public System.Collections.Generic.IReadOnlyList<Audit.ElementInfo> GetElementsWithoutCategory()
            => new System.Collections.Generic.List<Audit.ElementInfo>();
        public System.Collections.Generic.IReadOnlyList<Audit.PurgeableItem> GetPurgeableElements()
            => new System.Collections.Generic.List<Audit.PurgeableItem>();
        public int PurgeElements(System.Collections.Generic.IReadOnlyList<long> elementIds)
            => elementIds?.Count ?? 0;
        public System.Collections.Generic.IReadOnlyList<Audit.FamilyExportInfo> GetLoadedFamilies()
            => new System.Collections.Generic.List<Audit.FamilyExportInfo>();
        public bool ExportFamily(long familyId, string destinationPath)
            => true;
        public System.Collections.Generic.IReadOnlyList<BIMPills.Core.Gestion.WorksetInfo> GetWorksets()
            => new System.Collections.Generic.List<BIMPills.Core.Gestion.WorksetInfo>();
        public bool CreateWorkset(string name) => true;
        public bool RenameWorkset(long worksetId, string newName) => true;
        public System.Collections.Generic.IReadOnlyList<BIMPills.Core.Documentacion.DimensionTypeInfo> GetDimensionTypes()
            => new System.Collections.Generic.List<BIMPills.Core.Documentacion.DimensionTypeInfo>();
        public int GetDoorCountInActiveView() => 0;
        public string GetActiveViewName() => "Level 1";
        public int GetGridCountInActiveView() => 0;
        public int GetWallCountInActiveView() => 0;
        public int GetArqLevelCount() => 0;
        public System.Collections.Generic.IReadOnlyList<Models.SheetExportInfo> GetSheets()
            => new System.Collections.Generic.List<Models.SheetExportInfo>();
        public System.Collections.Generic.IReadOnlyList<Models.ExportableViewInfo> GetExportableViews()
            => new System.Collections.Generic.List<Models.ExportableViewInfo>();
        public string GetProjectName() => "TestProject";
        public string GetModelIdentifier() => "TestModel.rvt";
        public System.Collections.Generic.IReadOnlyList<Models.ScheduleInfo> GetSchedules()                             => new System.Collections.Generic.List<Models.ScheduleInfo>();
        public Models.ScheduleData GetScheduleData(long scheduleId) => GetScheduleData(scheduleId, false);
        public Models.ScheduleData GetScheduleData(long scheduleId, bool includeLinks)                                                     => new Models.ScheduleData();
        public Models.ParameterUpdateResult ApplyParameterUpdates(System.Collections.Generic.IReadOnlyList<Models.ParameterUpdateRequest> u) => new Models.ParameterUpdateResult();
    }

    internal sealed class NullAboutLogger : ILogger
    {
        public void Info(string message)    { }
        public void Warning(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
