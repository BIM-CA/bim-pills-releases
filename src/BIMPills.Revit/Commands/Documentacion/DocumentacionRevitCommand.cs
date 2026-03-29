using Autodesk.Revit.DB;
using BIMPills.Commands.Documentacion;
using BIMPills.Core.Commands;
using BIMPills.Core.Documentacion;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.DI;
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
                var logger = ServiceLocator.IsRegistered<ILogger>() ? ServiceLocator.Get<ILogger>() : null;
                logger?.Info($"Ejecutando esquema de acotado: {settings.Scheme}");
                try
                {
                    var doc = CommandData!.Application.ActiveUIDocument.Document;
                    var result = settings.Scheme switch
                    {
                        "wall-chain" => DoorDimensioningService.CreateWallChainDimensions(doc, settings),
                        "grid-combined" => DoorDimensioningService.CreateGridDimensions(doc, settings),
                        "interior-spaces" => DoorDimensioningService.CreateInteriorSpaceDimensions(doc, settings),
                        "arq-levels" => DoorDimensioningService.CreateArqLevelDimensions(doc, settings),
                        _ => DoorDimensioningService.CreateOpeningWidthDimensions(doc, settings),
                    };
                    logger?.Info($"Acotado completado: {result.DimensionsCreated} cotas creadas.");
                    return result;
                }
                catch (Exception ex)
                {
                    logger?.Error($"Error en esquema '{settings.Scheme}'", ex);
                    throw; // re-throw so UI can show the error dialog
                }
            };

            var window = new DocumentacionWindow();

            // Set document name from Revit
            try
            {
                var docName = CommandData!.Application.ActiveUIDocument.Document.Title;
                window.SetDocumentName(docName);
            }
            catch { }

            var logger = ServiceLocator.IsRegistered<ILogger>() ? ServiceLocator.Get<ILogger>() : null;
            window.InitializeAcotado(data, executeCallback, logger);
            window.ShowDialog();
        }
    }
}
