using System;
using System.Collections.Generic;
using System.IO;
using BIMPills.Core.Seleccionar;
using Newtonsoft.Json;

namespace BIMPills.Infrastructure.Persistence
{
    /// <summary>
    /// Repositorio de presets de filtros de selección.
    /// Persiste a %APPDATA%/Autodesk/Revit/Addins/BIMPills/SelectionPresets/selection-presets.json
    /// </summary>
    public class JsonFilterPresetRepository : IFilterPresetRepository
    {
        private readonly string _directory;
        private const string FileName = "selection-presets.json";

        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            Formatting       = Formatting.Indented
        };

        public JsonFilterPresetRepository(string? customPath = null)
        {
            _directory = customPath ?? GetDefaultDirectory();
            EnsureDirectoryExists();
        }

        public IReadOnlyList<FilterPreset> LoadAll()
        {
            var filePath = Path.Combine(_directory, FileName);
            if (!File.Exists(filePath))
                return new List<FilterPreset>();

            var json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            return JsonConvert.DeserializeObject<List<FilterPreset>>(json, _jsonSettings)
                   ?? new List<FilterPreset>();
        }

        public void Save(FilterPreset preset)
        {
            var presets = new List<FilterPreset>(LoadAll());

            // 1º: buscar por Id (actualización directa)
            var index = !string.IsNullOrEmpty(preset.Id)
                ? presets.FindIndex(p => p.Id == preset.Id)
                : -1;

            // 2º: fallback — buscar por nombre (case-insensitive) para garantizar que
            //     "Guardar como nuevo conjunto" con el mismo nombre SIEMPRE sobreescriba.
            if (index < 0 && !string.IsNullOrEmpty(preset.Name))
                index = presets.FindIndex(p =>
                    string.Equals(p.Name.Trim(), preset.Name.Trim(), StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
            {
                // Preservar Id y CreatedAt originales
                preset.Id         = presets[index].Id;
                preset.CreatedAt  = presets[index].CreatedAt;
                preset.ModifiedAt = DateTime.UtcNow;
                presets[index]    = preset;
            }
            else
            {
                if (string.IsNullOrEmpty(preset.Id))
                    preset.Id = Guid.NewGuid().ToString();
                preset.CreatedAt  = DateTime.UtcNow;
                preset.ModifiedAt = DateTime.UtcNow;
                presets.Add(preset);
            }

            Persist(presets);
        }

        public void Delete(string id)
        {
            var presets = new List<FilterPreset>(LoadAll());
            presets.RemoveAll(p => p.Id == id);
            Persist(presets);
        }

        private void Persist(List<FilterPreset> presets)
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
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Autodesk", "Revit", "Addins", "BIMPills", "SelectionPresets");
        }
    }
}
