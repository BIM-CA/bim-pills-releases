using System;
using System.IO;
using System.Linq;
using BIMPills.Core.ParameterExtractor;
using BIMPills.Infrastructure.Persistence;
using Xunit;

namespace BIMPills.Core.Tests.ParameterExtractor
{
    /// <summary>
    /// CRUD tests para JsonExtractionPresetRepository usando un directorio temporal.
    /// </summary>
    public class JsonExtractionPresetRepositoryTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly JsonExtractionPresetRepository _repo;

        public JsonExtractionPresetRepositoryTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "BIMPillsExtractorTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _repo = new JsonExtractionPresetRepository(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { /* best-effort */ }
        }

        private static ExtractionPreset SamplePreset(string name = "Sample") => new ExtractionPreset
        {
            Name = name,
            Config = new ExtractionConfig
            {
                LengthUnits = ExtractionLengthUnits.Meters,
                Decimals    = 3,
                Rules =
                {
                    new ExtractionRule
                    {
                        Source = ExtractionSourceKind.Category,
                        Target = new ExtractionTarget { ParameterName = "BP_Categoria", DataType = ExtractionDataType.Text, CreateIfMissing = true }
                    },
                    new ExtractionRule
                    {
                        Source = ExtractionSourceKind.LocationX,
                        CoordinateOrigin = CoordinateOrigin.ProjectBase,
                        Target = new ExtractionTarget { ParameterName = "BP_X", DataType = ExtractionDataType.Length, CreateIfMissing = true }
                    },
                }
            }
        };

        // ── GetAll ────────────────────────────────────────────────────────────

        [Fact]
        public void GetAll_EmptyRepo_ReturnsEmptyList()
        {
            var result = _repo.GetAll();
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        // ── Create ────────────────────────────────────────────────────────────

        [Fact]
        public void Create_AssignsIdAndTimestamps()
        {
            var preset = SamplePreset();

            var id = _repo.Create(preset);

            Assert.False(string.IsNullOrEmpty(id));
            Assert.Equal(id, preset.Id);
            Assert.NotEqual(default, preset.CreatedAt);
            Assert.NotEqual(default, preset.ModifiedAt);
        }

        [Fact]
        public void Create_PersistedAndRetrievable()
        {
            var preset = SamplePreset("Coordenadas PB");

            var id = _repo.Create(preset);
            var all = _repo.GetAll();

            Assert.Single(all);
            Assert.Equal(id, all[0].Id);
            Assert.Equal("Coordenadas PB", all[0].Name);
            Assert.Equal(2, all[0].Config.Rules.Count);
            Assert.Equal(ExtractionLengthUnits.Meters, all[0].Config.LengthUnits);
        }

        [Fact]
        public void Create_MultiplePresets_AllPersisted()
        {
            _repo.Create(SamplePreset("A"));
            _repo.Create(SamplePreset("B"));
            _repo.Create(SamplePreset("C"));

            var all = _repo.GetAll();
            Assert.Equal(3, all.Count);
            Assert.Contains(all, p => p.Name == "A");
            Assert.Contains(all, p => p.Name == "B");
            Assert.Contains(all, p => p.Name == "C");
        }

        // ── Update ────────────────────────────────────────────────────────────

        [Fact]
        public void Update_ModifiesExistingPreset()
        {
            var preset = SamplePreset("Original");
            _repo.Create(preset);

            preset.Name = "Modificado";
            preset.Config.Decimals = 6;
            _repo.Update(preset);

            var all = _repo.GetAll();
            Assert.Single(all);
            Assert.Equal("Modificado", all[0].Name);
            Assert.Equal(6, all[0].Config.Decimals);
        }

        [Fact]
        public void Update_SetsModifiedAt()
        {
            var preset = SamplePreset();
            _repo.Create(preset);
            var originalModified = preset.ModifiedAt;

            System.Threading.Thread.Sleep(10);
            preset.Name = "Cambiado";
            _repo.Update(preset);

            var updated = _repo.GetAll().First(p => p.Id == preset.Id);
            Assert.True(updated.ModifiedAt >= originalModified);
        }

        [Fact]
        public void Update_NonExistentId_DoesNotThrow()
        {
            var preset = new ExtractionPreset { Id = "non-existent", Name = "X" };
            _repo.Update(preset);
        }

        // ── Delete ────────────────────────────────────────────────────────────

        [Fact]
        public void Delete_RemovesPreset()
        {
            var id = _repo.Create(SamplePreset("ToDelete"));

            _repo.Delete(id);

            Assert.Empty(_repo.GetAll());
        }

        [Fact]
        public void Delete_OnlyRemovesTargetPreset()
        {
            var id1 = _repo.Create(SamplePreset("Keep"));
            var id2 = _repo.Create(SamplePreset("Delete"));

            _repo.Delete(id2);

            var all = _repo.GetAll();
            Assert.Single(all);
            Assert.Equal(id1, all[0].Id);
        }

        [Fact]
        public void Delete_NonExistentId_DoesNotThrow()
        {
            _repo.Delete("non-existent");
        }
    }
}
