using System;
using System.Collections.Generic;
using BIMPills.Core.Services;

namespace BIMPills.Core.Models
{
    /// <summary>
    /// Represents a connection configuration for an MCP (Model Context Protocol) service.
    /// Contains endpoint, credentials, and capability information.
    /// </summary>
    public class MCPConnectionConfig
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Endpoint { get; set; } = "";  // URL or local path
        public Dictionary<string, string> Credentials { get; set; } = new Dictionary<string, string>();
        public MCPConnectionStatus Status { get; set; } = MCPConnectionStatus.Unknown;
        public List<string> EnabledCapabilities { get; set; } = new List<string>();
        public DateTime CreatedAt { get; set; }
        public DateTime LastTestedAt { get; set; }

        // Display properties for UI binding
        public int CapabilitiesCount => EnabledCapabilities.Count;
        public string LastTestedDisplay => LastTestedAt == default ? "Nunca" : LastTestedAt.ToString("yyyy-MM-dd HH:mm");
        public string StatusLabel => Status switch
        {
            MCPConnectionStatus.Connected => "Conectado",
            MCPConnectionStatus.Disconnected => "Desconectado",
            MCPConnectionStatus.Error => "Error",
            _ => "Desconocido"
        };
    }

    /// <summary>
    /// Represents a capability provided by an MCP service.
    /// </summary>
    public class MCPCapability
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsEnabled { get; set; }
    }
}
