using BIMPills.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BIMPills.Core.Services
{
    /// <summary>
    /// Service for discovering and managing MCP (Model Context Protocol) connections.
    /// Handles connection CRUD, testing, and capability discovery.
    /// </summary>
    public interface IMCPDiscoveryService
    {
        Task<List<MCPConnectionConfig>> GetAllConnectionsAsync();
        Task<MCPConnectionConfig?> GetConnectionByIdAsync(string connectionId);
        Task<string> CreateConnectionAsync(MCPConnectionConfig config);
        Task UpdateConnectionAsync(MCPConnectionConfig config);
        Task DeleteConnectionAsync(string connectionId);
        Task<bool> TestConnectionAsync(MCPConnectionConfig config);
        Task<List<string>> DiscoverCapabilitiesAsync(MCPConnectionConfig config);
    }

    /// <summary>
    /// Enumeration of MCP connection statuses.
    /// </summary>
    public enum MCPConnectionStatus
    {
        Connected,
        Disconnected,
        Error,
        Unknown
    }
}
