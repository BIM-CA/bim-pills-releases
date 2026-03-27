using Autodesk.Revit.DB;
using BIMPills.Commands.Documentacion;
using BIMPills.Core.Commands;
using BIMPills.Core.Documentacion;
using BIMPills.Revit.Commands;
using BIMPills.Revit.Documentacion;
using BIMPills.UI.Documentacion;
using System;

namespace BIMPills.Revit.Commands.Documentacion
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class DocumentacionRevitCommand : RevitCommandBase
    {
        protected override IPluginCommand CreateCommand()
            => new AcotadoVanosCommand();

        protected override void OnSuccess(IPluginCommand command)
        {
            var data = AcotadoVanosCommand.LastResult;
            if (data == null) return;

            // Callback que ejecuta la creación de cotas en Revit
            Func<AcotadoVanosSettings, AcotadoVanosResult> executeCallback = (settings) =>
            {
                var doc = CommandData!.Application.ActiveUIDocument.Document;
                if (settings.Scheme == "wall-chain")
                    return DoorDimensioningService.CreateWallChainDimensions(doc, settings);
                else
                    return DoorDimensioningService.CreateOpeningWidthDimensions(doc, settings);
            };

            new AcotadoVanosWindow(data, executeCallback).ShowDialog();
        }
    }
}
