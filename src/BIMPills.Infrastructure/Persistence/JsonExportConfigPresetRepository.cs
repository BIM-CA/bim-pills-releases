using System;
using System.Collections.Generic;
using System.IO;
using BIMPills.Core.Models;
using Newtonsoft.Json;

namespace BIMPills.Infrastructure.Persistence
{
    /// <summary>
    /// Repository for persisting export-format configuration presets to JSON.
    /// Stores in %APPDATA%/Autodesk/Revit/Addins/BIMPills/ExportConfigPresets/
    /// </summary>
    public class JsonExportConfigPresetRepository
    {
        private readonly string _directory;
        private const string FileName = "export-config-presets.json";

        public JsonExportConfigPresetRepository(string? customPath = null)
        {
            _directory = customPath ?? GetDefaultDirectory();
            EnsureDirectoryExists();
        }

        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            Formatting       = Formatting.Indented
        };

        public List<ExportConfigPreset> GetAll()
        {
            var filePath = Path.Combine(_directory, FileName);
            if (!File.Exists(filePath))
                return new List<ExportConfigPreset>();

            var json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            return JsonConvert.DeserializeObject<List<ExportConfigPreset>>(json, _jsonSettings)
                   ?? new List<ExportConfigPreset>();
        }

        public string Create(ExportConfigPreset preset)
        {
            preset.Id         = Guid.NewGuid().ToString();
            preset.CreatedAt  = DateTime.UtcNow;
            preset.ModifiedAt = DateTime.UtcNow;

            var presets = GetAll();
            presets.Add(preset);
            Save(presets);

            return preset.Id;
        }

        public void Update(ExportConfigPreset preset)
        {
            preset.ModifiedAt = DateTime.UtcNow;
            var presets = GetAll();
            var index = presets.FindIndex(p => p.Id == preset.Id);
            if (index >= 0)
            {
                presets[index] = preset;
                Save(presets);
            }
        }

        public void Delete(string presetId)
        {
            var presets = GetAll();
            presets.RemoveAll(p => p.Id == presetId);
            Save(presets);
        }

        /// <summary>
        /// Serialize a single preset to a portable JSON string for export to file.
        /// </summary>
        public static string SerializeForExport(ExportConfigPreset preset)
            => JsonConvert.SerializeObject(preset, _jsonSettings);

        /// <summary>
        /// Deserialize a preset from a portable JSON string (imported from file).
        /// Assigns a new Id and timestamps so it is treated as a fresh entry.
        /// </summary>
        public static ExportConfigPreset? DeserializeFromImport(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            ExportConfigPreset? preset;
            try { preset = JsonConvert.DeserializeObject<ExportConfigPreset>(json, _jsonSettings); }
            catch { return null; }
            if (preset == null) return null;
            preset.Id         = Guid.NewGuid().ToString();
            preset.CreatedAt  = DateTime.UtcNow;
            preset.ModifiedAt = DateTime.UtcNow;
            return preset;
        }

        private void Save(List<ExportConfigPreset> presets)
        {
            var filePath = Path.Combine(_directory, FileName);
            var json = JsonConvert.SerializeObject(presets, _jsonSettings);
            File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_directory))
                Directory.CreateDirectory(_directory);
        }

        private static string GetDefaultDirectory()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataPath, "Autodesk", "Revit", "Addins", "BIMPills", "ExportConfigPresets");
        }
    }
}
