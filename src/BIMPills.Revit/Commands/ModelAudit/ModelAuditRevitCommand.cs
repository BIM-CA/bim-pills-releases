using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPills.Commands.ModelAudit;
using BIMPills.Revit.Commands;
using BIMPills.UI.ModelAudit;

namespace BIMPills.Revit.Commands.ModelAudit
{
    /// <summary>
    /// Revit entry point for Model Audit.
    /// Delegates business logic to ModelAuditCommand, then shows the results window.
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.ReadOnly)]
    public sealed class ModelAuditRevitCommand : RevitCommandBase
    {
        protected override Core.Commands.IPluginCommand CreateCommand()
            => new ModelAuditCommand();

        // Override Execute to show the UI after the command runs
        public new Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            var result = base.Execute(commandData, ref message, elements);

            if (result == Result.Succeeded && ModelAuditCommand.LastResult != null)
            {
                var window = new ModelAuditWindow(ModelAuditCommand.LastResult);
                window.ShowDialog();
            }

            return result;
        }
    }
}
