using System;
using System.IO;
using BIMPills.Infrastructure.Security;
using Newtonsoft.Json;

namespace BIMPills.Infrastructure.Licensing
{
    /// <summary>
    /// Manages the DPAPI-encrypted Airtable API key used for license validation.
    /// Stored in %APPDATA%/Autodesk/Revit/Addins/BIMPills/airtable_config.json
    /// </summary>
    public static class AirtableConfig
    {
        private static readonly string _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk", "Revit", "Addins", "BIMPills", "airtable_config.json");

        private class ConfigData
        {
            public string ApiKey { get; set; } = "";
        }

        public static void SaveApiKey(string apiKey)
        {
            var encrypted = SecureStorage.Protect(apiKey);
            var config = new ConfigData { ApiKey = encrypted };
            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            var dir = Path.GetDirectoryName(_configPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(_configPath, json, System.Text.Encoding.UTF8);
        }

        public static string? LoadApiKey()
        {
            if (!File.Exists(_configPath)) return null;

            try
            {
                var json = File.ReadAllText(_configPath, System.Text.Encoding.UTF8);
                var config = JsonConvert.DeserializeObject<ConfigData>(json);
                if (config == null || string.IsNullOrWhiteSpace(config.ApiKey)) return null;

                if (SecureStorage.TryUnprotect(config.ApiKey, out var apiKey))
                    return apiKey;

                // Legacy plaintext — re-save encrypted
                SaveApiKey(config.ApiKey);
                return config.ApiKey;
            }
            catch
            {
                return null;
            }
        }

        public static bool HasApiKey() => File.Exists(_configPath) && LoadApiKey() != null;
    }
}
