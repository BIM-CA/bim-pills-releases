using Autodesk.Revit.DB;
using BIMPills.Core.Audit;
using BIMPills.Core.Documentacion;
using BIMPills.Core.Gestion;
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

            var results = new List<WorksetInfo>();
            foreach (var ws in worksets)
            {
                // Count elements in this workset
                int elementCount = 0;
                try
                {
                    elementCount = new FilteredElementCollector(_doc)
                        .WhereElementIsNotElementType()
                        .WherePasses(new ElementWorksetFilter(ws.Id))
                        .GetElementCount();
                }
                catch { /* Some filters may fail */ }

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
                var activeView = _doc.ActiveView;
                if (activeView == null) return 0;

                return new FilteredElementCollector(_doc, activeView.Id)
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
