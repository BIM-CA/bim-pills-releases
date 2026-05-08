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
                var raw = _doc.PathName?.Trim().TrimEnd('\0');
                if (string.IsNullOrEmpty(raw)) return 0;

                var path = raw!.Replace('/', Path.DirectorySeparatorChar);

                if (!IsCloudOrServerPath(raw))
                {
                    try { var fi = new FileInfo(path); if (fi.Exists && fi.Length > 0) return fi.Length; } catch { }
                    try
                    {
                        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                                                      FileShare.ReadWrite | FileShare.Delete);
                        if (fs.Length > 0) return fs.Length;
                    }
                    catch { }
                }

                var title = _doc.Title;
                if (!string.IsNullOrEmpty(title))
                    return FindFileSizeByTitle(title);
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// Retorna true si el path es una URL de servidor/nube que FileInfo no puede resolver
        /// como ruta de sistema de archivos local (RSN://, BIM 360://, Autodesk Docs://, etc.).
        /// </summary>
        private static bool IsCloudOrServerPath(string path) =>
            path.IndexOf("://", StringComparison.Ordinal) >= 0 ||
            path.StartsWith("RSN:", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("BIM 360:", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("BIM360:", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("Autodesk Docs:", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Busca un archivo .rvt cuyo nombre comience con <paramref name="title"/> en las
        /// ubicaciones locales donde Revit guarda cachés y copias de modelos colaborativos.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private long FindFileSizeByTitle(string title)
        {
            var titleNoExt = title.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase)
                ? title.Substring(0, title.Length - 4) : title;

            var year      = _doc.Application.VersionNumber;
            var appData   = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var profile   = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var userDocs  = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var revitBase = Path.Combine(appData, "Autodesk", "Revit", $"Autodesk Revit {year}");

            // Carpetas con archivos nombrados normalmente (Desktop Connector, Revit Server, Documentos)
            var namedDirs = new[]
            {
                (Path.Combine(profile, "ACCDocs"),            SearchOption.AllDirectories),
                (Path.Combine(profile, "BIM 360"),            SearchOption.AllDirectories),
                (Path.Combine(profile, "Autodesk Docs"),      SearchOption.AllDirectories),
                (Path.Combine(revitBase, "RevitServerCache"), SearchOption.AllDirectories),
                (userDocs,                                     SearchOption.TopDirectoryOnly),
            };

            foreach (var (dir, opt) in namedDirs)
            {
                if (!Directory.Exists(dir)) continue;
                try
                {
                    var candidates = Directory.GetFiles(dir, "*.rvt", opt)
                        .Where(f =>
                        {
                            var fn = Path.GetFileNameWithoutExtension(f);
                            return fn.Equals(titleNoExt, StringComparison.OrdinalIgnoreCase) ||
                                   fn.StartsWith(titleNoExt + "_", StringComparison.OrdinalIgnoreCase);
                        })
                        .OrderByDescending(f => { try { return new FileInfo(f).LastWriteTime; } catch { return DateTime.MinValue; } });

                    foreach (var candidate in candidates)
                    {
                        try { var fi = new FileInfo(candidate); if (fi.Exists && fi.Length > 0) return fi.Length; } catch { }
                    }
                }
                catch { }
            }

            // CollaborationCache: BIM 360/ACC/Revit Server guardan copias locales con nombres GUID.
            // Tomamos el .rvt más recientemente modificado (= modelo activo en sesión).
            var collabCache = Path.Combine(revitBase, "CollaborationCache");
            if (Directory.Exists(collabCache))
            {
                try
                {
                    var best = Directory.GetFiles(collabCache, "*.rvt", SearchOption.AllDirectories)
                        .Select(f => { try { var fi = new FileInfo(f); return (Size: fi.Length, Modified: fi.LastWriteTime); } catch { return (Size: 0L, Modified: DateTime.MinValue); } })
                        .Where(x => x.Size > 0)
                        .OrderByDescending(x => x.Modified)
                        .FirstOrDefault();
                    if (best.Size > 0) return best.Size;
                }
                catch { }
            }

            // Último recurso: subdirectorios de Documentos
            try
            {
                foreach (var sub in Directory.EnumerateDirectories(userDocs))
                {
                    try
                    {
                        var hit = Directory.GetFiles(sub, "*.rvt", SearchOption.TopDirectoryOnly)
                            .FirstOrDefault(f =>
                            {
                                var fn = Path.GetFileNameWithoutExtension(f);
                                return fn.Equals(titleNoExt, StringComparison.OrdinalIgnoreCase) ||
                                       fn.StartsWith(titleNoExt + "_", StringComparison.OrdinalIgnoreCase);
                            });
                        if (hit != null) { var fi = new FileInfo(hit); if (fi.Exists && fi.Length > 0) return fi.Length; }
                    }
                    catch { }
                }
            }
            catch { }

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

        /// <summary>
        /// Clases de elementos internos de Revit que nunca tienen categoría asignada
        /// por diseño (parámetros del sistema, fases, patrones, impresión, etc.).
        /// Estos NUNCA deben aparecer como "elementos huérfanos" para el usuario:
        /// son esenciales para la operación de Revit y su eliminación puede corromper
        /// el modelo o bloquear operaciones críticas (ej. "no se permite suprimir
        /// todas las vistas abiertas del proyecto").
        /// </summary>
        private static readonly HashSet<string> _systemOrphanExclusions = new HashSet<string>
        {
            // Parámetros de proyecto y compartidos
            "ParameterElement", "SharedParameterElement", "GlobalParameter",
            "InternalDefinition", "ExternalDefinition",

            // Fases del proyecto
            "Phase", "PhaseFilter",

            // Información general del proyecto
            "ProjectInfo", "DocumentVersion",

            // Patrones de línea y relleno (recursos del sistema)
            "LinePatternElement", "FillPatternElement",

            // Visualización e impresión
            "WorksharingDisplaySettings", "PrintSetting", "PrintManager",
            "BrowserOrganization", "ViewSheetSet",

            // Esquemas de área
            "AreaScheme",

            // Revisiones
            "Revision", "RevisionSettings",

            // Sol y sombras
            "SunAndShadowSettings",

            // Origen y punto base
            "BasePoint", "InternalOrigin",

            // Planos de boceto y referencia
            "SketchPlane", "ReferencePlane",

            // Análisis energético
            "EnergyAnalysisDetailModel", "EnergyAnalysisSpace",
            "EnergyAnalysisSurface", "EnergyAnalysisOpeningBase",
            "EnergyAnalysisLineSurface",

            // Análisis y resultados
            "AnalysisResultSchema", "AnalysisDisplayStyle",

            // Filtros de selección
            "SelectionFilterElement",

            // Navegación
            "ViewNavigationToolSettings",

            // Worksharing (colaborativo)
            "WorksharingTooltipInfo",

            // Estructural
            "StructuralResultSchemaDescription",

            // Conjuntos de propiedades (IFC/COBie)
            "PropertySetElement",

            // Notas clave del sistema
            "KeynoteTable",

            // Editor de forma de losa (interno)
            "SlabShapeEditor",

            // Líneas de cuadrícula de muro cortina (interno)
            "CurtainGridLine",

            // Boceto del sistema
            "Sketch",

            // Materiales y apariencia (internos)
            "MaterialQuantities",
            "AppearanceAssetElement",
        };

        public IReadOnlyList<ElementInfo> GetElementsWithoutCategory()
        {
            var orphans = new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType()
                .Where(e =>
                {
                    // Solo elementos sin categoría
                    if (e.Category != null) return false;

                    // Excluir tipos del sistema conocidos (por nombre de clase Revit API)
                    var typeName = e.GetType().Name;
                    if (_systemOrphanExclusions.Contains(typeName)) return false;

                    // Excluir elementos con nombres del sistema Revit (<Solid fill>, <None>, …)
                    var name = e.Name ?? "";
                    if (name.StartsWith("<") && name.EndsWith(">")) return false;

                    // Excluir IDs negativos (elementos inválidos)
                    if (GetElementIdValue(e.Id) < 0) return false;

                    return true;
                })
                .ToList();

            return orphans.Select(e =>
            {
                var className   = FriendlyClassName(e);
                var description = OrphanDescription(e);

                // Elemento anclado (Pinned) → nunca se puede eliminar
                if (e.Pinned)
                    return new ElementInfo(GetElementIdValue(e.Id), e.Name ?? "(sin nombre)", null, className, false, description);

                // Lista blanca estricta: solo tipos explícitamente seguros pueden
                // eliminarse. Si llegara algún tipo del sistema que no esté en
                // _systemOrphanExclusions, caerá aquí como NO purgable — previene
                // crashes de Revit por eliminación de elementos críticos.
                bool canDelete = e.GetType().Name switch
                {
                    "ImportInstance"    => SafeImportInstanceDelete(e),
                    "RevitLinkInstance" => true,  // enlaces Revit pueden eliminarse
                    "Group"              => true, // grupos sin categoría son seguros
                    _                    => false // todo lo demás: no eliminar
                };

                return new ElementInfo(
                    GetElementIdValue(e.Id),
                    e.Name ?? "(sin nombre)",
                    null,
                    className,
                    canDelete,
                    description);
            }).ToList();
        }

        private static bool SafeImportInstanceDelete(Element e)
        {
            try
            {
                // Un ImportInstance vinculado (CAD link) debe desenlazarse antes
                // de eliminar — marcarlo como no-purgable evita errores de Revit.
                if (e is ImportInstance imp && imp.IsLinked) return false;
            }
            catch { /* IsLinked puede no estar disponible en todas las versiones */ }
            return true;
        }

        private static string FriendlyClassName(Element e)
        {
            return e.GetType().Name switch
            {
                "ImportInstance"     => "Importación CAD",
                "RevitLinkInstance"  => "Enlace Revit",
                "Group"              => "Grupo",
                "ModelLine"          => "Línea de modelo",
                "DetailLine"         => "Línea de detalle",
                "ModelCurve"         => "Curva de modelo",
                "DetailCurve"        => "Curva de detalle",
                "LinePatternElement" => "Patrón de línea",
                "FillPatternElement" => "Patrón de relleno",
                "ParameterElement"   => "Parámetro de proyecto",
                "Phase"              => "Fase del proyecto",
                "ProjectInfo"        => "Información del proyecto",
                var n                => n
            };
        }

        /// <summary>
        /// Descripción legible del elemento huérfano, explicando qué es, cómo llegó ahí,
        /// y si es seguro eliminarlo. Se muestra como tooltip para que el usuario
        /// decida con información suficiente.
        /// </summary>
        private static string OrphanDescription(Element e)
        {
            return e.GetType().Name switch
            {
                "ImportInstance" => e is ImportInstance imp && imp.IsLinked
                    ? "Enlace CAD (DWG/DXF) vinculado externamente. Para eliminarlo, desvincúlalo primero desde Administrar → Vínculos."
                    : "Importación CAD embebida en el modelo. Si ya no la necesitas, puede eliminarse para reducir el peso del archivo.",

                "RevitLinkInstance" =>
                    "Enlace a otro archivo Revit. Si el enlace está roto o no se usa, es seguro eliminarlo.",

                "Group" =>
                    "Grupo de elementos sin categoría asignada. Verifica que no tenga instancias activas en el modelo antes de eliminar.",

                "ModelLine" or "ModelCurve" =>
                    "Línea o curva de modelo sin categoría asignada. Normalmente es residuo de edición.",

                "DetailLine" or "DetailCurve" =>
                    "Línea o curva de detalle sin categoría asignada. Normalmente es residuo de edición.",

                var n =>
                    $"Elemento de tipo '{n}' sin categoría en Revit. No está clasificado como seguro para eliminación automática — revísalo manualmente antes de actuar."
            };
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

            // Tipos de familia sin uso (FamilySymbol con 0 instancias, en familias que sí tienen otros tipos usados)
            try
            {
                // Recopilar todos los TypeIds en uso en una sola pasada (más eficiente que FamilyInstanceFilter por tipo)
                var usedTypeIds = new FilteredElementCollector(_doc)
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .Select(fi => fi.GetTypeId())
                    .ToHashSet();

                var allSymbols = new FilteredElementCollector(_doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .Where(s => !usedTypeIds.Contains(s.Id))
                    .ToList();

                foreach (var symbol in allSymbols)
                {
                    try
                    {
                        var family = symbol.Family;
                        if (family == null) continue;

                        var allTypeIds = family.GetFamilySymbolIds();

                        // No se puede borrar el último tipo de una familia
                        if (allTypeIds.Count < 2) continue;

                        // Si ningún tipo de esta familia está en uso, ya queda cubierta por "Familia"
                        if (!allTypeIds.Any(id => usedTypeIds.Contains(id))) continue;

                        var category = family.FamilyCategory?.Name ?? "Sin categoría";
                        purgeable.Add(new PurgeableItem(
                            GetElementIdValue(symbol.Id),
                            $"{family.Name} : {symbol.Name}",
                            category,
                            "Tipo familia",
                            0));
                    }
                    catch { /* símbolo individual no crítico */ }
                }
            }
            catch { /* No crítico */ }

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

            // Estilos de texto sin uso (TextNoteType)
            try
            {
                var usedTextTypeIds = new FilteredElementCollector(_doc)
                    .OfClass(typeof(TextNote))
                    .Cast<TextNote>()
                    .Select(tn => tn.GetTypeId())
                    .ToHashSet();

                var textTypes = new FilteredElementCollector(_doc)
                    .OfClass(typeof(TextNoteType))
                    .Cast<TextNoteType>()
                    .Where(t => !usedTextTypeIds.Contains(t.Id))
                    .ToList();

                foreach (var t in textTypes)
                    purgeable.Add(new PurgeableItem(
                        GetElementIdValue(t.Id), t.Name,
                        "Estilos de texto", "Estilo texto", 0));
            }
            catch { /* No crítico */ }

            // Tipos de cota sin uso (DimensionType)
            try
            {
                var usedDimTypeIds = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Dimension))
                    .Cast<Dimension>()
                    .Select(d => d.GetTypeId())
                    .ToHashSet();

                var dimTypes = new FilteredElementCollector(_doc)
                    .OfClass(typeof(DimensionType))
                    .Cast<DimensionType>()
                    .Where(t => !usedDimTypeIds.Contains(t.Id))
                    .ToList();

                foreach (var t in dimTypes)
                    purgeable.Add(new PurgeableItem(
                        GetElementIdValue(t.Id), t.Name,
                        "Tipos de cota", "Tipo cota", 0));
            }
            catch { /* No crítico */ }

            // Tipos de región rellena sin uso (FilledRegionType)
            try
            {
                var usedFilledRegionTypeIds = new FilteredElementCollector(_doc)
                    .OfClass(typeof(FilledRegion))
                    .Cast<FilledRegion>()
                    .Select(fr => fr.GetTypeId())
                    .ToHashSet();

                var filledRegionTypes = new FilteredElementCollector(_doc)
                    .OfClass(typeof(FilledRegionType))
                    .Cast<FilledRegionType>()
                    .Where(t => !usedFilledRegionTypeIds.Contains(t.Id))
                    .ToList();

                foreach (var t in filledRegionTypes)
                    purgeable.Add(new PurgeableItem(
                        GetElementIdValue(t.Id), t.Name,
                        "Regiones rellenas", "Patron relleno", 0));
            }
            catch { /* No crítico */ }

            // Plantillas de vista sin uso (IsTemplate == true y ninguna vista las referencia)
            try
            {
                // IDs de plantillas usadas: vistas no-plantilla que tienen ViewTemplateId asignado
                var usedTemplateIds = new FilteredElementCollector(_doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => !v.IsTemplate && v.ViewTemplateId != ElementId.InvalidElementId)
                    .Select(v => v.ViewTemplateId)
                    .ToHashSet();

                var unusedTemplates = new FilteredElementCollector(_doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => v.IsTemplate && !usedTemplateIds.Contains(v.Id))
                    .ToList();

                foreach (var t in unusedTemplates)
                    purgeable.Add(new PurgeableItem(
                        GetElementIdValue(t.Id),
                        t.Name,
                        MapViewTypeToSpanish(t.ViewType),
                        "Plantilla vista",
                        0));
            }
            catch { /* No crítico */ }

            // Filtros de vista sin uso (ParameterFilterElement no aplicado en ninguna vista ni plantilla)
            try
            {
                // Recorre TODAS las vistas (incluyendo plantillas) para recolectar filtros en uso
                var usedFilterIds = new FilteredElementCollector(_doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .SelectMany(v =>
                    {
                        try { return v.GetFilters().AsEnumerable(); }
                        catch { return Enumerable.Empty<ElementId>(); }
                    })
                    .ToHashSet();

                var unusedFilters = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ParameterFilterElement))
                    .Cast<ParameterFilterElement>()
                    .Where(f => !usedFilterIds.Contains(f.Id))
                    .ToList();

                foreach (var f in unusedFilters)
                    purgeable.Add(new PurgeableItem(
                        GetElementIdValue(f.Id),
                        f.Name,
                        "Filtros de vista",
                        "Filtro vista",
                        0));
            }
            catch { /* No crítico */ }

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

        private static string MapViewTypeToSpanish(ViewType vt) => vt switch
        {
            ViewType.FloorPlan    => "Planta",
            ViewType.CeilingPlan  => "Planta de techo",
            ViewType.Elevation    => "Alzado",
            ViewType.Section      => "Sección",
            ViewType.ThreeD       => "Vista 3D",
            ViewType.Legend       => "Leyenda",
            ViewType.DraftingView => "Vista de boceto",
            ViewType.AreaPlan     => "Plano de área",
            ViewType.Detail       => "Vista de detalle",
            _                     => "Vista"
        };

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

        public string GetModelIdentifier()
        {
            // Document.PathName es única por archivo (ruta completa en disco
            // o cloud path para modelos workshared). ProjectInformation.Name
            // suele compartirse entre modelos (valor del template) y NO sirve
            // para diferenciar conjuntos por modelo.
            try
            {
                var path = _doc.PathName;
                if (!string.IsNullOrWhiteSpace(path)) return path;
                // Modelo no guardado — caemos al Title (suele ser "Project1" etc.)
                return _doc.Title ?? string.Empty;
            }
            catch
            {
                try { return _doc.Title ?? string.Empty; }
                catch { return string.Empty; }
            }
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
                var vs = _doc.GetElement(new ElementId(scheduleId)) as ViewSchedule;
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

                // Sample the first element to classify each field as type vs instance.
                // We do this before reading all rows so the per-column info is available.
                Element? sampleElem = null;
                Element? sampleType = null;
                try
                {
                    sampleElem = new FilteredElementCollector(_doc, vs.Id)
                        .WhereElementIsNotElementType()
                        .ToElements()
                        .FirstOrDefault(e => e is not RevitLinkInstance);

                    if (sampleElem != null)
                    {
                        var typeId = sampleElem.GetTypeId();
                        if (typeId != ElementId.InvalidElementId)
                            sampleType = _doc.GetElement(typeId);
                    }
                }
                catch { }

                var columns = fields.Select(f =>
                {
                    bool readOnly = f.IsCalculatedField || f.ParameterId == ElementId.InvalidElementId;
                    bool isType   = false;

                    if (!readOnly && sampleElem != null)
                    {
                        // If param is found directly on the instance it's an instance param.
                        bool onInstance = sampleElem.Parameters
                            .Cast<Parameter>()
                            .Any(p => p.Id == f.ParameterId);

                        if (!onInstance && sampleType != null)
                        {
                            bool onType = sampleType.Parameters
                                .Cast<Parameter>()
                                .Any(p => p.Id == f.ParameterId);
                            isType = onType;
                        }
                    }

                    return new ScheduleColumnInfo
                    {
                        Name            = f.ColumnHeading,
                        ParameterName   = f.GetName(),
                        IsReadOnly      = readOnly,
                        IsTypeParameter = isType,
                        StorageType     = "String"
                    };
                }).ToList();

                // Get only the actual elements in this schedule (excludes totals,
                // group headers, subtotals, and grand totals entirely).
                // Filters out RevitLinkInstance objects — they appear when the schedule
                // includes linked models but their parameters don't match host-model fields.
                var elements = new List<Element>();
                try
                {
                    elements = new FilteredElementCollector(_doc, vs.Id)
                        .WhereElementIsNotElementType()
                        .ToElements()
                        .Where(e => e is not RevitLinkInstance)
                        .ToList();
                }
                catch { }

                // Read parameter values directly from each element — this guarantees
                // only real data rows, with no totals or group headers.
                var rows       = new List<List<string>>();
                var elementIds = new List<long>();

                foreach (var element in elements)
                {
                    // Pre-fetch the type element once per element (type params like
                    // Función, Anchura, Altura are often defined on the FamilySymbol)
                    Element? typeElem = null;
                    try
                    {
                        var typeId = element.GetTypeId();
                        if (typeId != ElementId.InvalidElementId)
                            typeElem = _doc.GetElement(typeId);
                    }
                    catch { }

                    var cellValues = new List<string>();
                    foreach (var field in fields)
                    {
                        string val = "";
                        try
                        {
                            if (!field.IsCalculatedField && field.ParameterId != ElementId.InvalidElementId)
                            {
                                Parameter? param = null;

                                // 1. Search instance parameter collection by ParameterId.
                                //    Note: element.Parameters includes ONLY instance-level params;
                                //    type params (Función, Nota clave, Marca de tipo…) are NOT
                                //    always exposed here — they require the type element lookup.
                                param = element.Parameters
                                    .Cast<Parameter>()
                                    .FirstOrDefault(p => p.Id == field.ParameterId);

                                // 2. If not found (or has no value) on the instance, search the
                                //    FamilySymbol (type element) directly. This covers:
                                //      • Función (FUNCTION_PARAM)
                                //      • Nota clave (KEYNOTE_PARAM)
                                //      • Marca de tipo (ALL_MODEL_TYPE_MARK)
                                //      • Any project/shared param defined at type level.
                                if ((param == null || !param.HasValue) && typeElem != null)
                                    param = typeElem.Parameters
                                        .Cast<Parameter>()
                                        .FirstOrDefault(p => p.Id == field.ParameterId);

                                // 3. Fallback: name-based lookup on instance then type.
                                //    Catches shared params whose definition ElementId may differ
                                //    from what the schedule field reports (e.g. cross-doc mismatch).
                                if (param == null || !param.HasValue)
                                {
                                    var fieldName = field.GetName();
                                    var byName = element.LookupParameter(fieldName)
                                              ?? typeElem?.LookupParameter(fieldName);
                                    if (byName != null && byName.HasValue) param = byName;
                                }

                                if (param != null && param.HasValue)
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
                        var element = _doc.GetElement(new ElementId(req.ElementId));
                        if (element == null) { skipped++; continue; }

                        // 1. Try instance element first
                        Parameter? param = element.LookupParameter(req.ParameterName);

                        // 2. If not writable on the instance, try the type element.
                        //    Type params (Función, Descripción, Anchura, Altura, Nota clave…)
                        //    are defined on the FamilySymbol, not on the instance.
                        if (param == null || param.IsReadOnly)
                        {
                            try
                            {
                                var typeId = element.GetTypeId();
                                if (typeId != ElementId.InvalidElementId)
                                {
                                    var typeElem = _doc.GetElement(typeId);
                                    var tp = typeElem?.LookupParameter(req.ParameterName);
                                    if (tp != null && !tp.IsReadOnly)
                                        param = tp;
                                }
                            }
                            catch { }
                        }

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
                                // Values exported via AsValueString() include display units (e.g. "3.00 m").
                                // Try to re-parse via SetValueString first; fall back to raw double parse.
                                try { param.SetValueString(req.NewValue ?? ""); }
                                catch
                                {
                                    if (double.TryParse(req.NewValue,
                                        System.Globalization.NumberStyles.Any,
                                        System.Globalization.CultureInfo.InvariantCulture, out double dv))
                                        param.Set(dv);
                                    else { skipped++; continue; }
                                }
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
#pragma warning disable CS0618 // IntegerValue obsoleto en 2024 — necesario para net48
            return id.IntegerValue;
#pragma warning restore CS0618
#else
            return (int)id.Value;
#endif
        }
    }
}
