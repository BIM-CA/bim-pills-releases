using BIMPills.Core.Models;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.IO;

namespace BIMPills.Infrastructure.Export
{
    /// <summary>
    /// Reads an .xlsx file previously exported by ScheduleExcelExporter and
    /// produces a list of ParameterUpdateRequest — one per editable cell that
    /// has a non-empty value.
    /// The importer expects the hidden first column to contain the ElementId.
    /// </summary>
    public class ScheduleExcelImporter
    {
        /// <summary>
        /// Parses the Excel file at <paramref name="filePath"/> and returns only
        /// parameter update requests where the value actually changed compared to
        /// the original data. Read-only columns are skipped.
        /// </summary>
        /// <param name="filePath">Path to the .xlsx file.</param>
        /// <param name="readOnlyColumnIndices">0-based column indices to skip (optional).</param>
        /// <param name="originalRows">Original row data from Revit for change detection (optional).</param>
        /// <param name="originalElementIds">Element IDs parallel to <paramref name="originalRows"/> (optional).</param>
        public List<ParameterUpdateRequest> Import(
            string filePath,
            HashSet<int>?          readOnlyColumnIndices = null,
            List<List<string>>?    originalRows          = null,
            List<long>?            originalElementIds    = null)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Archivo Excel no encontrado.", filePath);

            var requests = new List<ParameterUpdateRequest>();

            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.Worksheet(1);

            // Row 1 = title, Row 2 = headers, Row 3+ = data
            // Column 1 = hidden ElementId, Column 2+ = parameters
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 2;
            var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 1;

            // Read header row (row 2) to get parameter names
            var paramNames = new List<string>();
            for (int col = 2; col <= lastCol; col++)
                paramNames.Add(ws.Cell(2, col).GetString());

            for (int row = 3; row <= lastRow; row++)
            {
                var idCell = ws.Cell(row, 1);
                if (!long.TryParse(idCell.GetString(), out long elementId) || elementId == 0)
                    continue;

                // Find original row index for change-detection
                int origRowIndex = originalElementIds?.IndexOf(elementId) ?? -1;

                for (int col = 2; col <= lastCol; col++)
                {
                    int paramIndex = col - 2;

                    // Skip read-only columns
                    if (readOnlyColumnIndices != null && readOnlyColumnIndices.Contains(paramIndex))
                        continue;

                    if (paramIndex >= paramNames.Count) break;

                    var paramName = paramNames[paramIndex];
                    if (string.IsNullOrWhiteSpace(paramName)) continue;

                    var value = ws.Cell(row, col).GetString();

                    // Skip if value is unchanged compared to original Revit data
                    if (origRowIndex >= 0 && originalRows != null &&
                        origRowIndex < originalRows.Count)
                    {
                        var origRow = originalRows[origRowIndex];
                        string originalValue = paramIndex < origRow.Count ? (origRow[paramIndex] ?? "") : "";
                        if (value == originalValue)
                            continue;
                    }

                    requests.Add(new ParameterUpdateRequest
                    {
                        ElementId     = elementId,
                        ParameterName = paramName,
                        NewValue      = value
                    });
                }
            }

            return requests;
        }

        /// <summary>
        /// Reads the schedule name from the title cell (Row 1, Col 1).
        /// </summary>
        public string ReadScheduleName(string filePath)
        {
            if (!File.Exists(filePath)) return "";
            using var wb = new XLWorkbook(filePath);
            return wb.Worksheets.Worksheet(1).Cell(1, 1).GetString();
        }

        /// <summary>
        /// Imports all sheets from a multi-schedule Excel file.
        /// Returns a dictionary mapping schedule name → list of updates.
        /// Each sheet's Row 1 contains the schedule name, Row 2 the headers.
        /// </summary>
        public Dictionary<string, List<ParameterUpdateRequest>> ImportMultiple(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Archivo Excel no encontrado.", filePath);

            var result = new Dictionary<string, List<ParameterUpdateRequest>>();

            using var wb = new XLWorkbook(filePath);
            foreach (var ws in wb.Worksheets)
            {
                // Row 1 = schedule name (title), Row 2 = headers, Row 3+ = data
                var scheduleName = ws.Cell(1, 1).GetString();
                if (string.IsNullOrWhiteSpace(scheduleName)) continue;

                var lastRow = ws.LastRowUsed()?.RowNumber() ?? 2;
                var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 1;

                // Read header row for parameter names
                var paramNames = new List<string>();
                for (int col = 2; col <= lastCol; col++)
                    paramNames.Add(ws.Cell(2, col).GetString());

                var updates = new List<ParameterUpdateRequest>();
                for (int row = 3; row <= lastRow; row++)
                {
                    if (!long.TryParse(ws.Cell(row, 1).GetString(), out long elementId) || elementId == 0)
                        continue;

                    for (int col = 2; col <= lastCol; col++)
                    {
                        int paramIndex = col - 2;
                        if (paramIndex >= paramNames.Count) break;

                        var paramName = paramNames[paramIndex];
                        if (string.IsNullOrWhiteSpace(paramName)) continue;

                        var value = ws.Cell(row, col).GetString();

                        updates.Add(new ParameterUpdateRequest
                        {
                            ElementId     = elementId,
                            ParameterName = paramName,
                            NewValue      = value
                        });
                    }
                }

                if (updates.Count > 0)
                    result[scheduleName] = updates;
            }

            return result;
        }
    }
}
