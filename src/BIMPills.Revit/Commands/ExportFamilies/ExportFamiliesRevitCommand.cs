using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using BIMPills.Commands.ExportFamilies;
using BIMPills.Core.Commands;
using BIMPills.Core.Models;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.DI;
using BIMPills.Revit.Commands;
using BIMPills.Revit.Context;
using BIMPills.UI.ExportFamilies;
using BIMPills.UI.Shared;
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
            Func<long, string, string, PdfExportSettings, bool>? pdfCallback = null;
            Func<long, string, string, DwgExportConfig?, bool>? dwgCallback = null;
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

                        // PDF export callback — works for sheets and individual views alike.
                        // Routes through either the native Revit PDF engine OR a system
                        // printer (PDF24 recommended), depending on the user's global
                        // engine choice persisted via JsonPdfEngineSettingsRepository.
                        pdfCallback = (viewId, folder, fileName, settings) =>
                        {
                            try
                            {
                                // ── Route to printer engine if the user selected one ──
                                if (_pdfEngineSnapshot?.Engine == PdfEngineKind.SystemPrinter
                                    && !string.IsNullOrWhiteSpace(_pdfEngineSnapshot.PrinterName))
                                {
                                    return PrintViewViaSystemPrinter(
                                        doc, viewId, folder, fileName,
                                        _pdfEngineSnapshot.PrinterName, logger);
                                }

                                logger?.Info($"[ExportViews] PDF (native): {fileName} → {folder}");
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

                                // ── Content-preservation options (fix missing lines/text) ──
                                // These defaults to `true` in some Revit builds, which causes
                                // content loss (coincident lines/text dropped, halftones replaced).
                                // We force them to the user-chosen values (default false = preserve).
                                opts.MaskCoincidentLines          = settings.MaskCoincidentLines;
                                opts.ReplaceHalftoneWithThinLines = settings.ReplaceHalftoneWithThinLines;
                                opts.AlwaysUseRaster              = settings.AlwaysUseRaster;

                                // Paper format/orientation: Auto lets Revit pick the best fit
                                // based on the view's titleblock, preventing clipping.
                                try { opts.PaperFormat      = ExportPaperFormat.Default; } catch { }
                                try { opts.PaperOrientation = PageOrientationType.Auto;  } catch { }

                                // Combine = true is required for Revit to honor opts.FileName.
                                // When Combine = false, Revit ignores FileName and uses the
                                // sheet's internal title as the output filename.
                                // Since the queue calls this method once per view, Combine = true
                                // still produces one file per view — just with our custom name.
                                opts.Combine = true;

                                // Delete existing file to avoid "file in use" conflicts
                                try
                                {
                                    var existingPdf = Path.Combine(folder, fileName + ".pdf");
                                    if (File.Exists(existingPdf)) File.Delete(existingPdf);
                                }
                                catch { }

                                return ExportWithWarningsSuppressed(doc, () => doc.Export(folder, viewIds, opts));
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
                        dwgCallback = (viewId, folder, fileName, dwgConfig) =>
                        {
                            try
                            {
                                logger?.Info($"[ExportViews] DWG: {fileName} → {folder}");
                                var viewIds = new List<ElementId> { new ElementId(viewId) };
                                DWGExportOptions opts;
                                bool fromPreset = false;

                                if (dwgConfig?.IsRevitPreset == true && !string.IsNullOrEmpty(dwgConfig.RevitPresetName))
                                {
                                    try
                                    {
                                        opts = DWGExportOptions.GetPredefinedOptions(doc, dwgConfig.RevitPresetName)
                                               ?? new DWGExportOptions();
                                        fromPreset = true;
                                    }
                                    catch { opts = new DWGExportOptions(); }
                                }
                                else if (dwgConfig != null)
                                {
                                    // Start from a stable baseline: load the "default" predefined setup
                                    // if available, otherwise fall back to blank options. Starting from
                                    // a blank DWGExportOptions() leaves layer/linework settings in an
                                    // undefined state which is a known cause of missing lines in DWG.
                                    opts = TryLoadDefaultDwgOptions(doc);
                                    opts.FileVersion    = MapFileVersion(dwgConfig.FileVersion);
                                    opts.ExportOfSolids = dwgConfig.ExportOfSolids == DwgSolidsExport.ACIS
                                        ? SolidGeometry.ACIS : SolidGeometry.Polymesh;
                                    opts.SharedCoords   = dwgConfig.SharedCoords;
                                }
                                else
                                {
                                    opts = TryLoadDefaultDwgOptions(doc);
                                }

                                // ── Content-preservation base options (fix missing lines) ──
                                // DWGExportOptions only exposes HideReferencePlane from the
                                // visibility filters (the rest are PDF-specific). The heavy
                                // lifting for preserving lines/layers comes from starting
                                // from a predefined preset in TryLoadDefaultDwgOptions above.
                                try { opts.HideReferencePlane = true; } catch { }

                                // MergedViews controlled by "Export as xrefs" checkbox:
                                //   checked  → false → proper paper space + xref files per viewport
                                //   unchecked → true  → single file, everything in model space
                                if (dwgConfig != null)
                                    opts.MergedViews = !dwgConfig.ExportLinkedAsXrefs;

                                // Guard rail: if the user picked a Revit preset we don't override
                                // any of its layer/linework settings (the preset is the user's
                                // source of truth). We only set HideScopeBoxes/etc which are
                                // visibility filters, not layer mappings.
                                _ = fromPreset;

                                // Delete existing file to avoid Revit "file in use" dialog
                                try
                                {
                                    var existingDwg = Path.Combine(folder, fileName + ".dwg");
                                    if (File.Exists(existingDwg)) File.Delete(existingDwg);
                                }
                                catch { /* file locked — Revit will show its own dialog */ }

                                bool exported = ExportWithWarningsSuppressed(doc, () => doc.Export(folder, fileName, viewIds, opts));

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

            window.ShowDialogOverRevit();

            // Snapshot the user's PDF engine choice right after the dialog closes.
            // The callback closures above capture `_pdfEngineSnapshot` by reference.
            try { _pdfEngineSnapshot = window.GetPdfEngineSettings(); }
            catch { _pdfEngineSnapshot = new PdfEngineSettings(); }

            // Non-blocking export: process queue via Idling event
            var exportQueue = window.PendingExportQueue;
            if (exportQueue != null && exportQueue.Count > 0)
            {
                var uiApp = CommandData!.Application;
                StartIdlingExport(uiApp, doc!, exportQueue, pdfCallback, dwgCallback, logger);
            }
        }

        /// <summary>
        /// Captured just before the idling export starts so the PDF callback can
        /// decide between native export and printing through a system printer.
        /// </summary>
        private static PdfEngineSettings? _pdfEngineSnapshot;

        private static Func<long, string, string, PdfExportSettings, bool>? _pdfCb;
        private static Func<long, string, string, DwgExportConfig?, bool>? _dwgCb;
        private static ILogger? _idlingLogger;
        private static List<ExportQueueItem>? _exportQueue;
        private static int _exportIndex;
        private static int _exported;
        private static int _failed;
        private static List<string>? _exportErrors;
        private static BimPillsProgressWindow? _progressWindow;
        private static bool _exportCancelled;
        private static System.Diagnostics.Stopwatch? _exportStopwatch;

        private static void StartIdlingExport(
            UIApplication uiApp, Document doc,
            List<ExportQueueItem> queue,
            Func<long, string, string, PdfExportSettings, bool>? pdfCb,
            Func<long, string, string, DwgExportConfig?, bool>? dwgCb,
            ILogger? logger)
        {
            _pdfCb = pdfCb;
            _dwgCb = dwgCb;
            _idlingLogger = logger;
            _exportQueue = queue;
            _exportIndex = 0;
            _exported = 0;
            _failed = 0;
            _exportErrors = new List<string>();
            _exportCancelled = false;
            _exportStopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Create branded BIM Pills progress window (modeless, anchored to Revit)
            _progressWindow = new BimPillsProgressWindow(
                header: "Exportando",
                total: queue.Count,
                message: $"Exportando {queue.Count} archivos…");
            _progressWindow.Cancelled += (_, __) => { _exportCancelled = true; };
            _progressWindow.ShowOverRevit();

            // Suprimir diálogos modales de Revit que aparecen al exportar planos con recursos
            // externos desactualizados (keynotes/familias en Autodesk Docs, vínculos cloud, etc.).
            // Sin esto el usuario tendría que pulsar "Continuar" una vez por cada hoja.
            uiApp.DialogBoxShowing += OnExportDialogBoxShowing;
            uiApp.Idling += OnExportIdling;
        }

        private static void OnExportDialogBoxShowing(object? sender, DialogBoxShowingEventArgs e)
        {
            try
            {
                string dialogId = e.DialogId ?? string.Empty;
                _idlingLogger?.Info($"[ExportViews] Dialog interceptado: '{dialogId}'");

                // Auto-pulsar "Continuar" en diálogos que aparecen durante la exportación batch.
                // Cubre el dialog "Actualizar recursos" (External Resources / Update Library /
                // Reload Latest) que sale una vez por hoja cuando hay keynotes o vínculos
                // a Autodesk Docs desactualizados.
                bool shouldSuppress =
                    dialogId.IndexOf("Resource",       StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dialogId.IndexOf("Updater",        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dialogId.IndexOf("OutOfDate",      StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dialogId.IndexOf("Out_Of_Date",    StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dialogId.IndexOf("Update_Library", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dialogId.IndexOf("Reload",         StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dialogId.IndexOf("Cloud",          StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dialogId.IndexOf("Library",        StringComparison.OrdinalIgnoreCase) >= 0;

                if (shouldSuppress)
                {
                    // CommandLink1 = "Continuar" (primer enlace de comando)
                    // Si el diálogo tiene botones OK/Cancel en lugar de CommandLinks,
                    // OverrideResult con CommandLink1 simplemente no aplica y probamos IDOK.
                    e.OverrideResult((int)Autodesk.Revit.UI.TaskDialogResult.CommandLink1);
                    _idlingLogger?.Info($"[ExportViews] Diálogo '{dialogId}' auto-respondido con Continuar.");
                }
            }
            catch (Exception ex)
            {
                _idlingLogger?.Warning($"[ExportViews] Error en OnExportDialogBoxShowing: {ex.Message}");
            }
        }

        private static void OnExportIdling(object? sender, IdlingEventArgs e)
        {
            var uiApp = sender as UIApplication;
            if (uiApp == null || _exportQueue == null) return;

            if (_exportCancelled || _exportIndex >= _exportQueue.Count)
            {
                // Capture state BEFORE closing window
                bool wasCancelled = _exportCancelled;
                _exportStopwatch?.Stop();
                var elapsed = _exportStopwatch?.Elapsed ?? TimeSpan.Zero;

                // Done or cancelled — cleanup
                uiApp.Idling -= OnExportIdling;
                uiApp.DialogBoxShowing -= OnExportDialogBoxShowing;
                _progressWindow?.Complete();

                int total = _exportQueue.Count;
                string elapsedStr = elapsed.TotalMinutes >= 1
                    ? $"{(int)elapsed.TotalMinutes} min {elapsed.Seconds} seg"
                    : $"{elapsed.TotalSeconds:F1} seg";

                string header = wasCancelled
                    ? "Exportación cancelada"
                    : "Exportación completada";
                string message = $"{_exported} de {total} archivos exportados · Tiempo: {elapsedStr}";

                string? detail = null;
                if (_failed > 0 && _exportErrors != null)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.Append(_failed).Append(" archivos fallaron:");
                    foreach (var name in _exportErrors.Take(10))
                        sb.Append("\n  • ").Append(name);
                    if (_exportErrors.Count > 10)
                        sb.Append("\n  … y ").Append(_exportErrors.Count - 10).Append(" más");
                    detail = sb.ToString();
                }

                _idlingLogger?.Info($"[ExportViews] Finalizado: {_exported}/{total}, fallos: {_failed}, cancelado: {wasCancelled}, tiempo: {elapsedStr}");

                if (_failed > 0 || wasCancelled)
                    BIMPills.UI.Shared.BimPillsDialog.Warning(header, message, detail);
                else
                    BIMPills.UI.Shared.BimPillsDialog.Success(header, message, detail);

                // Cleanup static refs
                _exportQueue = null;
                _exportErrors = null;
                _pdfCb = null;
                _dwgCb = null;
                _progressWindow = null;
                _exportStopwatch = null;
                return;
            }

            // Process one item
            var item = _exportQueue[_exportIndex++];
            try
            {
                _progressWindow?.Report(
                    current: _exportIndex,
                    total:   _exportQueue.Count,
                    currentItem: item.DisplayName,
                    message: $"Exportando {(item.Format == ExportFormat.Pdf ? "PDF" : "DWG")} ({_exportIndex} de {_exportQueue.Count})");

                bool ok = false;
                if (item.Format == ExportFormat.Pdf && _pdfCb != null)
                    ok = _pdfCb(item.ViewId, item.Folder, item.FileName, item.PdfSettings ?? new PdfExportSettings());
                else if (item.Format == ExportFormat.Dwg && _dwgCb != null)
                    ok = _dwgCb(item.ViewId, item.Folder, item.FileName, item.DwgConfig);

                if (ok) _exported++;
                else { _failed++; _exportErrors?.Add($"[{item.Format}] {item.DisplayName}"); }
            }
            catch (Exception ex)
            {
                _failed++;
                _exportErrors?.Add($"[{item.Format}] {item.DisplayName}: {ex.Message}");
                _idlingLogger?.Warning($"[ExportViews] Error: {item.DisplayName} — {ex.Message}");
            }

            e.SetRaiseWithoutDelay();
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

        /// <summary>
        /// Prints a single view/sheet through Revit's <see cref="PrintManager"/>
        /// redirected to a system PDF printer (PDF24, Microsoft Print to PDF, etc.).
        /// Used when the native PDF exporter drops lines or text on complex sheets.
        /// </summary>
        /// <remarks>
        /// PDF24 (with "Auto-save" configured) prints silently. Microsoft Print to PDF
        /// always shows a Save As dialog even with <c>PrintToFile=true</c> — there is
        /// no Revit-side workaround. Users who want silent printing should install PDF24.
        /// </remarks>
        private static bool PrintViewViaSystemPrinter(
            Document doc, long viewId, string folder, string fileName,
            string printerName, ILogger? logger)
        {
            try
            {
                var outputPath = Path.Combine(folder, fileName + ".pdf");
                logger?.Info($"[ExportViews] PDF (printer '{printerName}'): {outputPath}");

                Directory.CreateDirectory(folder);
                // Delete stale output without a pre-check (avoids TOCTOU).
                try { File.Delete(outputPath); }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }

                var view = doc.GetElement(new ElementId(viewId)) as View;
                if (view == null)
                {
                    logger?.Warning($"[ExportViews] Vista id={viewId} no encontrada.");
                    return false;
                }

                var pm = doc.PrintManager;

                try { pm.SelectNewPrintDriver(printerName); }
                catch (Exception ex)
                {
                    logger?.Error(
                        $"[ExportViews] No se pudo seleccionar la impresora '{printerName}'. " +
                        "Verificá que esté instalada y habilitada.", ex);
                    return false;
                }

                pm.PrintToFile     = true;
                pm.PrintToFileName = outputPath;
                pm.CombinedFile    = false;
                pm.PrintRange      = PrintRange.Select;
                pm.Apply();

                var viewSet = new ViewSet();
                viewSet.Insert(view);

                var vss = pm.ViewSheetSetting;
                try { vss.CurrentViewSheetSet.Views = viewSet; } catch { }

                // PrintRange.Select requires the view set to exist as a named
                // entry in the document, so we save it under a unique temp name
                // and delete it after printing.
                string tempSetName = "BIMPillsTemp_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                try { vss.SaveAs(tempSetName); } catch { }

                pm.Apply();

                // Document.Print(ViewSet) returns void in Revit 2024–2027; success
                // is inferred from whether the output file was actually created.
                bool printCalled = false;
                try
                {
                    doc.Print(viewSet);
                    printCalled = true;
                }
                catch (Exception ex)
                {
                    logger?.Error($"[ExportViews] doc.Print(ViewSet) falló para viewId={viewId}", ex);
                }

                try { vss.Delete(); } catch { }

                if (!printCalled) return false;
                if (!File.Exists(outputPath))
                {
                    logger?.Warning(
                        $"[ExportViews] La impresora '{printerName}' no generó el archivo '{outputPath}'. " +
                        "Si usás Microsoft Print to PDF, el diálogo Guardar como puede haber sido cancelado.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                logger?.Error($"[ExportViews] Error en PrintViewViaSystemPrinter viewId={viewId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Tries to load a stable baseline DWGExportOptions from the document's
        /// predefined setups. Starting from a blank <see cref="DWGExportOptions"/>
        /// leaves layer mapping and linework settings undefined, which is a known
        /// cause of missing lines/text in the exported DWG. This method prefers, in
        /// order: "in-session" &gt; "AEC Extended" &gt; "AEC Standard" &gt; the first
        /// available preset &gt; a new blank DWGExportOptions().
        /// </summary>
        private static DWGExportOptions TryLoadDefaultDwgOptions(Document doc)
        {
            try
            {
                var names = BaseExportOptions.GetPredefinedSetupNames(doc);
                if (names != null && names.Count > 0)
                {
                    string[] preferred =
                    {
                        "in-session", "<in-session>",
                        "AEC Extended", "AEC Standard", "ISO Standard"
                    };
                    foreach (var p in preferred)
                    {
                        var match = names.FirstOrDefault(n =>
                            string.Equals(n, p, StringComparison.OrdinalIgnoreCase));
                        if (match != null)
                        {
                            var loaded = DWGExportOptions.GetPredefinedOptions(doc, match);
                            if (loaded != null) return loaded;
                        }
                    }
                    // Fall back to the first available preset
                    var first = DWGExportOptions.GetPredefinedOptions(doc, names[0]);
                    if (first != null) return first;
                }
            }
            catch { /* fall through */ }

            return new DWGExportOptions();
        }

        /// <summary>
        /// Executes a Revit export action while silently dismissing any Revit warning dialogs
        /// (e.g. "crop region too large", "view boundary exceeds limits").
        /// Uses Application.FailuresProcessing because doc.Export() runs outside a transaction
        /// and therefore cannot use IFailuresPreprocessor via TransactionSettings.
        /// </summary>
        private static bool ExportWithWarningsSuppressed(Document doc, Func<bool> exportAction)
        {
            EventHandler<FailuresProcessingEventArgs> handler = (_, e) =>
            {
                var accessor = e.GetFailuresAccessor();
                foreach (var msg in accessor.GetFailureMessages())
                {
                    if (msg.GetSeverity() == FailureSeverity.Warning)
                        accessor.DeleteWarning(msg);
                }
                e.SetProcessingResult(FailureProcessingResult.Continue);
            };

            doc.Application.FailuresProcessing += handler;
            try   { return exportAction(); }
            finally { doc.Application.FailuresProcessing -= handler; }
        }
    }
}
