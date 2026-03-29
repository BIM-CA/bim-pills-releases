using BIMPills.Commands.MCPIntegration;
using BIMPills.Core.Commands;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.DI;
using BIMPills.Revit.Commands;
using BIMPills.UI.MCPIntegration;

namespace BIMPills.Revit.Commands.MCPIntegration
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class MCPIntegrationRevitCommand : RevitCommandBase
    {
        protected override IPluginCommand CreateCommand()
            => new MCPIntegrationCommand(ServiceLocator.Get<IMCPDiscoveryService>());

        protected override void OnSuccess(IPluginCommand command)
        {
            var logger = ServiceLocator.IsRegistered<ILogger>() ? ServiceLocator.Get<ILogger>() : null;

            try
            {
                var window = new MCPConnectionWindow();
                window.ShowDialog();
            }
            catch (System.Exception ex)
            {
                logger?.Error("Error al abrir MCPConnectionWindow", ex);
                throw;
            }
        }
    }
}
