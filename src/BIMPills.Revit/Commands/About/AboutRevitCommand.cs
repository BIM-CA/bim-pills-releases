using BIMPills.Commands.About;
using BIMPills.Core.Commands;
using BIMPills.Revit.Commands;
using BIMPills.UI.About;

namespace BIMPills.Revit.Commands.About
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.ReadOnly)]
    public sealed class AboutRevitCommand : RevitCommandBase
    {
        protected override IPluginCommand CreateCommand()
            => new AboutCommand();

        protected override void OnSuccess(IPluginCommand command)
        {
            if (AboutCommand.LastResult != null)
                new AboutWindow(AboutCommand.LastResult).ShowDialog();
        }
    }
}
