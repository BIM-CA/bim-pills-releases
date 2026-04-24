using BIMPills.Core.LegendFromExcel;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMPills.Infrastructure.LegendFromExcel
{
    public static class ExcelTableParser
    {
        /// <summary>
        /// Parsea la primera hoja del workbook (o <paramref name="sheetName"/> si se especifica)
        /// y devuelve un <see cref="ExcelTableModel"/> con todas las celdas del rango usado,
        /// incluyendo merged cells con RowSpan/ColSpan correctos.
        /// </summary>
        public static ExcelTableModel Parse(string filePath, string? sheetName = null)
        {
            using var workbook = new XLWorkbook(filePath);
            var ws = sheetName != null
                ? workbook.Worksheet(sheetName)
                : workbook.Worksheets.First();

            var usedRange = ws.RangeUsed();
            if (usedRange == null)
                return new ExcelTableModel { Cells = new List<ExcelCellModel>() };

            int firstRow = usedRange.FirstRow().RowNumber();
            int lastRow  = usedRange.LastRow().RowNumber();
            int firstCol = usedRange.FirstColumn().ColumnNumber();
            int lastCol  = usedRange.LastColumn().ColumnNumber();

            var cells = new List<ExcelCellModel>();

            for (int r = firstRow; r <= lastRow; r++)
            {
                for (int c = firstCol; c <= lastCol; c++)
                {
                    var cell = ws.Cell(r, c);

                    // Si la celda pertenece a un rango fusionado pero NO es la celda ancla, saltar
                    if (cell.IsMerged())
                    {
                        var merged = cell.MergedRange();
                        if (merged != null)
                        {
                            var anchor = merged.FirstCell();
                            if (anchor.Address.RowNumber != r || anchor.Address.ColumnNumber != c)
                                continue;
                        }
                    }

                    int rowSpan = 1, colSpan = 1;
                    if (cell.IsMerged())
                    {
                        var merged = cell.MergedRange();
                        if (merged != null)
                        {
                            rowSpan = merged.RowCount();
                            colSpan = merged.ColumnCount();
                        }
                    }

                    string? bgHex = null;
                    try
                    {
                        var fill = cell.Style.Fill;
                        if (fill.PatternType != XLFillPatternValues.None &&
                            fill.BackgroundColor.ColorType != XLColorType.Theme)
                        {
                            var color = fill.BackgroundColor.Color;
                            if (color.R != 255 || color.G != 255 || color.B != 255)
                                bgHex = $"{color.R:X2}{color.G:X2}{color.B:X2}";
                        }
                    }
                    catch { }

                    cells.Add(new ExcelCellModel
                    {
                        Row                = r - firstRow,
                        Column             = c - firstCol,
                        Text               = cell.GetFormattedString() ?? string.Empty,
                        BackgroundColorHex = bgHex,
                        IsMerged           = cell.IsMerged(),
                        RowSpan            = rowSpan,
                        ColSpan            = colSpan,
                    });
                }
            }

            return new ExcelTableModel
            {
                Cells       = cells,
                RowCount    = lastRow - firstRow + 1,
                ColumnCount = lastCol - firstCol + 1,
            };
        }
    }
}
