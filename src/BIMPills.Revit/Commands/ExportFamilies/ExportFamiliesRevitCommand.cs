using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using BIMPills.Commands.ExportFamilies;
using BIMPills.Core.Commands;
using BIMPills.Core.Models;
using BIMPills.Core.ParameterExtractor;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.DI;
using BIMPills.Infrastructure.Persistence;
using BIMPills.Revit.Commands;
using BIMPills.Revit.Commands.ParameterExtractor;
using BIMPills.Revit.Context;
using BIMPills.UI.Export;
using BIMPills.UI.Shared;
using Microsoft.Win32;
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
                    var projectName    = docServices.GetProjectName();
                    var modelKey       = docServices.GetModelIdentifier();

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
                                        _pdfEngineSnapshot.PrinterName, settings, logger);
                                }

                                return ExportViewAsPdfNative(doc, viewId, folder, fileName, settings, logger);
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

                                // MergedViews: for Revit presets, the profile owns this setting.
                                // For custom configs, ExportLinkedAsXrefs=true → MergedViews=false
                                // (xref mode: paper space + separate xref file per viewport).
                                if (dwgConfig != null && !fromPreset)
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
                            dwgPresetNames,
                            modelKey);
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

                    // Detect whether Navisworks NWC Export Utility is installed for this Revit version.
                    // Strategy: check for the addin/DLL on disk — more reliable than AppDomain
                    // assembly names, which may differ from file names (manifest vs file name).
                    // Known layout for Revit 2024+:
                    //   C:\ProgramData\Autodesk\Revit\Addins\{ver}\nwexportrevit.addin
                    //   C:\ProgramData\Autodesk\Revit\Addins\{ver}\revit_exporter.Addin.bundle\nwexportrevit\nwexportrevit.dll
                    bool nwcAvailable = false;
                    try
                    {
                        string revitVer = doc.Application.VersionNumber; // e.g. "2024"
                        string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                        string appData  = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        var candidates  = new[]
                        {
                            // Addin manifest files (fastest check)
                            Path.Combine(progData, "Autodesk", "Revit", "Addins", revitVer, "nwexportrevit.addin"),
                            Path.Combine(appData,  "Autodesk", "Revit", "Addins", revitVer, "nwexportrevit.addin"),
                            // DLL inside bundle (Revit 2024+ layout)
                            Path.Combine(progData, "Autodesk", "Revit", "Addins", revitVer,
                                "revit_exporter.Addin.bundle", "nwexportrevit", "nwexportrevit.dll"),
                            // Legacy / older layout
                            Path.Combine(progData, "Autodesk", "Revit", "Addins", revitVer,
                                "NavisworksExporters", "nwexportrevit.dll"),
                        };
                        nwcAvailable = candidates.Any(File.Exists);

                        // Fallback: scan AppDomain for assemblies whose name contains known keywords
                        if (!nwcAvailable)
                        {
                            nwcAvailable = System.AppDomain.CurrentDomain.GetAssemblies()
                                .Any(a =>
                                {
                                    var name = a.GetName().Name ?? string.Empty;
                                    return name.IndexOf("nwexportrevit",      StringComparison.OrdinalIgnoreCase) >= 0
                                        || name.IndexOf("NavisworksConverter", StringComparison.OrdinalIgnoreCase) >= 0
                                        || name.IndexOf("NavisworksExport",    StringComparison.OrdinalIgnoreCase) >= 0
                                        || name.IndexOf("LcRevitExport",       StringComparison.OrdinalIgnoreCase) >= 0;
                                });
                        }
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
                        var projInfo = doc.ProjectInformation;
                        if (projInfo != null)
                        {
                            foreach (Parameter p in projInfo.Parameters)
                            {
                                if (p.Definition?.Name == null || !p.HasValue) continue;
                                var val = p.AsValueString() ?? p.AsString() ?? "";
                                if (!string.IsNullOrEmpty(val) && !paramValues.ContainsKey(p.Definition.Name))
                                    paramValues[p.Definition.Name] = val;
                            }
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

            // ── Pestaña Parámetros ─────────────────────────────────────────
            try
            {
                var activeUidoc = CommandData!.Application.ActiveUIDocument;
                var (categories, paramsByCategory, hasCurveByCategory, familyTypesByCategory) = ExtractorCategoryResolver.ResolveFromModel(doc!);
                var currentSelection = activeUidoc.Selection.GetElementIds().ToList();

                window.InitializeExtractor(
                    selectedElementCount: currentSelection.Count,
                    applyCallback: config =>
                    {
                        IList<ElementId> targets;
                        switch (config.Scope)
                        {
                            case ExtractionScope.WholeModel:
                                targets = new FilteredElementCollector(doc)
                                    .WhereElementIsNotElementType()
                                    .ToElementIds().ToList();
                                break;
                            case ExtractionScope.ActiveView:
                                targets = new FilteredElementCollector(doc, activeUidoc.ActiveView.Id)
                                    .WhereElementIsNotElementType()
                                    .ToElementIds().ToList();
                                break;
                            default:
                                targets = currentSelection;
                                break;
                        }
                        var extractResult = ExtractorApplier.Apply(doc!, targets, config);
                        ShowExtractorResultDialog(extractResult, window);
                        return extractResult.Errors.Count == 0;
                    },
                    presetRepository: new JsonExtractionPresetRepository(),
                    availableCategories: categories,
                    paramsByCategory: paramsByCategory,
                    hasCurveByCategory: hasCurveByCategory,
                    familyTypesByCategory: familyTypesByCategory);
            }
            catch (Exception ex)
            {
                logger?.Warning($"[Extractor] No se pudo inicializar la pestaña Parámetros: {ex.Message}");
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
                StartIdlingExport(uiApp, doc!, exportQueue, pdfCallback, dwgCallback, logger, window.PendingExportFolder);
            }
        }

        private static void ShowExtractorResultDialog(ExtractionResult result, System.Windows.Window? owner)
        {
            var summary =
                $"Elementos procesados: {result.ElementsProcessed}\n" +
                $"Parámetros escritos:  {result.ParametersWritten}\n" +
                $"Parámetros creados:   {result.ParametersCreated}";

            if (result.Errors.Count == 0)
            {
                BimPillsDialog.Info("Extractor de Parámetros", "Extracción completada.", detail: summary, owner: owner);
            }
            else
            {
                var sample = string.Join("\n", result.Errors.Take(5));
                var more   = result.Errors.Count > 5 ? $"\n(+{result.Errors.Count - 5} más)" : "";
                BimPillsDialog.Warning("Extractor de Parámetros",
                    $"Extracción con {result.Errors.Count} errores.",
                    detail: summary + "\n\nErrores:\n" + sample + more,
                    owner: owner);
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
        private static string? _exportBaseFolder;
        private static int _exportIndex;
        private static int _exported;
        private static int _failed;
        private static List<string>? _exportErrors;
        private static BimPillsProgressWindow? _progressWindow;
        private static bool _exportCancelled;
        private static System.Diagnostics.Stopwatch? _exportStopwatch;

        private static bool _pdf24AutoSaveConfigured;

        // ── PDF24 runtime HKCU auto-save configuration ────────────────────────
        // The PDF24 Windows service reads handler settings from the registry.
        // HKLM is set by the installer (runs as admin). HKCU is set here at
        // runtime as a safety net in case the installer config was reverted
        // (e.g. PDF24 update). We configure the STANDARD "pdf24" service — no
        // separate printer needed.
        private static void EnsurePdf24AutoSave(string tempDir, ILogger? logger)
        {
            if (_pdf24AutoSaveConfigured) return;
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\PDF24\Services\bimpills");
                if (key == null) { logger?.Warning("[PDF24] No se pudo crear clave HKCU PDF24 Services."); return; }

                key.SetValue("Handler",                 "autoSave");
                key.SetValue("AutoSaveDir",             tempDir);
                key.SetValue("AutoSaveFilename",        "$fileName");
                key.SetValue("AutoSaveShowProgress",    0, RegistryValueKind.DWord);
                key.SetValue("AutoSaveUseFileChooser",  0, RegistryValueKind.DWord);
                key.SetValue("AutoSaveOverwriteFile",   1, RegistryValueKind.DWord);
                key.SetValue("LoadInCreatorIfOpen",     0, RegistryValueKind.DWord);
                key.SetValue("AutoSaveOpenDir",         0, RegistryValueKind.DWord);
                key.SetValue("AutoSaveUseFileCmd",      0, RegistryValueKind.DWord);

                _pdf24AutoSaveConfigured = true;
                logger?.Info($"[PDF24] HKCU auto-save configurado → {tempDir}");
            }
            catch (Exception ex)
            {
                logger?.Warning($"[PDF24] No se pudo configurar HKCU auto-save: {ex.Message}");
            }
        }

        private static void StartIdlingExport(
            UIApplication uiApp, Document doc,
            List<ExportQueueItem> queue,
            Func<long, string, string, PdfExportSettings, bool>? pdfCb,
            Func<long, string, string, DwgExportConfig?, bool>? dwgCb,
            ILogger? logger,
            string? baseFolder = null)
        {
            _pdfCb = pdfCb;
            _dwgCb = dwgCb;
            _idlingLogger = logger;
            _exportQueue = queue;
            _exportBaseFolder = baseFolder;
            _exportIndex = 0;
            _exported = 0;
            _failed = 0;
            _exportErrors = new List<string>();
            _exportCancelled = false;
            _exportStopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Reset PDF24 HKCU auto-save flag
            _pdf24AutoSaveConfigured = false;

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
                    dialogId.IndexOf("Library",        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    // Revit warns when switching printers to one without a saved Default
                    // setup — it falls back to in-session settings, which is fine.
                    dialogId.IndexOf("PrintSetup",     StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dialogId.IndexOf("PrintConfig",    StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dialogId.IndexOf("PrintDriver",    StringComparison.OrdinalIgnoreCase) >= 0;

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
                else if (!string.IsNullOrEmpty(_exportBaseFolder))
                    BIMPills.UI.Shared.BimPillsDialog.SuccessWithFolder(header, message, detail, _exportBaseFolder!);
                else
                    BIMPills.UI.Shared.BimPillsDialog.Success(header, message, detail);

                _pdf24AutoSaveConfigured = false;

                // Cleanup static refs
                _exportQueue = null;
                _exportErrors = null;
                _exportBaseFolder = null;
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
        /// Exports a single view/sheet as PDF using Revit's native PDF engine
        /// (<see cref="PDFExportOptions"/> / <c>doc.Export()</c>).
        /// This always produces the correct paper size because it uses Revit's
        /// internal rendering pipeline without any printer driver involvement.
        /// </summary>
        private static bool ExportViewAsPdfNative(
            Document doc, long viewId, string folder, string fileName,
            PdfExportSettings settings, ILogger? logger)
        {
            try
            {
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
                logger?.Error($"[ExportViews] Error PDF nativo viewId={viewId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Prints a single view/sheet through Revit's <see cref="PrintManager"/>
        /// redirected to a system PDF printer (PDF24, Microsoft Print to PDF, etc.).
        /// Used when the native PDF exporter drops lines or text on complex sheets.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Print flow:</b> Uses <c>pm.SubmitPrint()</c> (NOT <c>doc.Print()</c>)
        /// because <c>doc.Print(ViewSet)</c> ignores PrintManager local settings and
        /// always uses "default print settings". Paper size + zoom are configured via
        /// <c>PrintSetup.SaveAs()</c> inside a <see cref="Transaction"/> — required
        /// because SaveAs modifies the Revit document database.
        /// </para>
        /// <para>
        /// <b>Other printers:</b> Uses <c>PrintToFile=true</c> and waits for the
        /// file at the output path. Microsoft Print to PDF may show a Save-As dialog.
        /// </para>
        /// </remarks>
        private static bool PrintViewViaSystemPrinter(
            Document doc, long viewId, string folder, string fileName,
            string printerName, PdfExportSettings settings, ILogger? logger)
        {
            // PDF24 DEVMODE workaround: PSCRIPT5 always reverts DMPAPER_A1(197)→Letter(1)
            // via DocumentProperties during CreateDC. No per-user DEVMODE injection
            // can override this. Route through Revit's native PDF engine instead.
            bool isPdf24Early = printerName.IndexOf("pdf24", StringComparison.OrdinalIgnoreCase) >= 0;
            if (isPdf24Early)
            {
                logger?.Info(
                    "[ExportViews] PDF24: PSCRIPT5 revierte dmPaperSize=197→1 en CreateDC → " +
                    "redirigiendo a motor nativo Revit para garantizar tamaño correcto.");
                return ExportViewAsPdfNative(doc, viewId, folder, fileName, settings, logger);
            }

            // Declared outside try so finally can access them
            byte[]? savedDevMode = null;
            string  targetPrinter = printerName;

            try
            {
                var outputPath = Path.Combine(folder, fileName + ".pdf");
                logger?.Info($"[ExportViews] PDF (printer '{printerName}'): {outputPath}");

                Directory.CreateDirectory(folder);

                var view = doc.GetElement(new ElementId(viewId)) as View;
                if (view == null)
                {
                    logger?.Warning($"[ExportViews] Vista id={viewId} no encontrada.");
                    return false;
                }

                // ── Get sheet dimensions early (needed for DEVMODE setup) ────
                double sheetWMm = 0, sheetHMm = 0;
                if (view is ViewSheet vsSheet)
                {
                    var outline = vsSheet.Outline;
                    // ViewSheet.Outline is in feet; convert to mm
                    sheetWMm = (outline.Max.U - outline.Min.U) * 304.8;
                    sheetHMm = (outline.Max.V - outline.Min.V) * 304.8;
                    logger?.Info($"[ExportViews] Hoja {vsSheet.SheetNumber}: {sheetWMm:F1}×{sheetHMm:F1}mm");
                }

                // ── Set printer DEVMODE BEFORE Revit reads it ────────────────
                // Revit's PrintManager reads the printer's per-user DEVMODE when
                // SelectNewPrintDriver is called.  By writing DMPAPER_USER + exact
                // sheet dimensions first, Revit picks up the correct paper size
                // automatically — no matter what the driver's registered list has.
                if (sheetWMm > 1 && sheetHMm > 1)
                    savedDevMode = PrinterDevMode.SetCustomPageSize(targetPrinter, sheetWMm, sheetHMm, logger);

                var pm = doc.PrintManager;

                // Auto-upgrade: if user selected any PDF24 printer, prefer "PDF24 (BIMPills)"
                string actualPrinter = printerName;
                if (printerName.IndexOf("pdf24", StringComparison.OrdinalIgnoreCase) >= 0
                    && printerName.IndexOf("pdf24 (bimpills)", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    try
                    {
                        pm.SelectNewPrintDriver("PDF24 (BIMPills)");
                        actualPrinter = "PDF24 (BIMPills)";
                        logger?.Info("[ExportViews] Auto-upgraded printer to 'PDF24 (BIMPills)'.");
                    }
                    catch
                    {
                        logger?.Info($"[ExportViews] 'PDF24 (BIMPills)' no disponible, usando '{printerName}'.");
                    }
                }

                if (actualPrinter == printerName)
                {
                    try { pm.SelectNewPrintDriver(printerName); }
                    catch (Exception ex)
                    {
                        logger?.Error(
                            $"[ExportViews] No se pudo seleccionar la impresora '{printerName}'.", ex);
                        if (savedDevMode != null)
                            PrinterDevMode.Restore(targetPrinter, savedDevMode, logger);
                        return false;
                    }
                }

                bool isPdf24 = actualPrinter.IndexOf("pdf24", StringComparison.OrdinalIgnoreCase) >= 0;

                // ── PDF24 auto-save setup ────────────────────────────────────
                // PDF24 routes print data through a named pipe to its service.
                // The service auto-saves the rendered PDF to PDFTemp. We clear
                // the directory before printing so we can detect the new file.
                string? pdf24TempDir = null;
                if (isPdf24)
                {
                    pdf24TempDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "BIMPills", "PDFTemp");
                    Directory.CreateDirectory(pdf24TempDir);
                    EnsurePdf24AutoSave(pdf24TempDir, logger);

                    // Clear PDFTemp so we can detect the new file unambiguously
                    try
                    {
                        foreach (var f in Directory.GetFiles(pdf24TempDir, "*.pdf"))
                        {
                            try { File.Delete(f); } catch { }
                        }
                    }
                    catch { }
                    logger?.Info($"[ExportViews] PDF24 tempDir limpio: {pdf24TempDir}");
                }

                // Delete stale output at final destination
                try { File.Delete(outputPath); }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }

                // ── Configure PrintManager ───────────────────────────────────
                // IMPORTANT: Property order matters in Revit's PrintManager API.
                // 1. PrintToFile must be true for virtual printers (PDF24 throws if false).
                // 2. PrintRange must be set BEFORE CombinedFile (otherwise Revit throws
                //    "CombinedFile cannot be set to false when PrintRange is Current/Visible").
                // 3. CombinedFile = true avoids the above error and still produces one
                //    file per view when the ViewSet contains a single view.
                pm.PrintToFile     = true;
                pm.PrintToFileName = outputPath;
                pm.PrintRange      = PrintRange.Select;
                try { pm.CombinedFile = true; }
                catch { /* safe to ignore — some driver/range combos reject this */ }

                pm.Apply();

                var viewSet = new ViewSet();
                viewSet.Insert(view);

                // ── Paper size + zoom + ViewSheetSet — all inside a Transaction ──
                // PrintSetup.SaveAs() and ViewSheetSetting.SaveAs() modify the Revit
                // document database and REQUIRE a Transaction. Without one, SaveAs
                // silently fails and the paper size is never persisted — causing Revit
                // to fall back to Letter regardless of what we set on PrintParameters.
                string tempPrintSettingName = "BIMPillsTmpPS_" + Guid.NewGuid().ToString("N").Substring(0, 6);
                string tempSetName = "BIMPillsTemp_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                try
                {
                    using (var tx = new Transaction(doc, "BIMPills Configure Print"))
                    {
                        tx.Start();
                        var printParams = pm.PrintSetup.CurrentPrintSetting.PrintParameters;

                        // Zoom from UI settings
                        if (settings.ZoomType == PdfZoomType.Custom)
                        {
                            printParams.ZoomType = ZoomType.Zoom;
                            printParams.Zoom     = settings.ZoomPercent > 0 ? settings.ZoomPercent : 100;
                        }
                        else
                        {
                            printParams.ZoomType = ZoomType.FitToPage;
                        }

                        // Paper size from sheet dimensions (via pm.PaperSizes)
                        // IMPORTANT: For PDF24, we skip this assignment intentionally.
                        // PDF24's DocumentProperties reverts any standard code (e.g. DMPAPER_A1=197)
                        // back to Letter(1), so assigning PaperSize here would override our
                        // DMPAPER_USER=256 DEVMODE that we pre-loaded via SetCustomPageSize.
                        // With DMPAPER_USER=256 already in Revit's internal DEVMODE (read from
                        // the per-user default at SelectNewPrintDriver), and PDF24 preserving
                        // code=256 through DocumentProperties, skipping this assignment lets
                        // code=256 + exact A1 dims survive all pm.Apply() calls → correct output.
                        if (!isPdf24 && view is ViewSheet viewSheetForPs)
                        {
                            var    ol  = viewSheetForPs.Outline;
                            double wIn = (ol.Max.U - ol.Min.U) * 12.0;
                            double hIn = (ol.Max.V - ol.Min.V) * 12.0;
                            if (wIn > 0.1 && hIn > 0.1)
                            {
                                var ps = FindBestPaperSize(pm.PaperSizes, wIn, hIn, logger, actualPrinter);
                                if (ps != null)
                                {
                                    printParams.PaperSize = ps;
                                    logger?.Info($"[ExportViews] PaperSize set → '{ps.Name}'");
                                }
                            }
                        }
                        else if (isPdf24)
                        {
                            logger?.Info(
                                "[ExportViews] PDF24: saltando PaperSize API → " +
                                "DMPAPER_USER=256 + dims A1 en DEVMODE se preservará en pm.Apply().");
                        }

                        // SaveAs persists paper size into the named print setting
                        // (requires active Transaction — otherwise silently fails)
                        pm.PrintSetup.SaveAs(tempPrintSettingName);
                        pm.Apply();

                        // ViewSheetSet: assign the view and save as named set
                        var vss = pm.ViewSheetSetting;
                        try { vss.CurrentViewSheetSet.Views = viewSet; } catch { }
                        try { vss.SaveAs(tempSetName); } catch { }

                        tx.Commit();
                    }
                }
                catch (Exception ex)
                {
                    logger?.Warning($"[ExportViews] No se pudo configurar papel/zoom: {ex.Message}");
                }

                // ── Verify what Revit thinks the active paper size is ────────
                try
                {
                    var ps = pm.PrintSetup.CurrentPrintSetting;
                    logger?.Info(
                        $"[ExportViews] Post-TX PaperSize='" +
                        $"{ps.PrintParameters.PaperSize?.Name}', " +
                        $"Zoom={ps.PrintParameters.ZoomType}");
                }
                catch (Exception ex) { logger?.Warning($"[ExportViews] Post-TX check falló: {ex.Message}"); }

                // ── Submit print via PrintManager (NOT doc.Print) ────────────
                // doc.Print(ViewSet) uses Revit's "default print settings" and
                // ignores the PrintManager's local state. pm.SubmitPrint() applies
                // the configured settings (including the just-saved paper size)
                // and sends the job to the printer correctly.
                pm.Apply();

                // ── Re-patch DEVMODE right before SubmitPrint ────────────────
                // Revit's pm.Apply() internally calls DocumentProperties(DM_IN|DM_OUT)
                // which causes PDF24's driver to revert DMPAPER_A1(197) → Letter(1).
                // We force DMPAPER_USER=256 + exact sheet dimensions AFTER Apply() so
                // PSCRIPT5 takes the *CustomPageSize True path (pdf24.ppd has
                // *VariablePaperSize: True) using our explicit dimensions → correct size.
                if (isPdf24 && sheetWMm > 1 && sheetHMm > 1)
                {
                    try
                    {
                        PrinterDevMode.SetCustomPageSizeDirect(targetPrinter, sheetWMm, sheetHMm, logger);
                    }
                    catch (Exception ex)
                    {
                        logger?.Warning($"[DevMode] SetDirect pre-SubmitPrint falló: {ex.Message}");
                    }
                }

                bool printCalled = false;
                try
                {
                    printCalled = pm.SubmitPrint();
                    logger?.Info($"[ExportViews] pm.SubmitPrint() → {printCalled} para viewId={viewId}");
                }
                catch (Exception ex)
                {
                    logger?.Error($"[ExportViews] pm.SubmitPrint() falló para viewId={viewId}", ex);
                }

                // ── Cleanup temp print setting and ViewSheetSet ─────────────
                try
                {
                    using (var tx2 = new Transaction(doc, "BIMPills Cleanup Print"))
                    {
                        tx2.Start();
                        try { pm.ViewSheetSetting.Delete(); } catch { }
                        try { pm.PrintSetup.Delete(); } catch { }
                        tx2.Commit();
                    }
                }
                catch { /* cleanup is best-effort */ }

                if (!printCalled) return false;

                // ── Wait for output ──────────────────────────────────────────
                if (isPdf24 && pdf24TempDir != null)
                {
                    // PDF24 flow: poll PDFTemp for the new .pdf file, then
                    // move it to the final destination.
                    string? newFile = null;
                    for (int wait = 0; wait < 24; wait++) // up to 12 seconds
                    {
                        System.Threading.Thread.Sleep(500);
                        try
                        {
                            var files = Directory.GetFiles(pdf24TempDir, "*.pdf");
                            if (files.Length > 0)
                            {
                                newFile = files[0];
                                break;
                            }
                        }
                        catch { }
                    }

                    if (newFile == null)
                    {
                        logger?.Warning(
                            $"[ExportViews] PDF24 no generó archivo en '{pdf24TempDir}' después de 12 seg.");
                        return false;
                    }

                    // Brief pause to let PDF24 finish writing / release file handle
                    System.Threading.Thread.Sleep(500);
                    logger?.Info($"[ExportViews] PDF24 generó: {Path.GetFileName(newFile)}");

                    // Move to final destination (retry once if file is still locked)
                    for (int attempt = 0; attempt < 3; attempt++)
                    {
                        try
                        {
                            if (File.Exists(outputPath)) File.Delete(outputPath);
                            File.Move(newFile, outputPath);
                            logger?.Info($"[ExportViews] PDF24 → movido a {outputPath}");
                            return true;
                        }
                        catch (IOException) when (attempt < 2)
                        {
                            logger?.Info($"[ExportViews] Archivo aún bloqueado, reintentando ({attempt + 1}/3)...");
                            System.Threading.Thread.Sleep(1000);
                        }
                        catch (Exception ex)
                        {
                            logger?.Warning($"[ExportViews] No se pudo mover PDF de PDFTemp: {ex.Message}");
                            return false;
                        }
                    }
                    return false;
                }

                // ── Non-PDF24 path: wait for file at outputPath ──────────────
                if (!File.Exists(outputPath))
                {
                    for (int wait = 0; wait < 10 && !File.Exists(outputPath); wait++)
                        System.Threading.Thread.Sleep(500);
                }

                if (!File.Exists(outputPath))
                {
                    logger?.Warning(
                        $"[ExportViews] La impresora '{actualPrinter}' no generó el archivo '{outputPath}'.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                logger?.Error($"[ExportViews] Error en PrintViewViaSystemPrinter viewId={viewId}", ex);
                return false;
            }
            finally
            {
                // Restore printer DEVMODE regardless of success or failure
                if (savedDevMode != null)
                    PrinterDevMode.Restore(targetPrinter, savedDevMode, logger);
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

        /// <summary>
        /// Finds the best matching Revit <see cref="PaperSize"/> for the given sheet dimensions.
        /// Uses Win32 <c>DeviceCapabilities(DC_PAPERSIZE)</c> to get the actual paper dimensions
        /// registered in the printer driver (in tenths of mm), so the match is by geometry rather
        /// than by name — works regardless of locale or how PDF24 names its sizes.
        /// Falls back to Revit's own PaperSizeSet if the Win32 call fails.
        /// </summary>
        private static PaperSize? FindBestPaperSize(
            PaperSizeSet paperSizes, double sheetWIn, double sheetHIn,
            ILogger? logger, string printerName)
        {
            // ── 1. Try Win32 DeviceCapabilities to get real paper dimensions ──────────
            string? bestNameFromDriver = TryGetBestPaperNameFromDriver(
                printerName, sheetWIn, sheetHIn, logger);

            if (bestNameFromDriver != null)
            {
                // Find the Revit PaperSize whose Name matches what the driver reported
                foreach (PaperSize ps in paperSizes)
                {
                    if (string.Equals(ps.Name, bestNameFromDriver, StringComparison.OrdinalIgnoreCase))
                    {
                        logger?.Info($"[ExportViews] Papel (driver)='{ps.Name}' hoja={sheetWIn:F2}×{sheetHIn:F2}in");
                        return ps;
                    }
                }
                // Name match failed — driver has the size but Revit doesn't know it by that name;
                // log and fall through to the Revit-side iteration below.
                logger?.Warning(
                    $"[ExportViews] Driver reportó '{bestNameFromDriver}' pero no está en pm.PaperSizes. " +
                    "Intentando match parcial...");

                foreach (PaperSize ps in paperSizes)
                {
                    if (ps.Name.IndexOf(bestNameFromDriver, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        bestNameFromDriver.IndexOf(ps.Name,  StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        logger?.Info($"[ExportViews] Papel (parcial)='{ps.Name}'");
                        return ps;
                    }
                }
            }

            logger?.Warning(
                $"[ExportViews] No se pudo determinar el tamaño de papel para " +
                $"{sheetWIn:F2}×{sheetHIn:F2}in en '{printerName}'. " +
                "Se usará el tamaño por defecto del driver.");
            return null;
        }

        /// <summary>
        /// Uses Win32 <c>DeviceCapabilities</c> to enumerate the printer's paper sizes and
        /// return the <em>name</em> of the one whose dimensions are closest to the sheet.
        /// Paper sizes come back as POINT[] (cx=width, cy=height) in tenths of a millimetre.
        /// Paper names come back as an array of 64-WCHAR fixed-length strings.
        /// </summary>
        private static string? TryGetBestPaperNameFromDriver(
            string printerName, double sheetWIn, double sheetHIn, ILogger? logger)
        {
            try
            {
                const ushort DC_PAPERSIZE  = 3;
                const ushort DC_PAPERNAMES = 16;
                const int    NameChars     = 64; // fixed-length name field in DC_PAPERNAMES

                // How many paper sizes does this printer support?
                int count = NativeMethods.DeviceCapabilities(
                    printerName, null, DC_PAPERSIZE, IntPtr.Zero, IntPtr.Zero);
                if (count <= 0)
                {
                    logger?.Warning($"[ExportViews] DeviceCapabilities(DC_PAPERSIZE) devolvió {count} para '{printerName}'.");
                    return null;
                }

                // Allocate buffers: POINT = 2×int32 = 8 bytes; name = 64 WCHARs = 128 bytes
                int sizeBytes = count * 8;
                int nameBytes = count * NameChars * 2; // Unicode
                var sizeBuf = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeBytes);
                var nameBuf = System.Runtime.InteropServices.Marshal.AllocHGlobal(nameBytes);
                try
                {
                    NativeMethods.DeviceCapabilities(printerName, null, DC_PAPERSIZE,  sizeBuf, IntPtr.Zero);
                    NativeMethods.DeviceCapabilities(printerName, null, DC_PAPERNAMES, nameBuf, IntPtr.Zero);

                    string? bestName = null;
                    double  bestErr  = double.MaxValue;

                    for (int i = 0; i < count; i++)
                    {
                        // Dimensions in tenths of mm  →  convert to inches (25.4 mm/in × 10 = 254)
                        int    cx  = System.Runtime.InteropServices.Marshal.ReadInt32(sizeBuf, i * 8);
                        int    cy  = System.Runtime.InteropServices.Marshal.ReadInt32(sizeBuf, i * 8 + 4);
                        double wIn = cx / 254.0;
                        double hIn = cy / 254.0;

                        double e = Math.Min(
                            Math.Abs(wIn - sheetWIn) + Math.Abs(hIn - sheetHIn),
                            Math.Abs(wIn - sheetHIn) + Math.Abs(hIn - sheetWIn));

                        if (e < bestErr)
                        {
                            bestErr  = e;
                            bestName = System.Runtime.InteropServices.Marshal.PtrToStringUni(
                                nameBuf + i * NameChars * 2)?.Trim();
                        }
                    }

                    logger?.Info(
                        $"[ExportViews] Driver '{printerName}': {count} papeles, mejor='{bestName}' " +
                        $"delta={bestErr:F3}in para hoja={sheetWIn:F2}×{sheetHIn:F2}in");

                    return bestErr < 2.0 ? bestName : null;
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(sizeBuf);
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(nameBuf);
                }
            }
            catch (Exception ex)
            {
                logger?.Warning($"[ExportViews] DeviceCapabilities falló: {ex.Message}");
                return null;
            }
        }

        /// <summary>P/Invoke wrappers for winspool.drv.</summary>
        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport(
                "winspool.drv", CharSet = System.Runtime.InteropServices.CharSet.Unicode,
                SetLastError = true, EntryPoint = "DeviceCapabilitiesW")]
            public static extern int DeviceCapabilities(
                string pDevice, string? pPort, ushort fwCapability,
                IntPtr pOutput, IntPtr pDevMode);

            [System.Runtime.InteropServices.DllImport(
                "winspool.drv", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
            public static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

            [System.Runtime.InteropServices.DllImport("winspool.drv", SetLastError = true)]
            public static extern bool ClosePrinter(IntPtr hPrinter);

            [System.Runtime.InteropServices.DllImport(
                "winspool.drv", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
            public static extern int DocumentProperties(
                IntPtr hWnd, IntPtr hPrinter, string pDeviceName,
                IntPtr pDevModeOutput, IntPtr pDevModeInput, int fMode);

            [System.Runtime.InteropServices.DllImport(
                "winspool.drv", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
            public static extern bool SetPrinter(
                IntPtr hPrinter, uint dwLevel, IntPtr pPrinter, uint dwCommand);
        }

        /// <summary>
        /// Sets the per-user DEVMODE of a printer to the paper size matching the given
        /// sheet dimensions. First attempts to find a standard paper code via
        /// <c>DeviceCapabilities(DC_PAPERS)</c> and validates it through the driver.
        /// If the driver reverts the code (e.g. PDF24 reverts DMPAPER_A1=197 → 1),
        /// falls back to <c>DMPAPER_USER=256</c> which triggers the <c>*CustomPageSize True</c>
        /// PostScript path in PPDs with <c>*VariablePaperSize: True</c> — this uses
        /// explicit dimensions instead of a code-to-PPD lookup.
        /// Returns the original DEVMODE bytes so the caller can restore them afterwards.
        /// </summary>
        private static class PrinterDevMode
        {
            private const int DM_OUT_BUFFER      = 2;
            private const int DM_IN_BUFFER       = 8;
            private const short DMPAPER_USER     = 256;
            // DEVMODE field-flag bits
            private const int DM_ORIENTATION     = 0x0001;
            private const int DM_PAPERSIZE       = 0x0002;
            private const int DM_PAPERLENGTH     = 0x0004;
            private const int DM_PAPERWIDTH      = 0x0008;
            // dmOrientation values
            private const short DMORIENT_LANDSCAPE = 2;
            // DEVMODE field byte offsets (fixed for DEVMODEW)
            private const int OFF_FIELDS         = 72;
            private const int OFF_ORIENTATION    = 76;  // short: 1=portrait, 2=landscape
            private const int OFF_PAPERSIZE      = 78;
            private const int OFF_PAPERLENGTH    = 80;  // tenths of mm, short
            private const int OFF_PAPERWIDTH     = 82;  // tenths of mm, short

            /// <summary>
            /// Queries the printer driver via <c>DC_PAPERS</c> + <c>DC_PAPERSIZE</c>
            /// to find the standard paper code whose dimensions best match the sheet.
            /// Returns null if no match within 5 mm tolerance.
            /// </summary>
            private static short? FindMatchingPaperCode(
                string printerName, double widthMm, double heightMm, ILogger? logger)
            {
                try
                {
                    const ushort DC_PAPERS    = 2;  // array of WORD paper codes
                    const ushort DC_PAPERSIZE = 3;  // array of POINT (cx,cy) in tenths of mm

                    int count = NativeMethods.DeviceCapabilities(
                        printerName, null, DC_PAPERSIZE, IntPtr.Zero, IntPtr.Zero);
                    if (count <= 0) return null;

                    var sizeBuf = System.Runtime.InteropServices.Marshal.AllocHGlobal(count * 8);
                    var codeBuf = System.Runtime.InteropServices.Marshal.AllocHGlobal(count * 2);
                    try
                    {
                        NativeMethods.DeviceCapabilities(
                            printerName, null, DC_PAPERSIZE, sizeBuf, IntPtr.Zero);
                        NativeMethods.DeviceCapabilities(
                            printerName, null, DC_PAPERS, codeBuf, IntPtr.Zero);

                        short bestCode = 0;
                        double bestErr = double.MaxValue;

                        for (int i = 0; i < count; i++)
                        {
                            int cx = System.Runtime.InteropServices.Marshal.ReadInt32(sizeBuf, i * 8);
                            int cy = System.Runtime.InteropServices.Marshal.ReadInt32(sizeBuf, i * 8 + 4);
                            double wMm = cx / 10.0;
                            double hMm = cy / 10.0;

                            double e = Math.Min(
                                Math.Abs(wMm - widthMm) + Math.Abs(hMm - heightMm),
                                Math.Abs(wMm - heightMm) + Math.Abs(hMm - widthMm));

                            if (e < bestErr)
                            {
                                bestErr = e;
                                bestCode = System.Runtime.InteropServices.Marshal.ReadInt16(codeBuf, i * 2);
                            }
                        }

                        if (bestErr < 5.0) // within 5mm
                        {
                            logger?.Info(
                                $"[DevMode] Paper code match: dmPaperSize={bestCode} " +
                                $"(delta={bestErr:F1}mm) para {widthMm:F0}×{heightMm:F0}mm");
                            return bestCode;
                        }

                        logger?.Warning(
                            $"[DevMode] No standard paper code within 5mm for " +
                            $"{widthMm:F0}×{heightMm:F0}mm (best delta={bestErr:F1}mm)");
                        return null;
                    }
                    finally
                    {
                        System.Runtime.InteropServices.Marshal.FreeHGlobal(sizeBuf);
                        System.Runtime.InteropServices.Marshal.FreeHGlobal(codeBuf);
                    }
                }
                catch (Exception ex)
                {
                    logger?.Warning($"[DevMode] FindMatchingPaperCode falló: {ex.Message}");
                    return null;
                }
            }

            // DEVMODE offset for dmFormName (WCHAR[32] = 64 bytes starting at byte 102)
            private const int OFF_FORMNAME = 102;
            private const int DM_FORMNAME  = 0x00010000;

            public static byte[]? SetCustomPageSize(
                string printerName, double widthMm, double heightMm, ILogger? logger)
            {
                try
                {
                    // Find the standard paper code BEFORE opening the printer for DEVMODE
                    short? stdCode = FindMatchingPaperCode(printerName, widthMm, heightMm, logger);

                    if (!NativeMethods.OpenPrinter(printerName, out var hPrinter, IntPtr.Zero))
                    {
                        logger?.Warning($"[DevMode] OpenPrinter('{printerName}') falló.");
                        return null;
                    }
                    try
                    {
                        int size = NativeMethods.DocumentProperties(
                            IntPtr.Zero, hPrinter, printerName, IntPtr.Zero, IntPtr.Zero, 0);
                        if (size <= 0) return null;

                        var buf = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
                        NativeMethods.DocumentProperties(
                            IntPtr.Zero, hPrinter, printerName, buf, IntPtr.Zero, DM_OUT_BUFFER);

                        // Log what the driver currently has (before our patch)
                        short origCode   = System.Runtime.InteropServices.Marshal.ReadInt16(buf, OFF_PAPERSIZE);
                        short origLength = System.Runtime.InteropServices.Marshal.ReadInt16(buf, OFF_PAPERLENGTH);
                        short origWidth  = System.Runtime.InteropServices.Marshal.ReadInt16(buf, OFF_PAPERWIDTH);
                        logger?.Info(
                            $"[DevMode] ANTES patch: dmPaperSize={origCode}, " +
                            $"w={origWidth/10.0:F1}mm, h={origLength/10.0:F1}mm");

                        // Save original bytes to restore later
                        var saved = new byte[size];
                        System.Runtime.InteropServices.Marshal.Copy(buf, saved, 0, size);

                        // Patch dmFields to include paper-size + orientation flags + form-name flag
                        int fields = System.Runtime.InteropServices.Marshal.ReadInt32(buf, OFF_FIELDS);
                        System.Runtime.InteropServices.Marshal.WriteInt32(
                            buf, OFF_FIELDS,
                            fields | DM_ORIENTATION | DM_PAPERSIZE | DM_PAPERLENGTH | DM_PAPERWIDTH | DM_FORMNAME);

                        // Use standard paper code so PSCRIPT5 maps it to the correct PPD entry.
                        // Fall back to DMPAPER_USER only when no standard code is available.
                        short paperCode = stdCode ?? DMPAPER_USER;
                        System.Runtime.InteropServices.Marshal.WriteInt16(buf, OFF_PAPERSIZE,   paperCode);
                        System.Runtime.InteropServices.Marshal.WriteInt16(buf, OFF_PAPERLENGTH, (short)(heightMm * 10));
                        System.Runtime.InteropServices.Marshal.WriteInt16(buf, OFF_PAPERWIDTH,  (short)(widthMm  * 10));

                        // Set orientation explicitly: landscape sheets (w>h) must be LANDSCAPE so
                        // PSCRIPT5 and Revit both agree on paper orientation.
                        // Without this, the driver default (portrait) would be used even for A1.
                        if (widthMm > heightMm)
                            System.Runtime.InteropServices.Marshal.WriteInt16(buf, OFF_ORIENTATION, DMORIENT_LANDSCAPE);

                        // Also set dmFormName to the PPD paper name (e.g. "A1").
                        // PSCRIPT5 uses dmFormName as an additional lookup key when
                        // resolving the paper size — this helps when the dmPaperSize
                        // code is driver-specific and not in PSCRIPT5's own code table.
                        string? paperName = FindMatchingPaperName(printerName, widthMm, heightMm);
                        if (paperName != null && OFF_FORMNAME + paperName.Length * 2 + 2 <= size)
                        {
                            // Write zero-terminated Unicode string into dmFormName field
                            byte[] nameBytes = System.Text.Encoding.Unicode.GetBytes(paperName + '\0');
                            System.Runtime.InteropServices.Marshal.Copy(
                                nameBytes, 0, buf + OFF_FORMNAME,
                                Math.Min(nameBytes.Length, 64));
                            logger?.Info($"[DevMode] dmFormName='{paperName}'");
                        }

                        // ── Validate through driver (DM_IN_BUFFER | DM_OUT_BUFFER) ──
                        // NOTE: The driver may reset non-standard values during validation.
                        // We log BEFORE and AFTER to detect this, and also write the
                        // unvalidated buffer as a fallback if validation reverts the size.
                        var validated = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
                        NativeMethods.DocumentProperties(
                            IntPtr.Zero, hPrinter, printerName, validated, buf,
                            DM_IN_BUFFER | DM_OUT_BUFFER);

                        // Log what validation produced — the "validated" is what we'll store
                        short valCode   = System.Runtime.InteropServices.Marshal.ReadInt16(validated, OFF_PAPERSIZE);
                        short valLength = System.Runtime.InteropServices.Marshal.ReadInt16(validated, OFF_PAPERLENGTH);
                        short valWidth  = System.Runtime.InteropServices.Marshal.ReadInt16(validated, OFF_PAPERWIDTH);
                        logger?.Info(
                            $"[DevMode] DESPUÉS validación: dmPaperSize={valCode}, " +
                            $"w={valWidth/10.0:F1}mm, h={valLength/10.0:F1}mm");

                        // If validation reverted the paper code, fall back to DMPAPER_USER=256.
                        // PPDs with *VariablePaperSize: True (e.g. PDF24) define *CustomPageSize True
                        // which PSCRIPT5 invokes for code=256, using dmPaperWidth/dmPaperLength
                        // directly — bypassing the paper-code → PPD-entry lookup that causes Letter.
                        IntPtr toWrite;
                        bool   validationReverted = (valCode != paperCode);
                        if (validationReverted)
                        {
                            logger?.Warning(
                                $"[DevMode] Validación revirtió código {paperCode}→{valCode}. " +
                                "Intentando DMPAPER_USER=256 (*CustomPageSize True path)...");

                            // buf still has correct A1 dims — just change the code to DMPAPER_USER
                            System.Runtime.InteropServices.Marshal.WriteInt16(buf, OFF_PAPERSIZE, DMPAPER_USER);

                            var validated2 = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
                            NativeMethods.DocumentProperties(
                                IntPtr.Zero, hPrinter, printerName, validated2, buf,
                                DM_IN_BUFFER | DM_OUT_BUFFER);

                            short valCode2 = System.Runtime.InteropServices.Marshal.ReadInt16(validated2, OFF_PAPERSIZE);
                            short valW2    = System.Runtime.InteropServices.Marshal.ReadInt16(validated2, OFF_PAPERWIDTH);
                            short valH2    = System.Runtime.InteropServices.Marshal.ReadInt16(validated2, OFF_PAPERLENGTH);
                            logger?.Info(
                                $"[DevMode] DMPAPER_USER(256) validación: code={valCode2}, " +
                                $"w={valW2/10.0:F1}mm, h={valH2/10.0:F1}mm");

                            if (valCode2 == DMPAPER_USER)
                            {
                                // Driver preserved DMPAPER_USER — PSCRIPT5 will use *CustomPageSize True
                                toWrite   = validated2;
                                paperCode = DMPAPER_USER;
                                logger?.Info("[DevMode] DMPAPER_USER=256 preservado → usando buffer validado.");
                            }
                            else
                            {
                                // Driver also reverted 256; force code=256 + A1 dims into the
                                // first-validation buffer (which preserved correct dimensions)
                                System.Runtime.InteropServices.Marshal.WriteInt16(validated, OFF_PAPERSIZE, DMPAPER_USER);
                                int vf = System.Runtime.InteropServices.Marshal.ReadInt32(validated, OFF_FIELDS);
                                System.Runtime.InteropServices.Marshal.WriteInt32(validated, OFF_FIELDS,
                                    vf | DM_PAPERLENGTH | DM_PAPERWIDTH);
                                toWrite   = validated;
                                paperCode = DMPAPER_USER;
                                logger?.Warning(
                                    $"[DevMode] Driver revirtió 256→{valCode2}. " +
                                    "Forzando DMPAPER_USER=256 + dims A1 en buffer validado.");
                            }
                            System.Runtime.InteropServices.Marshal.FreeHGlobal(validated2);
                        }
                        else
                        {
                            toWrite = validated;
                        }

                        // Write as per-user default (PRINTER_INFO_9 = single pointer to DEVMODE)
                        var info9 = System.Runtime.InteropServices.Marshal.AllocHGlobal(IntPtr.Size);
                        System.Runtime.InteropServices.Marshal.WriteIntPtr(info9, toWrite);
                        bool setPrinterOk = NativeMethods.SetPrinter(hPrinter, 9, info9, 0);
                        int  setPrinterErr = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                        System.Runtime.InteropServices.Marshal.FreeHGlobal(info9);

                        if (!setPrinterOk)
                            logger?.Warning($"[DevMode] SetPrinter(9) FALLÓ, Win32 error={setPrinterErr}");
                        else
                            logger?.Info($"[DevMode] SetPrinter(9) OK.");

                        System.Runtime.InteropServices.Marshal.FreeHGlobal(validated);
                        System.Runtime.InteropServices.Marshal.FreeHGlobal(buf);

                        string codeLabel = paperCode == DMPAPER_USER ? "DMPAPER_USER"
                                        : stdCode.HasValue            ? "estándar"
                                        :                               "directo";
                        logger?.Info(
                            $"[DevMode] Tamaño establecido: {widthMm:F1}×{heightMm:F1}mm " +
                            $"(dmPaperSize={paperCode} {codeLabel}) en '{printerName}'.");
                        return saved;
                    }
                    finally { NativeMethods.ClosePrinter(hPrinter); }
                }
                catch (Exception ex)
                {
                    logger?.Warning($"[DevMode] SetCustomPageSize falló: {ex.Message}");
                    return null;
                }
            }

            /// <summary>
            /// Directly forces <c>DMPAPER_USER=256</c> + exact dimensions into the printer's
            /// per-user DEVMODE without calling <c>DocumentProperties</c> for validation.
            /// This is called between <c>pm.Apply()</c> and <c>pm.SubmitPrint()</c> to
            /// override any reversion that Revit's internal <c>DocumentProperties</c> call
            /// may have caused. With <c>*VariablePaperSize: True</c> in the PPD, PSCRIPT5
            /// takes the <c>*CustomPageSize True</c> path and uses the explicit dimensions,
            /// producing the correct paper size in the output PDF.
            /// </summary>
            public static void SetCustomPageSizeDirect(
                string printerName, double widthMm, double heightMm, ILogger? logger)
            {
                try
                {
                    if (!NativeMethods.OpenPrinter(printerName, out var hPrinter, IntPtr.Zero))
                    {
                        logger?.Warning($"[DevMode] SetDirect: OpenPrinter('{printerName}') falló.");
                        return;
                    }
                    try
                    {
                        int size = NativeMethods.DocumentProperties(
                            IntPtr.Zero, hPrinter, printerName, IntPtr.Zero, IntPtr.Zero, 0);
                        if (size <= 0) return;

                        var buf = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
                        NativeMethods.DocumentProperties(
                            IntPtr.Zero, hPrinter, printerName, buf, IntPtr.Zero, DM_OUT_BUFFER);

                        // Log what Revit's pm.Apply() left in the per-user DEVMODE
                        short curCode = System.Runtime.InteropServices.Marshal.ReadInt16(buf, OFF_PAPERSIZE);
                        short curW    = System.Runtime.InteropServices.Marshal.ReadInt16(buf, OFF_PAPERWIDTH);
                        short curH    = System.Runtime.InteropServices.Marshal.ReadInt16(buf, OFF_PAPERLENGTH);
                        logger?.Info(
                            $"[DevMode] Pre-SubmitPrint: code={curCode}, " +
                            $"w={curW/10.0:F1}mm, h={curH/10.0:F1}mm → forzando DMPAPER_USER=256");

                        // Force DMPAPER_USER=256 + correct dimensions + orientation (no driver validation)
                        int fields = System.Runtime.InteropServices.Marshal.ReadInt32(buf, OFF_FIELDS);
                        System.Runtime.InteropServices.Marshal.WriteInt32(buf, OFF_FIELDS,
                            fields | DM_ORIENTATION | DM_PAPERSIZE | DM_PAPERLENGTH | DM_PAPERWIDTH);
                        System.Runtime.InteropServices.Marshal.WriteInt16(buf, OFF_PAPERSIZE,   DMPAPER_USER);
                        System.Runtime.InteropServices.Marshal.WriteInt16(buf, OFF_PAPERLENGTH, (short)(heightMm * 10));
                        System.Runtime.InteropServices.Marshal.WriteInt16(buf, OFF_PAPERWIDTH,  (short)(widthMm  * 10));
                        if (widthMm > heightMm)
                            System.Runtime.InteropServices.Marshal.WriteInt16(buf, OFF_ORIENTATION, DMORIENT_LANDSCAPE);

                        var info9 = System.Runtime.InteropServices.Marshal.AllocHGlobal(IntPtr.Size);
                        System.Runtime.InteropServices.Marshal.WriteIntPtr(info9, buf);
                        bool ok  = NativeMethods.SetPrinter(hPrinter, 9, info9, 0);
                        int  err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                        System.Runtime.InteropServices.Marshal.FreeHGlobal(info9);
                        System.Runtime.InteropServices.Marshal.FreeHGlobal(buf);

                        if (ok)
                            logger?.Info($"[DevMode] SetDirect(256, {widthMm:F1}×{heightMm:F1}mm): SetPrinter(9) OK.");
                        else
                            logger?.Warning($"[DevMode] SetDirect(256, {widthMm:F1}×{heightMm:F1}mm): SetPrinter(9) FALLÓ, err={err}.");
                    }
                    finally { NativeMethods.ClosePrinter(hPrinter); }
                }
                catch (Exception ex)
                {
                    logger?.Warning($"[DevMode] SetCustomPageSizeDirect falló: {ex.Message}");
                }
            }

            /// <summary>Returns the PPD paper name (e.g. "A1") from the driver for the given dimensions.</summary>
            private static string? FindMatchingPaperName(string printerName, double widthMm, double heightMm)
            {
                try
                {
                    const ushort DC_PAPERSIZE  = 3;
                    const ushort DC_PAPERNAMES = 16;
                    const int    NameChars     = 64;

                    int count = NativeMethods.DeviceCapabilities(
                        printerName, null, DC_PAPERSIZE, IntPtr.Zero, IntPtr.Zero);
                    if (count <= 0) return null;

                    var sizeBuf = System.Runtime.InteropServices.Marshal.AllocHGlobal(count * 8);
                    var nameBuf = System.Runtime.InteropServices.Marshal.AllocHGlobal(count * NameChars * 2);
                    try
                    {
                        NativeMethods.DeviceCapabilities(printerName, null, DC_PAPERSIZE,  sizeBuf, IntPtr.Zero);
                        NativeMethods.DeviceCapabilities(printerName, null, DC_PAPERNAMES, nameBuf, IntPtr.Zero);

                        string? bestName = null;
                        double  bestErr  = double.MaxValue;
                        for (int i = 0; i < count; i++)
                        {
                            int cx = System.Runtime.InteropServices.Marshal.ReadInt32(sizeBuf, i * 8);
                            int cy = System.Runtime.InteropServices.Marshal.ReadInt32(sizeBuf, i * 8 + 4);
                            double e = Math.Min(
                                Math.Abs(cx / 10.0 - widthMm)  + Math.Abs(cy / 10.0 - heightMm),
                                Math.Abs(cx / 10.0 - heightMm) + Math.Abs(cy / 10.0 - widthMm));
                            if (e < bestErr)
                            {
                                bestErr  = e;
                                bestName = System.Runtime.InteropServices.Marshal.PtrToStringUni(
                                    nameBuf + i * NameChars * 2, NameChars)?.TrimEnd('\0', ' ');
                            }
                        }
                        return bestErr < 5.0 ? bestName : null;
                    }
                    finally
                    {
                        System.Runtime.InteropServices.Marshal.FreeHGlobal(sizeBuf);
                        System.Runtime.InteropServices.Marshal.FreeHGlobal(nameBuf);
                    }
                }
                catch { return null; }
            }

            public static void Restore(string printerName, byte[] savedDevMode, ILogger? logger)
            {
                try
                {
                    if (!NativeMethods.OpenPrinter(printerName, out var hPrinter, IntPtr.Zero)) return;
                    try
                    {
                        var buf = System.Runtime.InteropServices.Marshal.AllocHGlobal(savedDevMode.Length);
                        System.Runtime.InteropServices.Marshal.Copy(savedDevMode, 0, buf, savedDevMode.Length);
                        var info9 = System.Runtime.InteropServices.Marshal.AllocHGlobal(IntPtr.Size);
                        System.Runtime.InteropServices.Marshal.WriteIntPtr(info9, buf);
                        NativeMethods.SetPrinter(hPrinter, 9, info9, 0);
                        System.Runtime.InteropServices.Marshal.FreeHGlobal(info9);
                        System.Runtime.InteropServices.Marshal.FreeHGlobal(buf);
                        logger?.Info($"[DevMode] DEVMODE restaurado en '{printerName}'.");
                    }
                    finally { NativeMethods.ClosePrinter(hPrinter); }
                }
                catch (Exception ex)
                {
                    logger?.Warning($"[DevMode] Restore falló: {ex.Message}");
                }
            }
        }
    }
}
