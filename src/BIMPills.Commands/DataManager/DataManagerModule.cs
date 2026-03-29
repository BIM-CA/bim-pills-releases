using BIMPills.Core.Modules;
using System.IO;
using System.Reflection;

namespace BIMPills.Commands.DataManager
{
    public sealed class DataManagerModule : IPluginModule
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
                buttonName:          "Gestionar",
                tooltip:             "Exporta tablas de planificación a Excel e importa valores editados de vuelta al modelo.",
                commandTypeFullName: "BIMPills.Revit.Commands.DataManager.DataManagerRevitCommand",
                assemblyPath:        revitDll,
                iconKey:             "datamanager");
        }
    }
}
