using BIMPills.Commands.About;
using BIMPills.Core.Commands;
using BIMPills.Revit.Commands;
using BIMPills.UI.About;

namespace BIMPills.Revit.Commands.About
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.ReadOnly)]
    public sealed class AboutRevitCommand : RevitCommandBase
    {
        // About debe ser siempre accesible — es desde donde el usuario activa su licencia
        protected override bool RequiresLicense => false;

        protected override IPluginCommand CreateCommand()
            => new AboutCommand();

        protected override void OnSuccess(IPluginCommand command)
        {
            if (AboutCommand.LastResult != null)
                new AboutWindow(AboutCommand.LastResult).ShowDialog();
        }
    }
}
