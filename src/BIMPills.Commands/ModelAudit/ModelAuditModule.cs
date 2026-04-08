using BIMPills.Core.Modules;
using System.IO;
using System.Reflection;

namespace BIMPills.Commands.ModelAudit
{
    public sealed class ModelAuditModule : IPluginModule
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
                buttonName:          "Auditar",
                tooltip:             "Analiza advertencias, familias, vistas sin colocar y elementos huérfanos del modelo activo.",
                commandTypeFullName: "BIMPills.Revit.Commands.ModelAudit.ModelAuditRevitCommand",
                assemblyPath:        revitDll,
                iconKey:             "audit");
        }
    }
}
