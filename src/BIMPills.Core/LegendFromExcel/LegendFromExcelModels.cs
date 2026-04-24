using System.Collections.Generic;

namespace BIMPills.Core.LegendFromExcel
{
    public sealed class ExcelCellModel
    {
        public int    Row    { get; set; }
        public int    Column { get; set; }
        public string Text   { get; set; } = string.Empty;

        /// <summary>Hex sin '#', e.g. "FF0000". Null si sin fondo o blanco.</summary>
        public string? BackgroundColorHex { get; set; }

        public bool IsMerged { get; set; }
        public int  RowSpan  { get; set; } = 1;
        public int  ColSpan  { get; set; } = 1;
    }

    public sealed class ExcelTableModel
    {
        public IReadOnlyList<ExcelCellModel> Cells       { get; set; } = new List<ExcelCellModel>();
        public int                           RowCount    { get; set; }
        public int                           ColumnCount { get; set; }
    }

    public sealed class LegendDrawOptions
    {
        public string ViewName             { get; set; } = "Leyenda";
        public long   LineStyleId          { get; set; }

        // Texto para celdas normales (siempre activo)
        public long   TextStyleIdValues    { get; set; }

        // Encabezados — selector compartido de filas (0 = sin personalización)
        public int    HeaderRowsCount      { get; set; } = 0;

        // Texto diferenciado por fila de encabezado
        public bool   DifferentiateHeader  { get; set; } = false;
        public long   TextStyleIdHeader1   { get; set; }
        public long   TextStyleIdHeader2   { get; set; }

        // Región rellena por fila de encabezado
        public bool   ApplyFill            { get; set; } = false;
        public long   FillRegionTypeId1    { get; set; }
        public long   FillRegionTypeId2    { get; set; }

        public double CellWidthMm          { get; set; } = 50.0;
        public double CellHeightMm         { get; set; } = 8.0;
    }

    public sealed class LegendDrawResult
    {
        public bool   Success      { get; set; }
        public string? ErrorMessage { get; set; }
        public int    CellsDrawn  { get; set; }
        public long?  ViewId      { get; set; }
    }

    public sealed class RevitStyleInfo
    {
        public long   Id   { get; set; }
        public string Name { get; set; } = string.Empty;

        public override string ToString() => Name;
    }
}
