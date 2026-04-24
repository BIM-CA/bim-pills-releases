using BIMPills.Core.Modules;
using System.IO;
using System.Reflection;

namespace BIMPills.Commands.LegendFromExcel
{
    public sealed class DibujarModule : IPluginModule
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
                buttonName:          "Dibujar",
                tooltip:             "Dibuja leyendas y tablas en vistas de Revit a partir de un archivo Excel",
                commandTypeFullName: "BIMPills.Revit.Commands.LegendFromExcel.DibujarRevitCommand",
                assemblyPath:        revitDll,
                iconKey:             "dibujar");
        }
    }
}
