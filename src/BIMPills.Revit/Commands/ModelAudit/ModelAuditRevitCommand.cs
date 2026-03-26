using BIMPills.Commands.ModelAudit;
using BIMPills.Core.Commands;
using BIMPills.Revit.Commands;
using BIMPills.UI.ModelAudit;

namespace BIMPills.Revit.Commands.ModelAudit
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.ReadOnly)]
    public sealed class ModelAuditRevitCommand : RevitCommandBase
    {
        protected override IPluginCommand CreateCommand()
            => new ModelAuditCommand();

        protected override void OnSuccess(IPluginCommand command)
        {
            if (ModelAuditCommand.LastResult != null)
                new ModelAuditWindow(ModelAuditCommand.LastResult).ShowDialog();
        }
    }
}
