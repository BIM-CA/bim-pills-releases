using BIMPills.Core.Models;
using BIMPills.Infrastructure.Persistence;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace BIMPills.Core.Tests.ExportSheets
{
    /// <summary>
    /// Tests CRUD completo de JsonExportConfigPresetRepository usando un directorio temporal.
    /// </summary>
    public class ExportConfigPresetRepositoryTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly JsonExportConfigPresetRepository _repo;

        public ExportConfigPresetRepositoryTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "BIMPillsTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _repo = new JsonExportConfigPresetRepository(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { /* best-effort cleanup */ }
        }

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
            var preset = new ExportConfigPreset { Name = "Mi Preset" };

            var id = _repo.Create(preset);

            Assert.False(string.IsNullOrEmpty(id));
            Assert.Equal(id, preset.Id);
            Assert.NotEqual(default, preset.CreatedAt);
            Assert.NotEqual(default, preset.ModifiedAt);
        }

        [Fact]
        public void Create_PersistedAndRetrievable()
        {
            var preset = new ExportConfigPreset
            {
                Name      = "PDF + DWG",
                ExportPdf = true,
                ExportDwg = true,
                NamingPattern = "{sheet}-{rev}"
            };

            var id = _repo.Create(preset);
            var all = _repo.GetAll();

            Assert.Single(all);
            Assert.Equal(id, all[0].Id);
            Assert.Equal("PDF + DWG", all[0].Name);
            Assert.True(all[0].ExportPdf);
            Assert.True(all[0].ExportDwg);
            Assert.Equal("{sheet}-{rev}", all[0].NamingPattern);
        }

        [Fact]
        public void Create_MultiplePresets_AllPersisted()
        {
            _repo.Create(new ExportConfigPreset { Name = "A" });
            _repo.Create(new ExportConfigPreset { Name = "B" });
            _repo.Create(new ExportConfigPreset { Name = "C" });

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
            var preset = new ExportConfigPreset { Name = "Original" };
            _repo.Create(preset);

            preset.Name = "Modificado";
            _repo.Update(preset);

            var all = _repo.GetAll();
            Assert.Single(all);
            Assert.Equal("Modificado", all[0].Name);
        }

        [Fact]
        public void Update_SetsModifiedAt()
        {
            var preset = new ExportConfigPreset { Name = "Preset" };
            _repo.Create(preset);
            var originalModified = preset.ModifiedAt;

            System.Threading.Thread.Sleep(10); // ensure time passes
            preset.Name = "Cambiado";
            _repo.Update(preset);

            var updated = _repo.GetAll().First(p => p.Id == preset.Id);
            Assert.True(updated.ModifiedAt >= originalModified);
        }

        [Fact]
        public void Update_NonExistentId_DoesNotThrow()
        {
            var preset = new ExportConfigPreset { Id = "non-existent", Name = "X" };
            // Should not throw — silently ignores missing id
            _repo.Update(preset);
        }

        // ── Delete ────────────────────────────────────────────────────────────

        [Fact]
        public void Delete_RemovesPreset()
        {
            var preset = new ExportConfigPreset { Name = "ToDelete" };
            var id = _repo.Create(preset);

            _repo.Delete(id);

            Assert.Empty(_repo.GetAll());
        }

        [Fact]
        public void Delete_OnlyRemovesTargetPreset()
        {
            var id1 = _repo.Create(new ExportConfigPreset { Name = "Keep" });
            var id2 = _repo.Create(new ExportConfigPreset { Name = "Delete" });

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

        // ── Serialize / Deserialize for export ───────────────────────────────

        [Fact]
        public void SerializeDeserializeRoundtrip_PreservesFields()
        {
            var original = new ExportConfigPreset
            {
                Name          = "Test Export",
                ExportPdf     = true,
                ExportDwg     = false,
                NamingPattern = "{name}-{date}",
                PdfEngine     = PdfEngineKind.Native
            };
            _repo.Create(original);

            var json     = JsonExportConfigPresetRepository.SerializeForExport(original);
            var imported = JsonExportConfigPresetRepository.DeserializeFromImport(json);

            Assert.NotNull(imported);
            Assert.NotEqual(original.Id, imported!.Id);     // new Id assigned on import
            Assert.Equal("Test Export",  imported.Name);
            Assert.True(imported.ExportPdf);
            Assert.False(imported.ExportDwg);
            Assert.Equal("{name}-{date}", imported.NamingPattern);
        }

        [Fact]
        public void DeserializeFromImport_InvalidJson_ReturnsNull()
        {
            var result = JsonExportConfigPresetRepository.DeserializeFromImport("not-valid-json{{{");
            Assert.Null(result);
        }
    }
}
