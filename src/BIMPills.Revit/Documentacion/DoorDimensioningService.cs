using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using BIMPills.Core.Documentacion;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.DI;
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
        /// Result of a single-room dimensioning attempt.
        /// </summary>
        private class DimensionResult
        {
            public bool Success { get; set; }
            public string SkipReason { get; set; } = string.Empty;
        }
        /// <summary>
        /// Crea cotas de ancho de vano para todas las puertas en la vista activa.
        /// </summary>
        public static AcotadoVanosResult CreateOpeningWidthDimensions(
            Document doc,
            AcotadoVanosSettings settings)
        {
            var logger = ServiceLocator.IsRegistered<ILogger>() ? ServiceLocator.Get<ILogger>() : null;
            logger?.Info("[DoorDimensioningService] Iniciando CreateOpeningWidthDimensions...");
            try
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

            // OffsetMm está en mm modelo → convertir a pies: pies = mm / 304.8
            double offsetFeet = settings.OffsetMm / 304.8;

            using (var tx = new Transaction(doc, "BIMPills: Acotado de Vanos"))
            {
                try
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
                catch (Exception ex)
                {
                    if (tx.GetStatus() == TransactionStatus.Started)
                        tx.RollBack();
                    logger?.Error("[DoorDimensioningService] Transacción revertida en CreateOpeningWidthDimensions", ex);
                    return new AcotadoVanosResult(0, 0, 0, $"Error durante la operación: {ex.Message}");
                }
            }

            var msg = created > 0
                ? $"Se crearon {created} cotas en {processed} puertas."
                : "No se crearon cotas nuevas.";

            if (skipped > 0)
                msg += $" {skipped} puertas omitidas (ya acotadas o sin referencias válidas).";

            return new AcotadoVanosResult(created, processed, skipped, msg);
            }
            catch (Exception ex)
            {
                logger?.Error("[DoorDimensioningService] Error en CreateOpeningWidthDimensions", ex);
                return new AcotadoVanosResult(0, 0, 0, $"Error inesperado al acotar vanos: {ex.Message}");
            }
        }

        /// <summary>
        /// Crea cotas de cadena de muro: extremos del muro + caras del vano + muros intermedios.
        /// Genera una cadena continua de cotas a lo largo del muro host de cada puerta.
        /// </summary>
        public static AcotadoVanosResult CreateWallChainDimensions(
            Document doc,
            AcotadoVanosSettings settings)
        {
            var logger = ServiceLocator.IsRegistered<ILogger>() ? ServiceLocator.Get<ILogger>() : null;
            logger?.Info("[DoorDimensioningService] Iniciando CreateWallChainDimensions...");
            try
            {
            var view = doc.ActiveView;
            if (view == null)
                return new AcotadoVanosResult(0, 0, 0, "No hay vista activa.");

            var dimTypeId = new ElementId(settings.DimensionTypeId);
            var dimType = doc.GetElement(dimTypeId) as DimensionType;

            var doors = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_Doors)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .ToList();

            int created = 0;
            int skipped = 0;
            int processed = 0;
            double offsetFeet = settings.OffsetMm / 304.8;

            // Agrupar puertas por muro host para crear una cadena por muro
            var doorsByWall = doors
                .Where(d => d.Host is Wall)
                .GroupBy(d => d.Host.Id)
                .ToList();

            using (var tx = new Transaction(doc, "BIMPills: Cadena de Muro"))
            {
                try
                {
                    tx.Start();

                    foreach (var group in doorsByWall)
                    {
                        processed += group.Count();

                        try
                        {
                            var wall = doc.GetElement(group.Key) as Wall;
                            if (wall == null) { skipped += group.Count(); continue; }

                            var wallCurve = (wall.Location as LocationCurve)?.Curve as Line;
                            if (wallCurve == null) { skipped += group.Count(); continue; }

                            var dim = CreateWallChainForWall(doc, view, wall, wallCurve,
                                group.ToList(), dimType, offsetFeet);
                            if (dim != null)
                                created++;
                            else
                                skipped += group.Count();
                        }
                        catch
                        {
                            skipped += group.Count();
                        }
                    }

                    if (created > 0)
                        tx.Commit();
                    else
                        tx.RollBack();
                }
                catch (Exception ex)
                {
                    if (tx.GetStatus() == TransactionStatus.Started)
                        tx.RollBack();
                    logger?.Error("[DoorDimensioningService] Transacción revertida en CreateWallChainDimensions", ex);
                    return new AcotadoVanosResult(0, 0, 0, $"Error durante la operación: {ex.Message}");
                }
            }

            var msg = created > 0
                ? $"Se crearon {created} cadenas de cota en {processed} puertas."
                : "No se crearon cotas nuevas.";

            if (skipped > 0)
                msg += $" {skipped} puertas omitidas.";

            return new AcotadoVanosResult(created, processed, skipped, msg);
            }
            catch (Exception ex)
            {
                logger?.Error("[DoorDimensioningService] Error en CreateWallChainDimensions", ex);
                return new AcotadoVanosResult(0, 0, 0, $"Error inesperado al acotar cadena de muro: {ex.Message}");
            }
        }

        /// <summary>
        /// Crea una cadena de cotas a lo largo de un muro, incluyendo extremos y caras de vanos.
        /// </summary>
        private static Dimension? CreateWallChainForWall(
            Document doc, View view, Wall wall, Line wallCurve,
            List<FamilyInstance> doorsInWall, DimensionType? dimType, double offsetFeet)
        {
            var wallDir = wallCurve.Direction.Normalize();
            var wallNormal = new XYZ(-wallDir.Y, wallDir.X, 0);

            var options = new Options
            {
                ComputeReferences = true,
                View = view
            };

            // Recopilar todas las referencias a lo largo del muro:
            // 1. Extremos del muro (caras perpendiculares en los endpoints)
            // 2. Caras internas de cada vano de puerta
            var allRefs = new List<(Reference reference, double projection)>();

            // Obtener referencias de las caras del muro (extremos + vanos)
            var wallGeom = wall.get_Geometry(options);
            if (wallGeom == null) return null;

            var wallStart = wallCurve.GetEndPoint(0);

            foreach (var gObj in wallGeom)
            {
                var solid = gObj as Solid;
                if (solid == null || solid.Faces.Size == 0) continue;

                foreach (Face face in solid.Faces)
                {
                    if (face is not PlanarFace planar) continue;
                    if (face.Reference == null) continue;

                    // Solo caras perpendiculares al muro (paralelas a wallDir como normal)
                    var dot = Math.Abs(planar.FaceNormal.DotProduct(wallDir));
                    if (dot < 0.9) continue;

                    var projection = (planar.Origin - wallStart).DotProduct(wallDir);
                    allRefs.Add((face.Reference, projection));
                }
            }

            if (allRefs.Count < 2) return null;

            // Eliminar duplicados muy cercanos (< 5mm = ~0.016 ft)
            var sorted = allRefs.OrderBy(r => r.projection).ToList();
            var filtered = new List<(Reference reference, double projection)> { sorted[0] };
            for (int i = 1; i < sorted.Count; i++)
            {
                if (Math.Abs(sorted[i].projection - filtered.Last().projection) > 0.016)
                    filtered.Add(sorted[i]);
            }

            if (filtered.Count < 2) return null;

            var refs = new ReferenceArray();
            foreach (var r in filtered)
                refs.Append(r.reference);

            // Calcular la línea de cota desplazada
            var offsetDir = wallNormal;
            if (doorsInWall.Count > 0 && doorsInWall[0].FacingOrientation != null)
            {
                var faceDot = doorsInWall[0].FacingOrientation.DotProduct(wallNormal);
                offsetDir = faceDot >= 0 ? wallNormal : wallNormal.Negate();
            }

            var offsetVec = offsetDir * offsetFeet;
            var dimLine = Line.CreateBound(
                wallCurve.GetEndPoint(0) + offsetVec,
                wallCurve.GetEndPoint(1) + offsetVec);

            Dimension dim;
            if (dimType != null)
                dim = doc.Create.NewDimension(view, dimLine, refs, dimType);
            else
                dim = doc.Create.NewDimension(view, dimLine, refs);

            return dim;
        }

        /// <summary>
        /// Crea cotas a ejes: total (extremo a extremo) al offset indicado por el usuario,
        /// seguida de las parciales (cadena entre consecutivos) más alejadas con gap adaptativo al tipo de cota.
        /// </summary>
        public static AcotadoVanosResult CreateGridDimensions(
            Document doc,
            AcotadoVanosSettings settings)
        {
            var logger = ServiceLocator.IsRegistered<ILogger>() ? ServiceLocator.Get<ILogger>() : null;
            logger?.Info("[DoorDimensioningService] Iniciando CreateGridDimensions...");
            try
            {
            var view = doc.ActiveView;
            if (view == null)
                return new AcotadoVanosResult(0, 0, 0, "No hay vista activa.");

            var dimTypeId = new ElementId(settings.DimensionTypeId);
            var dimType = doc.GetElement(dimTypeId) as DimensionType;

            // Collect grids at document level — view-based collector misses grids
            // hidden via Visibility/Graphics overrides but still dimensionable.
            var grids = new FilteredElementCollector(doc)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .ToList();

            if (grids.Count < 2)
                return new AcotadoVanosResult(0, grids.Count, 0, "Se necesitan al menos 2 ejes para crear cotas.");

            // OffsetMm en mm modelo → pies: mm / 304.8
            // stackGap usa la "Distancia de forzado de cursor" del tipo de cota (papel) × escala → modelo
            double offsetFeet          = settings.OffsetMm / 304.8;
            double minStackGapFeet     = 5.0 * view.Scale / 304.8;  // 5 mm papel mínimo → modelo
            double defaultStackGapFeet = 8.0 * view.Scale / 304.8;  // 8 mm papel por defecto → modelo
            var rawGap = GetDimLineSnapDistance(dimType);
            double stackGapFeet = rawGap.HasValue
                ? Math.Max(rawGap.Value * view.Scale, minStackGapFeet)  // rawGap = papel pies × escala → modelo
                : defaultStackGapFeet;

            // Adaptive gap: ensure at least 1.5× text height in model space
            double adaptiveGapFeet = defaultStackGapFeet; // fallback
            if (dimType != null)
            {
                try
                {
                    var textSizeParam = dimType.get_Parameter(BuiltInParameter.TEXT_SIZE);
                    if (textSizeParam != null)
                    {
                        double textSizeFeet = textSizeParam.AsDouble(); // already in feet
                        adaptiveGapFeet = textSizeFeet * view.Scale * 1.5;
                    }
                }
                catch { /* use default */ }
            }
            stackGapFeet = Math.Max(stackGapFeet, adaptiveGapFeet);

            int created = 0;
            int processed = 0;

            var groups = GroupGridsByDirection(grids);

            using (var tx = new Transaction(doc, "BIMPills: Cotas a ejes"))
            {
                try
                {
                tx.Start();

                foreach (var group in groups)
                {
                    processed += group.Count;
                    if (group.Count < 2) continue;

                    try
                    {
                        var direction = GetGridDirection(group[0]);
                        var perpendicular = new XYZ(-direction.Y, direction.X, 0);

                        var sorted = group
                            .Select(g => (grid: g, proj: GetGridMidpoint(g).DotProduct(perpendicular)))
                            .OrderBy(x => x.proj)
                            .ToList();

                        var endpoints = GetGridEndpoints(settings.GridEndpoint);

                        foreach (var ep in endpoints)
                        {
                            bool useMax = (ep == 1);
                            var offsetDir = useMax ? direction : direction.Negate();
                            var firstPt = GetConsistentEndpoint(sorted.First().grid, direction, useMax);
                            var lastPt  = GetConsistentEndpoint(sorted.Last().grid,  direction, useMax);

                            bool hasPartial = sorted.Count > 2;

                            // Total refs (first and last grid only)
                            var totalRefs = new ReferenceArray();
                            totalRefs.Append(new Reference(sorted.First().grid));
                            totalRefs.Append(new Reference(sorted.Last().grid));

                            if (hasPartial)
                            {
                                // 1) Total dimension — at offsetFeet (closer, user-controlled)
                                var totalVec = offsetDir * offsetFeet;
                                var totalLine = Line.CreateBound(firstPt + totalVec, lastPt + totalVec);
                                Dimension td = dimType != null
                                    ? doc.Create.NewDimension(view, totalLine, totalRefs, dimType)
                                    : doc.Create.NewDimension(view, totalLine, totalRefs);
                                if (td != null) created++;

                                // 2) Partial chain — always further from endpoint than total.
                                // Gap must follow the sign of offsetFeet: negative offset means
                                // both dims go "downward" (or inward), so we subtract the gap.
                                double partialOffset = offsetFeet >= 0
                                    ? offsetFeet + stackGapFeet
                                    : offsetFeet - stackGapFeet;
                                var partialRefs = new ReferenceArray();
                                foreach (var item in sorted)
                                    partialRefs.Append(new Reference(item.grid));
                                var partialVec = offsetDir * partialOffset;
                                var partialLine = Line.CreateBound(firstPt + partialVec, lastPt + partialVec);
                                Dimension pd = dimType != null
                                    ? doc.Create.NewDimension(view, partialLine, partialRefs, dimType)
                                    : doc.Create.NewDimension(view, partialLine, partialRefs);
                                if (pd != null) created++;
                            }
                            else
                            {
                                // Only 2 grids — just place total at offsetFeet (no partial needed)
                                var totalVec = offsetDir * offsetFeet;
                                var totalLine = Line.CreateBound(firstPt + totalVec, lastPt + totalVec);

                                Dimension td = dimType != null
                                    ? doc.Create.NewDimension(view, totalLine, totalRefs, dimType)
                                    : doc.Create.NewDimension(view, totalLine, totalRefs);
                                if (td != null) created++;
                            }
                        }
                    }
                    catch { /* Skip failed group */ }
                }

                if (created > 0)
                    tx.Commit();
                else
                    tx.RollBack();
                }
                catch (Exception ex)
                {
                    if (tx.GetStatus() == TransactionStatus.Started)
                        tx.RollBack();
                    logger?.Error("[DoorDimensioningService] Transacción revertida en CreateGridDimensions", ex);
                    return new AcotadoVanosResult(0, 0, 0, $"Error durante la operación: {ex.Message}");
                }
            }

            var msg = created > 0
                ? $"Se crearon {created} cotas (totales + parciales) en {processed} ejes."
                : "No se crearon cotas.";
            return new AcotadoVanosResult(created, processed, 0, msg);
            }
            catch (Exception ex)
            {
                logger?.Error("[DoorDimensioningService] Error en CreateGridDimensions", ex);
                return new AcotadoVanosResult(0, 0, 0, $"Error inesperado al acotar ejes: {ex.Message}");
            }
        }

        /// <summary>
        /// Crea cotas de espacios interiores usando Room.GetBoundarySegments para obtener
        /// las caras de acabado de los muros que delimitan cada habitación en la vista activa.
        /// </summary>
        public static AcotadoVanosResult CreateInteriorSpaceDimensions(
            Document doc,
            AcotadoVanosSettings settings)
        {
            var logger = ServiceLocator.IsRegistered<ILogger>() ? ServiceLocator.Get<ILogger>() : null;
            logger?.Info("[DoorDimensioningService] Iniciando CreateInteriorSpaceDimensions...");
            try
            {
            View planView = doc.ActiveView;
            if (planView == null)
                return new AcotadoVanosResult(0, 0, 0, "No hay vista activa.");

            var rooms = new FilteredElementCollector(doc, planView.Id)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .Cast<Room>()
                .Where(r => r.Area > 0)
                .ToList();

            if (!rooms.Any())
                return new AcotadoVanosResult(0, 0, 0, "No se encontraron habitaciones en la vista activa.");

            var dimTypeId = new ElementId(settings.DimensionTypeId);
            var dimType = doc.GetElement(dimTypeId) as DimensionType;

            int successCount = 0;
            var skipped = new List<string>();

            using (var tx = new Transaction(doc, "BIMPills: Acotar espacios interiores"))
            {
                try
                {
                    tx.Start();
                    foreach (var room in rooms)
                    {
                        try
                        {
                            var result = DimensionRoom(doc, room, planView, settings, dimType);
                            if (!result.Success)
                                skipped.Add($"Habitación '{room.Name}' [{room.Id}]: {result.SkipReason}");
                            else
                                successCount++;
                        }
                        catch (ArgumentException ex)
                        {
                            skipped.Add($"Habitación '{room.Name}' [{room.Id}]: referencia inválida — {ex.Message}");
                        }
                        catch (Exception ex)
                        {
                            skipped.Add($"Habitación '{room.Name}' [{room.Id}]: error inesperado — {ex.Message}");
                        }
                    }

                    if (successCount > 0)
                        tx.Commit();
                    else
                        tx.RollBack();
                }
                catch (Exception ex)
                {
                    if (tx.GetStatus() == TransactionStatus.Started)
                        tx.RollBack();
                    logger?.Error("[DoorDimensioningService] Transacción revertida en CreateInteriorSpaceDimensions", ex);
                    return new AcotadoVanosResult(0, 0, 0, $"Error durante la operación: {ex.Message}");
                }
            }

            string summary = $"Acotadas: {successCount} habitación(es).";
            if (skipped.Any())
                summary += $"\nOmitidas: {skipped.Count}:\n" + string.Join("\n", skipped);

            return new AcotadoVanosResult(successCount, rooms.Count, skipped.Count, summary);
            }
            catch (Exception ex)
            {
                logger?.Error("[DoorDimensioningService] Error en CreateInteriorSpaceDimensions", ex);
                return new AcotadoVanosResult(0, 0, 0, $"Error inesperado al acotar espacios interiores: {ex.Message}");
            }
        }

        private static DimensionResult DimensionRoom(Document doc, Room room, View planView, AcotadoVanosSettings settings, DimensionType? dimType)
        {
            // 1. Get boundary segments at Finish face
            var opts = new SpatialElementBoundaryOptions
            {
                SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish
            };
            var loops = room.GetBoundarySegments(opts);

            if (loops == null || loops.Count == 0 || loops[0].Count < 3)
                return new DimensionResult { Success = false, SkipReason = "Sin contorno de habitación válido" };

            if (room.Location is not LocationPoint locPt)
                return new DimensionResult { Success = false, SkipReason = "Sin LocationPoint" };

            XYZ roomCenter = locPt.Point;

            // 2. Classify segments into H and V groups (skip non-linear, non-wall)
            var hGroup = new List<(Wall wall, Curve curve)>(); // runs horizontally (|dir.X| > |dir.Y|) → gives length
            var vGroup = new List<(Wall wall, Curve curve)>(); // runs vertically (|dir.Y| >= |dir.X|) → gives width

            foreach (var seg in loops[0])
            {
                if (seg.ElementId == ElementId.InvalidElementId) continue;
                var el = doc.GetElement(seg.ElementId);
                if (el is not Wall wall) continue;

                var curve = seg.GetCurve();
                if (curve is not Line) continue; // skip arcs

                // Guard: skip degenerate (zero-length) segments before normalizing
                XYZ delta = curve.GetEndPoint(1) - curve.GetEndPoint(0);
                if (delta.GetLength() < 1e-6) continue;

                XYZ dir = delta.Normalize();
                double ax = Math.Abs(dir.X);
                double ay = Math.Abs(dir.Y);

                if (ax > ay)
                    hGroup.Add((wall, curve));
                else
                    vGroup.Add((wall, curve));
            }

            if (vGroup.Count < 2)
                return new DimensionResult { Success = false, SkipReason = $"Ancho: solo {vGroup.Count} muro(s) vertical(es)" };
            if (hGroup.Count < 2)
                return new DimensionResult { Success = false, SkipReason = $"Largo: solo {hGroup.Count} muro(s) horizontal(es)" };

            // 3. Pick outermost walls for width (vGroup sorted by midpoint.X) and length (hGroup sorted by midpoint.Y)
            // Guard: filter out any curve that still can't be evaluated (e.g. invalid parametric range)
            vGroup.RemoveAll(x => { try { x.curve.Evaluate(0.5, true); return false; } catch { return true; } });
            hGroup.RemoveAll(x => { try { x.curve.Evaluate(0.5, true); return false; } catch { return true; } });

            if (vGroup.Count < 2)
                return new DimensionResult { Success = false, SkipReason = $"Ancho: solo {vGroup.Count} muro(s) válido(s) tras filtrar geometría inválida" };
            if (hGroup.Count < 2)
                return new DimensionResult { Success = false, SkipReason = $"Largo: solo {hGroup.Count} muro(s) válido(s) tras filtrar geometría inválida" };

            var vSorted = vGroup.OrderBy(x => x.curve.Evaluate(0.5, true).X).ToList();
            var hSorted = hGroup.OrderBy(x => x.curve.Evaluate(0.5, true).Y).ToList();

            var (wallV1, _) = vSorted.First();
            var (wallV2, _) = vSorted.Last();
            var (wallH1, _) = hSorted.First();
            var (wallH2, _) = hSorted.Last();

            // 4. Get room-facing references via HostObjectUtils
            Reference? refV1 = GetRoomFacingReference(wallV1, roomCenter);
            Reference? refV2 = GetRoomFacingReference(wallV2, roomCenter);
            Reference? refH1 = GetRoomFacingReference(wallH1, roomCenter);
            Reference? refH2 = GetRoomFacingReference(wallH2, roomCenter);

            if (refV1 == null || refV2 == null)
                return new DimensionResult { Success = false, SkipReason = "No se pudo resolver referencia de cara para muros de ancho" };
            if (refH1 == null || refH2 == null)
                return new DimensionResult { Success = false, SkipReason = "No se pudo resolver referencia de cara para muros de largo" };

            // 5. Build dimension lines using room bounding box
            BoundingBoxXYZ bb = room.get_BoundingBox(planView);
            if (bb == null)
                return new DimensionResult { Success = false, SkipReason = "Sin bounding box en la vista" };

            // OffsetMm en mm papel → pies modelo
            double offsetFt    = settings.OffsetMm * planView.Scale / 304.8;
            double minOffsetFt = 5.0 * planView.Scale / 304.8; // 5 mm papel mínimo
            if (offsetFt < minOffsetFt) offsetFt = minOffsetFt;
            double z = roomCenter.Z;

            // Width dimension: horizontal line INSIDE the room, offset inward from the bottom wall
            // The line runs parallel to X and is placed at bb.Min.Y + offset (going up into the room)
            double yWidth = bb.Min.Y + offsetFt;
            double halfSpanX = (bb.Max.X - bb.Min.X) * 0.5;
            var widthLine = Line.CreateBound(
                new XYZ(bb.Min.X + halfSpanX * 0.05, yWidth, z),
                new XYZ(bb.Max.X - halfSpanX * 0.05, yWidth, z));

            // Length dimension: vertical line INSIDE the room, offset inward from the left wall
            // The line runs parallel to Y and is placed at bb.Min.X + offset (going right into the room)
            double xLength = bb.Min.X + offsetFt;
            double halfSpanY = (bb.Max.Y - bb.Min.Y) * 0.5;
            var lengthLine = Line.CreateBound(
                new XYZ(xLength, bb.Min.Y + halfSpanY * 0.05, z),
                new XYZ(xLength, bb.Max.Y - halfSpanY * 0.05, z));

            // 6. Create dimensions
            var raWidth = new ReferenceArray();
            raWidth.Append(refV1);
            raWidth.Append(refV2);
            if (dimType != null)
                doc.Create.NewDimension(planView, widthLine, raWidth, dimType);
            else
                doc.Create.NewDimension(planView, widthLine, raWidth);

            var raLength = new ReferenceArray();
            raLength.Append(refH1);
            raLength.Append(refH2);
            if (dimType != null)
                doc.Create.NewDimension(planView, lengthLine, raLength, dimType);
            else
                doc.Create.NewDimension(planView, lengthLine, raLength);

            return new DimensionResult { Success = true };
        }

        private static Reference? GetRoomFacingReference(Wall wall, XYZ roomCenter)
        {
            try
            {
                var intFaces = HostObjectUtils.GetSideFaces(wall, ShellLayerType.Interior);
                var extFaces = HostObjectUtils.GetSideFaces(wall, ShellLayerType.Exterior);

                Reference? intRef = intFaces?.FirstOrDefault();
                Reference? extRef = extFaces?.FirstOrDefault();

                if (intRef == null) return extRef;
                if (extRef == null) return intRef;

                var intFace = wall.GetGeometryObjectFromReference(intRef) as PlanarFace;
                if (intFace == null) return intRef;

                XYZ toRoom = (roomCenter - intFace.Origin).Normalize();
                return toRoom.DotProduct(intFace.FaceNormal) > 0 ? intRef : extRef;
            }
            catch
            {
                return null;
            }
        }

        // ── Niveles ARQ: total + parciales ──

        /// <summary>
        /// Crea cotas totales y parciales entre niveles cuyo tipo contiene "ARQ".
        /// Usa los extremos de las curvas de nivel (inicio/fin/ambos) como posición base,
        /// igual que el esquema de rejillas. Total más cerca, parciales apiladas más lejos.
        /// Requiere una vista de sección o alzado.
        /// </summary>
        public static AcotadoVanosResult CreateArqLevelDimensions(
            Document doc,
            AcotadoVanosSettings settings)
        {
            var logger = ServiceLocator.IsRegistered<ILogger>() ? ServiceLocator.Get<ILogger>() : null;
            logger?.Info("[DoorDimensioningService] Iniciando CreateArqLevelDimensions...");
            try
            {
                var view = doc.ActiveView;
                if (view == null)
                    return new AcotadoVanosResult(0, 0, 0, "No hay vista activa.");

                // This scheme requires a section or elevation view (levels are not dimensionable in plan)
                if (view.ViewType != ViewType.Section && view.ViewType != ViewType.Elevation)
                    return new AcotadoVanosResult(0, 0, 0,
                        "Este esquema requiere una vista de sección o alzado. La vista activa es de tipo " + view.ViewType + ".");

                var dimTypeId = new ElementId(settings.DimensionTypeId);
                var dimType = doc.GetElement(dimTypeId) as DimensionType;

                // Collect levels whose LevelType name starts with "ARQ"
                var arqLevels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .Where(lv =>
                    {
                        var lvType = doc.GetElement(lv.GetTypeId());
                        return lvType != null && lvType.Name.IndexOf("ARQ", StringComparison.OrdinalIgnoreCase) >= 0;
                    })
                    .OrderBy(lv => lv.Elevation)
                    .ToList();

                if (arqLevels.Count < 2)
                    return new AcotadoVanosResult(0, arqLevels.Count, 0,
                        $"Se necesitan al menos 2 niveles ARQ para crear cotas (encontrados: {arqLevels.Count}).");

                // OffsetMm en mm modelo → pies; stackGap de "Forzado de cursor" (papel × escala → modelo)
                double offsetFeet          = settings.OffsetMm / 304.8;
                double minStackGapFeet     = 5.0 * view.Scale / 304.8;  // 5 mm papel → modelo
                double defaultStackGapFeet = 8.0 * view.Scale / 304.8;  // 8 mm papel → modelo

                // Use snap distance (same logic as grid dimensions)
                var rawGapLevel = GetDimLineSnapDistance(dimType);
                double stackGapFeet = rawGapLevel.HasValue
                    ? Math.Max(rawGapLevel.Value * view.Scale, minStackGapFeet)
                    : defaultStackGapFeet;

                // Also ensure at least 1.5× text height in model space
                if (dimType != null)
                {
                    try
                    {
                        var textSizeParam = dimType.get_Parameter(BuiltInParameter.TEXT_SIZE);
                        if (textSizeParam != null)
                        {
                            double textSizeFeet = textSizeParam.AsDouble();
                            stackGapFeet = Math.Max(stackGapFeet, textSizeFeet * view.Scale * 1.5);
                        }
                    }
                    catch { /* use default */ }
                }

                int created = 0;

                // In section/elevation, levels are horizontal lines.
                // We dimension vertically using the level curve endpoints
                // for positioning (just like grids use GetConsistentEndpoint).
                XYZ viewRight = view.RightDirection.Normalize();

                double lowestZ  = arqLevels.First().Elevation;
                double highestZ = arqLevels.Last().Elevation;

                bool hasPartial = arqLevels.Count > 2;

                // Total refs: first and last level only
                var totalRefs = new ReferenceArray();
                totalRefs.Append(new Reference(arqLevels.First()));
                totalRefs.Append(new Reference(arqLevels.Last()));

                // Partial refs: all levels
                var partialRefs = new ReferenceArray();
                foreach (var level in arqLevels)
                    partialRefs.Append(new Reference(level));

                var endpoints = GetGridEndpoints(settings.GridEndpoint);

                using (var tx = new Transaction(doc, "BIMPills: Cotas Niveles ARQ"))
                {
                    try
                    {
                        tx.Start();

                        foreach (var ep in endpoints)
                        {
                            bool useMax = (ep == 1);

                            // Get the actual endpoint X position from the level curves
                            // (like GetConsistentEndpoint does for grids).
                            // Level inherits from DatumPlane — use GetCurvesInView to get
                            // the visible extent in the current section/elevation view.
                            var refLevel = arqLevels.First();
                            var curves = refLevel.GetCurvesInView(DatumExtentType.ViewSpecific, view);
                            if (curves == null || curves.Count == 0)
                                curves = refLevel.GetCurvesInView(DatumExtentType.Model, view);
                            if (curves == null || curves.Count == 0) continue;

                            var refCurve = curves.First();
                            var p0 = refCurve.GetEndPoint(0);
                            var p1 = refCurve.GetEndPoint(1);

                            // Pick the endpoint in the viewRight direction
                            double d0 = p0.DotProduct(viewRight);
                            double d1 = p1.DotProduct(viewRight);
                            XYZ endpointPt = useMax ? (d0 >= d1 ? p0 : p1) : (d0 <= d1 ? p0 : p1);

                            // Offset direction: outward from the chosen endpoint
                            XYZ offsetDir = useMax ? viewRight : viewRight.Negate();

                            // Base X,Y from the level curve endpoint
                            double baseX = endpointPt.X;
                            double baseY = endpointPt.Y;

                            if (hasPartial)
                            {
                                // 1) Total — at user offset (closer to model)
                                var totalVec = offsetDir * offsetFeet;
                                var totalLine = Line.CreateBound(
                                    new XYZ(baseX + totalVec.X, baseY + totalVec.Y, lowestZ),
                                    new XYZ(baseX + totalVec.X, baseY + totalVec.Y, highestZ));
                                Dimension td = dimType != null
                                    ? doc.Create.NewDimension(view, totalLine, totalRefs, dimType)
                                    : doc.Create.NewDimension(view, totalLine, totalRefs);
                                if (td != null) created++;

                                // 2) Partial chain — stacked further out using adaptive gap
                                double partialDist = offsetFeet >= 0
                                    ? offsetFeet + stackGapFeet
                                    : offsetFeet - stackGapFeet;
                                var partialVec = offsetDir * partialDist;
                                var partialLine = Line.CreateBound(
                                    new XYZ(baseX + partialVec.X, baseY + partialVec.Y, lowestZ),
                                    new XYZ(baseX + partialVec.X, baseY + partialVec.Y, highestZ));
                                Dimension pd = dimType != null
                                    ? doc.Create.NewDimension(view, partialLine, partialRefs, dimType)
                                    : doc.Create.NewDimension(view, partialLine, partialRefs);
                                if (pd != null) created++;
                            }
                            else
                            {
                                // Only 2 levels — just total, no partial needed
                                var totalVec = offsetDir * offsetFeet;
                                var totalLine = Line.CreateBound(
                                    new XYZ(baseX + totalVec.X, baseY + totalVec.Y, lowestZ),
                                    new XYZ(baseX + totalVec.X, baseY + totalVec.Y, highestZ));
                                Dimension td = dimType != null
                                    ? doc.Create.NewDimension(view, totalLine, totalRefs, dimType)
                                    : doc.Create.NewDimension(view, totalLine, totalRefs);
                                if (td != null) created++;
                            }
                        }

                        if (created > 0)
                            tx.Commit();
                        else
                            tx.RollBack();
                    }
                    catch (Exception ex)
                    {
                        if (tx.GetStatus() == TransactionStatus.Started)
                            tx.RollBack();
                        logger?.Error("[DoorDimensioningService] Transacción revertida en CreateArqLevelDimensions", ex);
                        return new AcotadoVanosResult(0, 0, 0, $"Error durante la operación: {ex.Message}");
                    }
                }

                var msg = created > 0
                    ? $"Se crearon {created} cotas (totales + parciales) en {arqLevels.Count} niveles ARQ."
                    : "No se crearon cotas.";
                return new AcotadoVanosResult(created, arqLevels.Count, 0, msg);
            }
            catch (Exception ex)
            {
                logger?.Error("[DoorDimensioningService] Error en CreateArqLevelDimensions", ex);
                return new AcotadoVanosResult(0, 0, 0, $"Error inesperado al acotar niveles ARQ: {ex.Message}");
            }
        }

        // ── Helper methods for grid dimensioning ──

        /// <summary>
        /// Groups grids by direction (parallel grids go in the same group).
        /// Uses a tolerance of ~5 degrees for direction matching.
        /// </summary>
        private static List<List<Grid>> GroupGridsByDirection(List<Grid> grids)
        {
            var groups = new List<List<Grid>>();
            var used = new bool[grids.Count];

            for (int i = 0; i < grids.Count; i++)
            {
                if (used[i]) continue;

                var group = new List<Grid> { grids[i] };
                var dir = GetGridDirection(grids[i]);
                used[i] = true;

                for (int j = i + 1; j < grids.Count; j++)
                {
                    if (used[j]) continue;

                    var otherDir = GetGridDirection(grids[j]);
                    var cross = Math.Abs(dir.CrossProduct(otherDir).GetLength());
                    if (cross < 0.087) // ~5 degree tolerance (sin 5°)
                    {
                        group.Add(grids[j]);
                        used[j] = true;
                    }
                }

                groups.Add(group);
            }

            return groups;
        }

        /// <summary>
        /// Reads the "Dimension Line Snap Distance" from a DimensionType.
        /// Searches by BuiltInParameter and by name (EN/ES) to find the snap gap.
        /// Returns null if not found.
        /// </summary>
        private static double? GetDimLineSnapDistance(DimensionType? dimType)
        {
            if (dimType == null) return null;

            try
            {
                // Try common BuiltInParameters for dim line spacing
                var builtIns = new[]
                {
                    BuiltInParameter.DIM_LINE_EXTENSION,
                    BuiltInParameter.DIM_STYLE_DIM_LINE_SNAP_DIST,
                };
                foreach (var bip in builtIns)
                {
                    try
                    {
                        var param = dimType.get_Parameter(bip);
                        if (param != null && param.HasValue && param.StorageType == StorageType.Double)
                        {
                            var val = param.AsDouble();
                            if (val > 0) return val;
                        }
                    }
                    catch { /* BIP not available in this Revit version */ }
                }

                // Fallback: search all parameters by name (English + Spanish)
                var keywords = new[] { "snap dist", "forzado", "dim line snap", "línea de cota" };
                foreach (Parameter p in dimType.Parameters)
                {
                    var name = (p.Definition?.Name ?? "").ToLowerInvariant();
                    foreach (var kw in keywords)
                    {
                        if (name.Contains(kw) && p.HasValue && p.StorageType == StorageType.Double)
                        {
                            var val = p.AsDouble();
                            if (val > 0) return val;
                        }
                    }
                }
            }
            catch { /* Parameter not available */ }

            return null;
        }

        /// <summary>
        /// Finds the offset distance of an existing dimension near a grid endpoint.
        /// Returns the offset in feet from the endpoint, or null if no existing dim found.
        /// </summary>
        private static double? FindExistingGridDimOffset(
            Document doc, View view,
            Grid firstGrid, Grid lastGrid,
            int endpoint, XYZ gridDirection, XYZ perpendicular)
        {
            try
            {
                var dims = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(Dimension))
                    .Cast<Dimension>()
                    .Where(d => d.Curve is Line);

                var firstId = firstGrid.Id;
                var lastId = lastGrid.Id;
                var endpointPos = firstGrid.Curve.GetEndPoint(endpoint);

                foreach (var dim in dims)
                {
                    // Check if this dimension references any of our grids
                    bool refsFirst = false, refsLast = false;
                    try
                    {
                        foreach (Reference r in dim.References)
                        {
                            if (r.ElementId == firstId) refsFirst = true;
                            if (r.ElementId == lastId) refsLast = true;
                        }
                    }
                    catch { continue; }

                    if (!refsFirst || !refsLast) continue;

                    // This dim references both extreme grids — it's likely a total dim
                    // Calculate its offset from the grid endpoint
                    var dimLine = dim.Curve as Line;
                    if (dimLine == null) continue;

                    var dimMidpoint = (dimLine.GetEndPoint(0) + dimLine.GetEndPoint(1)) / 2.0;
                    var dist = Math.Abs((dimMidpoint - endpointPos).DotProduct(gridDirection));
                    return dist;
                }
            }
            catch { /* Could not search for existing dims */ }

            return null;
        }

        /// <summary>
        /// Returns which grid curve endpoints to use for dimension placement.
        /// 0 = start, 1 = end of grid curve.
        /// </summary>
        private static int[] GetGridEndpoints(string gridEndpoint)
        {
            return gridEndpoint switch
            {
                "start" => new[] { 0 },
                "both" => new[] { 0, 1 },
                _ => new[] { 1 }, // "end" default
            };
        }

        private static XYZ GetGridDirection(Grid grid)
        {
            var curve = grid.Curve;
            if (curve is Line line)
                return line.Direction.Normalize();

            // Fallback for non-linear grids: use chord direction
            var start = curve.GetEndPoint(0);
            var end = curve.GetEndPoint(1);
            return (end - start).Normalize();
        }

        private static XYZ GetGridMidpoint(Grid grid)
        {
            var curve = grid.Curve;
            var start = curve.GetEndPoint(0);
            var end = curve.GetEndPoint(1);
            return (start + end) / 2.0;
        }

        /// <summary>
        /// Returns the endpoint of a grid curve that is furthest in the given direction
        /// (useMax=true) or furthest against it (useMax=false), regardless of draw order.
        /// </summary>
        private static XYZ GetConsistentEndpoint(Grid grid, XYZ direction, bool useMax)
        {
            var curve = grid.Curve;
            var p0 = curve.GetEndPoint(0);
            var p1 = curve.GetEndPoint(1);
            double d0 = p0.DotProduct(direction);
            double d1 = p1.DotProduct(direction);
            return useMax ? (d0 >= d1 ? p0 : p1) : (d0 <= d1 ? p0 : p1);
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

        // ── Vanos exteriores: longitud de muros de fachada ──

        /// <summary>
        /// Crea una cota de longitud total para cada muro exterior visible en la vista activa.
        /// Los muros se detectan por la propiedad WallType.Function == Exterior; si el modelo
        /// no tiene ninguno marcado así, se dimensionan todos los muros de la vista.
        /// Requiere una vista en planta.
        /// </summary>
        public static AcotadoVanosResult CreateExteriorWallDimensions(
            Document doc,
            AcotadoVanosSettings settings)
        {
            var logger = ServiceLocator.IsRegistered<ILogger>() ? ServiceLocator.Get<ILogger>() : null;
            logger?.Info("[DoorDimensioningService] Iniciando CreateExteriorWallDimensions...");
            try
            {
                var view = doc.ActiveView;
                if (view == null)
                    return new AcotadoVanosResult(0, 0, 0, "No hay vista activa.");

                if (view.ViewType != ViewType.FloorPlan && view.ViewType != ViewType.CeilingPlan)
                    return new AcotadoVanosResult(0, 0, 0,
                        "Este esquema requiere una vista en planta. La vista activa es de tipo " + view.ViewType + ".");

                var dimTypeId = new ElementId(settings.DimensionTypeId);
                var dimType   = doc.GetElement(dimTypeId) as DimensionType;

                double offsetFeet = settings.OffsetMm / 304.8;

                // Collect walls visible in the current view
                var allWalls = new FilteredElementCollector(doc, view.Id)
                    .OfCategory(BuiltInCategory.OST_Walls)
                    .WhereElementIsNotElementType()
                    .Cast<Wall>()
                    .Where(w => (w.Location as LocationCurve)?.Curve is Line)
                    .ToList();

                if (allWalls.Count == 0)
                    return new AcotadoVanosResult(0, 0, 0, "No se encontraron muros en la vista activa.");

                // Prefer walls whose WallType is marked Exterior; fall back to all walls
                var exteriorWalls = allWalls
                    .Where(w => w.WallType?.Function == WallFunction.Exterior)
                    .ToList();

                var targetWalls = exteriorWalls.Count > 0 ? exteriorWalls : allWalls;

                int created   = 0;
                int skipped   = 0;
                int processed = 0;

                var options = new Options { ComputeReferences = true, View = view };

                using (var tx = new Transaction(doc, "BIMPills: Vanos Exteriores"))
                {
                    try
                    {
                        tx.Start();

                        foreach (var wall in targetWalls)
                        {
                            processed++;
                            try
                            {
                                var wallCurve = (wall.Location as LocationCurve)!.Curve as Line;
                                if (wallCurve == null) { skipped++; continue; }

                                var dim = CreateExteriorWallLengthDimension(
                                    doc, view, wall, wallCurve, dimType, offsetFeet, options);

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
                    catch (Exception ex)
                    {
                        if (tx.GetStatus() == TransactionStatus.Started)
                            tx.RollBack();
                        logger?.Error("[DoorDimensioningService] Transacción revertida en CreateExteriorWallDimensions", ex);
                        return new AcotadoVanosResult(0, 0, 0, $"Error durante la operación: {ex.Message}");
                    }
                }

                var fallbackNote = exteriorWalls.Count == 0
                    ? " (ningún muro marcado como Exterior — se acotaron todos los muros)"
                    : "";
                var msg = created > 0
                    ? $"Se crearon {created} cotas en {processed} muros{fallbackNote}."
                    : $"No se crearon cotas nuevas.{fallbackNote}";
                if (skipped > 0)
                    msg += $" {skipped} muros omitidos.";

                return new AcotadoVanosResult(created, processed, skipped, msg);
            }
            catch (Exception ex)
            {
                logger?.Error("[DoorDimensioningService] Error en CreateExteriorWallDimensions", ex);
                return new AcotadoVanosResult(0, 0, 0, $"Error inesperado al acotar vanos exteriores: {ex.Message}");
            }
        }

        /// <summary>
        /// Crea una cota de longitud total de un muro usando las caras extremas como referencias.
        /// La cota se coloca en el lado exterior del muro a la distancia especificada por offsetFeet.
        /// </summary>
        private static Dimension? CreateExteriorWallLengthDimension(
            Document doc, View view, Wall wall, Line wallCurve,
            DimensionType? dimType, double offsetFeet, Options geomOptions)
        {
            var wallDir    = wallCurve.Direction.Normalize();
            var wallNormal = new XYZ(-wallDir.Y, wallDir.X, 0).Normalize();
            var wallStart  = wallCurve.GetEndPoint(0);

            var refArray = new ReferenceArray();

            // Scan wall geometry for faces perpendicular to the wall direction (i.e. the end-cap faces)
            var wallGeom = wall.get_Geometry(geomOptions);
            if (wallGeom == null) return null;

            var endCapFaces = new List<(Face face, Reference reference, double projection)>();

            foreach (var gObj in wallGeom)
            {
                var solid = gObj as Solid;
                if (solid == null || solid.Faces.Size == 0) continue;

                foreach (Face face in solid.Faces)
                {
                    Reference? faceRef = face.Reference;
                    if (faceRef == null) continue;

                    // End-cap faces are perpendicular to the wall (normal is parallel to wallDir)
                    if (face is PlanarFace pf)
                    {
                        var normal = pf.FaceNormal.Normalize();
                        double dotAlongWall = Math.Abs(normal.DotProduct(wallDir));
                        if (dotAlongWall < 0.95) continue; // not an end-cap face

                        var facePt = pf.Origin;
                        double proj = (facePt - wallStart).DotProduct(wallDir);
                        endCapFaces.Add((face, faceRef, proj));
                    }
                }
            }

            if (endCapFaces.Count < 2) return null;

            // Use the two extreme faces (min and max projection along wall) as dimension references
            var sorted   = endCapFaces.OrderBy(f => f.projection).ToList();
            var startRef = sorted.First().reference;
            var endRef   = sorted.Last().reference;

            refArray.Append(startRef);
            refArray.Append(endRef);

            if (refArray.Size < 2) return null;

            // Place dimension line on the exterior side of the wall
            // Determine exterior direction: use wall normal, offset outward
            var wallHalfThick = wall.Width / 2.0;
            var wallMid       = wallCurve.Evaluate(0.5, true);

            // Offset the dimension line to the exterior face + extra offset
            var dimPt   = wallMid + wallNormal * (wallHalfThick + offsetFeet);
            var dimLine = Line.CreateBound(
                dimPt - wallDir * 1.0,  // extend slightly past wall ends
                dimPt + wallDir * 1.0);

            try
            {
                return doc.Create.NewDimension(view, dimLine, refArray, dimType);
            }
            catch
            {
                return null;
            }
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
