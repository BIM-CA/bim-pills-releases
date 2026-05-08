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

            WriteInstructionsSheet(wb);
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
            WriteInstructionsSheet(wb);
            wb.SaveAs(filePath);
            return filePath;
        }

        // ── Column colour palette ────────────────────────────────────────────
        // Read-only:           yellow  — cannot be edited
        // Instance (writable): blue    — edits this element only
        // Type (writable):     orange  — edits the whole element type
        // Linked element:      gray    — element from a linked file (read-only in this model)
        private static readonly XLColor ColHeaderReadOnly  = XLColor.FromHtml("#FFF9C4");
        private static readonly XLColor ColCellReadOnly    = XLColor.FromHtml("#FFFDE7");
        private static readonly XLColor ColHeaderInstance  = XLColor.FromHtml("#BBDEFB");
        private static readonly XLColor ColCellInstance    = XLColor.FromHtml("#E3F2FD");
        private static readonly XLColor ColHeaderType      = XLColor.FromHtml("#FFE0B2");
        private static readonly XLColor ColCellType        = XLColor.FromHtml("#FFF3E0");
        private static readonly XLColor ColLinkedRow       = XLColor.FromHtml("#EEEEEE");
        private static readonly XLColor ColLinkedRowAlt    = XLColor.FromHtml("#F5F5F5");

        private static void WriteScheduleSheet(IXLWorksheet ws, ScheduleData data)
        {
            bool hasLinkedRows = data.IsLinkedRow.Count > 0 && data.IsLinkedRow.Exists(x => x);
            // +1 for hidden ElementId col, +1 extra for Origen col when links are present
            int totalCols = data.Columns.Count + 1 + (hasLinkedRows ? 1 : 0);
            int origenCol  = data.Columns.Count + 2; // only meaningful when hasLinkedRows

            // ── Row 1: schedule title ────────────────────────────────────────
            var titleCell = ws.Cell(1, 1);
            titleCell.Value = data.Schedule.Name;
            titleCell.Style.Font.Bold = true;
            titleCell.Style.Font.FontSize = 13;
            titleCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#212B37");
            titleCell.Style.Font.FontColor = XLColor.White;
            ws.Range(1, 1, 1, totalCols).Merge();

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

                if (col.IsReadOnly)
                    headerCell.Style.Fill.BackgroundColor = ColHeaderReadOnly;
                else if (col.IsTypeParameter)
                    headerCell.Style.Fill.BackgroundColor = ColHeaderType;
                else
                    headerCell.Style.Fill.BackgroundColor = ColHeaderInstance;
            }

            // "Origen" header — only when there are linked rows
            if (hasLinkedRows)
            {
                var origenHeader = ws.Cell(2, origenCol);
                origenHeader.Value = "Origen (vínculo)";
                origenHeader.Style.Font.Bold = true;
                origenHeader.Style.Fill.BackgroundColor = XLColor.FromHtml("#E0E0E0");
                origenHeader.Style.Font.FontColor = XLColor.FromHtml("#616161");
            }

            // ── Rows 3+: data ────────────────────────────────────────────────
            int excelRow = 3;
            for (int r = 0; r < data.Rows.Count; r++)
            {
                long elementId = data.ElementIds.Count > r ? data.ElementIds[r] : 0L;
                if (elementId == 0) continue;

                bool isLinked = data.IsLinkedRow.Count > r && data.IsLinkedRow[r];

                // Linked rows: write "L:<id>" so long.TryParse fails in the importer → row is skipped automatically.
                // Host rows: write the numeric id as before.
                var idCell = ws.Cell(excelRow, 1);
                if (isLinked)
                {
                    idCell.Value = $"L:{elementId}";
                    idCell.Style.Fill.BackgroundColor = ColLinkedRow;
                }
                else
                {
                    idCell.Value = elementId;
                }

                var row = data.Rows[r];
                for (int c = 0; c < data.Columns.Count; c++)
                {
                    var col  = data.Columns[c];
                    var cell = ws.Cell(excelRow, c + 2);
                    cell.Value = c < row.Count ? row[c] : "";

                    if (isLinked)
                    {
                        cell.Style.Fill.BackgroundColor = excelRow % 2 == 1 ? ColLinkedRow : ColLinkedRowAlt;
                        cell.Style.Font.FontColor = XLColor.FromHtml("#616161");
                        cell.Style.Protection.Locked = true;
                    }
                    else if (col.IsReadOnly)
                    {
                        cell.Style.Fill.BackgroundColor = ColCellReadOnly;
                        cell.Style.Protection.Locked = true;
                    }
                    else if (col.IsTypeParameter && excelRow % 2 == 1)
                    {
                        cell.Style.Fill.BackgroundColor = ColCellType;
                    }
                    else if (!col.IsTypeParameter && excelRow % 2 == 1)
                    {
                        cell.Style.Fill.BackgroundColor = ColCellInstance;
                    }
                }

                // Write "Origen (vínculo)" value for linked rows
                if (hasLinkedRows)
                {
                    var origenCell = ws.Cell(excelRow, origenCol);
                    if (isLinked)
                    {
                        string linkName = data.LinkSourceName.Count > r ? data.LinkSourceName[r] : "";
                        origenCell.Value = linkName;
                        origenCell.Style.Fill.BackgroundColor = excelRow % 2 == 1 ? ColLinkedRow : ColLinkedRowAlt;
                        origenCell.Style.Font.FontColor = XLColor.FromHtml("#616161");
                        origenCell.Style.Font.Italic = true;
                    }
                    else
                    {
                        origenCell.Value = "";
                    }
                }

                excelRow++;
            }

            // ── Column widths ────────────────────────────────────────────────
            for (int c = 2; c <= data.Columns.Count + 1; c++)
                ws.Column(c).AdjustToContents(2, Math.Min(data.Rows.Count + 2, 500));

            if (hasLinkedRows)
                ws.Column(origenCol).AdjustToContents(2, Math.Min(data.Rows.Count + 2, 500));

            ws.SheetView.FreezeRows(2);

            // ── Auto-filter on header row ────────────────────────────────────
            int lastDataRow = Math.Max(excelRow - 1, 2);
            ws.Range(2, 1, lastDataRow, totalCols).SetAutoFilter();
        }

        private static void WriteInstructionsSheet(XLWorkbook wb)
        {
            var ws = wb.Worksheets.Add("📋 Instrucciones");

            // ── Helpers ──────────────────────────────────────────────────────
            void Title(int row, string text)
            {
                var c = ws.Cell(row, 1);
                c.Value = text;
                c.Style.Font.Bold      = true;
                c.Style.Font.FontSize  = 13;
                c.Style.Font.FontColor = XLColor.FromHtml("#212B37");
                ws.Range(row, 1, row, 6).Merge();
            }

            void Body(int row, string text)
            {
                var c = ws.Cell(row, 1);
                c.Value = text;
                c.Style.Font.FontSize = 11;
                ws.Range(row, 1, row, 6).Merge();
                c.Style.Alignment.WrapText = true;
            }

            void ColorRow(int row, XLColor headerColor, XLColor cellColor, string label, string description)
            {
                // Swatch cell
                var swatch = ws.Cell(row, 1);
                swatch.Value = label;
                swatch.Style.Fill.BackgroundColor = headerColor;
                swatch.Style.Font.Bold  = true;
                swatch.Style.Font.FontSize = 11;
                swatch.Style.Alignment.Horizontal  = XLAlignmentHorizontalValues.Center;
                swatch.Style.Alignment.Vertical    = XLAlignmentVerticalValues.Center;
                ws.Column(1).Width = 22;

                // Sample data cell
                var sample = ws.Cell(row, 2);
                sample.Value = "Ejemplo";
                sample.Style.Fill.BackgroundColor  = cellColor;
                sample.Style.Font.FontSize         = 11;
                sample.Style.Alignment.Horizontal  = XLAlignmentHorizontalValues.Center;
                sample.Style.Alignment.Vertical    = XLAlignmentVerticalValues.Center;
                ws.Column(2).Width = 14;

                // Description — columna ancha, sin WrapText para evitar corte por altura fija
                var desc = ws.Cell(row, 3);
                desc.Value = description;
                desc.Style.Font.FontSize           = 11;
                desc.Style.Alignment.Vertical      = XLAlignmentVerticalValues.Center;
                desc.Style.Alignment.WrapText      = false;   // texto en una línea, sin corte
                ws.Range(row, 3, row, 6).Merge();             // fusionar hasta col F para dar más espacio
                ws.Column(3).Width = 80;                      // suficiente para la descripción más larga

                // Altura fija cómoda para una sola línea
                ws.Row(row).Height = 28;
            }

            void Separator(int row)
            {
                ws.Range(row, 1, row, 5).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F5F5");
            }

            // ── Encabezado principal ──────────────────────────────────────────
            var header = ws.Cell(1, 1);
            header.Value = "BIMPills — Guía de uso del archivo Excel";
            header.Style.Font.Bold      = true;
            header.Style.Font.FontSize  = 15;
            header.Style.Font.FontColor = XLColor.White;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#212B37");
            ws.Range(1, 1, 1, 6).Merge();
            ws.Row(1).Height = 28;

            // ── Sección 1: Leyenda de colores ─────────────────────────────────
            Separator(2);
            Title(3, "1 · Leyenda de colores");
            Body(4, "Cada columna tiene un color en el encabezado que indica el tipo de parámetro. Las filas tienen colores adicionales según su origen:");
            ws.Row(4).Height = 22;

            ColorRow(5,
                XLColor.FromHtml("#BBDEFB"), XLColor.FromHtml("#E3F2FD"),
                "🔵  Instancia",
                "Parámetro de instancia — el valor es exclusivo de este elemento. " +
                "Puedes editarlo libremente sin afectar otros elementos.");

            ColorRow(6,
                XLColor.FromHtml("#FFE0B2"), XLColor.FromHtml("#FFF3E0"),
                "🟠  Tipo",
                "Parámetro de tipo — el valor es compartido por todos los elementos " +
                "del mismo tipo de familia. Ver sección 2.");

            ColorRow(7,
                XLColor.FromHtml("#FFF9C4"), XLColor.FromHtml("#FFFDE7"),
                "🟡  Solo lectura",
                "Parámetro calculado o de sistema — no se puede modificar. " +
                "Las celdas amarillas son ignoradas al importar.");

            ColorRow(8,
                XLColor.FromHtml("#EEEEEE"), XLColor.FromHtml("#F5F5F5"),
                "⚪  Vínculo",
                "Elemento proveniente de un archivo vinculado (Revit Link). " +
                "Solo lectura — estas filas se incluyen para conteo y cuantificación, pero son ignoradas automáticamente al importar.");

            // ── Sección 2: Parámetros de tipo ─────────────────────────────────
            Separator(9);
            Title(10, "2 · Parámetros de tipo (columnas naranjas)");

            Body(11,
                "⚠️  IMPORTANTE: los parámetros de tipo son compartidos entre todos los elementos " +
                "que usan el mismo tipo de familia.");
            ws.Row(11).Height = 30;

            Body(12,
                "Ejemplo: si tienes 10 puertas de tipo \"P-01\" y cambias la Descripción " +
                "en una fila, al importar se actualizará la Descripción del tipo \"P-01\" " +
                "completo — todas las 10 puertas quedarán con el mismo valor nuevo.");
            ws.Row(12).Height = 40;

            Body(13,
                "Si dos filas del mismo tipo tienen valores distintos en una columna naranja, " +
                "prevalecerá el último elemento procesado. Para evitar inconsistencias, " +
                "asegúrate de que todas las filas del mismo tipo tengan el mismo valor " +
                "en las columnas naranjas antes de importar.");
            ws.Row(13).Height = 50;

            // ── Sección 3: Cómo importar ──────────────────────────────────────
            Separator(15);
            Title(16, "3 · Cómo importar cambios al modelo");

            var steps = new[]
            {
                "① Edita solo las celdas que necesites cambiar (azules o naranjas).",
                "② No agregues ni elimines filas — el ID de elemento (columna oculta) es necesario para identificar cada fila.",
                "③ No cambies los encabezados de columna — son usados para identificar el parámetro.",
                "④ Guarda el archivo Excel.",
                "⑤ En Revit, abre BIMPills → Gestionar → pestaña Tablas → botón Importar.",
                "⑥ Selecciona este archivo. BIMPills mostrará solo los cambios detectados.",
                "⑦ Revisa la lista de cambios y pulsa Aplicar para confirmar.",
                "ℹ️  Las filas grises (elementos de vínculos) son ignoradas automáticamente al importar — no es necesario eliminarlas."
            };

            for (int i = 0; i < steps.Length; i++)
            {
                int r = 17 + i;
                Body(r, steps[i]);
                ws.Row(r).Height = 20;
            }

            // ── Pie ───────────────────────────────────────────────────────────
            int footerRow = 25;
            var footer = ws.Cell(footerRow, 1);
            footer.Value = $"Generado por BIMPills · {DateTime.Now:dd/MM/yyyy HH:mm}";
            footer.Style.Font.FontSize  = 9;
            footer.Style.Font.FontColor = XLColor.FromHtml("#86868B");
            ws.Range(footerRow, 1, footerRow, 6).Merge();

            // ── Ancho de columnas y formato general ───────────────────────────
            ws.Column(4).Width = 10;
            ws.Column(5).Width = 10;
            ws.SheetView.ZoomScale = 110;

            // No auto-filter en la hoja de instrucciones
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
