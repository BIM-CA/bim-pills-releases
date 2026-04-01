using System;
using System.IO;
using BIMPills.Core.Licensing;
using BIMPills.Infrastructure.Security;
using Newtonsoft.Json;

namespace BIMPills.Infrastructure.Licensing
{
    /// <summary>
    /// Persists license information locally using DPAPI encryption.
    /// Cache is valid for 24 hours — after that a re-validation against Airtable is required.
    /// </summary>
    public class LicenseCache
    {
        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            Formatting = Formatting.Indented,
            DateFormatString = "o"
        };

        private readonly string _cacheFilePath;
        private LicenseInfo? _inMemory;

        public LicenseCache(string? customPath = null)
        {
            var dir = customPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Autodesk", "Revit", "Addins", "BIMPills");
            Directory.CreateDirectory(dir);
            _cacheFilePath = Path.Combine(dir, "license.dat");
        }

        public void Save(LicenseInfo license)
        {
            var json = JsonConvert.SerializeObject(license, _jsonSettings);
            var encrypted = SecureStorage.Protect(json);
            File.WriteAllText(_cacheFilePath, encrypted);
            _inMemory = license;
        }

        public LicenseInfo? Load()
        {
            if (_inMemory != null) return _inMemory;

            if (!File.Exists(_cacheFilePath)) return null;

            try
            {
                var encrypted = File.ReadAllText(_cacheFilePath);
                if (!SecureStorage.TryUnprotect(encrypted, out var json))
                    return null;

                _inMemory = JsonConvert.DeserializeObject<LicenseInfo>(json, _jsonSettings);
                return _inMemory;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Returns true if the cached license was validated less than 24 hours ago.
        /// </summary>
        public bool IsCacheFresh()
        {
            var license = Load();
            if (license == null) return false;
            return (DateTime.UtcNow - license.ValidatedAt).TotalHours < 24;
        }

        public void Clear()
        {
            _inMemory = null;
            if (File.Exists(_cacheFilePath))
                File.Delete(_cacheFilePath);
        }
    }
}
