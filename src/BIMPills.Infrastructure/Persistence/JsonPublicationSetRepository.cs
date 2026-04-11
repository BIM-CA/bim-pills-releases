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

        public List<PublicationSet> GetAll()
        {
            var filePath = Path.Combine(_directory, FileName);
            if (!File.Exists(filePath))
                return new List<PublicationSet>();

            var json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            return JsonConvert.DeserializeObject<List<PublicationSet>>(json, _jsonSettings)
                   ?? new List<PublicationSet>();
        }

        public string Create(PublicationSet set)
        {
            set.Id = Guid.NewGuid().ToString();
            set.CreatedAt = DateTime.UtcNow;
            set.ModifiedAt = DateTime.UtcNow;

            var sets = GetAll();
            sets.Add(set);
            Save(sets);

            return set.Id;
        }

        public void Update(PublicationSet set)
        {
            set.ModifiedAt = DateTime.UtcNow;
            var sets = GetAll();
            var index = sets.FindIndex(s => s.Id == set.Id);
            if (index >= 0)
            {
                sets[index] = set;
                Save(sets);
            }
        }

        public void Delete(string setId)
        {
            var sets = GetAll();
            sets.RemoveAll(s => s.Id == setId);
            Save(sets);
        }

        private void Save(List<PublicationSet> sets)
        {
            var filePath = Path.Combine(_directory, FileName);
            var json = JsonConvert.SerializeObject(sets, _jsonSettings);
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
            return Path.Combine(appDataPath, "Autodesk", "Revit", "Addins", "BIMPills", "PublicationSets");
        }

        /// <summary>
        /// Returns a directory scoped to a specific model name.
        /// Sanitizes the model name for use as a folder name.
        /// </summary>
        public static string GetDirectoryForModel(string modelName)
        {
            var safeName = string.Join("_", modelName.Split(Path.GetInvalidFileNameChars()));
            if (string.IsNullOrWhiteSpace(safeName)) safeName = "_default";
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataPath, "Autodesk", "Revit", "Addins", "BIMPills", "PublicationSets", safeName);
        }
    }
}
