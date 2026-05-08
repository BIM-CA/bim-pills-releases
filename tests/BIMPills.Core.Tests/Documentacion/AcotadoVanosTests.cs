using BIMPills.Commands.Documentacion;
using BIMPills.Core.Audit;
using BIMPills.Core.Commands;
using BIMPills.Core.Documentacion;
using BIMPills.Core.Services;
using System.Collections.Generic;
using Xunit;

namespace BIMPills.Core.Tests.Documentacion
{
    // â”€â”€ AcotadoVanosResult â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public class AcotadoVanosResultTests
    {
        [Fact]
        public void Constructor_SuccessPath_SetsProperties()
        {
            var result = new AcotadoVanosResult(5, 7, 2, "OK");

            Assert.Equal(5, result.DimensionsCreated);
            Assert.Equal(7, result.DoorsProcessed);
            Assert.Equal(2, result.DoorsSkipped);
            Assert.Equal("OK", result.Message);
            Assert.Null(result.ErrorMessage);
            Assert.Null(result.SkippedItems);
        }

        [Fact]
        public void CreatedCount_AliasMatchesDimensionsCreated()
        {
            var result = new AcotadoVanosResult(3, 3, 0, "done");

            Assert.Equal(result.DimensionsCreated, result.CreatedCount);
        }

        [Fact]
        public void Constructor_WithSkippedItems_PopulatesSkippedItems()
        {
            var skipped = new List<string> { "Puerta 101: sin host", "Puerta 102: sin geometrÃ­a" };
            var result = new AcotadoVanosResult(4, 6, 2, "Parcial", skipped);

            Assert.Equal(2, result.SkippedItems!.Count);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public void CreateError_SetsErrorMessageAndZeroCounts()
        {
            var result = AcotadoVanosResult.CreateError("Fallo crÃ­tico");

            Assert.Equal("Fallo crÃ­tico", result.ErrorMessage);
            Assert.Equal(0, result.DimensionsCreated);
            Assert.Equal(0, result.DoorsProcessed);
            Assert.Equal(0, result.DoorsSkipped);
        }

        [Fact]
        public void CreateError_ErrorMessageAlsoInMessage()
        {
            var result = AcotadoVanosResult.CreateError("Sin tipos de cota");

            Assert.Equal("Sin tipos de cota", result.Message);
        }

        [Fact]
        public void CreateWithSkipped_SetsSkippedCountFromList()
        {
            var skipped = new List<string> { "A", "B", "C" };
            var result = AcotadoVanosResult.CreateWithSkipped(10, 13, "Parcial", skipped);

            Assert.Equal(10, result.DimensionsCreated);
            Assert.Equal(13, result.DoorsProcessed);
            Assert.Equal(3, result.DoorsSkipped);
            Assert.Equal(3, result.SkippedItems!.Count);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public void CreateWithSkipped_NullSkipped_DoorsSkippedIsZero()
        {
            var result = AcotadoVanosResult.CreateWithSkipped(5, 5, "OK", null!);

            Assert.Equal(0, result.DoorsSkipped);
        }
    }

    // â”€â”€ AcotadoVanosData â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public class AcotadoVanosDataTests
    {
        [Fact]
        public void Constructor_SetsAllProperties()
        {
            var dimTypes = new List<DimensionTypeInfo> { new DimensionTypeInfo(1, "Linear 2.5mm") };
            var data = new AcotadoVanosData(10, dimTypes, "Planta Baja", gridCount: 6, wallCount: 14);

            Assert.Equal(10, data.DoorCount);
            Assert.Equal(6, data.GridCount);
            Assert.Equal(14, data.WallCount);
            Assert.Equal("Planta Baja", data.ActiveViewName);
            Assert.Single(data.DimensionTypes);
        }

        [Fact]
        public void Constructor_DefaultGridAndWallCountIsZero()
        {
            var data = new AcotadoVanosData(3, new List<DimensionTypeInfo>(), "Vista 1");

            Assert.Equal(0, data.GridCount);
            Assert.Equal(0, data.WallCount);
        }

        [Fact]
        public void AvailableSchemes_HasFiveEntries()
        {
            Assert.Equal(5, AcotadoVanosData.AvailableSchemes.Count);
        }

        [Fact]
        public void AvailableSchemes_ContainsExpectedIds()
        {
            var ids = new HashSet<string>();
            foreach (var s in AcotadoVanosData.AvailableSchemes) ids.Add(s.Id);

            Assert.Contains("opening-width", ids);
            Assert.Contains("grid-combined", ids);
            Assert.Contains("interior-spaces", ids);
            Assert.Contains("arq-levels", ids);
            Assert.Contains("exterior-walls", ids);
        }
    }

    // â”€â”€ SchemeOptionInfo â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public class SchemeOptionInfoTests
    {
        [Fact]
        public void Constructor_SetsIdNameDescription()
        {
            var s = new SchemeOptionInfo("opening-width", "Anchos de vanos", "Cota el ancho de cada vano");

            Assert.Equal("opening-width", s.Id);
            Assert.Equal("Anchos de vanos", s.Name);
            Assert.Equal("Cota el ancho de cada vano", s.Description);
        }
    }

    // â”€â”€ AcotadoVanosSettings â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public class AcotadoVanosSettingsTests
    {
        [Fact]
        public void Defaults_AreCorrect()
        {
            var s = new AcotadoVanosSettings();

            Assert.Equal("opening-width", s.Scheme);
            Assert.Equal(0, s.DimensionTypeId);
            Assert.Equal(150, s.OffsetMm);
            Assert.True(s.UseActiveView);
            Assert.Equal("end", s.GridEndpoint);
        }

        [Fact]
        public void Properties_AreWritable()
        {
            var s = new AcotadoVanosSettings
            {
                Scheme = "grid-combined",
                DimensionTypeId = 42,
                OffsetMm = 300,
                UseActiveView = false,
                GridEndpoint = "both"
            };

            Assert.Equal("grid-combined", s.Scheme);
            Assert.Equal(42, s.DimensionTypeId);
            Assert.Equal(300, s.OffsetMm);
            Assert.False(s.UseActiveView);
            Assert.Equal("both", s.GridEndpoint);
        }
    }

    // â”€â”€ DimensionTypeInfo â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public class DimensionTypeInfoTests
    {
        [Fact]
        public void Constructor_SetsIdAndName()
        {
            var d = new DimensionTypeInfo(99, "Linear 3.5mm");

            Assert.Equal(99, d.Id);
            Assert.Equal("Linear 3.5mm", d.Name);
        }

        [Fact]
        public void ToString_ReturnsName()
        {
            var d = new DimensionTypeInfo(1, "Linear 2.5mm");

            Assert.Equal("Linear 2.5mm", d.ToString());
        }
    }

    // â”€â”€ AcotadoVanosCommand â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // AcotadoVanosCommand exposes its last result through a static property, so
    // test classes that exercise Execute() must run serially to avoid races when
    // xUnit parallelises test classes.

    [Collection("AcotadoVanosCommandLastResult")]
    public class AcotadoVanosCommandTests
    {
        [Fact]
        public void Execute_HappyPath_ReturnsSuccessAndPopulatesLastResult()
        {
            var context = new FakeAcotadoContext(new FakeAcotadoDocServices());
            var command = new AcotadoVanosCommand();

            var result = command.Execute(context);

            Assert.True(result.Success);
            Assert.NotNull(AcotadoVanosCommand.LastResult);
            Assert.Equal(5, AcotadoVanosCommand.LastResult!.DoorCount);
            Assert.Equal(4, AcotadoVanosCommand.LastResult.GridCount);
            Assert.Equal(8, AcotadoVanosCommand.LastResult.WallCount);
            Assert.Equal("Level 1", AcotadoVanosCommand.LastResult.ActiveViewName);
        }

        [Fact]
        public void Execute_NoDimensionTypes_ReturnsFailure()
        {
            var docs = new FakeAcotadoDocServices { DimTypes = new List<DimensionTypeInfo>() };
            var context = new FakeAcotadoContext(docs);

            var result = new AcotadoVanosCommand().Execute(context);

            Assert.False(result.Success);
            Assert.Contains("tipos de cota", result.Message);
        }

        [Fact]
        public void Execute_PopulatesDimensionTypes()
        {
            var dimTypes = new List<DimensionTypeInfo>
            {
                new DimensionTypeInfo(1, "Linear 2.5mm"),
                new DimensionTypeInfo(2, "Linear 3.5mm")
            };
            var docs = new FakeAcotadoDocServices { DimTypes = dimTypes };
            var context = new FakeAcotadoContext(docs);

            new AcotadoVanosCommand().Execute(context);

            Assert.Equal(2, AcotadoVanosCommand.LastResult!.DimensionTypes.Count);
        }

        [Fact]
        public void Execute_ResultMessageContainsCounts()
        {
            var context = new FakeAcotadoContext(new FakeAcotadoDocServices());

            var result = new AcotadoVanosCommand().Execute(context);

            Assert.Contains("5", result.Message);   // door count
            Assert.Contains("4", result.Message);   // grid count
            Assert.Contains("8", result.Message);   // wall count
        }
    }

    // â”€â”€ AcotadoVanosCommand â€” extended coverage â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Collection("AcotadoVanosCommandLastResult")]
    public class AcotadoVanosCommandExtendedTests
    {
        [Fact]
        public void Execute_ZeroDoors_StillSucceeds()
        {
            var docs = new FakeAcotadoDocServices { DoorCount = 0 };
            var result = new AcotadoVanosCommand().Execute(new FakeAcotadoContext(docs));

            Assert.True(result.Success);
        }

        [Fact]
        public void Execute_ZeroGrids_StillSucceeds()
        {
            var docs = new FakeAcotadoDocServices { GridCount = 0 };
            var result = new AcotadoVanosCommand().Execute(new FakeAcotadoContext(docs));

            Assert.True(result.Success);
        }

        [Fact]
        public void Execute_ZeroWalls_StillSucceeds()
        {
            var docs = new FakeAcotadoDocServices { WallCount = 0 };
            var result = new AcotadoVanosCommand().Execute(new FakeAcotadoContext(docs));

            Assert.True(result.Success);
        }

        [Fact]
        public void Execute_SetsLastResultLevelCount()
        {
            var docs = new FakeAcotadoDocServices(); // GetArqLevelCount() = 3
            new AcotadoVanosCommand().Execute(new FakeAcotadoContext(docs));

            Assert.Equal(3, AcotadoVanosCommand.LastResult!.LevelCount);
        }

        [Fact]
        public void Execute_MessageContainsLevelCount()
        {
            var docs = new FakeAcotadoDocServices(); // GetArqLevelCount() = 3
            var result = new AcotadoVanosCommand().Execute(new FakeAcotadoContext(docs));

            Assert.Contains("3", result.Message); // niveles ARQ
        }

        [Fact]
        public void Execute_SetsLastResultActiveViewName()
        {
            var docs = new FakeAcotadoDocServices { ViewName = "Planta Baja - ARQ" };
            new AcotadoVanosCommand().Execute(new FakeAcotadoContext(docs));

            Assert.Equal("Planta Baja - ARQ", AcotadoVanosCommand.LastResult!.ActiveViewName);
        }

        [Fact]
        public void Execute_OverridesLastResultOnSuccessiveRuns()
        {
            var docs1 = new FakeAcotadoDocServices { DoorCount = 2, ViewName = "Vista A" };
            var docs2 = new FakeAcotadoDocServices { DoorCount = 7, ViewName = "Vista B" };

            new AcotadoVanosCommand().Execute(new FakeAcotadoContext(docs1));
            new AcotadoVanosCommand().Execute(new FakeAcotadoContext(docs2));

            Assert.Equal(7, AcotadoVanosCommand.LastResult!.DoorCount);
            Assert.Equal("Vista B", AcotadoVanosCommand.LastResult.ActiveViewName);
        }

        [Fact]
        public void Execute_MultipleDimensionTypes_AllPopulated()
        {
            var dimTypes = new List<DimensionTypeInfo>
            {
                new DimensionTypeInfo(1, "Linear 2.5mm"),
                new DimensionTypeInfo(2, "Linear 3.5mm"),
                new DimensionTypeInfo(3, "Diagonal 2mm")
            };
            var docs = new FakeAcotadoDocServices { DimTypes = dimTypes };
            new AcotadoVanosCommand().Execute(new FakeAcotadoContext(docs));

            Assert.Equal(3, AcotadoVanosCommand.LastResult!.DimensionTypes.Count);
        }
    }

    // â”€â”€ AcotadoVanosData â€” extended coverage â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public class AcotadoVanosDataExtendedTests
    {
        [Fact]
        public void Constructor_WithLevelCount_SetsLevelCount()
        {
            var data = new AcotadoVanosData(5, new List<DimensionTypeInfo>(), "Vista", 4, 10, levelCount: 6);

            Assert.Equal(6, data.LevelCount);
        }

        [Fact]
        public void Constructor_LevelCountDefaultIsZero()
        {
            var data = new AcotadoVanosData(0, new List<DimensionTypeInfo>(), "Vista");

            Assert.Equal(0, data.LevelCount);
        }

        [Fact]
        public void Constructor_EmptyViewName_IsValid()
        {
            var data = new AcotadoVanosData(0, new List<DimensionTypeInfo>(), "");

            Assert.Equal("", data.ActiveViewName);
        }

        [Fact]
        public void Constructor_ZeroAllCounts_IsValid()
        {
            var data = new AcotadoVanosData(0, new List<DimensionTypeInfo>(), "Vista", 0, 0, 0);

            Assert.Equal(0, data.DoorCount);
            Assert.Equal(0, data.GridCount);
            Assert.Equal(0, data.WallCount);
            Assert.Equal(0, data.LevelCount);
        }

        [Fact]
        public void AvailableSchemes_AllHaveNonEmptyNames()
        {
            foreach (var scheme in AcotadoVanosData.AvailableSchemes)
            {
                Assert.False(string.IsNullOrWhiteSpace(scheme.Name),
                    $"Scheme '{scheme.Id}' has empty name");
                Assert.False(string.IsNullOrWhiteSpace(scheme.Description),
                    $"Scheme '{scheme.Id}' has empty description");
            }
        }

        [Fact]
        public void AvailableSchemes_IdsAreUnique()
        {
            var ids = new HashSet<string>();
            foreach (var scheme in AcotadoVanosData.AvailableSchemes)
                Assert.True(ids.Add(scheme.Id), $"Duplicate scheme id: {scheme.Id}");
        }
    }

    // â”€â”€ AcotadoVanosSettings â€” extended coverage â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public class AcotadoVanosSettingsExtendedTests
    {
        [Fact]
        public void OffsetMm_NegativeValue_IsValid()
        {
            var s = new AcotadoVanosSettings { OffsetMm = -300 };
            Assert.Equal(-300, s.OffsetMm);
        }

        [Fact]
        public void OffsetMm_LargeValue_IsValid()
        {
            var s = new AcotadoVanosSettings { OffsetMm = 5000 };
            Assert.Equal(5000, s.OffsetMm);
        }

        [Fact]
        public void Scheme_AllAvailableIds_AreAssignable()
        {
            foreach (var scheme in AcotadoVanosData.AvailableSchemes)
            {
                var s = new AcotadoVanosSettings { Scheme = scheme.Id };
                Assert.Equal(scheme.Id, s.Scheme);
            }
        }
    }

    // â”€â”€ Test doubles â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    internal sealed class FakeAcotadoContext : ICommandContext
    {
        public IDocumentServices Document { get; }
        public ILogger Logger { get; } = new NullAcotadoLogger();
        public FakeAcotadoContext(IDocumentServices doc) { Document = doc; }
    }

    internal sealed class FakeAcotadoDocServices : IDocumentServices
    {
        public string Title { get; set; } = "TestModel.rvt";
        public bool IsWorkshared { get; set; } = false;
        public long ModelFileSize { get; set; } = 50_000_000;
        public int ElementCount { get; set; } = 10_000;
        public List<DimensionTypeInfo> DimTypes { get; set; } = new List<DimensionTypeInfo>
        {
            new DimensionTypeInfo(1, "Linear - 2.5mm Arial")
        };
        public int DoorCount  { get; set; } = 5;
        public int GridCount  { get; set; } = 4;
        public int WallCount  { get; set; } = 8;
        public string ViewName { get; set; } = "Level 1";

        public IReadOnlyList<ModelWarningInfo>   GetWarnings()             => new List<ModelWarningInfo>();
        public IReadOnlyList<FamilyInfo>         GetFamilySizes()          => new List<FamilyInfo>();
        public IReadOnlyList<ViewInfo>           GetUnplacedViews()        => new List<ViewInfo>();
        public IReadOnlyList<ElementInfo>        GetElementsWithoutCategory() => new List<ElementInfo>();
        public IReadOnlyList<PurgeableItem>      GetPurgeableElements()    => new List<PurgeableItem>();
        public int PurgeElements(IReadOnlyList<long> ids)                  => 0;
        public IReadOnlyList<Core.Models.SheetExportInfo> GetSheets()       => new List<Core.Models.SheetExportInfo>();
        public IReadOnlyList<Core.Models.ExportableViewInfo> GetExportableViews() => new List<Core.Models.ExportableViewInfo>();
        public string GetProjectName()                                     => "TestProject";
        public string GetModelIdentifier()                                 => "TestModel.rvt";
        public IReadOnlyList<Core.Models.ScheduleInfo> GetSchedules()                                          => new List<Core.Models.ScheduleInfo>();
        public Core.Models.ScheduleData GetScheduleData(long scheduleId) => GetScheduleData(scheduleId, false);
        public Core.Models.ScheduleData GetScheduleData(long scheduleId, bool includeLinks)                                      => new Core.Models.ScheduleData();
        public Core.Models.ParameterUpdateResult ApplyParameterUpdates(IReadOnlyList<Core.Models.ParameterUpdateRequest> u) => new Core.Models.ParameterUpdateResult();
        public IReadOnlyList<FamilyExportInfo>   GetLoadedFamilies()       => new List<FamilyExportInfo>();
        public bool ExportFamily(long id, string path)                     => true;
        public IReadOnlyList<Core.Gestion.WorksetInfo> GetWorksets()       => new List<Core.Gestion.WorksetInfo>();
        public bool CreateWorkset(string name)                             => true;
        public bool RenameWorkset(long worksetId, string newName)          => true;
        public long GetModelFileSize()                                     => ModelFileSize;
        public int GetTotalElementCount()                                  => ElementCount;

        public IReadOnlyList<DimensionTypeInfo> GetDimensionTypes()        => DimTypes;
        public int    GetDoorCountInActiveView()                           => DoorCount;
        public int    GetGridCountInActiveView()                           => GridCount;
        public int    GetWallCountInActiveView()                           => WallCount;
        public int    GetArqLevelCount()                                   => 3;
        public string GetActiveViewName()                                  => ViewName;
    }

    internal sealed class NullAcotadoLogger : ILogger
    {
        public void Info(string message)    { }
        public void Warning(string message) { }
        public void Error(string message, System.Exception? ex = null) { }
    }
}
