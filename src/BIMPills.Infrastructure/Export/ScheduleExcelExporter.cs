using BIMPills.Core.Models;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.IO;

namespace BIMPills.Infrastructure.Export
{
    /// <summary>
    /// Exports ScheduleData snapshots to .xlsx files using ClosedXML.
    /// The first column is always "ElementId" (hidden by default) so the importer
    /// can match rows back to Revit elements.
    /// Read-only columns are highlighted in light yellow.
    /// </summary>
    public class ScheduleExcelExporter
    {
        public string Export(ScheduleData data, string destinationFolder) =>
            ExportToPath(data, null, destinationFolder);

        public string Export(ScheduleData data, string destinationFolder, string? customFileName) =>
            ExportToPath(data, customFileName, destinationFolder);

        /// <summary>
        /// Exports multiple schedules into a single .xlsx file, one sheet per schedule.
        /// </summary>
        public string ExportMultiple(IReadOnlyList<ScheduleData> schedules, string destinationFolder, string? customFileName)
        {
            if (schedules == null || schedules.Count == 0)
                throw new ArgumentException("No hay tablas para exportar.");

            Directory.CreateDirectory(destinationFolder);

            string filePath;
            if (!string.IsNullOrEmpty(customFileName))
            {
                var ext = Path.GetExtension(customFileName);
                if (string.IsNullOrEmpty(ext)) customFileName += ".xlsx";
                filePath = Path.Combine(destinationFolder, customFileName);
            }
            else
            {
                filePath = Path.Combine(destinationFolder, $"Tablas_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }

            using var wb = new XLWorkbook();

            // Track sheet names to avoid duplicates
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var data in schedules)
            {
                var sheetName = SanitizeSheetName(data.Schedule.Name);

                // Ensure unique sheet name
                var baseName = sheetName;
                int suffix = 2;
                while (usedNames.Contains(sheetName))
                {
                    sheetName = baseName.Length > 27 ? baseName.Substring(0, 27) + $" ({suffix})" : $"{baseName} ({suffix})";
                    suffix++;
                }
                usedNames.Add(sheetName);

                var ws = wb.Worksheets.Add(sheetName);
                WriteScheduleSheet(ws, data);
            }

            wb.SaveAs(filePath);
            return filePath;
        }

        private string ExportToPath(ScheduleData data, string? customFileName, string destinationFolder)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            Directory.CreateDirectory(destinationFolder);

            var safeName = string.Join("_", data.Schedule.Name.Split(Path.GetInvalidFileNameChars()));
            string filePath;
            if (!string.IsNullOrEmpty(customFileName))
            {
                var ext = Path.GetExtension(customFileName);
                if (string.IsNullOrEmpty(ext)) customFileName += ".xlsx";
                filePath = Path.Combine(destinationFolder, customFileName);
            }
            else
            {
                var fileName = $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                filePath = Path.Combine(destinationFolder, fileName);
            }

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add(safeName.Length > 31 ? safeName.Substring(0, 31) : safeName);
            WriteScheduleSheet(ws, data);
            wb.SaveAs(filePath);
            return filePath;
        }

        private static void WriteScheduleSheet(IXLWorksheet ws, ScheduleData data)
        {
            // ── Row 1: schedule title ────────────────────────────────────────
            var titleCell = ws.Cell(1, 1);
            titleCell.Value = data.Schedule.Name;
            titleCell.Style.Font.Bold = true;
            titleCell.Style.Font.FontSize = 13;
            titleCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#212B37");
            titleCell.Style.Font.FontColor = XLColor.White;
            ws.Range(1, 1, 1, data.Columns.Count + 1).Merge();

            // ── Row 2: column headers ────────────────────────────────────────
            var idHeader = ws.Cell(2, 1);
            idHeader.Value = "ElementId";
            idHeader.Style.Font.Bold = true;
            idHeader.Style.Fill.BackgroundColor = XLColor.FromHtml("#E5E5EA");
            idHeader.Style.Font.FontColor = XLColor.FromHtml("#86868B");
            ws.Column(1).Hide();

            for (int c = 0; c < data.Columns.Count; c++)
            {
                var col = data.Columns[c];
                var headerCell = ws.Cell(2, c + 2);
                headerCell.Value = col.Name;
                headerCell.Style.Font.Bold = true;
                headerCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E5E5EA");
                if (col.IsReadOnly)
                    headerCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF9C4");
            }

            // ── Rows 3+: data ────────────────────────────────────────────────
            int excelRow = 3;
            for (int r = 0; r < data.Rows.Count; r++)
            {
                long elementId = data.ElementIds.Count > r ? data.ElementIds[r] : 0L;
                if (elementId == 0) continue;

                ws.Cell(excelRow, 1).Value = elementId;

                var row = data.Rows[r];
                for (int c = 0; c < data.Columns.Count; c++)
                {
                    var cell = ws.Cell(excelRow, c + 2);
                    cell.Value = c < row.Count ? row[c] : "";

                    if (data.Columns[c].IsReadOnly)
                    {
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFFDE7");
                        cell.Style.Protection.Locked = true;
                    }
                }

                if (excelRow % 2 == 1)
                {
                    for (int c = 2; c <= data.Columns.Count + 1; c++)
                    {
                        if (!data.Columns[c - 2].IsReadOnly)
                            ws.Cell(excelRow, c).Style.Fill.BackgroundColor = XLColor.FromHtml("#FAFAFA");
                    }
                }

                excelRow++;
            }

            // ── Column widths ────────────────────────────────────────────────
            for (int c = 2; c <= data.Columns.Count + 1; c++)
                ws.Column(c).AdjustToContents(2, Math.Min(data.Rows.Count + 2, 500));

            ws.SheetView.FreezeRows(2);
        }

        private static string SanitizeSheetName(string name)
        {
            // Excel sheet name max 31 chars, no special chars
            var invalid = new[] { '\\', '/', '?', '*', '[', ']', ':' };
            foreach (var c in invalid)
                name = name.Replace(c, '_');
            return name.Length > 31 ? name.Substring(0, 31) : name;
        }
    }
}
