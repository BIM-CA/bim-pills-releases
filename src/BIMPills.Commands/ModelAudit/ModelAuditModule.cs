using BIMPills.Core.Modules;
using System.IO;
using System.Reflection;

namespace BIMPills.Commands.ModelAudit
{
    public sealed class ModelAuditModule : IPluginModule
    {
        public string TabName   => "BIM Pills";
        public string PanelName => "Auditoría";

        public void BuildRibbon(IRibbonBuilder builder)
        {
            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            var imageDir     = Path.Combine(Path.GetDirectoryName(assemblyPath)!, "Images");

            builder.AddPushButton(
                panelName:           PanelName,
                buttonName:          "Auditar Modelo",
                tooltip:             "Analiza advertencias, familias, vistas sin colocar y elementos huérfanos del modelo activo.",
                commandTypeFullName: "BIMPills.Revit.Commands.ModelAudit.ModelAuditRevitCommand",
                assemblyPath:        Path.Combine(Path.GetDirectoryName(assemblyPath)!, "BIMPills.Revit.dll"),
                largeImagePath:      Path.Combine(imageDir, "audit_32.png"));
        }
    }
}
