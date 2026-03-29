using BIMPills.Core.Modules;
using System.IO;
using System.Reflection;

namespace BIMPills.Commands.Gestion
{
    public sealed class GestionModule : IPluginModule
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
                buttonName:          "Estandarizar",
                tooltip:             "Crear y gestionar subproyectos (Worksets) del modelo",
                commandTypeFullName: "BIMPills.Revit.Commands.Gestion.GestionRevitCommand",
                assemblyPath:        revitDll,
                iconKey:             "gestion");
        }
    }
}
