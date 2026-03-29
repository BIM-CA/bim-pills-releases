using BIMPills.Commands.ModelAudit;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.DI;
using BIMPills.UI.ExportAudit;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIMPills.Revit.Commands.ExportAudit
{
    /// <summary>
    /// ExportAudit is launched from the ModelAudit window, not from a ribbon button.
    /// It simply opens the ExportAuditWindow with the last audit result.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public sealed class ExportAuditRevitCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var logger = ServiceLocator.IsRegistered<ILogger>() ? ServiceLocator.Get<ILogger>() : null;

            try
            {
                var auditResult = ModelAuditCommand.LastResult;
                var window = new ExportAuditWindow(auditResult);
                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                logger?.Error("Error al abrir ExportAuditWindow", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
