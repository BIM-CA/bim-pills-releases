using BIMPills.Core.Modules;
using System.IO;
using System.Reflection;

namespace BIMPills.Commands.MCPIntegration
{
    public sealed class MCPIntegrationModule : IPluginModule
    {
        public string TabName   => "BIMPills";
        public string PanelName => "Datos";

        public void BuildRibbon(IRibbonBuilder builder)
        {
            var revitDll = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
                "BIMPills.Revit.dll");

            builder.AddPushButton(
                tabName:             TabName,
                panelName:           PanelName,
                buttonName:          "Conectar",
                tooltip:             "Configura la integración con servicios MCP para análisis inteligente del modelo BIM.",
                commandTypeFullName: "BIMPills.Revit.Commands.MCPIntegration.MCPIntegrationRevitCommand",
                assemblyPath:        revitDll,
                iconKey:             "connect");
        }
    }
}
