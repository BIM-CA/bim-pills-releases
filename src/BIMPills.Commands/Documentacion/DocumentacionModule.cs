using BIMPills.Core.Modules;
using System.IO;
using System.Reflection;

namespace BIMPills.Commands.Documentacion
{
    public sealed class DocumentacionModule : IPluginModule
    {
        public string TabName   => "BIMPills";
        public string PanelName => "Procesos";

        public void BuildRibbon(IRibbonBuilder builder)
        {
            var revitDll = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
                "BIMPills.Revit.dll");

            builder.AddPushButton(
                tabName:             TabName,
                panelName:           PanelName,
                buttonName:          "Documentación",
                tooltip:             "Herramientas de documentación del modelo (próximamente)",
                commandTypeFullName: "BIMPills.Revit.Commands.Documentacion.DocumentacionRevitCommand",
                assemblyPath:        revitDll,
                iconKey:             "documentacion");
        }
    }
}
