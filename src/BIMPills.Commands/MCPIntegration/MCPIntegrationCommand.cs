using BIMPills.Core.Commands;
using BIMPills.Core.Services;

namespace BIMPills.Commands.MCPIntegration
{
    /// <summary>
    /// Command to manage MCP (Model Context Protocol) connections.
    /// Depends on IMCPDiscoveryService from Infrastructure layer.
    /// No Revit API dependencies.
    /// </summary>
    public sealed class MCPIntegrationCommand : IPluginCommand
    {
        private readonly IMCPDiscoveryService _mcpService;

        public MCPIntegrationCommand(IMCPDiscoveryService mcpService)
        {
            _mcpService = mcpService;
        }

        public CommandResult Execute(ICommandContext context)
        {
            // This command is initiated from the UI window (MCPConnectionWindow)
            // which handles connection management asynchronously.
            // The command simply returns OK — actual logic is in the window.
            return CommandResult.Ok("MCP integration window opened");
        }
    }
}
