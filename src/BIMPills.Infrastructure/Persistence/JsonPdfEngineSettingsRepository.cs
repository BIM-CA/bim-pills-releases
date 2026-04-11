using System;
using System.IO;
using BIMPills.Core.Models;
using Newtonsoft.Json;

namespace BIMPills.Infrastructure.Persistence
{
    /// <summary>
    /// Persists the user's global PDF engine choice (native vs system printer)
    /// in <c>%APPDATA%/Autodesk/Revit/Addins/BIMPills/pdf-engine.json</c>.
    /// One file per user (not per model) — this is a global preference.
    /// </summary>
    public class JsonPdfEngineSettingsRepository
    {
        private readonly string _filePath;
        private const string FileName = "pdf-engine.json";

        public JsonPdfEngineSettingsRepository(string? customDirectory = null)
        {
            var dir = customDirectory ?? GetDefaultDirectory();
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, FileName);
        }

        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            Formatting       = Formatting.Indented
        };

        /// <summary>
        /// Loads the saved settings. Returns defaults (Native engine) if nothing
        /// has been saved yet or the file is corrupt.
        /// </summary>
        public PdfEngineSettings Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return new PdfEngineSettings();

                var json = File.ReadAllText(_filePath, System.Text.Encoding.UTF8);
                var loaded = JsonConvert.DeserializeObject<PdfEngineSettings>(json, _jsonSettings);
                return loaded ?? new PdfEngineSettings();
            }
            catch
            {
                return new PdfEngineSettings();
            }
        }

        /// <summary>
        /// Saves the settings to disk. Silently swallows I/O errors — settings
        /// are a preference, not critical state.
        /// </summary>
        public void Save(PdfEngineSettings settings)
        {
            if (settings == null) return;
            try
            {
                var json = JsonConvert.SerializeObject(settings, _jsonSettings);
                File.WriteAllText(_filePath, json, System.Text.Encoding.UTF8);
            }
            catch { /* ignore */ }
        }

        private static string GetDefaultDirectory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Autodesk", "Revit", "Addins", "BIMPills");
        }
    }
}
