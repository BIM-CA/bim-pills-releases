using Autodesk.Revit.DB;
using BIMPills.Core.Audit;
using BIMPills.Core.Services;
using System.Collections.Generic;
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
                var typeIds  = family.GetFamilySymbolIds();
                var instCount = new FilteredElementCollector(_doc)
                    .WherePasses(new FamilyInstanceFilter(_doc, typeIds.FirstOrDefault()))
                    .GetElementCount();

                // Approximate size via document size heuristic (no direct API exists)
                results.Add(new FamilyInfo(family.Name, category, instCount, 0));
            }

            return results.OrderByDescending(f => f.InstanceCount).ToList();
        }

        public IReadOnlyList<ViewInfo> GetUnplacedViews()
        {
            // Collect all sheet-placed viewports
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
