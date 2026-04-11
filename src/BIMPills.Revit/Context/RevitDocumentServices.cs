using Autodesk.Revit.DB;
using BIMPills.Core.Audit;
using BIMPills.Core.Documentacion;
using BIMPills.Core.Gestion;
using BIMPills.Core.Models;
using BIMPills.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BIMPills.Revit.Context
{
    /// <summary>
    /// Implements IDocumentServices using the real Revit Document.
    /// This is the only place in the Commands layer that touches RevitAPI types.
    /// </summary>
    internal sealed class RevitDocumentServices : IDocumentServices
    {
        private readonly Document _doc;

        public RevitDocumentServices(Document doc)
        {
            _doc = doc;
        }

        public string Title => _doc.Title;
        public bool IsWorkshared => _doc.IsWorkshared;

        public long GetModelFileSize()
        {
            try
            {
                var path = _doc.PathName;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return new FileInfo(path).Length;
            }
            catch { /* Modelo no guardado o sin acceso */ }
            return 0;
        }

        public int GetTotalElementCount()
        {
            return new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType()
                .GetElementCount();
        }

        public IReadOnlyList<ModelWarningInfo> GetWarnings()
        {
            return _doc.GetWarnings()
                .Select(w => new ModelWarningInfo(
                    description:  w.GetDescriptionText(),
                    severity:     w.GetSeverity().ToString(),
                    elementCount: w.GetFailingElements().Count()))
                .ToList();
        }

        public IReadOnlyList<FamilyInfo> GetFamilySizes()
        {
            var families = new FilteredElementCollector(_doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .ToList();

            var results = new List<FamilyInfo>(families.Count);
            foreach (var family in families)
            {
                var category = family.FamilyCategory?.Name ?? "Sin categoría";

                // Count instances of all types in this family
                var typeIds = family.GetFamilySymbolIds();
                int instCount = 0;
                if (typeIds.Count > 0)
                {
                    instCount = new FilteredElementCollector(_doc)
                        .WherePasses(new FamilyInstanceFilter(_doc, typeIds.First()))
                        .GetElementCount();
                }

                // Estimate family size from its editable document if possible
                long sizeBytes = EstimateFamilySize(family);

                results.Add(new FamilyInfo(family.Name, category, instCount, sizeBytes));
            }

            return results.OrderByDescending(f => f.SizeBytes).ThenByDescending(f => f.InstanceCount).ToList();
        }

        public IReadOnlyList<ViewInfo> GetUnplacedViews()
        {
            var placedViewIds = new FilteredElementCollector(_doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .Select(vp => vp.ViewId)
                .ToHashSet();

            return new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && !placedViewIds.Contains(v.Id))
                .Select(v => new ViewInfo(v.Name, v.ViewType.ToString(), isOnSheet: false))
                .ToList();
        }

        public IReadOnlyList<ElementInfo> GetElementsWithoutCategory()
        {
            return new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType()
                .Where(e => e.Category == null)
                .Select(e => new ElementInfo(GetElementIdValue(e.Id), e.Name ?? "(sin nombre)", null))
                .ToList();
        }

        public IReadOnlyList<PurgeableItem> GetPurgeableElements()
        {
            var purgeable = new List<PurgeableItem>();

            // Familias sin instancias
            var families = new FilteredElementCollector(_doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .ToList();

            foreach (var family in families)
            {
                var typeIds = family.GetFamilySymbolIds();
                bool hasInstances = false;

                foreach (var typeId in typeIds)
                {
                    var count = new FilteredElementCollector(_doc)
                        .WherePasses(new FamilyInstanceFilter(_doc, typeId))
                        .GetElementCount();
                    if (count > 0) { hasInstances = true; break; }
                }

                if (!hasInstances)
                {
                    var category = family.FamilyCategory?.Name ?? "Sin categoría";
                    var size = EstimateFamilySize(family);
                    purgeable.Add(new PurgeableItem(
                        GetElementIdValue(family.Id),
                        family.Name,
                        category,
                        "Familia",
                        size));
                }
            }

            // Vistas no colocadas en planos (excluyendo plantillas y vistas del sistema)
            var placedViewIds = new FilteredElementCollector(_doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .Select(vp => vp.ViewId)
                .ToHashSet();

            var views = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate
                    && !placedViewIds.Contains(v.Id)
                    && v.ViewType != ViewType.ProjectBrowser
                    && v.ViewType != ViewType.SystemBrowser
                    && v.ViewType != ViewType.Internal
                    && v.ViewType != ViewType.DrawingSheet)
                .ToList();

            foreach (var view in views)
            {
                purgeable.Add(new PurgeableItem(
                    GetElementIdValue(view.Id),
                    view.Name,
                    view.ViewType.ToString(),
                    "Vista",
                    0));
            }

            return purgeable;
        }

        public int PurgeElements(IReadOnlyList<long> elementIds)
        {
            if (elementIds == null || elementIds.Count == 0) return 0;

            int deleted = 0;
            using (var trans = new Transaction(_doc, "BIMPills - Purgar elementos"))
            {
                trans.Start();
                foreach (var id in elementIds)
                {
                    try
                    {
                        var elementId = new ElementId(id);
                        var elem = _doc.GetElement(elementId);
                        if (elem == null) continue;

                        // Revit 2025+ bug: pinned elements throw on Delete
                        if (elem.Pinned)
                            elem.Pinned = false;

                        _doc.Delete(elementId);
                        deleted++;
                    }
                    catch { /* Elemento no se pudo eliminar — continuar con el siguiente */ }
                }
                trans.Commit();
            }
            return deleted;
        }

        public IReadOnlyList<FamilyExportInfo> GetLoadedFamilies()
        {
            var families = new FilteredElementCollector(_doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Where(f => !f.IsInPlace) // Skip in-place families (can't export)
                .Select(f => new FamilyExportInfo(
                    (long)GetElementIdValue(f.Id),
                    f.Name,
                    f.FamilyCategory?.Name ?? "Sin categoría"))
                .OrderBy(f => f.Category)
                .ThenBy(f => f.Name)
                .ToList();
            return families;
        }

        public bool ExportFamily(long familyId, string destinationPath)
        {
            try
            {
                var elementId = new ElementId(familyId);
                var family = _doc.GetElement(elementId) as Family;
                if (family == null) return false;

                var familyDoc = _doc.EditFamily(family);
                if (familyDoc == null) return false;

                try
                {
                    // Ensure directory exists
                    var dir = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);

                    var saveOptions = new SaveAsOptions { OverwriteExistingFile = true };
                    familyDoc.SaveAs(destinationPath, saveOptions);
                    return true;
                }
                finally
                {
                    familyDoc.Close(false); // Close without saving changes to original
                }
            }
            catch
            {
                return false;
            }
        }

        public IReadOnlyList<WorksetInfo> GetWorksets()
        {
            if (!_doc.IsWorkshared)
                return new List<WorksetInfo>();

            var worksets = new FilteredWorksetCollector(_doc)
                .OfKind(WorksetKind.UserWorkset)
                .ToList();

            var defaultWorksetId = _doc.GetWorksetTable().GetActiveWorksetId();

            // Single pass: iterate all non-type elements and group by workset
            // Avoids running one FilteredElementCollector per workset (N full scans → 1 scan)
            var countsByWorkset = new Dictionary<int, int>();
            try
            {
                foreach (Element el in new FilteredElementCollector(_doc).WhereElementIsNotElementType())
                {
                    var wsId = el.WorksetId.IntegerValue;
                    countsByWorkset[wsId] = countsByWorkset.TryGetValue(wsId, out var c) ? c + 1 : 1;
                }
            }
            catch { /* Non-critical */ }

            var results = new List<WorksetInfo>();
            foreach (var ws in worksets)
            {
                countsByWorkset.TryGetValue(ws.Id.IntegerValue, out var elementCount);
                results.Add(new WorksetInfo
                {
                    Id = ws.Id.IntegerValue,
                    Name = ws.Name,
                    IsOpen = ws.IsOpen,
                    IsDefault = ws.Id == defaultWorksetId,
                    IsEditable = ws.IsEditable,
                    Owner = ws.Owner ?? "",
                    ElementCount = elementCount
                });
            }

            return results;
        }

        public bool CreateWorkset(string name)
        {
            if (!_doc.IsWorkshared || string.IsNullOrWhiteSpace(name))
                return false;

            try
            {
                using (var tx = new Transaction(_doc, "BIMPills: Crear subproyecto"))
                {
                    tx.Start();
                    Workset.Create(_doc, name);
                    tx.Commit();
                    return true;
                }
            }
            catch { return false; }
        }

        public bool RenameWorkset(long worksetId, string newName)
        {
            if (!_doc.IsWorkshared || string.IsNullOrWhiteSpace(newName))
                return false;

            try
            {
                using (var tx = new Transaction(_doc, "BIMPills: Renombrar subproyecto"))
                {
                    tx.Start();
                    var wsId = new WorksetId((int)worksetId);
                    WorksetTable.RenameWorkset(_doc, wsId, newName);
                    tx.Commit();
                    return true;
                }
            }
            catch { return false; }
        }

        // ── Documentación: Acotado de Vanos ──

        public IReadOnlyList<DimensionTypeInfo> GetDimensionTypes()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(DimensionType))
                .Cast<DimensionType>()
                .Where(dt => dt.StyleType == DimensionStyleType.Linear)
                .Select(dt => new DimensionTypeInfo(GetElementIdValue(dt.Id), dt.Name))
                .OrderBy(dt => dt.Name)
                .ToList();
        }

        public int GetDoorCountInActiveView()
        {
            try
            {
                return new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .WhereElementIsNotElementType()
                    .GetElementCount();
            }
            catch { return 0; }
        }

        public string GetActiveViewName()
        {
            try { return _doc.ActiveView?.Name ?? "Vista desconocida"; }
            catch { return "Vista desconocida"; }
        }

        public int GetGridCountInActiveView()
        {
            try
            {
                return new FilteredElementCollector(_doc)
                    .OfClass(typeof(Grid))
                    .GetElementCount();
            }
            catch { return 0; }
        }

        public int GetWallCountInActiveView()
        {
            try
            {
                return new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_Walls)
                    .WhereElementIsNotElementType()
                    .GetElementCount();
            }
            catch { return 0; }
        }

        public int GetArqLevelCount()
        {
            try
            {
                return new FilteredElementCollector(_doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .Count(lv =>
                    {
                        var lvType = _doc.GetElement(lv.GetTypeId());
                        return lvType != null && lvType.Name.IndexOf("ARQ", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    });
            }
            catch { return 0; }
        }

        // ── Exportar Planos ──

        public IReadOnlyList<SheetExportInfo> GetSheets()
        {
            try
            {
                return new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Where(s => !s.IsPlaceholder)
                    .Select(s => new SheetExportInfo(
                        GetElementIdValue(s.Id),
                        s.SheetNumber,
                        s.Name,
                        s.get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION)?.AsString() ?? "",
                        s.LookupParameter("Discipline")?.AsString() ?? "",
                        GetAllParameterValues(s)))
                    .OrderBy(s => s.SheetNumber)
                    .ToList();
            }
            catch { return new List<SheetExportInfo>(); }
        }

        public IReadOnlyList<ExportableViewInfo> GetExportableViews()
        {
            try
            {
                var systemViewTypes = new HashSet<ViewType>
                {
                    ViewType.ProjectBrowser,
                    ViewType.SystemBrowser,
                    ViewType.Internal,
                    ViewType.Undefined
                };

                // Sheets
                var sheets = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Where(s => !s.IsPlaceholder)
                    .Select(s => new ExportableViewInfo(
                        GetElementIdValue(s.Id),
                        s.UniqueId,
                        s.Name,
                        ExportableItemType.Sheet,
                        s.SheetNumber,
                        s.get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION)?.AsString() ?? "",
                        s.LookupParameter("Discipline")?.AsString() ?? "",
                        GetAllParameterValues(s)))
                    .OrderBy(s => s.SheetNumber)
                    .ToList();

                // Individual views (not templates, not sheets, not system views)
                var views = new FilteredElementCollector(_doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => !v.IsTemplate
                        && !systemViewTypes.Contains(v.ViewType)
                        && v.ViewType != ViewType.DrawingSheet)
                    .Select(v => new ExportableViewInfo(
                        GetElementIdValue(v.Id),
                        v.UniqueId,
                        v.Name,
                        MapViewType(v.ViewType),
                        "",
                        "",
                        "",
                        GetAllParameterValues(v)))
                    .OrderBy(v => v.Name)
                    .ToList();

                var result = new List<ExportableViewInfo>(sheets.Count + views.Count);
                result.AddRange(sheets);
                result.AddRange(views);
                return result;
            }
            catch { return new List<ExportableViewInfo>(); }
        }

        /// <summary>Returns all non-template 3D views — used as NWC export scope targets.</summary>
        public IReadOnlyList<NwcViewInfo> GetNwcViews()
        {
            try
            {
                return new FilteredElementCollector(_doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => !v.IsTemplate && v.ViewType == ViewType.ThreeD)
                    .Select(v => new NwcViewInfo
                    {
                        ElementId = GetElementIdValue(v.Id),
                        Name      = v.Name
                    })
                    .OrderBy(v => v.Name)
                    .ToList();
            }
            catch { return new List<NwcViewInfo>(); }
        }

        private static ExportableItemType MapViewType(ViewType vt) => vt switch
        {
            ViewType.FloorPlan    => ExportableItemType.FloorPlan,
            ViewType.CeilingPlan  => ExportableItemType.CeilingPlan,
            ViewType.Elevation    => ExportableItemType.Elevation,
            ViewType.Section      => ExportableItemType.Section,
            ViewType.ThreeD       => ExportableItemType.ThreeDView,
            ViewType.Legend       => ExportableItemType.Legend,
            ViewType.DraftingView => ExportableItemType.DraftingView,
            ViewType.AreaPlan     => ExportableItemType.AreaPlan,
            _                     => ExportableItemType.Other
        };

        public IReadOnlyList<string> GetSheetParameterNames()
        {
            try
            {
                var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

                // Collect from first sheet
                var firstSheet = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSheet))
                    .FirstElement();
                if (firstSheet != null)
                    CollectParameterNames(firstSheet, names);

                // Collect from first non-template view (may have different project parameters)
                var firstView = new FilteredElementCollector(_doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .FirstOrDefault(v => !v.IsTemplate && !(v is ViewSheet)
                        && v.ViewType != ViewType.ProjectBrowser
                        && v.ViewType != ViewType.SystemBrowser
                        && v.ViewType != ViewType.Internal);
                if (firstView != null)
                    CollectParameterNames(firstView, names);

                return names.ToList();
            }
            catch { return new List<string>(); }
        }

        private static void CollectParameterNames(Element element, SortedSet<string> names)
        {
            foreach (Parameter p in element.Parameters)
            {
                if (p.Definition?.Name == null || p.StorageType == StorageType.None) continue;
                if (p.IsReadOnly && p.StorageType == StorageType.ElementId) continue;
                names.Add(p.Definition.Name);
            }
        }

        private static Dictionary<string, string> GetAllParameterValues(Element element)
        {
            var values = new Dictionary<string, string>();
            try
            {
                foreach (Parameter p in element.Parameters)
                {
                    if (p.Definition?.Name == null || !p.HasValue) continue;
                    string val;
                    switch (p.StorageType)
                    {
                        case StorageType.String:
                            val = p.AsString() ?? "";
                            break;
                        case StorageType.Integer:
                            val = p.AsInteger().ToString();
                            break;
                        case StorageType.Double:
                            val = p.AsValueString() ?? p.AsDouble().ToString("F2");
                            break;
                        case StorageType.ElementId:
                            val = p.AsValueString() ?? "";
                            break;
                        default:
                            continue;
                    }
                    if (!string.IsNullOrEmpty(val))
                        values[p.Definition.Name] = val;
                }
            }
            catch { }
            return values;
        }

        public string GetProjectName()
        {
            try
            {
                return _doc.ProjectInformation?.Name ?? _doc.Title;
            }
            catch { return _doc.Title; }
        }

        // ── Gestionar: SheetLink ──

        public IReadOnlyList<ScheduleInfo> GetSchedules()
        {
            try
            {
                return new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSchedule))
                    .Cast<ViewSchedule>()
                    .Where(vs => !vs.IsTemplate && !vs.IsTitleblockRevisionSchedule)
                    .Select(vs =>
                    {
                        int rowCount = 0;
                        try
                        {
                            rowCount = vs.GetTableData()
                                         .GetSectionData(SectionType.Body)
                                         .NumberOfRows;
                        }
                        catch { }

                        string categoryName = "";
                        try
                        {
                            if (vs.Definition.CategoryId != ElementId.InvalidElementId)
                                categoryName = Category.GetCategory(_doc, vs.Definition.CategoryId)?.Name ?? "";
                        }
                        catch { }

                        return new ScheduleInfo
                        {
                            Id           = GetElementIdValue(vs.Id),
                            Name         = vs.Name,
                            CategoryName = categoryName,
                            RowCount     = rowCount,
                            ColumnCount  = vs.Definition.GetFieldCount()
                        };
                    })
                    .OrderBy(s => s.Name)
                    .ToList();
            }
            catch { return new List<ScheduleInfo>(); }
        }

        public ScheduleData GetScheduleData(long scheduleId)
        {
            try
            {
                var vs = _doc.GetElement(new ElementId((int)scheduleId)) as ViewSchedule;
                if (vs == null) return new ScheduleData();

                var definition = vs.Definition;
                var fieldCount = definition.GetFieldCount();

                // Build visible fields list for column metadata
                var fields = new List<ScheduleField>();
                for (int i = 0; i < fieldCount; i++)
                {
                    var f = definition.GetField(i);
                    if (!f.IsHidden) fields.Add(f);
                }

                var columns = fields.Select(f => new ScheduleColumnInfo
                {
                    Name          = f.ColumnHeading,
                    ParameterName = f.GetName(),
                    IsReadOnly    = f.IsCalculatedField || f.ParameterId == ElementId.InvalidElementId,
                    StorageType   = "String"
                }).ToList();

                // Get only the actual elements in this schedule (excludes totals,
                // group headers, subtotals, and grand totals entirely).
                var elements = new List<Element>();
                try
                {
                    elements = new FilteredElementCollector(_doc, vs.Id)
                        .WhereElementIsNotElementType()
                        .ToElements()
                        .ToList();
                }
                catch { }

                // Read parameter values directly from each element — this guarantees
                // only real data rows, with no totals or group headers.
                var rows       = new List<List<string>>();
                var elementIds = new List<long>();

                foreach (var element in elements)
                {
                    var cellValues = new List<string>();
                    foreach (var field in fields)
                    {
                        string val = "";
                        try
                        {
                            if (!field.IsCalculatedField && field.ParameterId != ElementId.InvalidElementId)
                            {
                                // Look up by matching ElementId in the element's parameter collection
                                var param = element.Parameters
                                    .Cast<Parameter>()
                                    .FirstOrDefault(p => p.Id == field.ParameterId);
                                if (param != null)
                                {
                                    val = param.StorageType switch
                                    {
                                        StorageType.String    => param.AsString() ?? "",
                                        StorageType.Integer   => param.AsInteger().ToString(),
                                        StorageType.Double    => param.AsValueString() ?? "",
                                        StorageType.ElementId => param.AsValueString() ?? "",
                                        _                     => ""
                                    };
                                }
                            }
                        }
                        catch { }
                        cellValues.Add(val);
                    }
                    rows.Add(cellValues);
                    elementIds.Add((long)GetElementIdValue(element.Id));
                }

                var schedules = GetSchedules();
                var info = schedules.FirstOrDefault(s => s.Id == scheduleId)
                           ?? new ScheduleInfo { Id = scheduleId, Name = vs.Name };

                return new ScheduleData
                {
                    Schedule   = info,
                    Columns    = columns,
                    ElementIds = elementIds,
                    Rows       = rows
                };
            }
            catch { return new ScheduleData(); }
        }

        public ParameterUpdateResult ApplyParameterUpdates(IReadOnlyList<ParameterUpdateRequest> updates)
        {
            int updated = 0, skipped = 0;
            var errors = new List<string>();

            try
            {
                using var tx = new Transaction(_doc, "BIMPills: Importar parámetros");
                tx.Start();
                foreach (var req in updates)
                {
                    try
                    {
                        var element = _doc.GetElement(new ElementId((int)req.ElementId));
                        var param   = element?.LookupParameter(req.ParameterName);
                        if (param == null || param.IsReadOnly) { skipped++; continue; }

                        switch (param.StorageType)
                        {
                            case StorageType.String:
                                param.Set(req.NewValue ?? "");
                                break;
                            case StorageType.Integer:
                                if (int.TryParse(req.NewValue, out int iv)) param.Set(iv);
                                else skipped++;
                                break;
                            case StorageType.Double:
                                if (double.TryParse(req.NewValue, out double dv)) param.Set(dv);
                                else skipped++;
                                break;
                            default:
                                skipped++;
                                continue;
                        }
                        updated++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"ID {req.ElementId} / {req.ParameterName}: {ex.Message}");
                        skipped++;
                    }
                }
                if (updated > 0) tx.Commit(); else tx.RollBack();
            }
            catch (Exception ex)
            {
                errors.Add($"Error de transacción: {ex.Message}");
            }

            return new ParameterUpdateResult { Updated = updated, Skipped = skipped, Errors = errors };
        }

        private long EstimateFamilySize(Family family)
        {
            // Revit no expone directamente el tamaño de una familia cargada.
            // Usamos la cantidad de tipos como heurística aproximada.
            // En futuras versiones se puede exportar temporalmente para medir.
            var typeCount = family.GetFamilySymbolIds().Count;
            return typeCount * 50_000L; // ~50KB por tipo como estimación conservadora
        }

        private static int GetElementIdValue(ElementId id)
        {
#if REVIT2024
            return id.IntegerValue;
#else
            return (int)id.Value;
#endif
        }
    }
}
