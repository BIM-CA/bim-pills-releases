using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BIMPills.Core.Models;
using Newtonsoft.Json;

namespace BIMPills.Infrastructure.Persistence
{
    /// <summary>
    /// Repository for persisting sheet export profiles to JSON files.
    /// Stores profiles in %APPDATA%/Autodesk/Revit/Addins/BIMPills/Profiles/
    /// </summary>
    public class JsonSheetExportProfileRepository
    {
        private readonly string _profileDirectory;
        private const string FileName = "sheet-exports.json";

        public JsonSheetExportProfileRepository(string? customPath = null)
        {
            _profileDirectory = customPath ?? GetDefaultProfileDirectory();
            EnsureDirectoryExists();
        }

        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            Formatting       = Formatting.Indented
        };

        public List<SheetExportProfile> GetAll()
        {
            var filePath = Path.Combine(_profileDirectory, FileName);
            if (!File.Exists(filePath))
                return new List<SheetExportProfile>();

            var json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            return JsonConvert.DeserializeObject<List<SheetExportProfile>>(json, _jsonSettings)
                   ?? new List<SheetExportProfile>();
        }

        public SheetExportProfile? GetById(string profileId)
        {
            var profiles = GetAll();
            return profiles.Find(p => p.Id == profileId);
        }

        public string Create(SheetExportProfile profile)
        {
            profile.Id = Guid.NewGuid().ToString();
            profile.CreatedAt = DateTime.UtcNow;
            profile.ModifiedAt = DateTime.UtcNow;

            var profiles = GetAll();
            profiles.Add(profile);
            Save(profiles);

            return profile.Id;
        }

        public void Update(SheetExportProfile profile)
        {
            profile.ModifiedAt = DateTime.UtcNow;
            var profiles = GetAll();
            var index = profiles.FindIndex(p => p.Id == profile.Id);
            if (index >= 0)
            {
                profiles[index] = profile;
                Save(profiles);
            }
        }

        public void Delete(string profileId)
        {
            var profiles = GetAll();
            profiles.RemoveAll(p => p.Id == profileId);
            Save(profiles);
        }

        private void Save(List<SheetExportProfile> profiles)
        {
            var filePath = Path.Combine(_profileDirectory, FileName);
            var json = JsonConvert.SerializeObject(profiles, _jsonSettings);
            File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_profileDirectory))
                Directory.CreateDirectory(_profileDirectory);
        }

        private static string GetDefaultProfileDirectory()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataPath, "Autodesk", "Revit", "Addins", "BIMPills", "Profiles");
        }
    }
}
