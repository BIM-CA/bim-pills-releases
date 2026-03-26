using BIMPills.Core.Modules;
using System.IO;
using System.Reflection;

namespace BIMPills.Commands.About
{
    public sealed class AboutModule : IPluginModule
    {
        public string TabName   => "BIM Pills";
        public string PanelName => "Información";

        public void BuildRibbon(IRibbonBuilder builder)
        {
            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            var imageDir     = Path.Combine(Path.GetDirectoryName(assemblyPath)!, "Images");

            builder.AddPushButton(
                panelName:           PanelName,
                buttonName:          "Acerca de",
                tooltip:             "Información sobre BIM Pills y BIM-CA",
                commandTypeFullName: "BIMPills.Revit.Commands.About.AboutRevitCommand",
                assemblyPath:        Path.Combine(Path.GetDirectoryName(assemblyPath)!, "BIMPills.Revit.dll"),
                largeImagePath:      Path.Combine(imageDir, "about_32.png"));
        }
    }
}
