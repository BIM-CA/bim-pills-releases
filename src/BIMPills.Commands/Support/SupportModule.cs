using BIMPills.Core.Modules;
using System.IO;
using System.Reflection;

namespace BIMPills.Commands.Support
{
    public sealed class SupportModule : IPluginModule
    {
        public string TabName   => "BIM Pills";
        public string PanelName => "Información";

        public void BuildRibbon(IRibbonBuilder builder)
        {
            var revitDll = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
                "BIMPills.Revit.dll");

            builder.AddPushButton(
                tabName:             TabName,
                panelName:           PanelName,
                buttonName:          "Soporte",
                tooltip:             "Contactar al equipo de BIM-CA",
                commandTypeFullName: "BIMPills.Revit.Commands.Support.SupportRevitCommand",
                assemblyPath:        revitDll,
                iconKey:             "support");
        }
    }
}
