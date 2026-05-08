using BIMPills.Commands.Ordering;
using BIMPills.Core.Audit;
using BIMPills.Core.Commands;
using BIMPills.Core.Documentacion;
using BIMPills.Core.Services;
using System;
using System.Collections.Generic;
using Xunit;

namespace BIMPills.Core.Tests.Ordering
{
    public class OrderingCommandTests
    {
        [Fact]
        public void Execute_WithValidDocument_ReturnsSuccess()
        {
            var context = new FakeOrderingContext(new FakeOrderingDocumentServices());
            var command = new OrderingCommand();
            var result  = command.Execute(context);

            Assert.True(result.Success);
            Assert.Contains("listo", result.Message);
        }

        [Fact]
        public void Execute_NullContext_ReturnsFailure()
        {
            var command = new OrderingCommand();
            var result  = command.Execute(null!);

            Assert.False(result.Success);
            Assert.Contains("No se pudo", result.Message);
        }

        [Fact]
        public void Execute_NullDocument_ReturnsFailure()
        {
            var context = new FakeOrderingContextWithNullDocument();
            var command = new OrderingCommand();
            var result  = command.Execute(context);

            Assert.False(result.Success);
            Assert.Contains("No se pudo", result.Message);
        }
    }

    // â”€â”€ Test doubles â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    internal sealed class FakeOrderingContext : ICommandContext
    {
        public IDocumentServices Document { get; }
        public ILogger Logger { get; } = new NullOrderingLogger();

        public FakeOrderingContext(IDocumentServices doc) { Document = doc; }
    }

    internal sealed class FakeOrderingContextWithNullDocument : ICommandContext
    {
        public IDocumentServices Document => null!;
        public ILogger Logger { get; } = new NullOrderingLogger();
    }

    internal sealed class FakeOrderingDocumentServices : IDocumentServices
    {
        public string Title       => "TestModel.rvt";
        public bool IsWorkshared  => false;

        public long GetModelFileSize()       => 50_000_000;
        public int  GetTotalElementCount()   => 10_000;

        public IReadOnlyList<ModelWarningInfo>  GetWarnings()              => new List<ModelWarningInfo>();
        public IReadOnlyList<FamilyInfo>        GetFamilySizes()           => new List<FamilyInfo>();
        public IReadOnlyList<ViewInfo>          GetUnplacedViews()         => new List<ViewInfo>();
        public IReadOnlyList<ElementInfo>       GetElementsWithoutCategory() => new List<ElementInfo>();
        public IReadOnlyList<PurgeableItem>     GetPurgeableElements()     => new List<PurgeableItem>();
        public int PurgeElements(IReadOnlyList<long> ids)                  => ids?.Count ?? 0;
        public IReadOnlyList<FamilyExportInfo>  GetLoadedFamilies()        => new List<FamilyExportInfo>();
        public bool ExportFamily(long familyId, string path)               => true;
        public IReadOnlyList<Core.Gestion.WorksetInfo> GetWorksets()       => new List<Core.Gestion.WorksetInfo>();
        public bool CreateWorkset(string name)                             => true;
        public bool RenameWorkset(long worksetId, string newName)          => true;
        public IReadOnlyList<DimensionTypeInfo> GetDimensionTypes()        => new List<DimensionTypeInfo>();
        public int  GetDoorCountInActiveView()                             => 0;
        public string GetActiveViewName()                                  => "Level 1";
        public int  GetGridCountInActiveView()                             => 0;
        public int  GetWallCountInActiveView()                             => 0;
        public int  GetArqLevelCount()                                     => 0;
        public IReadOnlyList<Models.SheetExportInfo> GetSheets()           => new List<Models.SheetExportInfo>();
        public IReadOnlyList<Models.ExportableViewInfo> GetExportableViews() => new List<Models.ExportableViewInfo>();
        public string GetProjectName()                                     => "TestProject";
        public string GetModelIdentifier()                                 => "TestModel.rvt";
        public IReadOnlyList<Models.ScheduleInfo> GetSchedules()           => new List<Models.ScheduleInfo>();
        public Models.ScheduleData GetScheduleData(long scheduleId) => GetScheduleData(scheduleId, false);
        public Models.ScheduleData GetScheduleData(long scheduleId, bool includeLinks)        => new Models.ScheduleData();
        public Models.ParameterUpdateResult ApplyParameterUpdates(IReadOnlyList<Models.ParameterUpdateRequest> u)
            => new Models.ParameterUpdateResult();
    }

    internal sealed class NullOrderingLogger : ILogger
    {
        public void Info(string message)    { }
        public void Warning(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
