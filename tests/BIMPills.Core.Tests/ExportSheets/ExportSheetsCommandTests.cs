using BIMPills.Commands.ExportSheets;
using BIMPills.Core.Audit;
using BIMPills.Core.Commands;
using BIMPills.Core.Documentacion;
using BIMPills.Core.Services;
using System;
using System.Collections.Generic;
using Xunit;

namespace BIMPills.Core.Tests.ExportSheets
{
    public class ExportSheetsCommandTests
    {
        [Fact]
        public void Execute_WithValidDocument_ReturnsSuccess()
        {
            var context = new FakeSheetContext(new FakeSheetDocServices());
            var command = new ExportSheetsCommand();
            var result = command.Execute(context);

            Assert.True(result.Success);
        }

        [Fact]
        public void Execute_StoresLastResultWithProjectName()
        {
            var context = new FakeSheetContext(new FakeSheetDocServices());
            var command = new ExportSheetsCommand();
            command.Execute(context);

            Assert.NotNull(ExportSheetsCommand.LastResult);
            Assert.Equal("TestProject", ExportSheetsCommand.LastResult.ProjectName);
        }

        [Fact]
        public void Execute_NullContext_Throws()
        {
            var command = new ExportSheetsCommand();
            Assert.Throws<NullReferenceException>(() => command.Execute(null!));
        }
    }

    internal sealed class FakeSheetContext : ICommandContext
    {
        public IDocumentServices Document { get; }
        public ILogger Logger { get; } = new NullSheetLogger();
        public FakeSheetContext(IDocumentServices doc) { Document = doc; }
    }

    internal sealed class FakeSheetDocServices : IDocumentServices
    {
        public string Title => "TestModel.rvt";
        public bool IsWorkshared => false;
        public long GetModelFileSize() => 50_000_000;
        public int GetTotalElementCount() => 1000;
        public IReadOnlyList<ModelWarningInfo> GetWarnings() => new List<ModelWarningInfo>();
        public IReadOnlyList<FamilyInfo> GetFamilySizes() => new List<FamilyInfo>();
        public IReadOnlyList<ViewInfo> GetUnplacedViews() => new List<ViewInfo>();
        public IReadOnlyList<ElementInfo> GetElementsWithoutCategory() => new List<ElementInfo>();
        public IReadOnlyList<PurgeableItem> GetPurgeableElements() => new List<PurgeableItem>();
        public int PurgeElements(IReadOnlyList<long> ids) => ids?.Count ?? 0;
        public IReadOnlyList<FamilyExportInfo> GetLoadedFamilies() => new List<FamilyExportInfo>();
        public bool ExportFamily(long familyId, string path) => true;
        public IReadOnlyList<BIMPills.Core.Gestion.WorksetInfo> GetWorksets() => new List<BIMPills.Core.Gestion.WorksetInfo>();
        public bool CreateWorkset(string name) => true;
        public bool RenameWorkset(long worksetId, string newName) => true;
        public IReadOnlyList<DimensionTypeInfo> GetDimensionTypes() => new List<DimensionTypeInfo>();
        public int GetDoorCountInActiveView() => 0;
        public string GetActiveViewName() => "Level 1";
        public int GetGridCountInActiveView() => 0;
        public int GetWallCountInActiveView() => 0;
        public int GetArqLevelCount() => 0;
        public IReadOnlyList<Models.SheetExportInfo> GetSheets() => new List<Models.SheetExportInfo>();
        public IReadOnlyList<Models.ExportableViewInfo> GetExportableViews() => new List<Models.ExportableViewInfo>();
        public string GetProjectName() => "TestProject";
        public string GetModelIdentifier() => "TestModel.rvt";
        public IReadOnlyList<Models.ScheduleInfo> GetSchedules() => new List<Models.ScheduleInfo>();
        public Models.ScheduleData GetScheduleData(long scheduleId) => new Models.ScheduleData();
        public Models.ParameterUpdateResult ApplyParameterUpdates(IReadOnlyList<Models.ParameterUpdateRequest> u)
            => new Models.ParameterUpdateResult();
    }

    internal sealed class NullSheetLogger : ILogger
    {
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
