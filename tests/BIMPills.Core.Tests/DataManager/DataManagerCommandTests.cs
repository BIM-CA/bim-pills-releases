using BIMPills.Commands.DataManager;
using BIMPills.Core.Audit;
using BIMPills.Core.Commands;
using BIMPills.Core.Documentacion;
using BIMPills.Core.Models;
using BIMPills.Core.Services;
using System;
using System.Collections.Generic;
using Xunit;

namespace BIMPills.Core.Tests.DataManager
{
    public class DataManagerCommandTests
    {
        [Fact]
        public void Execute_WithSchedules_ReturnsSuccessWithCount()
        {
            var context = new FakeDataManagerContext(new FakeDataManagerDocumentServices
            {
                Schedules = new List<ScheduleInfo>
                {
                    new ScheduleInfo { Id = 1, Name = "Tabla de Puertas",  CategoryName = "Puertas",  RowCount = 10 },
                    new ScheduleInfo { Id = 2, Name = "Tabla de Ventanas", CategoryName = "Ventanas", RowCount = 5  }
                }
            });

            var command = new DataManagerCommand();
            var result  = command.Execute(context);

            Assert.True(result.Success);
            Assert.Contains("2", result.Message);
        }

        [Fact]
        public void Execute_WithEmptyScheduleList_ReturnsSuccessWithZero()
        {
            var context = new FakeDataManagerContext(new FakeDataManagerDocumentServices());
            var command = new DataManagerCommand();
            var result  = command.Execute(context);

            Assert.True(result.Success);
            Assert.Contains("0", result.Message);
        }

        [Fact]
        public void Execute_NullSchedules_ReturnsFailure()
        {
            var context = new FakeDataManagerContext(new NullSchedulesDocumentServices());
            var command = new DataManagerCommand();
            var result  = command.Execute(context);

            Assert.False(result.Success);
            Assert.Contains("No se pudo", result.Message);
        }
    }

    // ── Test doubles ────────────────────────────────────────────────────────────

    internal sealed class FakeDataManagerContext : ICommandContext
    {
        public IDocumentServices Document { get; }
        public ILogger Logger { get; } = new NullDataManagerLogger();

        public FakeDataManagerContext(IDocumentServices doc) { Document = doc; }
    }

    internal sealed class FakeDataManagerDocumentServices : IDocumentServices
    {
        public string Title      => "TestModel.rvt";
        public bool IsWorkshared => false;

        public IReadOnlyList<ScheduleInfo> Schedules { get; set; } = new List<ScheduleInfo>();

        public long GetModelFileSize()       => 50_000_000;
        public int  GetTotalElementCount()   => 10_000;

        public IReadOnlyList<ModelWarningInfo>  GetWarnings()                => new List<ModelWarningInfo>();
        public IReadOnlyList<FamilyInfo>        GetFamilySizes()             => new List<FamilyInfo>();
        public IReadOnlyList<ViewInfo>          GetUnplacedViews()           => new List<ViewInfo>();
        public IReadOnlyList<ElementInfo>       GetElementsWithoutCategory() => new List<ElementInfo>();
        public IReadOnlyList<PurgeableItem>     GetPurgeableElements()       => new List<PurgeableItem>();
        public int PurgeElements(IReadOnlyList<long> ids)                    => ids?.Count ?? 0;
        public IReadOnlyList<FamilyExportInfo>  GetLoadedFamilies()          => new List<FamilyExportInfo>();
        public bool ExportFamily(long familyId, string path)                 => true;
        public IReadOnlyList<Core.Gestion.WorksetInfo> GetWorksets()         => new List<Core.Gestion.WorksetInfo>();
        public bool CreateWorkset(string name)                               => true;
        public bool RenameWorkset(long worksetId, string newName)            => true;
        public IReadOnlyList<DimensionTypeInfo> GetDimensionTypes()          => new List<DimensionTypeInfo>();
        public int  GetDoorCountInActiveView()                               => 0;
        public string GetActiveViewName()                                    => "Level 1";
        public int  GetGridCountInActiveView()                               => 0;
        public int  GetWallCountInActiveView()                               => 0;
        public int  GetArqLevelCount()                                       => 0;
        public IReadOnlyList<Models.SheetExportInfo> GetSheets()             => new List<Models.SheetExportInfo>();
        public IReadOnlyList<Models.ExportableViewInfo> GetExportableViews() => new List<Models.ExportableViewInfo>();
        public string GetProjectName()                                       => "TestProject";
        public IReadOnlyList<ScheduleInfo> GetSchedules()                    => Schedules;
        public ScheduleData GetScheduleData(long scheduleId)                 => new ScheduleData();
        public ParameterUpdateResult ApplyParameterUpdates(IReadOnlyList<ParameterUpdateRequest> u)
            => new ParameterUpdateResult();
    }

    /// <summary>Returns null from GetSchedules() to exercise the failure branch.</summary>
    internal sealed class NullSchedulesDocumentServices : IDocumentServices
    {
        public string Title      => "TestModel.rvt";
        public bool IsWorkshared => false;

        public long GetModelFileSize()       => 0;
        public int  GetTotalElementCount()   => 0;

        public IReadOnlyList<ModelWarningInfo>  GetWarnings()                => new List<ModelWarningInfo>();
        public IReadOnlyList<FamilyInfo>        GetFamilySizes()             => new List<FamilyInfo>();
        public IReadOnlyList<ViewInfo>          GetUnplacedViews()           => new List<ViewInfo>();
        public IReadOnlyList<ElementInfo>       GetElementsWithoutCategory() => new List<ElementInfo>();
        public IReadOnlyList<PurgeableItem>     GetPurgeableElements()       => new List<PurgeableItem>();
        public int PurgeElements(IReadOnlyList<long> ids)                    => 0;
        public IReadOnlyList<FamilyExportInfo>  GetLoadedFamilies()          => new List<FamilyExportInfo>();
        public bool ExportFamily(long familyId, string path)                 => false;
        public IReadOnlyList<Core.Gestion.WorksetInfo> GetWorksets()         => new List<Core.Gestion.WorksetInfo>();
        public bool CreateWorkset(string name)                               => false;
        public bool RenameWorkset(long worksetId, string newName)            => false;
        public IReadOnlyList<DimensionTypeInfo> GetDimensionTypes()          => new List<DimensionTypeInfo>();
        public int  GetDoorCountInActiveView()                               => 0;
        public string GetActiveViewName()                                    => "";
        public int  GetGridCountInActiveView()                               => 0;
        public int  GetWallCountInActiveView()                               => 0;
        public int  GetArqLevelCount()                                       => 0;
        public IReadOnlyList<Models.SheetExportInfo> GetSheets()             => new List<Models.SheetExportInfo>();
        public IReadOnlyList<Models.ExportableViewInfo> GetExportableViews() => new List<Models.ExportableViewInfo>();
        public string GetProjectName()                                       => "";
        public IReadOnlyList<ScheduleInfo> GetSchedules()                    => null!;  // triggers failure branch
        public ScheduleData GetScheduleData(long scheduleId)                 => null!;
        public ParameterUpdateResult ApplyParameterUpdates(IReadOnlyList<ParameterUpdateRequest> u)
            => new ParameterUpdateResult();
    }

    internal sealed class NullDataManagerLogger : ILogger
    {
        public void Info(string message)    { }
        public void Warning(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
