using Autodesk.Revit.DB;
using BIMPills.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMPills.Revit.Dimensioning
{
    /// <summary>
    /// Maps <see cref="WallReferenceType"/> and <see cref="DimensionRuleParameter"/>
    /// to actual Revit <see cref="Reference"/> objects for each element type.
    /// All geometry extraction uses <c>ComputeReferences = true</c>.
    /// </summary>
    internal static class ReferenceResolver
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Wall references
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Resolves a <see cref="WallReferenceType"/> to a Revit <see cref="Reference"/>
        /// on the given wall.
        /// </summary>
        public static Reference? GetWallReference(Wall wall, WallReferenceType refType, View view)
        {
            try
            {
                switch (refType)
                {
                    case WallReferenceType.FinishFaceExterior:
                        return GetSideFaceReference(wall, ShellLayerType.Exterior);

                    case WallReferenceType.FinishFaceInterior:
                        return GetSideFaceReference(wall, ShellLayerType.Interior);

                    case WallReferenceType.CoreFaceExterior:
                        return GetCoreFaceReference(wall, view, isExterior: true);

                    case WallReferenceType.CoreFaceInterior:
                        return GetCoreFaceReference(wall, view, isExterior: false);

                    case WallReferenceType.WallCenterline:
                        return GetWallCenterlineReference(wall);

                    case WallReferenceType.CoreCenterline:
                        return GetWallCenterlineReference(wall);

                    default:
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets finish-face references using <see cref="HostObjectUtils.GetSideFaces"/>.
        /// </summary>
        private static Reference? GetSideFaceReference(Wall wall, ShellLayerType shellLayer)
        {
            var faces = HostObjectUtils.GetSideFaces(wall, shellLayer);
            if (faces == null || faces.Count == 0) return null;
            return faces[0];
        }

        /// <summary>
        /// Gets a core-layer face reference by iterating wall geometry and finding
        /// faces that are inward from the finish faces.
        /// </summary>
        private static Reference? GetCoreFaceReference(Wall wall, View view, bool isExterior)
        {
            // Core faces are not directly exposed — we approximate by finding
            // planar faces perpendicular to the wall normal, sorted by distance
            // from centerline, and picking the ones between finish and center.
            var options = new Options { ComputeReferences = true, View = view };
            var geom = wall.get_Geometry(options);
            if (geom == null) return null;

            var wallCurve = (wall.Location as LocationCurve)?.Curve as Line;
            if (wallCurve == null) return null;

            var wallDir = wallCurve.Direction.Normalize();
            var wallNormal = new XYZ(-wallDir.Y, wallDir.X, 0);
            var wallCenter = wallCurve.Evaluate(0.5, true);

            var candidates = new List<(Reference reference, double distance)>();

            foreach (var gObj in geom)
            {
                var solid = gObj as Solid;
                if (solid == null || solid.Faces.Size == 0) continue;

                foreach (Face face in solid.Faces)
                {
                    if (face is not PlanarFace planar) continue;
                    if (face.Reference == null) continue;

                    // Only faces parallel to wall direction (perpendicular to normal)
                    var dot = Math.Abs(planar.FaceNormal.DotProduct(wallNormal));
                    if (dot < 0.9) continue;

                    var dist = (planar.Origin - wallCenter).DotProduct(wallNormal);
                    candidates.Add((face.Reference, dist));
                }
            }

            if (candidates.Count < 2) return null;

            // Sort by distance from center along normal
            var sorted = candidates.OrderBy(c => c.distance).ToList();

            // The outermost faces are finish faces; the next inward ones are core faces
            if (isExterior)
            {
                // Core exterior is the second-from-outside on the exterior side
                // Exterior side = positive direction along normal (last elements)
                return sorted.Count >= 4
                    ? sorted[sorted.Count - 2].reference
                    : sorted.Last().reference;
            }
            else
            {
                // Core interior is the second-from-outside on the interior side
                return sorted.Count >= 4
                    ? sorted[1].reference
                    : sorted.First().reference;
            }
        }

        /// <summary>
        /// Gets the wall centerline reference from the LocationCurve.
        /// </summary>
        private static Reference? GetWallCenterlineReference(Wall wall)
        {
            var locCurve = wall.Location as LocationCurve;
            if (locCurve == null) return null;

            // The location curve itself can serve as a reference for centerline dims.
            // We use endpoint references which are always available.
            return locCurve.Curve.Reference;
        }

        /// <summary>
        /// Gets the endpoint references of a wall's location curve.
        /// Returns (startRef, endRef).
        /// </summary>
        public static (Reference? start, Reference? end) GetWallEndpointReferences(Wall wall)
        {
            try
            {
                var locCurve = wall.Location as LocationCurve;
                if (locCurve == null) return (null, null);

                var startRef = locCurve.Curve.GetEndPointReference(0);
                var endRef = locCurve.Curve.GetEndPointReference(1);
                return (startRef, endRef);
            }
            catch
            {
                return (null, null);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Door / Window references
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Gets references for a door or window based on the <see cref="DimensionRuleParameter"/>.
        /// Returns the pair of references that span the measured property.
        /// </summary>
        public static (Reference? refA, Reference? refB) GetFamilyInstanceReferences(
            FamilyInstance instance, DimensionRuleParameter parameter)
        {
            try
            {
                switch (parameter)
                {
                    case DimensionRuleParameter.Width:
                    case DimensionRuleParameter.RoughOpeningWidth:
                    {
                        var left = instance.GetReferences(FamilyInstanceReferenceType.Left);
                        var right = instance.GetReferences(FamilyInstanceReferenceType.Right);
                        return (
                            left?.Count > 0 ? left[0] : null,
                            right?.Count > 0 ? right[0] : null
                        );
                    }

                    case DimensionRuleParameter.Height:
                    case DimensionRuleParameter.RoughOpeningHeight:
                    {
                        var top = instance.GetReferences(FamilyInstanceReferenceType.Top);
                        var bottom = instance.GetReferences(FamilyInstanceReferenceType.Bottom);
                        return (
                            top?.Count > 0 ? top[0] : null,
                            bottom?.Count > 0 ? bottom[0] : null
                        );
                    }

                    default:
                    {
                        // For generic parameters, use Left/Right as default
                        var left = instance.GetReferences(FamilyInstanceReferenceType.Left);
                        var right = instance.GetReferences(FamilyInstanceReferenceType.Right);
                        return (
                            left?.Count > 0 ? left[0] : null,
                            right?.Count > 0 ? right[0] : null
                        );
                    }
                }
            }
            catch
            {
                return (null, null);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Column references
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Gets face references for a column (structural or architectural).
        /// Returns references for left/right faces along the given view direction.
        /// </summary>
        public static (Reference? refA, Reference? refB) GetColumnReferences(
            FamilyInstance column, View view)
        {
            try
            {
                // Try named references first
                var left = column.GetReferences(FamilyInstanceReferenceType.Left);
                var right = column.GetReferences(FamilyInstanceReferenceType.Right);
                if (left?.Count > 0 && right?.Count > 0)
                    return (left[0], right[0]);

                // Fallback: iterate geometry faces
                var options = new Options { ComputeReferences = true, View = view };
                var geom = column.get_Geometry(options);
                if (geom == null) return (null, null);

                var viewDir = view.ViewDirection;
                var faces = new List<(Reference reference, double dist)>();

                foreach (var gObj in GetSolids(geom))
                {
                    foreach (Face face in gObj.Faces)
                    {
                        if (face is not PlanarFace planar) continue;
                        if (face.Reference == null) continue;

                        // Faces perpendicular to view right direction
                        var viewRight = view.RightDirection;
                        var dot = Math.Abs(planar.FaceNormal.DotProduct(viewRight));
                        if (dot < 0.9) continue;

                        var dist = planar.Origin.DotProduct(viewRight);
                        faces.Add((face.Reference, dist));
                    }
                }

                if (faces.Count < 2) return (null, null);

                var sorted = faces.OrderBy(f => f.dist).ToList();
                return (sorted.First().reference, sorted.Last().reference);
            }
            catch
            {
                return (null, null);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Grid references
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Gets a dimension reference for a grid line.
        /// </summary>
        public static Reference? GetGridReference(Grid grid)
        {
            try
            {
                return new Reference(grid);
            }
            catch
            {
                return null;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Geometry helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Recursively extracts all Solid objects from a GeometryElement,
        /// descending into GeometryInstance transforms.
        /// </summary>
        public static IEnumerable<Solid> GetSolids(GeometryElement geom)
        {
            foreach (var gObj in geom)
            {
                if (gObj is Solid solid && solid.Faces.Size > 0)
                {
                    yield return solid;
                }
                else if (gObj is GeometryInstance gi)
                {
                    foreach (var inner in GetSolids(gi.GetInstanceGeometry()))
                        yield return inner;
                }
            }
        }

        /// <summary>
        /// Gets the perpendicular face references of a wall for dimensioning its thickness.
        /// Returns references for the two main parallel faces (exterior and interior).
        /// </summary>
        public static (Reference? exterior, Reference? interior) GetWallThicknessReferences(
            Wall wall, View view)
        {
            var extRef = GetSideFaceReference(wall, ShellLayerType.Exterior);
            var intRef = GetSideFaceReference(wall, ShellLayerType.Interior);
            return (extRef, intRef);
        }
    }
}
