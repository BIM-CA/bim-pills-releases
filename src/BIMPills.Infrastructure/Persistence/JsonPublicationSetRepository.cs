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
        /// Serialize a single set to a portable JSON string for export to file.
        /// </summary>
        public static string SerializeForExport(PublicationSet set)
            => JsonConvert.SerializeObject(set, _jsonSettings);

        /// <summary>
        /// Deserialize a set from a portable JSON string (imported from file).
        /// Assigns a new Id and timestamps so it is treated as a fresh entry.
        /// </summary>
        public static PublicationSet? DeserializeFromImport(string json)
        {
            var set = JsonConvert.DeserializeObject<PublicationSet>(json, _jsonSettings);
            if (set == null) return null;
            set.Id         = Guid.NewGuid().ToString();
            set.CreatedAt  = DateTime.UtcNow;
            set.ModifiedAt = DateTime.UtcNow;
            return set;
        }

        /// <summary>
        /// Returns a directory scoped to a specific model.
        /// <para>
        /// El identificador puede ser una ruta de archivo larga (Document.PathName) o un
        /// nombre corto. Se sanitiza el último segmento como prefijo legible y se anexa
        /// un hash SHA1 corto del identificador completo para garantizar unicidad y
        /// nombres de carpeta razonables aunque la ruta sea larga.
        /// </para>
        /// </summary>
        public static string GetDirectoryForModel(string modelKey)
        {
            if (string.IsNullOrWhiteSpace(modelKey)) modelKey = "_default";

            // Tomamos solo el último segmento (filename) como prefijo legible
            var lastSeg = modelKey;
            var slashIdx = modelKey.LastIndexOfAny(new[] { '\\', '/' });
            if (slashIdx >= 0 && slashIdx < modelKey.Length - 1)
                lastSeg = modelKey.Substring(slashIdx + 1);

            var safeName = string.Join("_", lastSeg.Split(Path.GetInvalidFileNameChars()));
            if (string.IsNullOrWhiteSpace(safeName)) safeName = "model";
            if (safeName.Length > 40) safeName = safeName.Substring(0, 40);

            // Hash corto del identificador completo → unicidad incluso si distintos
            // modelos tienen el mismo filename en carpetas distintas.
            string hash;
            using (var sha = System.Security.Cryptography.SHA1.Create())
            {
                var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(modelKey));
                hash = BitConverter.ToString(bytes).Replace("-", "").Substring(0, 8).ToLowerInvariant();
            }

            var folder = $"{safeName}_{hash}";
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataPath, "Autodesk", "Revit", "Addins", "BIMPills", "PublicationSets", folder);
        }
    }
}
