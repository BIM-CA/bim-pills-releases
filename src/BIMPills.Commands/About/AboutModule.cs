using BIMPills.Core.Modules;
using System.IO;
using System.Reflection;

namespace BIMPills.Commands.About
{
    public sealed class AboutModule : IPluginModule
    {
        public string TabName   => "BIMPills";
        public string PanelName => "Información";

        public void BuildRibbon(IRibbonBuilder builder)
        {
            var revitDll = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
                "BIMPills.Revit.dll");

            builder.AddPushButton(
                tabName:             TabName,
                panelName:           PanelName,
                buttonName:          "Acerca de",
                tooltip:             "Información sobre BIMPills y BIM-CA",
                commandTypeFullName: "BIMPills.Revit.Commands.About.AboutRevitCommand",
                assemblyPath:        revitDll,
                iconKey:             "about");
        }
    }
}
