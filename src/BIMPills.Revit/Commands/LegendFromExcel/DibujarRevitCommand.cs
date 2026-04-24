using Autodesk.Revit.DB;
using BIMPills.Commands.LegendFromExcel;
using BIMPills.Core.Commands;
using BIMPills.Core.LegendFromExcel;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.DI;
using BIMPills.Infrastructure.LegendFromExcel;
using BIMPills.Revit.Commands;
using BIMPills.UI.Documentacion;
using BIMPills.UI.Shared;
using System;

namespace BIMPills.Revit.Commands.LegendFromExcel
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class DibujarRevitCommand : RevitCommandBase
    {
        protected override IPluginCommand CreateCommand()
            => new DrawExcelLegendCommand();

        protected override void OnSuccess(IPluginCommand command)
        {
            var doc    = CommandData!.Application.ActiveUIDocument.Document;
            var logger = ServiceLocator.IsRegistered<ILogger>() ? ServiceLocator.Get<ILogger>() : null;

            var textStyles = RevitProjectStylesService.GetTextStyles(doc);
            var lineStyles = RevitProjectStylesService.GetLineStyles(doc);
            var fillTypes  = RevitProjectStylesService.GetFillRegionTypes(doc);

            logger?.Info($"[Dibujar] Estilos cargados — texto:{textStyles.Count}, línea:{lineStyles.Count}, relleno:{fillTypes.Count}");

            var window = new DocumentacionWindow();
            window.SetDocumentName(doc.Title);

            window.InitializeDibujar(
                textStyles, lineStyles, fillTypes,
                drawCallback: (filePath, options) =>
                {
                    try
                    {
                        logger?.Info($"[Dibujar] Parseando Excel: {filePath}");
                        var table = ExcelTableParser.Parse(filePath);
                        logger?.Info($"[Dibujar] Tabla: {table.RowCount}×{table.ColumnCount}, {table.Cells.Count} celdas");

                        View legendView;
                        using (var tx = new Transaction(doc, "Crear Vista Dibujo"))
                        {
                            tx.Start();
                            legendView = LegendViewBuilder.CreateOrGet(doc, options.ViewName);
                            tx.Commit();
                        }

                        var result = LegendGeometryWriter.Draw(doc, legendView, table, options);
                        logger?.Info($"[Dibujar] {result.CellsDrawn} celdas dibujadas, éxito={result.Success}");

                        if (!result.Success)
                        {
                            BimPillsDialog.Error("Dibujar Leyenda", "Error al dibujar la leyenda.",
                                detail: result.ErrorMessage, owner: window);
                            return false;
                        }

                        BimPillsDialog.Info("Dibujar Leyenda",
                            $"Vista '{options.ViewName}' creada con {result.CellsDrawn} celdas.",
                            owner: window);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        logger?.Error("[Dibujar] Error inesperado", ex);
                        BimPillsDialog.Error("Dibujar Leyenda", "Error inesperado al dibujar.",
                            detail: ex.Message, owner: window);
                        return false;
                    }
                });

            window.NavigateToTab("leyenda");
            window.ShowDialogOverRevit();
        }
    }
}
