using Autodesk.Revit.DB;
using BIMPills.Core.LegendFromExcel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMPills.Revit.Commands.LegendFromExcel
{
    internal static class LegendGeometryWriter
    {
        private const double MmToFeet = 1.0 / 304.8;

        /// <summary>
        /// Dibuja la tabla Excel como geometría de detalle en una vista Leyenda.
        /// Gestiona su propia Transaction.
        /// </summary>
        public static LegendDrawResult Draw(
            Document doc, View view, ExcelTableModel table, LegendDrawOptions options)
        {
            if (table.RowCount == 0 || table.ColumnCount == 0)
                return new LegendDrawResult { ErrorMessage = "La tabla Excel está vacía." };

            double cellW = options.CellWidthMm  * MmToFeet;
            double cellH = options.CellHeightMm * MmToFeet;

            var defaultTextId    = GetDefaultTextTypeId(doc);
            var valuesTextTypeId = options.TextStyleIdValues  > 0 ? new ElementId(options.TextStyleIdValues)  : defaultTextId;
            var header1TextId    = options.TextStyleIdHeader1 > 0 ? new ElementId(options.TextStyleIdHeader1) : valuesTextTypeId;
            var header2TextId    = options.TextStyleIdHeader2 > 0 ? new ElementId(options.TextStyleIdHeader2) : header1TextId;

            GraphicsStyle? lineStyle = null;
            if (options.LineStyleId > 0)
                lineStyle = doc.GetElement(new ElementId(options.LineStyleId)) as GraphicsStyle;

            ElementId? fill1Id = options.ApplyFill && options.FillRegionTypeId1 > 0 ? new ElementId(options.FillRegionTypeId1) : null;
            ElementId? fill2Id = options.ApplyFill && options.FillRegionTypeId2 > 0 ? new ElementId(options.FillRegionTypeId2) : null;

            int cellsDrawn = 0;

            using var tx = new Transaction(doc, "Dibujar Leyenda desde Excel");
            tx.Start();
            try
            {
                foreach (var cell in table.Cells)
                {
                    double x1 = cell.Column * cellW;
                    double y1 = -(cell.Row  * cellH);
                    double x2 = x1 + cell.ColSpan * cellW;
                    double y2 = y1 - cell.RowSpan * cellH;

                    // 1. Relleno por fila de encabezado
                    if (cell.Row == 0 && options.HeaderRowsCount >= 1 && fill1Id != null)
                        TryDrawFill(doc, view, x1, y1, x2, y2, fill1Id);
                    else if (cell.Row == 1 && options.HeaderRowsCount >= 2 && fill2Id != null)
                        TryDrawFill(doc, view, x1, y1, x2, y2, fill2Id);

                    // 2. Borde de celda
                    DrawCellBorder(doc, view, x1, y1, x2, y2, lineStyle);

                    // 3. Texto — estilo por fila
                    ElementId textTypeId;
                    if (options.DifferentiateHeader && cell.Row == 0 && options.HeaderRowsCount >= 1)
                        textTypeId = header1TextId;
                    else if (options.DifferentiateHeader && cell.Row == 1 && options.HeaderRowsCount >= 2)
                        textTypeId = header2TextId;
                    else
                        textTypeId = valuesTextTypeId;

                    if (!string.IsNullOrEmpty(cell.Text) && textTypeId != ElementId.InvalidElementId)
                    {
                        double textX = x1 + 1.5 * MmToFeet;
                        // El origen del TextNote es la esquina superior-izquierda del texto.
                        // Para centrarlo verticalmente: origen = centro_celda + mitad_altura_texto.
                        double textHeight = GetTextHeight(doc, textTypeId);
                        double textY = (y1 + y2) / 2.0 + textHeight / 2.0;
                        try
                        {
                            TextNote.Create(doc, view.Id, new XYZ(textX, textY, 0), cell.Text, textTypeId);
                        }
                        catch { /* texto inválido o tipo incompatible — ignorar celda */ }
                    }

                    cellsDrawn++;
                }

                tx.Commit();
                return new LegendDrawResult
                {
                    Success    = true,
                    CellsDrawn = cellsDrawn,
                    ViewId     = GetId(view.Id),
                };
            }
            catch (Exception ex)
            {
                if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack();
                return new LegendDrawResult { ErrorMessage = ex.Message };
            }
        }

        private static void DrawCellBorder(
            Document doc, View view,
            double x1, double y1, double x2, double y2,
            GraphicsStyle? style)
        {
            var corners = new[]
            {
                (new XYZ(x1, y1, 0), new XYZ(x2, y1, 0)),
                (new XYZ(x2, y1, 0), new XYZ(x2, y2, 0)),
                (new XYZ(x2, y2, 0), new XYZ(x1, y2, 0)),
                (new XYZ(x1, y2, 0), new XYZ(x1, y1, 0)),
            };

            foreach (var (a, b) in corners)
            {
                try
                {
                    var dl = doc.Create.NewDetailCurve(view, Line.CreateBound(a, b));
                    if (style != null && dl is DetailLine detailLine)
                        detailLine.LineStyle = style;
                }
                catch { /* vértices coincidentes en celdas degeneradas */ }
            }
        }

        private static void TryDrawFill(
            Document doc, View view,
            double x1, double y1, double x2, double y2,
            ElementId fillTypeId)
        {
            try
            {
                var loop = new CurveLoop();
                loop.Append(Line.CreateBound(new XYZ(x1, y1, 0), new XYZ(x2, y1, 0)));
                loop.Append(Line.CreateBound(new XYZ(x2, y1, 0), new XYZ(x2, y2, 0)));
                loop.Append(Line.CreateBound(new XYZ(x2, y2, 0), new XYZ(x1, y2, 0)));
                loop.Append(Line.CreateBound(new XYZ(x1, y2, 0), new XYZ(x1, y1, 0)));
                FilledRegion.Create(doc, fillTypeId, view.Id, new List<CurveLoop> { loop });
            }
            catch { /* relleno opcional — ignorar errores */ }
        }

        private static ElementId GetDefaultTextTypeId(Document doc)
        {
            var type = new FilteredElementCollector(doc)
                .OfClass(typeof(TextNoteType))
                .FirstElement();
            return type?.Id ?? ElementId.InvalidElementId;
        }

        private static double GetTextHeight(Document doc, ElementId typeId)
        {
            const double FallbackMm = 2.5;
            if (doc.GetElement(typeId) is TextNoteType tnt)
            {
                var p = tnt.get_Parameter(BuiltInParameter.TEXT_SIZE);
                if (p != null) return p.AsDouble();
            }
            return FallbackMm * MmToFeet;
        }

        private static long GetId(ElementId id)
        {
#if REVIT2024
#pragma warning disable CS0618
            return (long)id.IntegerValue;
#pragma warning restore CS0618
#else
            return id.Value;
#endif
        }
    }
}
