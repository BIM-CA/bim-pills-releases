using BIMPills.Commands.Support;
using BIMPills.Core.Commands;
using BIMPills.Revit.Commands;
using BIMPills.UI.Shared;
using BIMPills.UI.Support;

namespace BIMPills.Revit.Commands.Support
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.ReadOnly)]
    public sealed class SupportRevitCommand : RevitCommandBase
    {
        // Soporte debe ser siempre accesible — no requiere licencia activa
        protected override bool RequiresLicense => false;

        protected override IPluginCommand CreateCommand()
            => new SupportCommand();

        protected override void OnSuccess(IPluginCommand command)
        {
            new SupportWindow().ShowDialogOverRevit();
        }
    }
}
