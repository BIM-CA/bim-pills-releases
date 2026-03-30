using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using BIMPills.Core.Models;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
        /// Discovers capabilities exposed by an MCP service using the MCP JSON-RPC protocol.
        /// Sends an <c>initialize</c> handshake followed by a <c>tools/list</c> request and
        /// returns the names of all available tools as capability strings.
        /// Falls back to returning the server-level capability keys if tool listing fails.
        /// </summary>
        public async Task<List<string>> DiscoverCapabilitiesAsync(MCPConnectionConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var capabilities = new List<string>();

            try
            {
                // Step 1 — MCP initialize handshake
                var initPayload = new
                {
                    jsonrpc = "2.0",
                    id      = 1,
                    method  = "initialize",
                    @params = new
                    {
                        protocolVersion = "2024-11-05",
                        capabilities    = new { },
                        clientInfo      = new { name = "BIMPills", version = "1.0.0-beta.1" }
                    }
                };

                var initJson    = JsonConvert.SerializeObject(initPayload);
                var initContent = new StringContent(initJson, Encoding.UTF8, "application/json");
                var initResp    = await _httpClient.PostAsync(config.Endpoint, initContent);

                if (!initResp.IsSuccessStatusCode)
                    return capabilities;

                var initBody = await initResp.Content.ReadAsStringAsync();
                var initResult = JObject.Parse(initBody)?["result"];

                // Collect server-level capability keys (e.g. "tools", "resources", "prompts")
                var serverCaps = initResult?["capabilities"];
                if (serverCaps is JObject capsObj)
                {
                    foreach (var prop in capsObj.Properties())
                        capabilities.Add(prop.Name);
                }

                // Step 2 — list tools if the server advertises the "tools" capability
                if (capabilities.Contains("tools"))
                {
                    capabilities.Clear(); // replace category keys with actual tool names
                    var toolsPayload = new
                    {
                        jsonrpc = "2.0",
                        id      = 2,
                        method  = "tools/list",
                        @params = new { }
                    };

                    var toolsJson    = JsonConvert.SerializeObject(toolsPayload);
                    var toolsContent = new StringContent(toolsJson, Encoding.UTF8, "application/json");
                    var toolsResp    = await _httpClient.PostAsync(config.Endpoint, toolsContent);

                    if (toolsResp.IsSuccessStatusCode)
                    {
                        var toolsBody   = await toolsResp.Content.ReadAsStringAsync();
                        var toolsResult = JObject.Parse(toolsBody)?["result"]?["tools"] as JArray;
                        if (toolsResult != null)
                        {
                            foreach (var tool in toolsResult)
                            {
                                var toolName = tool["name"]?.ToString();
                                if (!string.IsNullOrWhiteSpace(toolName))
                                    capabilities.Add(toolName!);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Discovery is best-effort — network errors are silently swallowed
            }

            return capabilities;
        }
    }
}
