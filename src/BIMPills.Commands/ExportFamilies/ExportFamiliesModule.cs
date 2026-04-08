using BIMPills.Core.Modules;
using System.IO;
using System.Reflection;

namespace BIMPills.Commands.ExportFamilies
{
    public sealed class ExportFamiliesModule : IPluginModule
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
                buttonName:          "Exportar",
                tooltip:             "Exporta las familias del modelo a una carpeta local organizadas por categoría",
                commandTypeFullName: "BIMPills.Revit.Commands.ExportFamilies.ExportFamiliesRevitCommand",
                assemblyPath:        revitDll,
                iconKey:             "export");
        }
    }
}
