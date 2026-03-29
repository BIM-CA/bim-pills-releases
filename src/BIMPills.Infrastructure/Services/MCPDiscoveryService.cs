using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using BIMPills.Core.Models;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.Persistence;

namespace BIMPills.Infrastructure.Services
{
    /// <summary>
    /// Service for discovering and managing MCP (Model Context Protocol) connections.
    /// Delegates persistence to <see cref="JsonMCPConnectionRepository"/> and provides
    /// connection testing and capability discovery.
    /// </summary>
    public class MCPDiscoveryService : IMCPDiscoveryService
    {
        private readonly JsonMCPConnectionRepository _repository;
        private readonly HttpClient _httpClient;

        public MCPDiscoveryService(JsonMCPConnectionRepository repository)
            : this(repository, new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
        {
        }

        public MCPDiscoveryService(JsonMCPConnectionRepository repository, HttpClient httpClient)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<List<MCPConnectionConfig>> GetAllConnectionsAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<MCPConnectionConfig?> GetConnectionByIdAsync(string connectionId)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
                throw new ArgumentException("Connection ID cannot be null or empty.", nameof(connectionId));

            return await _repository.GetByIdAsync(connectionId);
        }

        public async Task<string> CreateConnectionAsync(MCPConnectionConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            return await _repository.CreateAsync(config);
        }

        public async Task UpdateConnectionAsync(MCPConnectionConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            await _repository.UpdateAsync(config);
        }

        public async Task DeleteConnectionAsync(string connectionId)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
                throw new ArgumentException("Connection ID cannot be null or empty.", nameof(connectionId));

            await _repository.DeleteAsync(connectionId);
        }

        public async Task<bool> TestConnectionAsync(MCPConnectionConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            bool isReachable;
            try
            {
                var response = await _httpClient.GetAsync(config.Endpoint);
                isReachable = response.IsSuccessStatusCode;
            }
            catch
            {
                isReachable = false;
            }

            // Update status and timestamp
            config.Status = isReachable
                ? MCPConnectionStatus.Connected
                : MCPConnectionStatus.Error;
            config.LastTestedAt = DateTime.UtcNow;

            // Persist updated status
            if (!string.IsNullOrWhiteSpace(config.Id))
            {
                await _repository.UpdateAsync(config);
            }

            return isReachable;
        }

        /// <summary>
        /// Discovers capabilities exposed by an MCP service.
        /// Currently returns an empty list — placeholder for full MCP protocol implementation.
        /// </summary>
        public Task<List<string>> DiscoverCapabilitiesAsync(MCPConnectionConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            // TODO: Implement MCP protocol capability discovery
            return Task.FromResult(new List<string>());
        }
    }
}
