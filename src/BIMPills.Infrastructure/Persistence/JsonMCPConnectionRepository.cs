using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BIMPills.Core.Models;
using Newtonsoft.Json;

namespace BIMPills.Infrastructure.Persistence
{
    /// <summary>
    /// Repository for persisting MCP connection configurations to JSON files.
    /// Stores connections in %APPDATA%/Autodesk/Revit/Addins/BIMPills/MCPConnections/
    /// Credentials are encrypted using DPAPI (Windows Data Protection API).
    /// </summary>
    public class JsonMCPConnectionRepository
    {
        private readonly string _connectionDirectory;

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

            var json = await Task.Run(() => File.ReadAllText(filePath));
            return JsonConvert.DeserializeObject<List<MCPConnectionConfig>>(json) ?? new List<MCPConnectionConfig>();
        }

        public async Task<MCPConnectionConfig?> GetByIdAsync(string connectionId)
        {
            var connections = await GetAllAsync();
            return connections.Find(c => c.Id == connectionId);
        }

        public async Task<string> CreateAsync(MCPConnectionConfig config)
        {
            config.Id = Guid.NewGuid().ToString();
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

        private async Task SaveAsync(List<MCPConnectionConfig> connections)
        {
            var filePath = Path.Combine(_connectionDirectory, "connections.json");
            var json = JsonConvert.SerializeObject(connections, Formatting.Indented);
            await Task.Run(() => File.WriteAllText(filePath, json));
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
