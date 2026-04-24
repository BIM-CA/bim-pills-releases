using Autodesk.Revit.DB;
using BIMPills.Commands.Documentacion;
using BIMPills.Core.Commands;
using BIMPills.Core.Documentacion;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.DI;
using BIMPills.Infrastructure.LegendFromExcel;
using BIMPills.Revit.Commands;
using BIMPills.Revit.Commands.LegendFromExcel;
using BIMPills.Revit.Documentacion;
using BIMPills.UI.Documentacion;
using BIMPills.UI.Shared;
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
                        "wall-chain"      => DoorDimensioningService.CreateWallChainDimensions(doc, settings),
                        "grid-combined"   => DoorDimensioningService.CreateGridDimensions(doc, settings),
                        "interior-spaces" => DoorDimensioningService.CreateInteriorSpaceDimensions(doc, settings),
                        "arq-levels"      => DoorDimensioningService.CreateArqLevelDimensions(doc, settings),
                        "exterior-walls"  => DoorDimensioningService.CreateExteriorWallDimensions(doc, settings),
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

            // Inicializar tab Leyenda Excel con estilos del modelo
            try
            {
                var doc = CommandData!.Application.ActiveUIDocument.Document;
                var textStyles = RevitProjectStylesService.GetTextStyles(doc);
                var lineStyles = RevitProjectStylesService.GetLineStyles(doc);
                var fillTypes  = RevitProjectStylesService.GetFillRegionTypes(doc);
                logger?.Info($"[Documentar] Estilos Leyenda — texto:{textStyles.Count}, línea:{lineStyles.Count}, relleno:{fillTypes.Count}");

                window.InitializeDibujar(textStyles, lineStyles, fillTypes,
                    drawCallback: (filePath, options) =>
                    {
                        try
                        {
                            var table = ExcelTableParser.Parse(filePath);
                            View legendView;
                            using (var tx = new Transaction(doc, "Crear Vista Dibujo"))
                            {
                                tx.Start();
                                legendView = LegendViewBuilder.CreateOrGet(doc, options.ViewName);
                                tx.Commit();
                            }
                            var result = LegendGeometryWriter.Draw(doc, legendView, table, options);
                            if (!result.Success)
                            {
                                BimPillsDialog.Error("Leyenda desde Excel", "Error al dibujar.", detail: result.ErrorMessage, owner: window);
                                return false;
                            }
                            BimPillsDialog.Info("Leyenda desde Excel", $"Vista '{options.ViewName}' creada con {result.CellsDrawn} celdas.", owner: window);
                            return true;
                        }
                        catch (Exception ex)
                        {
                            logger?.Error("[Documentar/Dibujar] Error", ex);
                            BimPillsDialog.Error("Leyenda desde Excel", "Error inesperado.", detail: ex.Message, owner: window);
                            return false;
                        }
                    });
            }
            catch (Exception ex)
            {
                logger?.Warning($"[Documentar] No se pudieron cargar estilos para Leyenda Excel: {ex.Message}");
            }

            window.ShowDialogOverRevit();
        }
    }
}
