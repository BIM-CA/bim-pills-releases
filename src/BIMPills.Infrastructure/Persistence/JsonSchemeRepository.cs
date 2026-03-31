using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BIMPills.Core.Models;
using Newtonsoft.Json;

namespace BIMPills.Infrastructure.Persistence
{
    /// <summary>
    /// Repository for persisting custom dimension schemes to JSON files.
    /// Stores schemes in %APPDATA%/Autodesk/Revit/Addins/BIMPills/Schemes/
    /// </summary>
    public class JsonSchemeRepository
    {
        private readonly string _schemeDirectory;

        public JsonSchemeRepository(string? customPath = null)
        {
            _schemeDirectory = customPath ?? GetDefaultSchemeDirectory();
            EnsureDirectoryExists();
        }

        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            Formatting       = Formatting.Indented
        };

        public async Task<List<CustomDimensionScheme>> GetAllAsync()
        {
            var filePath = Path.Combine(_schemeDirectory, "dimensions.json");
            if (!File.Exists(filePath))
                return new List<CustomDimensionScheme>();

            var json = await Task.Run(() => File.ReadAllText(filePath, System.Text.Encoding.UTF8));
            return JsonConvert.DeserializeObject<List<CustomDimensionScheme>>(json, _jsonSettings)
                   ?? new List<CustomDimensionScheme>();
        }

        public async Task<CustomDimensionScheme?> GetByIdAsync(string schemeId)
        {
            var schemes = await GetAllAsync();
            return schemes.Find(s => s.Id == schemeId);
        }

        public async Task<string> CreateAsync(CustomDimensionScheme scheme)
        {
            scheme.Id = Guid.NewGuid().ToString();
            scheme.CreatedAt = DateTime.UtcNow;
            scheme.ModifiedAt = DateTime.UtcNow;

            var schemes = await GetAllAsync();
            schemes.Add(scheme);
            await SaveAsync(schemes);

            return scheme.Id;
        }

        public async Task UpdateAsync(CustomDimensionScheme scheme)
        {
            scheme.ModifiedAt = DateTime.UtcNow;
            var schemes = await GetAllAsync();
            var index = schemes.FindIndex(s => s.Id == scheme.Id);
            if (index >= 0)
            {
                schemes[index] = scheme;
                await SaveAsync(schemes);
            }
        }

        public async Task DeleteAsync(string schemeId)
        {
            var schemes = await GetAllAsync();
            schemes.RemoveAll(s => s.Id == schemeId);
            await SaveAsync(schemes);
        }

        private async Task SaveAsync(List<CustomDimensionScheme> schemes)
        {
            var filePath = Path.Combine(_schemeDirectory, "dimensions.json");
            var json = JsonConvert.SerializeObject(schemes, _jsonSettings);
            await Task.Run(() => File.WriteAllText(filePath, json, System.Text.Encoding.UTF8));
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_schemeDirectory))
                Directory.CreateDirectory(_schemeDirectory);
        }

        private static string GetDefaultSchemeDirectory()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataPath, "Autodesk", "Revit", "Addins", "BIMPills", "Schemes");
        }
    }
}
