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

        public async Task<List<SheetExportProfile>> GetAllAsync()
        {
            var filePath = Path.Combine(_profileDirectory, FileName);
            if (!File.Exists(filePath))
                return new List<SheetExportProfile>();

            var json = await Task.Run(() => File.ReadAllText(filePath, System.Text.Encoding.UTF8));
            return JsonConvert.DeserializeObject<List<SheetExportProfile>>(json, _jsonSettings)
                   ?? new List<SheetExportProfile>();
        }

        public async Task<SheetExportProfile?> GetByIdAsync(string profileId)
        {
            var profiles = await GetAllAsync();
            return profiles.Find(p => p.Id == profileId);
        }

        public async Task<string> CreateAsync(SheetExportProfile profile)
        {
            profile.Id = Guid.NewGuid().ToString();
            profile.CreatedAt = DateTime.UtcNow;
            profile.ModifiedAt = DateTime.UtcNow;

            var profiles = await GetAllAsync();
            profiles.Add(profile);
            await SaveAsync(profiles);

            return profile.Id;
        }

        public async Task UpdateAsync(SheetExportProfile profile)
        {
            profile.ModifiedAt = DateTime.UtcNow;
            var profiles = await GetAllAsync();
            var index = profiles.FindIndex(p => p.Id == profile.Id);
            if (index >= 0)
            {
                profiles[index] = profile;
                await SaveAsync(profiles);
            }
        }

        public async Task DeleteAsync(string profileId)
        {
            var profiles = await GetAllAsync();
            profiles.RemoveAll(p => p.Id == profileId);
            await SaveAsync(profiles);
        }

        private async Task SaveAsync(List<SheetExportProfile> profiles)
        {
            var filePath = Path.Combine(_profileDirectory, FileName);
            var json = JsonConvert.SerializeObject(profiles, _jsonSettings);
            await Task.Run(() => File.WriteAllText(filePath, json, System.Text.Encoding.UTF8));
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
