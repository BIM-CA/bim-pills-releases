using BIMPills.Core.Modules;
using System.IO;
using System.Reflection;

namespace BIMPills.Commands.Transfer
{
    public sealed class TransferModule : IPluginModule
    {
        public string TabName   => "BIM Pills";
        public string PanelName => "Procesos";

        public void BuildRibbon(IRibbonBuilder builder)
        {
            var revitDll = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
                "BIMPills.Revit.dll");

            builder.AddPushButton(
                tabName:             TabName,
                panelName:           PanelName,
                buttonName:          "Importar",
                tooltip:             "Importa plantillas de vista, filtros y normas de proyecto desde otros proyectos abiertos.",
                commandTypeFullName: "BIMPills.Revit.Commands.Transfer.TransferRevitCommand",
                assemblyPath:        revitDll,
                iconKey:             "transfer");
        }
    }
}
