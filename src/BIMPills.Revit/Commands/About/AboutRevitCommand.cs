using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPills.Commands.About;
using BIMPills.Revit.Commands;
using BIMPills.UI.About;

namespace BIMPills.Revit.Commands.About
{
    /// <summary>
    /// Punto de entrada de Revit para el comando Acerca de.
    /// Delega la lógica a AboutCommand y luego muestra la ventana de información.
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.ReadOnly)]
    public sealed class AboutRevitCommand : RevitCommandBase
    {
        protected override Core.Commands.IPluginCommand CreateCommand()
            => new AboutCommand();

        public new Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            var result = base.Execute(commandData, ref message, elements);

            if (result == Result.Succeeded && AboutCommand.LastResult != null)
            {
                var window = new AboutWindow(AboutCommand.LastResult);
                window.ShowDialog();
            }

            return result;
        }
    }
}
