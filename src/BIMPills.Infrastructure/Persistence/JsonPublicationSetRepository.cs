using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BIMPills.Core.Models;
using Newtonsoft.Json;

namespace BIMPills.Infrastructure.Persistence
{
    /// <summary>
    /// Repository for persisting publication sets (saved sheet/view selections) to JSON.
    /// Stores in %APPDATA%/Autodesk/Revit/Addins/BIMPills/PublicationSets/
    /// </summary>
    public class JsonPublicationSetRepository
    {
        private readonly string _directory;
        private const string FileName = "publication-sets.json";

        public JsonPublicationSetRepository(string? customPath = null)
        {
            _directory = customPath ?? GetDefaultDirectory();
            EnsureDirectoryExists();
        }

        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            Formatting       = Formatting.Indented
        };

        public async Task<List<PublicationSet>> GetAllAsync()
        {
            var filePath = Path.Combine(_directory, FileName);
            if (!File.Exists(filePath))
                return new List<PublicationSet>();

            var json = await Task.Run(() => File.ReadAllText(filePath, System.Text.Encoding.UTF8));
            return JsonConvert.DeserializeObject<List<PublicationSet>>(json, _jsonSettings)
                   ?? new List<PublicationSet>();
        }

        public async Task<string> CreateAsync(PublicationSet set)
        {
            set.Id = Guid.NewGuid().ToString();
            set.CreatedAt = DateTime.UtcNow;
            set.ModifiedAt = DateTime.UtcNow;

            var sets = await GetAllAsync();
            sets.Add(set);
            await SaveAsync(sets);

            return set.Id;
        }

        public async Task UpdateAsync(PublicationSet set)
        {
            set.ModifiedAt = DateTime.UtcNow;
            var sets = await GetAllAsync();
            var index = sets.FindIndex(s => s.Id == set.Id);
            if (index >= 0)
            {
                sets[index] = set;
                await SaveAsync(sets);
            }
        }

        public async Task DeleteAsync(string setId)
        {
            var sets = await GetAllAsync();
            sets.RemoveAll(s => s.Id == setId);
            await SaveAsync(sets);
        }

        private async Task SaveAsync(List<PublicationSet> sets)
        {
            var filePath = Path.Combine(_directory, FileName);
            var json = JsonConvert.SerializeObject(sets, _jsonSettings);
            await Task.Run(() => File.WriteAllText(filePath, json, System.Text.Encoding.UTF8));
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_directory))
                Directory.CreateDirectory(_directory);
        }

        private static string GetDefaultDirectory()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataPath, "Autodesk", "Revit", "Addins", "BIMPills", "PublicationSets");
        }
    }
}
