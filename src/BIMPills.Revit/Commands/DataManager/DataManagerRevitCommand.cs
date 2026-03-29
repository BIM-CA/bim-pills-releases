using BIMPills.Commands.DataManager;
using BIMPills.Core.Commands;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.DI;
using BIMPills.Revit.Commands;
using BIMPills.Revit.Context;
using BIMPills.UI.DataManager;

namespace BIMPills.Revit.Commands.DataManager
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class DataManagerRevitCommand : RevitCommandBase
    {
        protected override IPluginCommand CreateCommand()
            => new DataManagerCommand();

        protected override void OnSuccess(IPluginCommand command)
        {
            var logger = ServiceLocator.IsRegistered<ILogger>()
                ? ServiceLocator.Get<ILogger>() : null;

            var doc = CommandData!.Application.ActiveUIDocument.Document;
            var documentServices = new RevitDocumentServices(doc);

            var modelName = doc.Title ?? "Modelo";
            var window = new GestionarWindow(documentServices, modelName);
            window.ShowDialog();
        }
    }
}
