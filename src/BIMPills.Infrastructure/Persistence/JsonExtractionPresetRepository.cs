using System;
using System.Collections.Generic;
using System.IO;
using BIMPills.Core.ParameterExtractor;
using Newtonsoft.Json;

namespace BIMPills.Infrastructure.Persistence
{
    /// <summary>
    /// Repositorio de perfiles guardados del Extractor de Parámetros.
    /// Persiste a %APPDATA%/Autodesk/Revit/Addins/BIMPills/ExtractionPresets/extraction-presets.json
    /// </summary>
    public class JsonExtractionPresetRepository
    {
        private readonly string _directory;
        private const string FileName = "extraction-presets.json";

        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            Formatting       = Formatting.Indented
        };

        public JsonExtractionPresetRepository(string? customPath = null)
        {
            _directory = customPath ?? GetDefaultDirectory();
            EnsureDirectoryExists();
        }

        public List<ExtractionPreset> GetAll()
        {
            var filePath = Path.Combine(_directory, FileName);
            if (!File.Exists(filePath))
                return new List<ExtractionPreset>();

            var json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            return JsonConvert.DeserializeObject<List<ExtractionPreset>>(json, _jsonSettings)
                   ?? new List<ExtractionPreset>();
        }

        public string Create(ExtractionPreset preset)
        {
            preset.Id         = Guid.NewGuid().ToString();
            preset.CreatedAt  = DateTime.UtcNow;
            preset.ModifiedAt = DateTime.UtcNow;

            var presets = GetAll();
            presets.Add(preset);
            Save(presets);

            return preset.Id;
        }

        public void Update(ExtractionPreset preset)
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

        private void Save(List<ExtractionPreset> presets)
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
            return Path.Combine(appDataPath, "Autodesk", "Revit", "Addins", "BIMPills", "ExtractionPresets");
        }
    }
}
