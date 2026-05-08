using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace BIMPills.Revit.Commands.ParameterExtractor
{
    /// <summary>
    /// Resolves which categories, families and types are present in the model
    /// (or a specific element set) and which of them have curve-based location.
    ///
    /// Optimised: does NOT read element.Parameters (that list is no longer shown in
    /// the UI), and caches type-level lookups so family/type name is resolved only
    /// once per unique ElementType, not once per instance.
    /// </summary>
    internal static class ExtractorCategoryResolver
    {
        public static (
            IReadOnlyList<string> Categories,
            IReadOnlyDictionary<string, IReadOnlyList<string>> ParametersByCategory,
            IReadOnlyDictionary<string, bool> HasCurveByCategory,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> FamilyTypesByCategory)
            ResolveFromModel(Document doc)
        {
            // ToElements() is a single batch call — much faster than ToElementIds()
            // followed by N individual doc.GetElement(id) calls inside the loop.
            var elements = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .ToElements();

            return Resolve(doc, elements);
        }

        // ── Core resolver ────────────────────────────────────────────────────

        private static (
            IReadOnlyList<string> Categories,
            IReadOnlyDictionary<string, IReadOnlyList<string>> ParametersByCategory,
            IReadOnlyDictionary<string, bool> HasCurveByCategory,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> FamilyTypesByCategory)
            Resolve(Document doc, IList<Element> elements)
        {
            var categories = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var hasCurve   = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            // cat → family → types
            var famTypes   = new Dictionary<string,
                                 Dictionary<string, SortedSet<string>>>(
                                 StringComparer.OrdinalIgnoreCase);

            // Cache type-level info so we only call doc.GetElement(typeId) once
            // per unique ElementType — not once per instance.
            var typeCache  = new Dictionary<ElementId, (string fam, string type)>();

            foreach (var elem in elements)
            {
                if (elem == null) continue;

                // Only elements that have spatial coordinates
                bool isCurve = elem.Location is LocationCurve;
                if (!isCurve && !(elem.Location is LocationPoint)) continue;

                var catName = elem.Category?.Name;
                if (string.IsNullOrWhiteSpace(catName)) continue;

                categories.Add(catName!);

                // Curve flag — once set to true for a category it never goes back
                if (!hasCurve.TryGetValue(catName!, out var already) || !already)
                    hasCurve[catName!] = isCurve;

                // Family / Type — cached per ElementType
                var typeId = elem.GetTypeId();
                if (typeId == ElementId.InvalidElementId) continue;

                if (!typeCache.TryGetValue(typeId, out var cached))
                {
                    cached = (GetFamilyName(doc, elem), GetTypeName(doc, elem));
                    typeCache[typeId] = cached;
                }

                if (string.IsNullOrWhiteSpace(cached.fam) &&
                    string.IsNullOrWhiteSpace(cached.type)) continue;

                if (!famTypes.TryGetValue(catName!, out var famDict))
                    famTypes[catName!] = famDict =
                        new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);

                if (!famDict.TryGetValue(cached.fam, out var typeSet))
                    famDict[cached.fam] = typeSet =
                        new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

                if (!string.IsNullOrWhiteSpace(cached.type))
                    typeSet.Add(cached.type);
            }

            // Build read-only results
            // paramsByCategory is intentionally empty — the parameter picker was
            // removed from the UI; computing it would require reading elem.Parameters
            // for every element (extremely slow on large models).
            var emptyParams = (IReadOnlyDictionary<string, IReadOnlyList<string>>)
                new Dictionary<string, IReadOnlyList<string>>();

            var famTypesResult = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var kv in famTypes)
            {
                var inner = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var fk in kv.Value)
                    inner[fk.Key] = fk.Value.Count > 0
                        ? (IReadOnlyList<string>)new List<string>(fk.Value)
                        : Array.Empty<string>();
                famTypesResult[kv.Key] = inner;
            }

            return (new List<string>(categories),
                    emptyParams,
                    hasCurve,
                    famTypesResult);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string GetFamilyName(Document doc, Element elem)
        {
            if (elem is FamilyInstance fi) return fi.Symbol?.Family?.Name ?? string.Empty;
            var typeId = elem.GetTypeId();
            if (typeId == ElementId.InvalidElementId) return string.Empty;
            return (doc.GetElement(typeId) as ElementType)?.FamilyName ?? string.Empty;
        }

        private static string GetTypeName(Document doc, Element elem)
        {
            var typeId = elem.GetTypeId();
            if (typeId == ElementId.InvalidElementId) return elem.Name;
            return doc.GetElement(typeId)?.Name ?? elem.Name;
        }
    }
}
