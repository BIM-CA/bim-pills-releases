using Autodesk.Revit.DB;
using BIMPills.Commands.ExportFamilies;
using BIMPills.Core.Commands;
using BIMPills.Core.Models;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.DI;
using BIMPills.Revit.Commands;
using BIMPills.Revit.Context;
using BIMPills.UI.ExportFamilies;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BIMPills.Revit.Commands.ExportFamilies
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class ExportFamiliesRevitCommand : RevitCommandBase
    {
        protected override IPluginCommand CreateCommand()
            => new ExportFamiliesCommand();

        protected override void OnSuccess(IPluginCommand command)
        {
            if (ExportFamiliesCommand.LastResult == null) return;

            var doc = CommandData?.Application.ActiveUIDocument.Document;

            var logger = ServiceLocator.IsRegistered<ILogger>() ? ServiceLocator.Get<ILogger>() : null;

            Func<long, string, bool>? exportCallback = null;
            if (doc != null)
            {
                exportCallback = (familyId, destinationPath) =>
                {
                    logger?.Info($"[ExportFamilies] Exportando familia Id={familyId} → {destinationPath}");
                    try
                    {
                        var elementId = new ElementId(familyId);
                        var family = doc.GetElement(elementId) as Family;
                        if (family == null)
                        {
                            logger?.Warning($"[ExportFamilies] Familia Id={familyId} no encontrada en el documento.");
                            return false;
                        }

                        var familyDoc = doc.EditFamily(family);
                        if (familyDoc == null)
                        {
                            logger?.Warning($"[ExportFamilies] No se pudo abrir el documento de familia '{family.Name}'.");
                            return false;
                        }

                        try
                        {
                            // Ensure directory exists
                            var dir = System.IO.Path.GetDirectoryName(destinationPath);
                            if (!string.IsNullOrEmpty(dir))
                                System.IO.Directory.CreateDirectory(dir);

                            var saveOptions = new SaveAsOptions { OverwriteExistingFile = true };
                            familyDoc.SaveAs(destinationPath, saveOptions);
                            logger?.Info($"[ExportFamilies] Familia '{family.Name}' exportada correctamente.");
                            return true;
                        }
                        finally
                        {
                            familyDoc.Close(false); // Close without saving changes to original
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.Error($"[ExportFamilies] Error al exportar familia Id={familyId}", ex);
                        return false;
                    }
                };
            }

            var result = ExportFamiliesCommand.LastResult;
            int revitVersion = doc?.Application.VersionNumber != null
                ? int.TryParse(doc.Application.VersionNumber, out var v) ? v : 0
                : 0;

            var window = new ExportarWindow();

            // Set document name from Revit
            try
            {
                var docName = CommandData!.Application.ActiveUIDocument.Document.Title;
                window.SetDocumentName(docName);
            }
            catch { }

            // Initialize Families tab
            window.InitializeExportFamilies(
                result.Families,
                exportCallback,
                result.DocumentTitle,
                revitVersion,
                logger);

            // Initialize Sheets/Views tab — gather exportable views and build PDF/DWG callbacks
            if (doc != null)
            {
                try
                {
                    var docServices = new RevitDocumentServices(doc);
                    var exportableViews = docServices.GetExportableViews();
                    var projectName = docServices.GetProjectName();

                    if (exportableViews.Count > 0)
                    {
                        var paramNames = docServices.GetSheetParameterNames();

                        // PDF export callback — works for sheets and individual views alike
                        Func<long, string, string, PdfExportSettings, bool> pdfCallback = (viewId, folder, fileName, settings) =>
                        {
                            try
                            {
                                logger?.Info($"[ExportViews] PDF: {fileName} → {folder}");
                                var viewIds = new List<ElementId> { new ElementId(viewId) };
                                var opts = new PDFExportOptions();
                                opts.FileName = fileName;

                                opts.PaperPlacement = settings.PaperPlacement == PdfPaperPlacement.OffsetFromCorner
                                    ? Autodesk.Revit.DB.PaperPlacementType.LowerLeft
                                    : Autodesk.Revit.DB.PaperPlacementType.Center;
                                if (settings.PaperPlacement == PdfPaperPlacement.OffsetFromCorner)
                                {
                                    opts.OriginOffsetX = settings.OffsetX;
                                    opts.OriginOffsetY = settings.OffsetY;
                                }

                                if (settings.ZoomType == PdfZoomType.Custom)
                                {
                                    opts.ZoomType = Autodesk.Revit.DB.ZoomType.Zoom;
                                    opts.ZoomPercentage = settings.ZoomPercent;
                                }
                                else
                                {
                                    opts.ZoomType = Autodesk.Revit.DB.ZoomType.FitToPage;
                                }

                                opts.ColorDepth = settings.ColorDepth switch
                                {
                                    PdfColorDepth.Grayscale  => Autodesk.Revit.DB.ColorDepthType.GrayScale,
                                    PdfColorDepth.BlackLines => Autodesk.Revit.DB.ColorDepthType.BlackLine,
                                    _                        => Autodesk.Revit.DB.ColorDepthType.Color
                                };

                                opts.RasterQuality = settings.RasterQuality switch
                                {
                                    PdfRasterQuality.Low          => Autodesk.Revit.DB.RasterQualityType.Low,
                                    PdfRasterQuality.High         => Autodesk.Revit.DB.RasterQualityType.High,
                                    PdfRasterQuality.Presentation => Autodesk.Revit.DB.RasterQualityType.Presentation,
                                    _                             => Autodesk.Revit.DB.RasterQualityType.Medium
                                };

                                opts.HideScopeBoxes           = settings.HideScopeBoxes;
                                opts.HideCropBoundaries       = settings.HideCropBoundaries;
                                opts.HideReferencePlane       = settings.HideRefWorkPlanes;
                                opts.HideUnreferencedViewTags = settings.HideUnreferencedViewTags;
                                opts.ViewLinksInBlue          = settings.ViewLinksInBlue;
                                opts.StopOnError              = false;

                                return doc.Export(folder, viewIds, opts);
                            }
                            catch (Exception ex)
                            {
                                logger?.Error($"[ExportViews] Error PDF viewId={viewId}", ex);
                                return false;
                            }
                        };

                        // DWG export preset names
                        var dwgPresetNames = new List<string>();
                        try
                        {
                            var presets = BaseExportOptions.GetPredefinedSetupNames(doc);
                            if (presets != null)
                                dwgPresetNames.AddRange(presets);
                        }
                        catch { }
                        if (dwgPresetNames.Count == 0)
                            dwgPresetNames.AddRange(new[] { "AEC Extended", "AEC Standard", "ISO Standard" });

                        // DWG export callback
                        Func<long, string, string, DwgExportConfig?, bool> dwgCallback = (viewId, folder, fileName, dwgConfig) =>
                        {
                            try
                            {
                                logger?.Info($"[ExportViews] DWG: {fileName} → {folder}");
                                var viewIds = new List<ElementId> { new ElementId(viewId) };
                                DWGExportOptions opts;

                                if (dwgConfig?.IsRevitPreset == true && !string.IsNullOrEmpty(dwgConfig.RevitPresetName))
                                {
                                    try
                                    {
                                        opts = DWGExportOptions.GetPredefinedOptions(doc, dwgConfig.RevitPresetName)
                                               ?? new DWGExportOptions();
                                    }
                                    catch { opts = new DWGExportOptions(); }
                                }
                                else if (dwgConfig != null)
                                {
                                    opts = new DWGExportOptions();
                                    opts.FileVersion    = MapFileVersion(dwgConfig.FileVersion);
                                    opts.ExportOfSolids = dwgConfig.ExportOfSolids == DwgSolidsExport.ACIS
                                        ? SolidGeometry.ACIS : SolidGeometry.Polymesh;
                                    opts.SharedCoords   = dwgConfig.SharedCoords;
                                }
                                else
                                {
                                    opts = new DWGExportOptions();
                                }

                                bool exported = doc.Export(folder, fileName, viewIds, opts);

                                if (exported && dwgConfig?.CleanPcpFiles == true)
                                {
                                    try
                                    {
                                        var pcpPath = Path.Combine(folder, fileName + ".pcp");
                                        if (File.Exists(pcpPath)) File.Delete(pcpPath);
                                    }
                                    catch { }
                                }

                                return exported;
                            }
                            catch (Exception ex)
                            {
                                logger?.Error($"[ExportViews] Error DWG viewId={viewId}", ex);
                                return false;
                            }
                        };

                        window.InitializeExportViews(
                            exportableViews,
                            pdfCallback,
                            dwgCallback,
                            projectName,
                            logger,
                            paramNames,
                            dwgPresetNames);
                    }
                }
                catch (Exception ex)
                {
                    logger?.Warning($"[ExportViews] No se pudieron cargar las vistas: {ex.Message}");
                }
            }

            // Initialize Model tab (NWC export)
            if (doc != null)
            {
                try
                {
                    var docServices = new RevitDocumentServices(doc);
                    var activeViewName = doc.ActiveView?.Name;
                    var availableViews = docServices.GetNwcViews();

                    // Detect whether Navisworks NWC Export Utility is loaded in this Revit process.
                    // NavisworksExportOptions lives in RevitAPI.dll and always instantiates,
                    // so we must check if NavisworksConverter is actually loaded instead.
                    bool nwcAvailable = false;
                    try
                    {
                        nwcAvailable = System.AppDomain.CurrentDomain.GetAssemblies()
                            .Any(a => a.GetName().Name
                                .IndexOf("NavisworksConverter", StringComparison.OrdinalIgnoreCase) >= 0);
                    }
                    catch { }

                    // Gather model-level parameter values for filename tokens
                    var paramValues = new Dictionary<string, string>
                    {
                        ["Proyecto"]  = doc.ProjectInformation?.Name ?? doc.Title,
                        ["Disciplina"] = doc.ProjectInformation?.LookupParameter("Discipline")?.AsString() ?? "",
                        ["Número"]    = doc.ProjectInformation?.Number ?? "",
                        ["Fecha"]     = DateTime.Now.ToString("yyyy-MM-dd")
                    };
                    // Merge project information parameters
                    try
                    {
                        foreach (Parameter p in doc.ProjectInformation.Parameters)
                        {
                            if (p.Definition?.Name == null || !p.HasValue) continue;
                            var val = p.AsValueString() ?? p.AsString() ?? "";
                            if (!string.IsNullOrEmpty(val) && !paramValues.ContainsKey(p.Definition.Name))
                                paramValues[p.Definition.Name] = val;
                        }
                    }
                    catch { }

                    // NWC export callback
                    Func<NwcExportConfig, bool> nwcCallback = null!;
                    if (nwcAvailable)
                    {
                        nwcCallback = cfg =>
                        {
                            try
                            {
                                logger?.Info($"[ExportModel] NWC: {cfg.FileName} → {cfg.DestinationFolder}");

                                Directory.CreateDirectory(cfg.DestinationFolder);

                                var opts = new NavisworksExportOptions();

                                // Scope (NavisworksExportScope.View = specific view; default = full model)
                                opts.ExportScope = cfg.Scope == NwcExportScope.SpecificView
                                    ? NavisworksExportScope.View
                                    : NavisworksExportScope.Model;
                                if (cfg.Scope == NwcExportScope.SpecificView && cfg.ViewId.HasValue)
                                    opts.ViewId = new ElementId(cfg.ViewId.Value);

                                // Links
                                opts.ExportLinks = cfg.ExportLinks;

                                // Coordinates
                                opts.Coordinates = cfg.Coordinates == NwcCoordinates.Shared
                                    ? NavisworksCoordinates.Shared
                                    : NavisworksCoordinates.Internal;

                                // Parameters
                                opts.Parameters = cfg.Parameters switch
                                {
                                    NwcParameters.Elements => NavisworksParameters.Elements,
                                    NwcParameters.None     => NavisworksParameters.None,
                                    _                      => NavisworksParameters.All
                                };

                                // Faceting precision (Low=0.3, Medium=0.5, High=1.0)
                                opts.FacetingFactor = cfg.FacetingPrecision switch
                                {
                                    NwcFacetingPrecision.Low  => 0.3,
                                    NwcFacetingPrecision.High => 1.0,
                                    _                         => 0.5
                                };

                                // Additional options
                                opts.ConvertElementProperties = cfg.ConvertElementProperties;
                                opts.ExportRoomAsAttribute    = cfg.ExportRoomAsAttribute;
                                opts.ExportRoomGeometry       = cfg.ExportRoomGeometry;
                                opts.DivideFileIntoLevels     = cfg.DivideFileIntoLevels;
                                opts.ExportUrls               = cfg.ExportUrls;
                                opts.FindMissingMaterials     = cfg.FindMissingMaterials;

                                // Resolve filename (strip .nwc if already included)
                                var fileName = cfg.FileName.EndsWith(".nwc", StringComparison.OrdinalIgnoreCase)
                                    ? cfg.FileName.Substring(0, cfg.FileName.Length - 4) : cfg.FileName;

                                        doc.Export(cfg.DestinationFolder, fileName, opts);
                                logger?.Info("[ExportModel] NWC exportado correctamente.");
                                return true;
                            }
                            catch (Exception ex)
                            {
                                logger?.Error("[ExportModel] Error NWC", ex);
                                // Re-throw with clear message so the UI can display it to the user
                                throw new InvalidOperationException(
                                    $"Revit no pudo completar la exportación NWC.\n\n{ex.Message}", ex);
                            }
                        };
                    }

                    window.InitializeExportModel(
                        doc.Title,
                        activeViewName,
                        nwcAvailable,
                        nwcCallback,
                        logger,
                        paramValues.Keys.ToList(),
                        paramValues,
                        presets: null,        // presets are persisted by UI layer (JSON)
                        availableViews: availableViews);
                }
                catch (Exception ex)
                {
                    logger?.Warning($"[ExportModel] No se pudo inicializar la pestaña Modelo: {ex.Message}");
                }
            }

            window.ShowDialog();
        }

        private static ACADVersion MapFileVersion(DwgFileVersion version)
        {
            switch (version)
            {
                case DwgFileVersion.AutoCAD2000: return ACADVersion.R2007;
                case DwgFileVersion.AutoCAD2007: return ACADVersion.R2007;
                case DwgFileVersion.AutoCAD2010: return ACADVersion.R2010;
                case DwgFileVersion.AutoCAD2013: return ACADVersion.R2013;
                case DwgFileVersion.AutoCAD2018: return ACADVersion.R2018;
                default: return ACADVersion.R2018;
            }
        }
    }
}
