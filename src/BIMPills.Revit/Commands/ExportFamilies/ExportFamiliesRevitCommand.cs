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

            // Initialize Sheets tab — gather sheets and build PDF/DWG callbacks
            if (doc != null)
            {
                try
                {
                    // Gather sheet data directly from RevitDocumentServices
                    var docServices = new RevitDocumentServices(doc);
                    var sheets = docServices.GetSheets();
                    var projectName = docServices.GetProjectName();

                    if (sheets.Count > 0)
                    {
                        // Gather available parameter names from sheets
                        var paramNames = docServices.GetSheetParameterNames();

                        // PDF export callback
                        Func<long, string, string, PdfExportSettings, bool> pdfCallback = (sheetId, folder, fileName, settings) =>
                        {
                            try
                            {
                                logger?.Info($"[ExportSheets] PDF: {fileName} → {folder}");
                                var viewIds = new List<ElementId> { new ElementId(sheetId) };
                                var opts = new PDFExportOptions();
                                opts.FileName = fileName;

                                // Paper placement
                                opts.PaperPlacement = settings.PaperPlacement == PdfPaperPlacement.OffsetFromCorner
                                    ? Autodesk.Revit.DB.PaperPlacementType.LowerLeft
                                    : Autodesk.Revit.DB.PaperPlacementType.Center;
                                if (settings.PaperPlacement == PdfPaperPlacement.OffsetFromCorner)
                                {
                                    opts.OriginOffsetX = settings.OffsetX;
                                    opts.OriginOffsetY = settings.OffsetY;
                                }

                                // Zoom
                                if (settings.ZoomType == PdfZoomType.Custom)
                                {
                                    opts.ZoomType = Autodesk.Revit.DB.ZoomType.Zoom;
                                    opts.ZoomPercentage = settings.ZoomPercent;
                                }
                                else
                                {
                                    opts.ZoomType = Autodesk.Revit.DB.ZoomType.FitToPage;
                                }

                                // Color
                                opts.ColorDepth = settings.ColorDepth switch
                                {
                                    PdfColorDepth.Grayscale  => Autodesk.Revit.DB.ColorDepthType.GrayScale,
                                    PdfColorDepth.BlackLines => Autodesk.Revit.DB.ColorDepthType.BlackLine,
                                    _                        => Autodesk.Revit.DB.ColorDepthType.Color
                                };

                                // Raster quality
                                opts.RasterQuality = settings.RasterQuality switch
                                {
                                    PdfRasterQuality.Low          => Autodesk.Revit.DB.RasterQualityType.Low,
                                    PdfRasterQuality.High         => Autodesk.Revit.DB.RasterQualityType.High,
                                    PdfRasterQuality.Presentation => Autodesk.Revit.DB.RasterQualityType.Presentation,
                                    _                             => Autodesk.Revit.DB.RasterQualityType.Medium
                                };

                                // Visibility options
                                opts.HideScopeBoxes           = settings.HideScopeBoxes;
                                opts.HideCropBoundaries       = settings.HideCropBoundaries;
                                opts.HideReferencePlane       = settings.HideRefWorkPlanes;
                                opts.HideUnreferencedViewTags = settings.HideUnreferencedViewTags;
                                opts.ViewLinksInBlue          = settings.ViewLinksInBlue;

                                // Always stop on error for batch
                                opts.StopOnError = false;

                                return doc.Export(folder, viewIds, opts);
                            }
                            catch (Exception ex)
                            {
                                logger?.Error($"[ExportSheets] Error PDF sheetId={sheetId}", ex);
                                return false;
                            }
                        };

                        // Get Revit's native DWG export preset names
                        var dwgPresetNames = new List<string>();
                        try
                        {
                            var presets = BaseExportOptions.GetPredefinedSetupNames(doc);
                            if (presets != null)
                                dwgPresetNames.AddRange(presets);
                        }
                        catch { }
                        // If no presets found, add sensible defaults
                        if (dwgPresetNames.Count == 0)
                            dwgPresetNames.AddRange(new[] { "AEC Extended", "AEC Standard", "ISO Standard" });

                        // DWG export callback
                        Func<long, string, string, DwgExportConfig?, bool> dwgCallback = (sheetId, folder, fileName, dwgConfig) =>
                        {
                            try
                            {
                                logger?.Info($"[ExportSheets] DWG: {fileName} → {folder}");
                                var viewIds = new List<ElementId> { new ElementId(sheetId) };
                                DWGExportOptions opts;

                                if (dwgConfig?.IsRevitPreset == true && !string.IsNullOrEmpty(dwgConfig.RevitPresetName))
                                {
                                    // Use Revit's native export setup
                                    try
                                    {
                                        opts = DWGExportOptions.GetPredefinedOptions(doc, dwgConfig.RevitPresetName)
                                               ?? new DWGExportOptions();
                                        logger?.Info($"[ExportSheets] DWG preset '{dwgConfig.RevitPresetName}' cargado.");
                                    }
                                    catch
                                    {
                                        opts = new DWGExportOptions();
                                    }
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

                                bool result = doc.Export(folder, fileName, viewIds, opts);

                                // Clean .pcp files if requested
                                if (result && dwgConfig?.CleanPcpFiles == true)
                                {
                                    try
                                    {
                                        var pcpPath = System.IO.Path.Combine(folder, fileName + ".pcp");
                                        if (System.IO.File.Exists(pcpPath))
                                            System.IO.File.Delete(pcpPath);
                                    }
                                    catch { /* non-critical */ }
                                }

                                return result;
                            }
                            catch (Exception ex)
                            {
                                logger?.Error($"[ExportSheets] Error DWG sheetId={sheetId}", ex);
                                return false;
                            }
                        };

                        window.InitializeExportSheets(
                            sheets,
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
                    logger?.Warning($"[ExportSheets] No se pudieron cargar los planos: {ex.Message}");
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
