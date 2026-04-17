using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using BIMPills.Commands.Transfer;
using BIMPills.Core.Commands;
using BIMPills.Core.Models;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.DI;
using BIMPills.Revit.Commands;
using BIMPills.Revit.Context;
using BIMPills.Revit.Commands.DataManager;
using BIMPills.UI.Shared;
using BIMPills.UI.Transfer;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMPills.Revit.Commands.Transfer
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class TransferRevitCommand : RevitCommandBase
    {
        protected override IPluginCommand CreateCommand()
            => new TransferCommand();

        protected override void OnSuccess(IPluginCommand command)
        {
            var logger = ServiceLocator.IsRegistered<ILogger>()
                ? ServiceLocator.Get<ILogger>() : null;

            var app        = CommandData!.Application.Application;
            var targetDoc  = CommandData.Application.ActiveUIDocument.Document;
            var modelName  = targetDoc.Title ?? "Modelo";

            var window = new TransferWindow();
            window.SetModelName(modelName);

            var openDocs = BuildOpenDocsList(app, targetDoc);

            // ── Plantillas de Vista ──────────────────────────────────────────

            try
            {
                Func<string, IReadOnlyList<ViewTemplateInfo>> getTemplates = docTitle =>
                {
                    try
                    {
                        foreach (Document d in app.Documents)
                        {
                            if (!d.Title.Equals(docTitle, StringComparison.OrdinalIgnoreCase)) continue;
                            return new FilteredElementCollector(d)
                                .OfClass(typeof(View))
                                .Cast<View>()
                                .Where(v => v.IsTemplate)
                                .Select(v => new ViewTemplateInfo
                                {
                                    Id          = RevitId(v.Id),
                                    Name        = v.Name,
                                    ViewType    = v.ViewType.ToString(),
                                    FilterCount = SafeFilterCount(v),
                                    SourceDocumentTitle = docTitle
                                })
                                .OrderBy(t => t.Name)
                                .ToList();
                        }
                    }
                    catch (Exception ex) { logger?.Warning($"[Transferir] getTemplates '{docTitle}': {ex.Message}"); }
                    return new List<ViewTemplateInfo>();
                };

                Func<string, long, ViewTemplateDetail?> getDetail = (docTitle, id) =>
                {
                    try
                    {
                        foreach (Document d in app.Documents)
                        {
                            if (!d.Title.Equals(docTitle, StringComparison.OrdinalIgnoreCase)) continue;
                            var view = d.GetElement(new ElementId(id)) as View;
                            if (view == null || !view.IsTemplate) return null;

                            int assigned = 0;
                            try { assigned = new FilteredElementCollector(targetDoc).OfClass(typeof(View)).Cast<View>()
                                    .Count(v => !v.IsTemplate && v.ViewTemplateId != ElementId.InvalidElementId
                                        && d.GetElement(v.ViewTemplateId) is View vt
                                        && vt.Name.Equals(view.Name, StringComparison.OrdinalIgnoreCase)); }
                            catch { }

                            return new ViewTemplateDetail
                            {
                                Name              = view.Name,
                                ViewType          = view.ViewType.ToString(),
                                AssignedViewCount = assigned,
                                Parameters        = BuildTemplateParameters(view)
                            };
                        }
                    }
                    catch (Exception ex) { logger?.Warning($"[Transferir] getDetail {id}: {ex.Message}"); }
                    return null;
                };

                Func<string, IReadOnlyList<long>, ConflictResolution, TransferResult> transferTemplates =
                    (docTitle, ids, conflict) =>
                {
                    var result = new TransferResult();
                    try
                    {
                        var sourceDoc = FindDoc(app, docTitle);
                        if (sourceDoc == null) { result.Errors.Add($"Documento '{docTitle}' no encontrado."); return result; }

                        var revitIds = ids.Select(id => new ElementId(id)).ToList();

                        // Collect dependent filter elements — view templates reference
                        // ParameterFilterElements by ID but CopyElements won't include them
                        var allIds = new List<ElementId>(revitIds);
                        foreach (var tid in revitIds)
                        {
                            try
                            {
                                if (sourceDoc.GetElement(tid) is View tmpl && tmpl.IsTemplate)
                                {
                                    var filterIds = tmpl.GetFilters();
                                    foreach (var fid in filterIds)
                                    {
                                        if (!allIds.Contains(fid))
                                            allIds.Add(fid);
                                    }
                                }
                            }
                            catch { }
                        }
                        logger?.Info($"[Transferir] Plantillas: {revitIds.Count} templates + {allIds.Count - revitIds.Count} filtros dependientes");

                        using var tx = new Transaction(targetDoc, "BIM Pills: Importar plantillas de vista");
                        tx.Start();

                        if (conflict == ConflictResolution.Replace)
                        {
                            // Delete existing templates with matching names
                            var sourceNames = revitIds.Select(id => sourceDoc.GetElement(id)).OfType<View>()
                                .Select(v => v.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                            var toDelete = new FilteredElementCollector(targetDoc).OfClass(typeof(View)).Cast<View>()
                                .Where(v => v.IsTemplate && sourceNames.Contains(v.Name))
                                .Select(v => v.Id).ToList();
                            foreach (var delId in toDelete)
                            { try { targetDoc.Delete(delId); result.Conflicts++; } catch { } }
                        }

                        // Snapshot existing filter names → IDs before copy
                        var existingFilters = new FilteredElementCollector(targetDoc)
                            .OfClass(typeof(ParameterFilterElement))
                            .Cast<ParameterFilterElement>()
                            .ToDictionary(f => f.Name, f => f.Id, StringComparer.OrdinalIgnoreCase);

                        var opts = new CopyPasteOptions();
                        opts.SetDuplicateTypeNamesHandler(new BIMPillsDuplicateHandler(conflict));
                        var copied = ElementTransformUtils.CopyElements(sourceDoc, allIds, targetDoc, Transform.Identity, opts);

                        // Overwrite existing filters with source definitions (like Revit's
                        // native "Transfer Project Standards" overwrite behavior).
                        // CopyElements renames duplicates with "(N)" suffix — we detect them,
                        // overwrite the original filter's rules/categories, remap templates
                        // to the original, and delete the "(N)" copies.
                        if (copied != null)
                        {
                            var copiedList = copied.ToList();
                            var newFilterIds = new List<ElementId>();
                            foreach (var cid in copiedList)
                            {
                                if (targetDoc.GetElement(cid) is ParameterFilterElement)
                                    newFilterIds.Add(cid);
                            }

                            if (newFilterIds.Count > 0)
                            {
                                // Build mapping: "(N)" duplicate → original existing filter
                                var remapFilter = new Dictionary<ElementId, ElementId>();
                                foreach (var nfId in newFilterIds)
                                {
                                    var newFilter = targetDoc.GetElement(nfId) as ParameterFilterElement;
                                    if (newFilter == null) continue;

                                    // Try exact name match first
                                    if (existingFilters.TryGetValue(newFilter.Name, out var origId) && origId != nfId)
                                    {
                                        remapFilter[nfId] = origId;
                                        continue;
                                    }

                                    // Strip "(N)" suffix that CopyElements appends to duplicates
                                    var suffixMatch = System.Text.RegularExpressions.Regex.Match(
                                        newFilter.Name, @"\s*\(\d+\)$");
                                    if (suffixMatch.Success)
                                    {
                                        string baseName = newFilter.Name.Substring(0, suffixMatch.Index);
                                        if (existingFilters.TryGetValue(baseName, out origId) && origId != nfId)
                                            remapFilter[nfId] = origId;
                                    }
                                }

                                if (remapFilter.Count > 0)
                                {
                                    // Overwrite original filters with source definitions
                                    foreach (var kvp in remapFilter)
                                    {
                                        try
                                        {
                                            var dupFilter  = targetDoc.GetElement(kvp.Key)   as ParameterFilterElement;
                                            var origFilter = targetDoc.GetElement(kvp.Value) as ParameterFilterElement;
                                            if (dupFilter != null && origFilter != null)
                                            {
                                                origFilter.SetCategories(dupFilter.GetCategories());
                                                var ef = dupFilter.GetElementFilter();
                                                if (ef != null)
                                                    origFilter.SetElementFilter(ef);
                                            }
                                        }
                                        catch (Exception ex) { logger?.Warning($"[Transferir] Overwrite filtro: {ex.Message}"); }
                                    }

                                    // Remap new templates to use original filters instead of "(N)" copies
                                    foreach (var cid in copiedList)
                                    {
                                        if (targetDoc.GetElement(cid) is View newTmpl && newTmpl.IsTemplate)
                                        {
                                            try
                                            {
                                                var tmplFilters = newTmpl.GetFilters();
                                                foreach (var fid in tmplFilters)
                                                {
                                                    if (remapFilter.TryGetValue(fid, out var origFid))
                                                    {
                                                        var overrides = newTmpl.GetFilterOverrides(fid);
                                                        var visible   = newTmpl.GetFilterVisibility(fid);
                                                        newTmpl.RemoveFilter(fid);
                                                        newTmpl.AddFilter(origFid);
                                                        newTmpl.SetFilterOverrides(origFid, overrides);
                                                        newTmpl.SetFilterVisibility(origFid, visible);
                                                    }
                                                }
                                            }
                                            catch { }
                                        }
                                    }

                                    // Delete "(N)" copies — safe, no longer referenced
                                    foreach (var dupId in remapFilter.Keys)
                                    { try { targetDoc.Delete(dupId); } catch { } }
                                    logger?.Info($"[Transferir] Sobrescritos {remapFilter.Count} filtros existentes con definiciones de origen");
                                }
                            }
                        }

                        result.Transferred = revitIds.Count;
                        result.Skipped = Math.Max(0, ids.Count - result.Transferred);
                        tx.Commit();
                        logger?.Info($"[Transferir] Plantillas: {result.Transferred}/{ids.Count} desde '{docTitle}'.");
                    }
                    catch (Exception ex) { logger?.Error("[Transferir] Error plantillas", ex); result.Errors.Add(ex.Message); }
                    return result;
                };

                window.InitializeViewTemplates(openDocs, getTemplates, getDetail, transferTemplates);
            }
            catch (Exception ex) { logger?.Warning($"[Transferir] InitializeViewTemplates: {ex.Message}"); }

            // ── Filtros de Vista ─────────────────────────────────────────────

            try
            {
                Func<string, IReadOnlyList<TransferableFilterInfo>> getFilters = docTitle =>
                {
                    try
                    {
                        foreach (Document d in app.Documents)
                        {
                            if (!d.Title.Equals(docTitle, StringComparison.OrdinalIgnoreCase)) continue;
                            return new FilteredElementCollector(d)
                                .OfClass(typeof(ParameterFilterElement))
                                .Cast<ParameterFilterElement>()
                                .Select(f => new TransferableFilterInfo
                                {
                                    Id            = RevitId(f.Id),
                                    Name          = f.Name,
                                    FilterType    = "Par\u00e1metro",
                                    CategoryCount = SafeCategoryCount(f),
                                    RuleCount     = SafeRuleCount(f)
                                })
                                .OrderBy(f => f.Name)
                                .ToList();
                        }
                    }
                    catch (Exception ex) { logger?.Warning($"[Transferir] getFilters '{docTitle}': {ex.Message}"); }
                    return new List<TransferableFilterInfo>();
                };

                Func<string, long, FilterDetail?> getFilterDetail = (docTitle, id) =>
                {
                    try
                    {
                        foreach (Document d in app.Documents)
                        {
                            if (!d.Title.Equals(docTitle, StringComparison.OrdinalIgnoreCase)) continue;
                            var filter = d.GetElement(new ElementId(id)) as ParameterFilterElement;
                            if (filter == null) return null;

                            var categories = filter.GetCategories()
                                .Select(catId => Category.GetCategory(d, catId)?.Name ?? catId.ToString())
                                .OrderBy(n => n)
                                .ToList();

                            return new FilterDetail
                            {
                                Name       = filter.Name,
                                FilterType = "Par\u00e1metro",
                                Categories = categories,
                                Rules      = BuildFilterRules(filter, d)
                            };
                        }
                    }
                    catch (Exception ex) { logger?.Warning($"[Transferir] getFilterDetail {id}: {ex.Message}"); }
                    return null;
                };

                Func<string, IReadOnlyList<long>, ConflictResolution, TransferResult> transferFilters =
                    (docTitle, ids, conflict) =>
                {
                    var result = new TransferResult();
                    try
                    {
                        var sourceDoc = FindDoc(app, docTitle);
                        if (sourceDoc == null) { result.Errors.Add($"Documento '{docTitle}' no encontrado."); return result; }

                        var revitIds = ids.Select(id => new ElementId(id)).ToList();

                        using var tx = new Transaction(targetDoc, "BIM Pills: Importar filtros de vista");
                        tx.Start();

                        if (conflict == ConflictResolution.Replace)
                        {
                            var sourceNames = revitIds
                                .Select(id => sourceDoc.GetElement(id)?.Name)
                                .Where(n => n != null).ToHashSet(StringComparer.OrdinalIgnoreCase);
                            var toDelete = new FilteredElementCollector(targetDoc)
                                .OfClass(typeof(ParameterFilterElement))
                                .Where(e => sourceNames.Contains(e.Name!))
                                .Select(e => e.Id).ToList();
                            foreach (var delId in toDelete)
                            { try { targetDoc.Delete(delId); result.Conflicts++; } catch { } }
                        }

                        var opts = new CopyPasteOptions();
                        opts.SetDuplicateTypeNamesHandler(new BIMPillsDuplicateHandler(conflict));
                        var copied = ElementTransformUtils.CopyElements(sourceDoc, revitIds, targetDoc, Transform.Identity, opts);

                        result.Transferred = copied?.Count ?? 0;
                        result.Skipped = Math.Max(0, ids.Count - result.Transferred);
                        tx.Commit();
                        logger?.Info($"[Transferir] Filtros: {result.Transferred}/{ids.Count} desde '{docTitle}'.");
                    }
                    catch (Exception ex) { logger?.Error("[Transferir] Error filtros", ex); result.Errors.Add(ex.Message); }
                    return result;
                };

                window.InitializeViewFilters(openDocs, getFilters, getFilterDetail, transferFilters);
            }
            catch (Exception ex) { logger?.Warning($"[Transferir] InitializeViewFilters: {ex.Message}"); }

            // ── Otros Estándares ─────────────────────────────────────────────

            try
            {
                Func<string, string, IReadOnlyList<ProjectStandardItem>> getItems =
                    (docTitle, categoryKey) => GetStandardItems(app, docTitle, categoryKey, logger);

                Func<string, IReadOnlyList<long>, ConflictResolution, Action<int, int, string>?, ProjectStandardTransferResult> transferStandards =
                    (docTitle, ids, conflict, progress) =>
                {
                    var result = new ProjectStandardTransferResult();
                    try
                    {
                        var sourceDoc = FindDoc(app, docTitle);
                        if (sourceDoc == null) { result.Errors.Add($"Documento '{docTitle}' no encontrado."); return result; }

                        var revitIds = ids.Select(id => new ElementId(id)).ToList();

                        using var tx = new Transaction(targetDoc, "BIM Pills: Importar normas de proyecto");

                        var failOpts = tx.GetFailureHandlingOptions();
                        failOpts.SetFailuresPreprocessor(new SilentWarningsPreprocessor());
                        failOpts.SetClearAfterRollback(true);
                        tx.SetFailureHandlingOptions(failOpts);

                        tx.Start();

                        // Transfer element by element to report progress
                        for (int i = 0; i < revitIds.Count; i++)
                        {
                            var id = revitIds[i];
                            var srcEl = sourceDoc.GetElement(id);
                            var elName = srcEl?.Name ?? id.ToString();

                            progress?.Invoke(i + 1, revitIds.Count, elName);

                            try
                            {
                                if (conflict == ConflictResolution.Replace && srcEl != null)
                                {
                                    // Never delete DimensionType or TextNoteType — Revit manages these internally.
                                    // Let CopyPasteOptions + DuplicateHandler handle conflicts for these types.
                                    // Never delete dimension/text types — let Revit handle conflicts via CopyPasteOptions
                                    bool safeToDelete = !(srcEl is DimensionType || srcEl is SpotDimensionType || srcEl is TextNoteType);
                                    if (safeToDelete)
                                    {
                                        var targetMatch = new FilteredElementCollector(targetDoc)
                                            .OfClass(srcEl.GetType())
                                            .FirstOrDefault(e => e.Name.Equals(elName, StringComparison.OrdinalIgnoreCase));
                                        if (targetMatch != null && RevitId(targetMatch.Id) > 0)
                                        {
                                            try { targetDoc.Delete(targetMatch.Id); result.Conflicts++; } catch { }
                                        }
                                    }
                                }

                                // Check if element already exists in target (for accurate counting)
                                bool existedBefore = false;
                                if (srcEl is DimensionType || srcEl is SpotDimensionType || srcEl is TextNoteType)
                                {
                                    existedBefore = new FilteredElementCollector(targetDoc)
                                        .OfClass(srcEl.GetType())
                                        .Any(e => e.Name.Equals(elName, StringComparison.OrdinalIgnoreCase));
                                }

                                var opts = new CopyPasteOptions();
                                opts.SetDuplicateTypeNamesHandler(new BIMPillsDuplicateHandler(conflict));
                                var copied = ElementTransformUtils.CopyElements(
                                    sourceDoc, new List<ElementId> { id },
                                    targetDoc, Transform.Identity, opts);

                                if (copied != null && copied.Count > 0)
                                {
                                    if (existedBefore)
                                    {
                                        result.Skipped++;
                                        result.SkippedNames.Add(elName);
                                    }
                                    else
                                        result.Transferred++;
                                }
                                else
                                {
                                    result.Skipped++;
                                    result.SkippedNames.Add(elName);
                                }
                            }
                            catch
                            {
                                result.Skipped++;
                            }
                        }

                        tx.Commit();
                        logger?.Info($"[Transferir] Estándares: {result.Transferred}/{ids.Count} desde '{docTitle}'.");
                    }
                    catch (Exception ex) { logger?.Error("[Transferir] Error estándares", ex); result.Errors.Add(ex.Message); }
                    return result;
                };

                window.InitializeProjectStandards(openDocs, getItems, transferStandards);
            }
            catch (Exception ex) { logger?.Warning($"[Transferir] InitializeProjectStandards: {ex.Message}"); }

            window.ShowDialogOverRevit();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static IReadOnlyList<OpenDocumentInfo> BuildOpenDocsList(
            Autodesk.Revit.ApplicationServices.Application app, Document targetDoc)
        {
            var list = new List<OpenDocumentInfo>();
            try
            {
                foreach (Document d in app.Documents)
                {
                    if (d.IsFamilyDocument) continue;
                    list.Add(new OpenDocumentInfo
                    {
                        Title     = d.Title,
                        PathName  = d.PathName ?? "",
                        IsCurrent = string.Equals(d.Title, targetDoc.Title, StringComparison.OrdinalIgnoreCase)
                    });
                }
            }
            catch { }
            return list;
        }

        private static Document? FindDoc(Autodesk.Revit.ApplicationServices.Application app, string title)
        {
            foreach (Document d in app.Documents)
                if (d.Title.Equals(title, StringComparison.OrdinalIgnoreCase)) return d;
            return null;
        }

        private static long RevitId(ElementId id)
        {
#if REVIT2024
#pragma warning disable CS0618 // IntegerValue obsoleto en 2024 — necesario para net48
            return (long)id.IntegerValue;
#pragma warning restore CS0618
#else
            return id.Value;
#endif
        }

        private static int SafeFilterCount(View view)   { try { return view.GetFilters().Count; } catch { return 0; } }
        private static int SafeCategoryCount(ParameterFilterElement f) { try { return f.GetCategories().Count; } catch { return 0; } }
        private static int SafeRuleCount(ParameterFilterElement f)
        {
            try
            {
                var ef = f.GetElementFilter();
                if (ef is LogicalAndFilter laf) return laf.GetFilters().Count;
                if (ef is LogicalOrFilter lof)  return lof.GetFilters().Count;
                return ef != null ? 1 : 0;
            }
            catch { return 0; }
        }

        private static List<string> BuildFilterRules(ParameterFilterElement filter, Document doc)
        {
            var rules = new List<string>();
            try
            {
                var ef = filter.GetElementFilter();
                IList<Autodesk.Revit.DB.ElementFilter>? children = null;
                if (ef is LogicalAndFilter laf) children = laf.GetFilters();
                else if (ef is LogicalOrFilter lof) children = lof.GetFilters();

                var paramFilters = children != null
                    ? children.OfType<ElementParameterFilter>().ToList()
                    : ef is ElementParameterFilter epf ? new List<ElementParameterFilter> { epf } : new List<ElementParameterFilter>();

                foreach (var pf in paramFilters)
                {
                    foreach (var rule in pf.GetRules())
                    {
                        try
                        {
                            var paramId = rule.GetRuleParameter();
                            // Try shared/project parameter first
                            var paramName = doc.GetElement(paramId)?.Name;
                            if (string.IsNullOrEmpty(paramName))
                            {
                                // Built-in parameter: convert ID to enum and get label
                                try
                                {
#if REVIT2024
#pragma warning disable CS0618 // IntegerValue obsoleto en 2024 — necesario para net48
                                    var bip = (BuiltInParameter)paramId.IntegerValue;
#pragma warning restore CS0618
#else
                                    var bip = (BuiltInParameter)(int)paramId.Value;
#endif
                                    paramName = LabelUtils.GetLabelFor(bip);
                                }
                                catch { paramName = null; }
                            }
                            rules.Add(paramName ?? "(condición)");
                        }
                        catch { rules.Add("(condición)"); }
                    }
                }
            }
            catch { }
            return rules;
        }

        // ── View Template parameters ──────────────────────────────────────────

        private static readonly HashSet<BuiltInParameter> _viewTemplateBips = new HashSet<BuiltInParameter>
        {
            BuiltInParameter.VIEW_SCALE_PULLDOWN_METRIC,
            BuiltInParameter.VIEW_SCALE_PULLDOWN_IMPERIAL,
            BuiltInParameter.VIEW_DETAIL_LEVEL,
            BuiltInParameter.VIEW_PARTS_VISIBILITY,
            BuiltInParameter.VIEW_PHASE,
            BuiltInParameter.VIEW_PHASE_FILTER,
            BuiltInParameter.VIEW_DISCIPLINE,
            BuiltInParameter.VIEW_UNDERLAY_ORIENTATION,
            BuiltInParameter.MODEL_GRAPHICS_STYLE,
            BuiltInParameter.VIEWER_CROP_REGION,
            BuiltInParameter.VIEWER_CROP_REGION_VISIBLE,
            BuiltInParameter.VIEWER_ANNOTATION_CROP_ACTIVE,
            BuiltInParameter.VIEWER_BOUND_FAR_CLIPPING,
            BuiltInParameter.VIEWER_BOUND_OFFSET_FAR,
            BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP,
            BuiltInParameter.VIEW_SHOW_HIDDEN_LINES,
            BuiltInParameter.GRAPHIC_DISPLAY_OPTIONS_SHADOWS,
            BuiltInParameter.GRAPHIC_DISPLAY_OPTIONS_LIGHTING,
            BuiltInParameter.GRAPHIC_DISPLAY_OPTIONS_BACKGROUND,
            BuiltInParameter.VIEW_TEMPLATE,
            BuiltInParameter.PLAN_VIEW_LEVEL,
            BuiltInParameter.VIEWER_OPTION_VISIBILITY,
        };

        private static List<ViewTemplateParameter> BuildTemplateParameters(View view)
        {
            var rows = new List<ViewTemplateParameter>();
            try
            {
                var seen = new HashSet<BuiltInParameter>();
                foreach (Parameter p in view.Parameters)
                {
                    if (p.IsShared) continue;
                    if (p.Definition is not InternalDefinition internalDef) continue;
                    var bip = internalDef.BuiltInParameter;
                    if (!_viewTemplateBips.Contains(bip)) continue;
                    if (!seen.Add(bip)) continue;

                    string value = "";
                    bool isComplex = false;
                    try
                    {
                        if (p.StorageType == StorageType.String)
                            value = p.AsString() ?? "";
                        else
                        {
                            value = p.AsValueString() ?? "";
                            if (string.IsNullOrEmpty(value) && p.StorageType == StorageType.ElementId)
                                isComplex = true;
                        }
                    }
                    catch { isComplex = true; }

                    rows.Add(new ViewTemplateParameter
                    {
                        Name      = p.Definition.Name,
                        Value     = value,
                        IsComplex = isComplex,
                        Include   = true
                    });
                }

                void AddComplex(string name)
                {
                    if (rows.All(r => !string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)))
                        rows.Add(new ViewTemplateParameter { Name = name, Value = "", IsComplex = true, Include = true });
                }
                AddComplex("Modificaciones de V/G - Modelo");
                AddComplex("Modificaciones de V/G - Anotaci\u00f3n");
                AddComplex("Modificaciones de V/G - Filtros");
                AddComplex("Modificaciones de V/G - Importaciones");
                AddComplex("Modificaciones de V/G - Modelo anal\u00edtico");
                AddComplex("Modificaciones de V/G - Iluminaci\u00f3n");
            }
            catch { }
            return rows.OrderBy(r => r.Name).ToList();
        }

        // ── Project Standards ─────────────────────────────────────────────────

        private static IReadOnlyList<ProjectStandardItem> GetStandardItems(
            Autodesk.Revit.ApplicationServices.Application app,
            string docTitle, string categoryKey, ILogger? logger)
        {
            var results = new List<ProjectStandardItem>();
            try
            {
                var sourceDoc = FindDoc(app, docTitle);
                if (sourceDoc == null) return results;

                switch (categoryKey)
                {
                    case ProjectStandardKeys.DimensionTypes:
                        CollectDimensionTypes(sourceDoc, results);
                        break;
                    case ProjectStandardKeys.SpotDimTypes:
                        CollectSpotDimensionTypes(sourceDoc, results);
                        break;
                    case ProjectStandardKeys.TextNoteTypes:
                        CollectElements<TextNoteType>(sourceDoc, results);
                        break;
                    case ProjectStandardKeys.LineStyles:
                        CollectLineStyles(sourceDoc, results);
                        break;
                    case ProjectStandardKeys.WallTypes:
                        CollectElements<WallType>(sourceDoc, results,
                            e => ((WallType)e).Kind != WallKind.Curtain ? DescribeCompoundStructure(((WallType)e).GetCompoundStructure()) : "Muro cortina");
                        break;
                    case ProjectStandardKeys.FloorTypes:
                        CollectElements<FloorType>(sourceDoc, results,
                            e => DescribeCompoundStructure(((FloorType)e).GetCompoundStructure()));
                        break;
                    case ProjectStandardKeys.CeilingTypes:
                        CollectCeilingTypes(sourceDoc, results);
                        break;
                    case ProjectStandardKeys.RoofTypes:
                        CollectElements<RoofType>(sourceDoc, results,
                            e => DescribeCompoundStructure(((RoofType)e).GetCompoundStructure()));
                        break;
                    case ProjectStandardKeys.FillPatterns:
                        CollectElements<FillPatternElement>(sourceDoc, results,
                            e =>
                            {
                                var fp = ((FillPatternElement)e).GetFillPattern();
                                return fp.IsSolidFill ? "Relleno s\u00f3lido"
                                     : fp.Target == FillPatternTarget.Model ? "Modelo" : "Dise\u00f1o";
                            });
                        break;
                }
            }
            catch (Exception ex) { logger?.Warning($"[Transferir] GetStandardItems {categoryKey} '{docTitle}': {ex.Message}"); }
            return results;
        }

        private static bool IsSystemElement(Element el)
            => el.Name.StartsWith("<") || el.Name.EndsWith(">");

        /// <summary>
        /// Collects only user-created dimension types, excluding system dimension types
        /// (Angular, ArcLength, Radial, Diameter, SpotElevation, SpotCoordinate, SpotSlope)
        /// that would corrupt Revit's dimension tool if transferred.
        /// </summary>
        private static readonly Dictionary<DimensionStyleType, string> _dimStyleLabels = new()
        {
            { DimensionStyleType.Linear,         "Lineal" },
            { DimensionStyleType.LinearFixed,     "Lineal fija" },
            { DimensionStyleType.Angular,         "Angular" },
            { DimensionStyleType.Radial,          "Radial" },
            { DimensionStyleType.ArcLength,       "Longitud de arco" },
        };

        /// <summary>
        /// Known DimensionStyleType values for standard dimensions (not spot dimensions).
        /// Types with StyleType outside this set (AlignmentStationLabel, SpotElevation, etc.) are excluded.
        /// </summary>
        private static readonly HashSet<DimensionStyleType> _standardDimStyles = new()
        {
            DimensionStyleType.Linear,
            DimensionStyleType.LinearFixed,
            DimensionStyleType.Angular,
            DimensionStyleType.Radial,
            DimensionStyleType.ArcLength,
        };

        private static void CollectDimensionTypes(Document doc, List<ProjectStandardItem> results)
        {
            foreach (var dt in new FilteredElementCollector(doc).OfClass(typeof(DimensionType)).Cast<DimensionType>())
            {
                if (IsSystemElement(dt)) continue;

                // Skip spot dimension types (collected separately in CollectSpotDimensionTypes)
                if (dt is SpotDimensionType) continue;

                // Only include known standard dimension style types
                try { if (!_standardDimStyles.Contains(dt.StyleType)) continue; }
                catch { continue; }

                // Skip system family default types (name == family name, e.g. "Estilo de cota lineal")
                try { if (dt.FamilyName == dt.Name) continue; }
                catch { }

                string detail = _dimStyleLabels.TryGetValue(dt.StyleType, out var label) ? label : "";
                results.Add(new ProjectStandardItem { Id = RevitId(dt.Id), Name = dt.Name, Detail = detail });
            }
            results.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }

        private static void CollectSpotDimensionTypes(Document doc, List<ProjectStandardItem> results)
        {
            // SpotDimensionType may inherit from DimensionType, so collect from DimensionType and filter
            foreach (var dt in new FilteredElementCollector(doc).OfClass(typeof(DimensionType)).Cast<DimensionType>())
            {
                if (IsSystemElement(dt)) continue;
                if (!(dt is SpotDimensionType)) continue;

                // Skip system family default types
                try { if (dt.FamilyName == dt.Name) continue; }
                catch { }

                results.Add(new ProjectStandardItem { Id = RevitId(dt.Id), Name = dt.Name });
            }

            // Also try direct collector (works in some Revit versions)
            try
            {
                var existingIds = new HashSet<long>(results.Select(r => r.Id));
                foreach (var st in new FilteredElementCollector(doc).OfClass(typeof(SpotDimensionType)).Cast<SpotDimensionType>())
                {
                    if (IsSystemElement(st)) continue;
                    var id = RevitId(st.Id);
                    if (existingIds.Contains(id)) continue;
                    try { if (st.FamilyName == st.Name) continue; }
                    catch { }
                    results.Add(new ProjectStandardItem { Id = id, Name = st.Name });
                }
            }
            catch { /* SpotDimensionType collector not supported in this version */ }

            results.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }

        private static void CollectElements<T>(Document doc, List<ProjectStandardItem> results,
            Func<Element, string>? detailSelector = null) where T : Element
        {
            foreach (var el in new FilteredElementCollector(doc).OfClass(typeof(T)).Cast<T>())
            {
                if (IsSystemElement(el)) continue;
                string detail = "";
                if (detailSelector != null) { try { detail = detailSelector(el); } catch { } }
                results.Add(new ProjectStandardItem { Id = RevitId(el.Id), Name = el.Name, Detail = detail });
            }
            results.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }

        private static void CollectLineStyles(Document doc, List<ProjectStandardItem> results)
        {
            var linesCategory = Category.GetCategory(doc, BuiltInCategory.OST_Lines);
            if (linesCategory == null) return;

            foreach (var gs in new FilteredElementCollector(doc)
                .OfClass(typeof(GraphicsStyle)).Cast<GraphicsStyle>()
                .Where(gs => gs.GraphicsStyleType == GraphicsStyleType.Projection
                          && gs.GraphicsStyleCategory?.Parent?.Id == linesCategory.Id
                          && !IsSystemElement(gs))
                .OrderBy(gs => gs.Name))
            {
                results.Add(new ProjectStandardItem
                {
                    Id     = RevitId(gs.Id),
                    Name   = gs.Name,
                    Detail = ""
                });
            }
        }

        private static void CollectCeilingTypes(Document doc, List<ProjectStandardItem> results)
        {
            try
            {
                var ceilingTypeClass = typeof(RailingType).Assembly.GetType("Autodesk.Revit.DB.Architecture.CeilingType");
                if (ceilingTypeClass != null)
                {
                    foreach (var el in new FilteredElementCollector(doc).OfClass(ceilingTypeClass).Cast<Element>().OrderBy(e => e.Name))
                        results.Add(new ProjectStandardItem { Id = RevitId(el.Id), Name = el.Name });
                }
            }
            catch { }
        }

        private static string DescribeCompoundStructure(CompoundStructure? cs)
        {
            if (cs == null) return "";
            try { int n = cs.LayerCount; return n == 1 ? "1 capa" : $"{n} capas"; }
            catch { return ""; }
        }

        private static void DeleteMatchingElements(
            Document targetDoc, HashSet<string> sourceNames,
            IReadOnlyList<ElementId> sourceIds, Document sourceDoc,
            ref ProjectStandardTransferResult result)
        {
            var typesToSearch = sourceIds
                .Select(id => sourceDoc.GetElement(id)?.GetType())
                .Where(t => t != null).Distinct().ToList();

            var allToDelete = new List<ElementId>();
            foreach (var type in typesToSearch)
            {
                try
                {
                    var toDelete = new FilteredElementCollector(targetDoc)
                        .OfClass(type!).Where(e => sourceNames.Contains(e.Name))
                        .Select(e => e.Id).ToList();
                    allToDelete.AddRange(toDelete);
                }
                catch { }
            }
            if (allToDelete.Count > 0)
            {
                try { targetDoc.Delete(allToDelete); result.Conflicts += allToDelete.Count; }
                catch { }
            }
        }
    }
}
