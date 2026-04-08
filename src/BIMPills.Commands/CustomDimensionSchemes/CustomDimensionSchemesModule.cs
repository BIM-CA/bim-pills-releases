using BIMPills.Core.Modules;
using System.IO;
using System.Reflection;

namespace BIMPills.Commands.CustomDimensionSchemes
{
    public sealed class CustomDimensionSchemesModule : IPluginModule
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
                buttonName:          "Esquemas",
                tooltip:             "Crea, edita y gestiona esquemas de cotas personalizados basados en reglas y funciones.",
                commandTypeFullName: "BIMPills.Revit.Commands.CustomDimensionSchemes.CustomDimensionSchemesRevitCommand",
                assemblyPath:        revitDll,
                iconKey:             "dimension");
        }
    }
}
