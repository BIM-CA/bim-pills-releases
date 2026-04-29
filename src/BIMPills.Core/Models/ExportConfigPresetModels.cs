using System;

namespace BIMPills.Core.Models
{
    /// <summary>
    /// A named preset that captures the full export-format configuration from Tab 2
    /// (Formato) of the ExportSheetsPanel: engine, PDF options, DWG options, naming.
    /// Persisted to JSON in %APPDATA%/Autodesk/Revit/Addins/BIMPills/ExportConfigPresets/.
    /// </summary>
    public class ExportConfigPreset
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }

        // ── Export toggles ──
        public bool ExportPdf { get; set; } = true;
        public bool ExportDwg { get; set; } = false;

        // ── Naming & folders ──
        public string NamingPattern { get; set; } = "";
        public FolderOrganization FolderOrganization { get; set; } = FolderOrganization.ByFormat;

        // ── Destination ──
        /// <summary>Carpeta de destino guardada en el Paso 3. Vacío = no guardado.</summary>
        public string DestinationFolder { get; set; } = "";

        // ── PDF engine ──
        public PdfEngineKind PdfEngine { get; set; } = PdfEngineKind.Native;
        /// <summary>Windows printer name when PdfEngine = SystemPrinter.</summary>
        public string PrinterName { get; set; } = "";

        // ── PDF options ──
        public PdfExportSettings? PdfSettings { get; set; }

        // ── DWG options ──
        public DwgExportConfig? DwgConfig { get; set; }
    }
}
