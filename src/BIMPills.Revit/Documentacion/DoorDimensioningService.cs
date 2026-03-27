using Autodesk.Revit.DB;
using BIMPills.Core.Documentacion;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMPills.Revit.Documentacion
{
    /// <summary>
    /// Servicio que crea cotas automáticas en vanos de puerta usando la API de Revit.
    /// Se invoca desde el callback de la ventana de Acotado de Vanos.
    /// </summary>
    internal static class DoorDimensioningService
    {
        /// <summary>
        /// Crea cotas de ancho de vano para todas las puertas en la vista activa.
        /// </summary>
        public static AcotadoVanosResult CreateOpeningWidthDimensions(
            Document doc,
            AcotadoVanosSettings settings)
        {
            var view = doc.ActiveView;
            if (view == null)
                return new AcotadoVanosResult(0, 0, 0, "No hay vista activa.");

            // Obtener DimensionType seleccionado
            var dimTypeId = new ElementId(settings.DimensionTypeId);
            var dimType = doc.GetElement(dimTypeId) as DimensionType;

            // Recopilar puertas en la vista
            var doors = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_Doors)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .ToList();

            int created = 0;
            int skipped = 0;
            int processed = 0;

            // Convertir offset de mm a pies internos de Revit
            double offsetFeet = settings.OffsetMm / 304.8;

            using (var tx = new Transaction(doc, "BIMPills: Acotado de Vanos"))
            {
                tx.Start();

                foreach (var door in doors)
                {
                    processed++;

                    try
                    {
                        // Verificar si ya tiene cota en esta vista
                        if (HasExistingDimension(doc, view, door))
                        {
                            skipped++;
                            continue;
                        }

                        var dim = CreateDimensionForDoor(doc, view, door, dimType, offsetFeet);
                        if (dim != null)
                            created++;
                        else
                            skipped++;
                    }
                    catch
                    {
                        skipped++;
                    }
                }

                if (created > 0)
                    tx.Commit();
                else
                    tx.RollBack();
            }

            var msg = created > 0
                ? $"Se crearon {created} cotas en {processed} puertas."
                : "No se crearon cotas nuevas.";

            if (skipped > 0)
                msg += $" {skipped} puertas omitidas (ya acotadas o sin referencias válidas).";

            return new AcotadoVanosResult(created, processed, skipped, msg);
        }

        /// <summary>
        /// Crea cotas de cadena de muro (opening + tramos adyacentes).
        /// </summary>
        public static AcotadoVanosResult CreateWallChainDimensions(
            Document doc,
            AcotadoVanosSettings settings)
        {
            // Implementación futura — por ahora redirige a opening width
            return CreateOpeningWidthDimensions(doc, settings);
        }

        private static Dimension? CreateDimensionForDoor(
            Document doc, View view, FamilyInstance door,
            DimensionType? dimType, double offsetFeet)
        {
            // Obtener el muro anfitrión
            var host = door.Host as Wall;
            if (host == null) return null;

            var wallCurve = (host.Location as LocationCurve)?.Curve as Line;
            if (wallCurve == null) return null;

            // Obtener punto de inserción de la puerta
            var doorLoc = door.Location as LocationPoint;
            if (doorLoc == null) return null;
            var doorPoint = doorLoc.Point;

            // Obtener el ancho del vano
            double doorWidth = GetDoorWidth(door);
            if (doorWidth <= 0) return null;

            // Dirección del muro
            var wallDir = wallCurve.Direction.Normalize();
            var wallNormal = new XYZ(-wallDir.Y, wallDir.X, 0); // Normal perpendicular

            // Calcular puntos de las caras del vano
            var halfWidth = doorWidth / 2.0;
            var p1 = doorPoint + wallDir * halfWidth;
            var p2 = doorPoint - wallDir * halfWidth;

            // Determinar a qué lado colocar la cota (side del offset)
            // Usamos la normal del muro hacia afuera del recinto
            var offsetDir = GetDimensionSide(door, host, wallNormal);
            var offsetVec = offsetDir * offsetFeet;

            // Línea de la cota (paralela al muro, desplazada)
            var dimP1 = p1 + offsetVec;
            var dimP2 = p2 + offsetVec;
            var dimLine = Line.CreateBound(dimP1, dimP2);

            // Intentar obtener referencias geométricas de las caras del vano
            var refs = GetOpeningReferences(doc, host, door, wallDir, doorPoint, halfWidth);
            if (refs == null || refs.Size < 2) return null;

            // Crear la cota
            Dimension dim;
            if (dimType != null)
                dim = doc.Create.NewDimension(view, dimLine, refs, dimType);
            else
                dim = doc.Create.NewDimension(view, dimLine, refs);

            return dim;
        }

        private static ReferenceArray? GetOpeningReferences(
            Document doc, Wall host, FamilyInstance door,
            XYZ wallDir, XYZ doorCenter, double halfWidth)
        {
            var refs = new ReferenceArray();

            // Método 1: Buscar referencias en la geometría del muro (caras del vano)
            var options = new Options
            {
                ComputeReferences = true,
                View = doc.ActiveView
            };

            var geom = host.get_Geometry(options);
            if (geom == null) return null;

            // Buscar caras perpendiculares al muro y cercanas al vano
            var candidateFaces = new List<(Reference reference, double distance)>();

            foreach (var gObj in geom)
            {
                var solid = gObj as Solid;
                if (solid == null || solid.Faces.Size == 0) continue;

                foreach (Face face in solid.Faces)
                {
                    if (face is not PlanarFace planar) continue;
                    if (face.Reference == null) continue;

                    // Buscamos caras cuya normal sea paralela al muro (perpendiculares al corte)
                    var faceNormal = planar.FaceNormal;
                    var dot = Math.Abs(faceNormal.DotProduct(wallDir));
                    if (dot < 0.9) continue; // No es perpendicular al corte

                    // Verificar que la cara está cerca del vano
                    var facePoint = planar.Origin;
                    var projection = (facePoint - doorCenter).DotProduct(wallDir);

                    if (Math.Abs(Math.Abs(projection) - halfWidth) < 0.1) // ~30mm tolerance
                    {
                        candidateFaces.Add((face.Reference, projection));
                    }
                }
            }

            // Necesitamos exactamente 2 caras opuestas
            if (candidateFaces.Count >= 2)
            {
                // Tomar la más negativa y la más positiva
                var sorted = candidateFaces.OrderBy(f => f.distance).ToList();
                refs.Append(sorted.First().reference);
                refs.Append(sorted.Last().reference);
                return refs;
            }

            // Método 2: Fallback — usar referencias del door instance
            try
            {
                var doorGeom = door.get_Geometry(options);
                if (doorGeom != null)
                {
                    var doorRefs = new List<(Reference reference, double distance)>();
                    foreach (var gObj in doorGeom)
                    {
                        ProcessGeometryObject(gObj, wallDir, doorCenter, doorRefs);
                    }

                    if (doorRefs.Count >= 2)
                    {
                        var sorted = doorRefs.OrderBy(f => f.distance).ToList();
                        refs.Append(sorted.First().reference);
                        refs.Append(sorted.Last().reference);
                        return refs;
                    }
                }
            }
            catch { /* Fallback failed */ }

            return null;
        }

        private static void ProcessGeometryObject(
            GeometryObject gObj, XYZ wallDir, XYZ doorCenter,
            List<(Reference reference, double distance)> results)
        {
            if (gObj is Solid solid && solid.Faces.Size > 0)
            {
                foreach (Face face in solid.Faces)
                {
                    if (face is not PlanarFace planar) continue;
                    if (face.Reference == null) continue;

                    var dot = Math.Abs(planar.FaceNormal.DotProduct(wallDir));
                    if (dot < 0.9) continue;

                    var projection = (planar.Origin - doorCenter).DotProduct(wallDir);
                    results.Add((face.Reference, projection));
                }
            }
            else if (gObj is GeometryInstance gi)
            {
                foreach (var nested in gi.GetInstanceGeometry())
                {
                    ProcessGeometryObject(nested, wallDir, doorCenter, results);
                }
            }
        }

        private static double GetDoorWidth(FamilyInstance door)
        {
            // Intentar obtener el ancho desde parámetros conocidos
            var widthParam = door.Symbol.get_Parameter(BuiltInParameter.DOOR_WIDTH)
                          ?? door.Symbol.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM)
                          ?? door.get_Parameter(BuiltInParameter.DOOR_WIDTH);

            if (widthParam != null && widthParam.HasValue)
                return widthParam.AsDouble(); // En pies

            // Fallback: usar BoundingBox
            var bb = door.get_BoundingBox(null);
            if (bb != null)
            {
                var wallDir = ((door.Host as Wall)?.Location as LocationCurve)?.Curve as Line;
                if (wallDir != null)
                {
                    var dir = wallDir.Direction.Normalize();
                    var diag = bb.Max - bb.Min;
                    return Math.Abs(diag.DotProduct(dir));
                }
            }

            return 0;
        }

        private static XYZ GetDimensionSide(FamilyInstance door, Wall host, XYZ wallNormal)
        {
            // Colocar la cota del lado de la cara exterior del muro
            // (opuesto a la dirección de apertura de la puerta)
            if (door.FacingOrientation != null)
            {
                var dot = door.FacingOrientation.DotProduct(wallNormal);
                return dot >= 0 ? wallNormal : wallNormal.Negate();
            }
            return wallNormal;
        }

        private static bool HasExistingDimension(Document doc, View view, FamilyInstance door)
        {
            // Buscar cotas existentes que referencien este elemento
            try
            {
                var dims = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(Dimension))
                    .Cast<Dimension>()
                    .Where(d => d.References != null);

                var doorId = door.Id;
                foreach (var dim in dims)
                {
                    try
                    {
                        foreach (Reference r in dim.References)
                        {
                            if (r.ElementId == doorId || r.ElementId == door.Host?.Id)
                            {
                                // Verificar que es una cota de ancho de vano (2 refs, paralela al muro)
                                if (dim.References.Size == 2)
                                    return true;
                            }
                        }
                    }
                    catch { continue; }
                }
            }
            catch { /* No se pudo verificar */ }

            return false;
        }
    }
}
