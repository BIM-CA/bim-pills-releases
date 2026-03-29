using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using BIMPills.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMPills.Revit.Dimensioning
{
    /// <summary>
    /// Result returned by <see cref="SchemeExecutor.Execute"/>.
    /// </summary>
    public class SchemeExecutionResult
    {
        public int DimensionsCreated { get; set; }
        public int ElementsProcessed { get; set; }
        public int ElementsSkipped { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public string Summary { get; set; } = string.Empty;

        public bool HasErrors => Errors.Count > 0;
    }

    /// <summary>
    /// Executes a <see cref="CustomDimensionScheme"/> against a Revit model,
    /// creating dimensions in the specified view. All Revit modifications run
    /// inside a single Transaction.
    /// </summary>
    internal sealed class SchemeExecutor
    {
        private readonly Document _doc;
        private readonly View _view;
        private readonly DimensionType? _dimType;

        /// <summary>Offset from element to dimension line, in feet.</summary>
        private const double DefaultOffsetFeet = 100.0 / 304.8; // 100 mm

        public SchemeExecutor(Document doc, View view, DimensionType? dimType = null)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _dimType = dimType;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Executes the given scheme: collects elements, filters them,
        /// resolves references per rule, and creates Revit dimensions.
        /// </summary>
        public SchemeExecutionResult Execute(CustomDimensionScheme scheme)
        {
            if (scheme == null) throw new ArgumentNullException(nameof(scheme));

            var result = new SchemeExecutionResult();
            var activeRules = scheme.Rules.Where(r => r.IsActive).OrderBy(r => r.DisplayOrder).ToList();

            if (activeRules.Count == 0)
            {
                result.Summary = "No active rules in the scheme.";
                return result;
            }

            // Collect elements for all target categories
            var elements = CollectElements(scheme);
            result.ElementsProcessed = elements.Count;

            if (elements.Count == 0)
            {
                result.Summary = "No elements found matching the scheme criteria.";
                return result;
            }

            using (var tx = new Transaction(_doc, $"BIMPills: {scheme.Name}"))
            {
                try
                {
                    tx.Start();

                    foreach (var element in elements)
                    {
                        foreach (var rule in activeRules)
                        {
                            try
                            {
                                var dim = CreateDimensionForElement(element, rule);
                                if (dim != null)
                                    result.DimensionsCreated++;
                                else
                                    result.ElementsSkipped++;
                            }
                            catch (Exception ex)
                            {
                                result.ElementsSkipped++;
                                result.Errors.Add(
                                    $"Element {element.Id.Value} / Rule '{rule.Id}': {ex.Message}");
                            }
                        }
                    }

                    if (result.DimensionsCreated > 0)
                        tx.Commit();
                    else
                        tx.RollBack();
                }
                catch (Exception ex)
                {
                    if (tx.GetStatus() == TransactionStatus.Started)
                        tx.RollBack();
                    result.Errors.Add($"Transaction failed: {ex.Message}");
                }
            }

            result.Summary = BuildSummary(result, scheme.Name);
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Element collection & filtering
        // ─────────────────────────────────────────────────────────────────────

        private List<Element> CollectElements(CustomDimensionScheme scheme)
        {
            var allElements = new List<Element>();

            foreach (var elementType in scheme.ElementTypes)
            {
                var bic = MapToBuiltInCategory(elementType);
                if (!bic.HasValue) continue;

                IEnumerable<Element> collected;

                // Grids are document-level; others are view-scoped
                if (elementType == DimensionSchemeElementType.Grids)
                {
                    collected = new FilteredElementCollector(_doc)
                        .OfClass(typeof(Grid))
                        .WhereElementIsNotElementType()
                        .Cast<Element>();
                }
                else if (elementType == DimensionSchemeElementType.Rooms)
                {
                    collected = new FilteredElementCollector(_doc, _view.Id)
                        .OfCategory(bic.Value)
                        .Cast<Room>()
                        .Where(r => r.Area > 0)
                        .Cast<Element>();
                }
                else
                {
                    collected = new FilteredElementCollector(_doc, _view.Id)
                        .OfCategory(bic.Value)
                        .WhereElementIsNotElementType()
                        .Cast<Element>();
                }

                allElements.AddRange(collected);
            }

            // Apply element filter
            if (scheme.ElementFilter != null &&
                scheme.ElementFilter.FilterType != ElementFilterType.All)
            {
                allElements = ApplyElementFilter(allElements, scheme.ElementFilter);
            }

            return allElements;
        }

        private List<Element> ApplyElementFilter(List<Element> elements, Core.Models.ElementFilter filter)
        {
            if (string.IsNullOrWhiteSpace(filter.Value))
                return elements;

            return elements.Where(e => MatchesFilter(e, filter)).ToList();
        }

        private bool MatchesFilter(Element element, Core.Models.ElementFilter filter)
        {
            string? valueToTest = null;

            switch (filter.FilterType)
            {
                case ElementFilterType.ByTypeName:
                {
                    var typeId = element.GetTypeId();
                    if (typeId != ElementId.InvalidElementId)
                    {
                        var type = _doc.GetElement(typeId);
                        valueToTest = type?.Name;
                    }
                    break;
                }

                case ElementFilterType.ByParameter:
                {
                    if (!string.IsNullOrEmpty(filter.ParameterName))
                    {
                        // Try BuiltInParameter first
                        if (Enum.TryParse<BuiltInParameter>(filter.ParameterName, out var bip))
                        {
                            var param = element.get_Parameter(bip);
                            valueToTest = param?.AsValueString() ?? param?.AsString();
                        }

                        // Fallback: search by parameter name
                        if (valueToTest == null)
                        {
                            var param = element.LookupParameter(filter.ParameterName);
                            valueToTest = param?.AsValueString() ?? param?.AsString();
                        }
                    }
                    break;
                }
            }

            if (valueToTest == null) return false;

            return filter.Condition switch
            {
                FilterCondition.Contains =>
                    valueToTest.IndexOf(filter.Value, StringComparison.OrdinalIgnoreCase) >= 0,
                FilterCondition.Equals =>
                    valueToTest.Equals(filter.Value, StringComparison.OrdinalIgnoreCase),
                FilterCondition.StartsWith =>
                    valueToTest.StartsWith(filter.Value, StringComparison.OrdinalIgnoreCase),
                FilterCondition.EndsWith =>
                    valueToTest.EndsWith(filter.Value, StringComparison.OrdinalIgnoreCase),
                _ => false,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Dimension creation per element
        // ─────────────────────────────────────────────────────────────────────

        private Dimension? CreateDimensionForElement(Element element, DimensionRule rule)
        {
            if (element is Wall wall)
                return CreateWallDimension(wall, rule);

            if (element is FamilyInstance fi)
            {
                var catId = element.Category?.Id?.Value ?? 0;
                if (catId == (long)BuiltInCategory.OST_Doors || catId == (long)BuiltInCategory.OST_Windows)
                    return CreateFamilyInstanceDimension(fi, rule);

                if (catId == (long)BuiltInCategory.OST_Columns || catId == (long)BuiltInCategory.OST_StructuralColumns)
                    return CreateColumnDimension(fi, rule);

                if (catId == (long)BuiltInCategory.OST_StructuralFraming)
                    return CreateFamilyInstanceDimension(fi, rule);

                // Generic family instance fallback
                return CreateFamilyInstanceDimension(fi, rule);
            }

            if (element is Grid grid)
                return null; // Grids are dimensioned as chains, handled separately

            return null;
        }

        // ── Wall dimension ──────────────────────────────────────────────────

        private Dimension? CreateWallDimension(Wall wall, DimensionRule rule)
        {
            Reference? startRef;
            Reference? endRef;

            if (rule.Parameter == DimensionRuleParameter.Length)
            {
                // Length: dimension between wall endpoints
                var (s, e) = ReferenceResolver.GetWallEndpointReferences(wall);
                startRef = s;
                endRef = e;
            }
            else if (rule.Parameter == DimensionRuleParameter.Thickness)
            {
                // Thickness: dimension across the wall section
                var (ext, intr) = ReferenceResolver.GetWallThicknessReferences(wall, _view);
                startRef = ext;
                endRef = intr;
            }
            else
            {
                // Use explicit wall reference types from the rule
                startRef = ReferenceResolver.GetWallReference(wall, rule.WallStartReference, _view);
                endRef = ReferenceResolver.GetWallReference(wall, rule.WallEndReference, _view);
            }

            if (startRef == null || endRef == null) return null;

            var refs = new ReferenceArray();
            refs.Append(startRef);
            refs.Append(endRef);

            var dimLine = ComputeWallDimensionLine(wall);
            if (dimLine == null) return null;

            return _dimType != null
                ? _doc.Create.NewDimension(_view, dimLine, refs, _dimType)
                : _doc.Create.NewDimension(_view, dimLine, refs);
        }

        private Line? ComputeWallDimensionLine(Wall wall)
        {
            var locCurve = wall.Location as LocationCurve;
            if (locCurve == null) return null;

            var curve = locCurve.Curve;
            var start = curve.GetEndPoint(0);
            var end = curve.GetEndPoint(1);

            var dir = (end - start).Normalize();
            var normal = new XYZ(-dir.Y, dir.X, 0);
            var offset = normal * DefaultOffsetFeet;

            return Line.CreateBound(start + offset, end + offset);
        }

        // ── Door / Window dimension ─────────────────────────────────────────

        private Dimension? CreateFamilyInstanceDimension(FamilyInstance instance, DimensionRule rule)
        {
            var (refA, refB) = ReferenceResolver.GetFamilyInstanceReferences(instance, rule.Parameter);

            if (refA == null || refB == null) return null;

            var refs = new ReferenceArray();
            refs.Append(refA);
            refs.Append(refB);

            var dimLine = ComputeFamilyInstanceDimensionLine(instance, rule.Parameter);
            if (dimLine == null) return null;

            return _dimType != null
                ? _doc.Create.NewDimension(_view, dimLine, refs, _dimType)
                : _doc.Create.NewDimension(_view, dimLine, refs);
        }

        private Line? ComputeFamilyInstanceDimensionLine(FamilyInstance instance, DimensionRuleParameter param)
        {
            var locPt = instance.Location as LocationPoint;
            if (locPt == null) return null;

            var origin = locPt.Point;
            var facing = instance.FacingOrientation;
            var hand = instance.HandOrientation;

            bool isVertical = param == DimensionRuleParameter.Height
                           || param == DimensionRuleParameter.RoughOpeningHeight;

            if (isVertical)
            {
                // Vertical dimension line: offset along hand direction
                var offset = hand * DefaultOffsetFeet;
                var p1 = origin + offset;
                var p2 = p1 + XYZ.BasisZ * 3.0; // 3 ft span for line definition
                return Line.CreateBound(p1, p2);
            }
            else
            {
                // Horizontal dimension line: offset along facing direction
                var offset = facing * DefaultOffsetFeet;
                var p1 = origin + offset;
                var p2 = p1 + hand * 3.0; // 3 ft span for line definition
                return Line.CreateBound(p1, p2);
            }
        }

        // ── Column dimension ────────────────────────────────────────────────

        private Dimension? CreateColumnDimension(FamilyInstance column, DimensionRule rule)
        {
            var (refA, refB) = ReferenceResolver.GetColumnReferences(column, _view);

            if (refA == null || refB == null) return null;

            var refs = new ReferenceArray();
            refs.Append(refA);
            refs.Append(refB);

            var locPt = column.Location as LocationPoint;
            if (locPt == null) return null;

            var origin = locPt.Point;
            var viewRight = _view.RightDirection;
            var offset = _view.UpDirection * DefaultOffsetFeet;

            var dimLine = Line.CreateBound(
                origin + offset - viewRight * 3.0,
                origin + offset + viewRight * 3.0);

            return _dimType != null
                ? _doc.Create.NewDimension(_view, dimLine, refs, _dimType)
                : _doc.Create.NewDimension(_view, dimLine, refs);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Grid chain dimensioning (public, called externally for grid schemes)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a dimension chain across all collected grids in the scheme.
        /// Grids must be dimensioned as a group rather than individually.
        /// </summary>
        public int CreateGridChainDimensions(List<Grid> grids)
        {
            if (grids.Count < 2) return 0;

            int created = 0;

            // Group grids by direction (horizontal vs vertical)
            var groups = GroupGridsByDirection(grids);

            foreach (var group in groups)
            {
                if (group.Count < 2) continue;

                try
                {
                    var direction = GetGridDirection(group[0]);
                    var perpendicular = new XYZ(-direction.Y, direction.X, 0);

                    var sorted = group
                        .Select(g => (grid: g, proj: GetGridMidpoint(g).DotProduct(perpendicular)))
                        .OrderBy(x => x.proj)
                        .ToList();

                    var refs = new ReferenceArray();
                    foreach (var item in sorted)
                    {
                        var gridRef = ReferenceResolver.GetGridReference(item.grid);
                        if (gridRef != null)
                            refs.Append(gridRef);
                    }

                    if (refs.Size < 2) continue;

                    // Compute dimension line along grid endpoint + offset
                    var firstPt = GetGridEndpoint(sorted.First().grid, direction);
                    var lastPt = GetGridEndpoint(sorted.Last().grid, direction);
                    var offsetVec = direction * DefaultOffsetFeet;

                    var dimLine = Line.CreateBound(firstPt + offsetVec, lastPt + offsetVec);

                    var dim = _dimType != null
                        ? _doc.Create.NewDimension(_view, dimLine, refs, _dimType)
                        : _doc.Create.NewDimension(_view, dimLine, refs);

                    if (dim != null) created++;
                }
                catch
                {
                    // Skip failed group
                }
            }

            return created;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static BuiltInCategory? MapToBuiltInCategory(DimensionSchemeElementType elementType)
        {
            return elementType switch
            {
                DimensionSchemeElementType.Walls => BuiltInCategory.OST_Walls,
                DimensionSchemeElementType.Doors => BuiltInCategory.OST_Doors,
                DimensionSchemeElementType.Windows => BuiltInCategory.OST_Windows,
                DimensionSchemeElementType.Columns => BuiltInCategory.OST_Columns,
                DimensionSchemeElementType.StructuralFraming => BuiltInCategory.OST_StructuralFraming,
                DimensionSchemeElementType.Rooms => BuiltInCategory.OST_Rooms,
                DimensionSchemeElementType.Grids => BuiltInCategory.OST_Grids,
                _ => null,
            };
        }

        private static List<List<Grid>> GroupGridsByDirection(List<Grid> grids)
        {
            var groups = new List<List<Grid>>();
            var used = new HashSet<int>();

            for (int i = 0; i < grids.Count; i++)
            {
                if (used.Contains(i)) continue;

                var dir = GetGridDirection(grids[i]);
                var group = new List<Grid> { grids[i] };
                used.Add(i);

                for (int j = i + 1; j < grids.Count; j++)
                {
                    if (used.Contains(j)) continue;
                    var otherDir = GetGridDirection(grids[j]);
                    if (Math.Abs(dir.DotProduct(otherDir)) > 0.95)
                    {
                        group.Add(grids[j]);
                        used.Add(j);
                    }
                }

                groups.Add(group);
            }

            return groups;
        }

        private static XYZ GetGridDirection(Grid grid)
        {
            var curve = grid.Curve;
            var start = curve.GetEndPoint(0);
            var end = curve.GetEndPoint(1);
            return (end - start).Normalize();
        }

        private static XYZ GetGridMidpoint(Grid grid)
        {
            var curve = grid.Curve;
            return curve.Evaluate(0.5, true);
        }

        private static XYZ GetGridEndpoint(Grid grid, XYZ direction)
        {
            var curve = grid.Curve;
            var p0 = curve.GetEndPoint(0);
            var p1 = curve.GetEndPoint(1);
            // Return the endpoint with larger projection along direction
            return p0.DotProduct(direction) > p1.DotProduct(direction) ? p0 : p1;
        }

        private static string BuildSummary(SchemeExecutionResult result, string schemeName)
        {
            var parts = new List<string>();

            if (result.DimensionsCreated > 0)
                parts.Add($"{result.DimensionsCreated} dimensions created");
            else
                parts.Add("No dimensions created");

            parts.Add($"{result.ElementsProcessed} elements processed");

            if (result.ElementsSkipped > 0)
                parts.Add($"{result.ElementsSkipped} skipped");

            if (result.Errors.Count > 0)
                parts.Add($"{result.Errors.Count} errors");

            return $"Scheme '{schemeName}': {string.Join(", ", parts)}.";
        }
    }
}
