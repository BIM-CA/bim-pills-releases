using BIMPills.Core.Modules;
using System.IO;
using System.Reflection;

namespace BIMPills.Commands.Seleccionar
{
    public sealed class SeleccionarModule : IPluginModule
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
                buttonName:          "Organizar",
                tooltip:             "Buscar y seleccionar elementos, numerarlos en secuencia y asignar valores de parámetros en lote",
                commandTypeFullName: "BIMPills.Revit.Commands.Seleccionar.SeleccionarRevitCommand",
                assemblyPath:        revitDll,
                iconKey:             "organize");
        }
    }
}
