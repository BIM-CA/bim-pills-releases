using System;
using System.Collections.Generic;
using System.IO;

namespace BIMPills.Core.Models
{
    /// <summary>
    /// Represents a sheet (ViewSheet) from the Revit model for export selection.
    /// No Revit API dependencies — pure data carrier.
    /// </summary>
    public sealed class SheetExportInfo
    {
        public long Id { get; }
        public string SheetNumber { get; }
        public string SheetName { get; }
        public string Revision { get; }
        public string Discipline { get; }
        public bool IsSelected { get; set; }

        /// <summary>
        /// All Revit parameter values for this sheet (name → value).
        /// Used for custom naming tokens like {Param:Designed By}.
        /// </summary>
        public Dictionary<string, string> ParameterValues { get; }

        public SheetExportInfo(long id, string sheetNumber, string sheetName,
                               string revision, string discipline,
                               Dictionary<string, string>? parameterValues = null)
        {
            Id = id;
            SheetNumber = sheetNumber;
            SheetName = sheetName;
            Revision = revision ?? "";
            Discipline = discipline ?? "";
            IsSelected = true;
            ParameterValues = parameterValues ?? new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Export format for sheet export (PDF, DWG, or both).
    /// </summary>
    public enum SheetExportFormat
    {
        PDF,
        DWG,
        Both
    }

    /// <summary>
    /// How to organize exported files into folders.
    /// </summary>
    public enum FolderOrganization
    {
        /// <summary>All files in the same folder.</summary>
        Flat,
        /// <summary>Separate /PDF and /DWG subfolders.</summary>
        ByFormat,
        /// <summary>Subfolders by discipline (Architecture, Structure, etc.).</summary>
        ByDiscipline,
        /// <summary>Format first, then discipline: /PDF/Architecture, /DWG/Structure.</summary>
        ByFormatAndDiscipline
    }

    /// <summary>
    /// Parametric naming convention for exported sheet files.
    /// Supports tokens: {SheetNumber}, {SheetName}, {Revision}, {Date}, {ProjectName}.
    /// </summary>
    public class SheetNamingConvention
    {
        public string Pattern { get; set; } = "{SheetNumber}-{SheetName}";

        public string GenerateFileName(SheetExportInfo sheet, string projectName, DateTime date,
            Dictionary<string, string>? parameterValues = null)
        {
            var name = Pattern
                .Replace("{SheetNumber}", Sanitize(sheet.SheetNumber))
                .Replace("{SheetName}", Sanitize(sheet.SheetName))
                .Replace("{Revision}", Sanitize(sheet.Revision))
                .Replace("{Date}", date.ToString("yyyy-MM-dd"))
                .Replace("{ProjectName}", Sanitize(projectName));

            // Resolve custom Revit parameter tokens: {Param:ParameterName}
            if (parameterValues != null)
            {
                foreach (var kvp in parameterValues)
                    name = name.Replace($"{{Param:{kvp.Key}}}", Sanitize(kvp.Value));
            }

            return name;
        }

        private static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var invalid = Path.GetInvalidFileNameChars();
            var result = s;
            foreach (var c in invalid)
                result = result.Replace(c, '-');
            return result;
        }
    }

    /// <summary>
    /// A saved profile for sheet export settings (format, naming, folder organization).
    /// </summary>
    public class SheetExportProfile
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public SheetExportFormat Format { get; set; } = SheetExportFormat.Both;
        public SheetNamingConvention NamingConvention { get; set; } = new SheetNamingConvention();
        public FolderOrganization FolderOrganization { get; set; } = FolderOrganization.ByFormat;
        public string DwgExportConfigId { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
    }

    /// <summary>
    /// Result of exporting a single sheet (tracks per-format success/failure).
    /// </summary>
    public sealed class SheetExportResultItem
    {
        public SheetExportInfo Sheet { get; set; } = null!;
        public bool PdfSuccess { get; set; }
        public bool DwgSuccess { get; set; }
        public string ErrorMessage { get; set; } = "";
    }

    // ── DWG Export Configuration ──

    /// <summary>AutoCAD file version for DWG export.</summary>
    public enum DwgFileVersion
    {
        AutoCAD2000,
        AutoCAD2007,
        AutoCAD2010,
        AutoCAD2013,
        AutoCAD2018
    }

    /// <summary>Solids export mode for DWG.</summary>
    public enum DwgSolidsExport
    {
        Polymesh,
        ACIS
    }

    /// <summary>
    /// Named DWG export configuration preset.
    /// Built-in presets (AEC Standard, ISO Standard) have IsBuiltIn = true.
    /// </summary>
    public class DwgExportConfig
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        // Custom config fields (used when IsRevitPreset = false)
        public DwgFileVersion FileVersion { get; set; } = DwgFileVersion.AutoCAD2018;
        public DwgSolidsExport ExportOfSolids { get; set; } = DwgSolidsExport.Polymesh;
        public bool SharedCoords { get; set; } = false;
        // Revit native preset
        public bool   IsRevitPreset   { get; set; } = false;
        public string RevitPresetName { get; set; } = "";
        // Extra options (like ProSheets checkboxes)
        public bool ExportLinkedAsXrefs { get; set; } = true;
        public bool CleanPcpFiles       { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }

    // ── PDF Export Settings ──

    public enum PdfColorDepth    { Color, Grayscale, BlackLines }
    public enum PdfRasterQuality { Low, Medium, High, Presentation }
    public enum PdfZoomType      { FitToPage, Custom }
    public enum PdfPaperPlacement { Center, OffsetFromCorner }
    public enum PdfHiddenViewProcessing { Vector, Raster }

    public class PdfExportSettings
    {
        // Paper
        public PdfPaperPlacement PaperPlacement { get; set; } = PdfPaperPlacement.Center;
        public double OffsetX { get; set; } = 0;
        public double OffsetY { get; set; } = 0;

        // Zoom
        public PdfZoomType ZoomType { get; set; } = PdfZoomType.FitToPage;
        public int ZoomPercent { get; set; } = 100;

        // Rendering
        public PdfHiddenViewProcessing HiddenViewProcessing { get; set; } = PdfHiddenViewProcessing.Vector;
        public PdfColorDepth ColorDepth { get; set; } = PdfColorDepth.Color;
        public PdfRasterQuality RasterQuality { get; set; } = PdfRasterQuality.Medium;

        // Visibility options
        public bool HideScopeBoxes            { get; set; } = true;
        public bool HideCropBoundaries        { get; set; } = true;
        public bool HideRefWorkPlanes         { get; set; } = true;
        public bool HideUnreferencedViewTags  { get; set; } = false;
        public bool ViewLinksInBlue           { get; set; } = false;

        // Advanced rendering options (content preservation — fix content loss)
        /// <summary>
        /// When true, Revit hides lines it considers coincident. Known cause of content
        /// loss (missing text/annotations/fine lines). Default false = preserve everything.
        /// </summary>
        public bool MaskCoincidentLines        { get; set; } = false;
        /// <summary>
        /// When true, halftones are replaced with thin lines. Can hide hatches/shaded areas.
        /// Default false = preserve halftones.
        /// </summary>
        public bool ReplaceHalftoneWithThinLines { get; set; } = false;
        /// <summary>
        /// When true, the entire view is rasterized (larger file, but guarantees visual fidelity
        /// for shadows/transparencies/complex materials). Default false = vector where possible.
        /// </summary>
        public bool AlwaysUseRaster            { get; set; } = false;

        // Output
        public bool CombineIntoPdf            { get; set; } = false;
        public string CombinedFileName        { get; set; } = "Planos_Combinados";
    }

    // ── PDF Engine (native vs printer) ──

    /// <summary>
    /// Which PDF rendering engine to use for sheet export.
    /// Native = Revit built-in PDFExportOptions (fast, single call).
    /// SystemPrinter = Revit prints to a selected PDF printer driver (PDF24, Adobe PDF, etc.).
    /// </summary>
    public enum PdfEngineKind
    {
        Native,
        SystemPrinter
    }

    /// <summary>
    /// Global PDF engine configuration — persisted between sessions.
    /// </summary>
    public class PdfEngineSettings
    {
        public PdfEngineKind Engine { get; set; } = PdfEngineKind.Native;
        /// <summary>Windows printer name (e.g. "PDF24", "Microsoft Print to PDF"). Used when Engine = SystemPrinter.</summary>
        public string PrinterName { get; set; } = "";
        /// <summary>
        /// True once the user has explicitly interacted with the engine selector.
        /// Prevents the auto-upgrade from Native → SystemPrinter from overriding
        /// the user's deliberate choice on subsequent dialog opens.
        /// </summary>
        public bool HasChosenEngine { get; set; } = false;
    }
}
