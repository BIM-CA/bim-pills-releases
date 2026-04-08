using BIMPills.Core.Modules;
using System.IO;
using System.Reflection;

namespace BIMPills.Commands.Ordering
{
    public sealed class OrderingModule : IPluginModule
    {
        public string TabName   => "BIM Pills";
        public string PanelName => "Datos";

        public void BuildRibbon(IRibbonBuilder builder)
        {
            var revitDll = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
                "BIMPills.Revit.dll");

            builder.AddPushButton(
                tabName:             TabName,
                panelName:           PanelName,
                buttonName:          "Ordenar",
                tooltip:             "Asigna valores incrementales a parámetros de elementos seleccionando uno a uno en la vista.",
                commandTypeFullName: "BIMPills.Revit.Commands.Ordering.OrdenarRevitCommand",
                assemblyPath:        revitDll,
                iconKey:             "ordering");
        }
    }
}
