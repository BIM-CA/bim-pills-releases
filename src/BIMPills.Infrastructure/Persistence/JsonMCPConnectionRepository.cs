using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BIMPills.Core.Models;
using BIMPills.Infrastructure.Security;
using Newtonsoft.Json;

namespace BIMPills.Infrastructure.Persistence
{
    /// <summary>
    /// Repository for persisting MCP connection configurations to JSON files.
    /// Stores connections in %APPDATA%/Autodesk/Revit/Addins/BIMPills/MCPConnections/
    /// Credential values are encrypted using DPAPI (Windows Data Protection API) so they
    /// are never written to disk in plaintext.
    /// </summary>
    public class JsonMCPConnectionRepository
    {
        private readonly string _connectionDirectory;

        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            Formatting       = Formatting.Indented
        };

        public JsonMCPConnectionRepository(string? customPath = null)
        {
            _connectionDirectory = customPath ?? GetDefaultConnectionDirectory();
            EnsureDirectoryExists();
        }

        public async Task<List<MCPConnectionConfig>> GetAllAsync()
        {
            var filePath = Path.Combine(_connectionDirectory, "connections.json");
            if (!File.Exists(filePath))
                return new List<MCPConnectionConfig>();

            var json        = await Task.Run(() => File.ReadAllText(filePath, System.Text.Encoding.UTF8));
            var connections = JsonConvert.DeserializeObject<List<MCPConnectionConfig>>(json, _jsonSettings)
                              ?? new List<MCPConnectionConfig>();

            bool needsResave = false;
            foreach (var conn in connections)
                needsResave |= DecryptCredentials(conn);

            // Re-save if any plaintext credentials were found (migration from unencrypted files).
            if (needsResave)
                await SaveAsync(connections);

            return connections;
        }

        public async Task<MCPConnectionConfig?> GetByIdAsync(string connectionId)
        {
            var connections = await GetAllAsync();
            return connections.Find(c => c.Id == connectionId);
        }

        public async Task<string> CreateAsync(MCPConnectionConfig config)
        {
            config.Id        = Guid.NewGuid().ToString();
            config.CreatedAt = DateTime.UtcNow;

            var connections = await GetAllAsync();
            connections.Add(config);
            await SaveAsync(connections);

            return config.Id;
        }

        public async Task UpdateAsync(MCPConnectionConfig config)
        {
            var connections = await GetAllAsync();
            var index = connections.FindIndex(c => c.Id == config.Id);
            if (index >= 0)
            {
                connections[index] = config;
                await SaveAsync(connections);
            }
        }

        public async Task DeleteAsync(string connectionId)
        {
            var connections = await GetAllAsync();
            connections.RemoveAll(c => c.Id == connectionId);
            await SaveAsync(connections);
        }

        // ── Private helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Encrypts each credential value before writing to disk.
        /// Works on a deep copy so the in-memory config is never modified.
        /// </summary>
        private async Task SaveAsync(List<MCPConnectionConfig> connections)
        {
            var filePath   = Path.Combine(_connectionDirectory, "connections.json");
            var toStore    = connections.Select(c => EncryptCredentialsCopy(c)).ToList();
            var json       = JsonConvert.SerializeObject(toStore, _jsonSettings);
            await Task.Run(() => File.WriteAllText(filePath, json, System.Text.Encoding.UTF8));
        }

        /// <summary>
        /// Returns a shallow copy of <paramref name="config"/> with all credential values
        /// replaced by their DPAPI-encrypted Base64 equivalents.
        /// </summary>
        private static MCPConnectionConfig EncryptCredentialsCopy(MCPConnectionConfig config)
        {
            var copy = new MCPConnectionConfig
            {
                Id            = config.Id,
                Name          = config.Name,
                Endpoint      = config.Endpoint,
                Status        = config.Status,
                CreatedAt     = config.CreatedAt,
                LastTestedAt  = config.LastTestedAt,
                Credentials   = new Dictionary<string, string>()
            };

            foreach (var kv in config.Credentials)
                copy.Credentials[kv.Key] = SecureStorage.Protect(kv.Value);

            return copy;
        }

        /// <summary>
        /// Decrypts credential values in-place using DPAPI.
        /// Falls back silently for legacy plaintext values (migration path).
        /// Returns <c>true</c> if any plaintext value was found (triggers a re-save).
        /// </summary>
        private static bool DecryptCredentials(MCPConnectionConfig config)
        {
            bool hadPlaintext = false;
            var keys = new List<string>(config.Credentials.Keys);
            foreach (var key in keys)
            {
                if (!SecureStorage.TryUnprotect(config.Credentials[key], out var decrypted))
                    hadPlaintext = true;  // was plaintext — mark for re-save
                config.Credentials[key] = decrypted;
            }
            return hadPlaintext;
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_connectionDirectory))
                Directory.CreateDirectory(_connectionDirectory);
        }

        private static string GetDefaultConnectionDirectory()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataPath, "Autodesk", "Revit", "Addins", "BIMPills", "MCPConnections");
        }
    }
}
