using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
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
        private readonly Action<int, int, string>? _onProgress;

        public RevitDocumentServices(Document doc, Action<int, int, string>? onProgress = null)
        {
            _doc        = doc;
            _onProgress = onProgress;
        }

        private void ReportProgress(int current, int total, string phase)
            => _onProgress?.Invoke(current, total, phase);

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

            ReportProgress(0, 100, "Escaneando referencias del modelo...");

            // ── Single-pass: build the complete "in-use type IDs" set.
            // This replaces per-family/per-symbol collector calls with O(1) HashSet lookups.
            var inUse = BuildInUseTypeIdSet();

            ReportProgress(20, 100, "Verificando familias...");

            // ── Collect Revit default element type IDs.
            // Default types (e.g. the project's default dimension style) cannot be deleted —
            // Revit hangs instead of raising a failure when doc.Delete() is called on them.
            var defaultTypeIds = BuildDefaultTypeIdSet();

            // ── Familias sin instancias
            var families = new FilteredElementCollector(_doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .ToList();

            foreach (var family in families)
            {
                if (family.IsInPlace) continue;
                if (!family.IsEditable) continue;   // built-in system families (mullions, panels…) — not deletable

                // Profile families (OST_ProfileFamilies) are referenced by TopRailType,
                // HandRailType, ContinuousRailType, WallSweepType, StairsStringerType, etc.
                // Revit stores those references via internal data structures that are NOT
                // exposed in elem.Parameters — ScanParamsDeep cannot see them regardless
                // of how broadly we scan. Excluding by category is the only reliable guard.
                if (IsProfileFamily(family)) continue;

                // Some Revit parameters store the Family.Id directly (not a FamilySymbol.Id).
                // Check both the family itself and its symbols against the inUse set.
                if (inUse.Contains(family.Id)) continue;
                if (family.GetFamilySymbolIds().Any(tid => inUse.Contains(tid))) continue;

                var category = family.FamilyCategory?.Name ?? "Sin categoría";
                purgeable.Add(new PurgeableItem(
                    GetElementIdValue(family.Id),
                    family.Name,
                    category,
                    "Familia",
                    EstimateFamilySize(family),
                    DetectionConfidence.Heuristic,
                    RiskLevel.High));
            }

            ReportProgress(40, 100, "Verificando tipos de familia...");

            // ── Tipos de familia sin uso (FamilySymbol con 0 instancias,
            //    en familias que sí tienen otros tipos usados)
            try
            {
                var allSymbols = new FilteredElementCollector(_doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .ToList();

                foreach (var symbol in allSymbols)
                {
                    try
                    {
                        var family = symbol.Family;
                        if (family == null) continue;
                        if (family.IsInPlace) continue;
                        if (!family.IsEditable) continue;
                        if (IsProfileFamily(family)) continue;
                        var allTypeIds = family.GetFamilySymbolIds();
                        if (allTypeIds.Count < 2) continue;

                        // This symbol is in use
                        if (inUse.Contains(symbol.Id)) continue;

                        // If the ENTIRE family is unused, it already appears as "Familia" above
                        if (!allTypeIds.Any(tid => inUse.Contains(tid))) continue;

                        var category = family.FamilyCategory?.Name ?? "Sin categoría";
                        purgeable.Add(new PurgeableItem(
                            GetElementIdValue(symbol.Id),
                            $"{family.Name} : {symbol.Name}",
                            category,
                            "Tipo familia",
                            0,
                            DetectionConfidence.Heuristic,
                            RiskLevel.High));
                    }
                    catch { /* símbolo individual no crítico */ }
                }
            }
            catch { /* No crítico */ }

            ReportProgress(60, 100, "Verificando vistas y estilos...");

            // Vistas no colocadas en planos (excluyendo plantillas y vistas del sistema)
            // Viewport cubre vistas normales (planos, secciones, alzados, 3D, leyendas).
            // ScheduleSheetInstance cubre tablas de planificación — no crean Viewport.
            var placedViewIds = new FilteredElementCollector(_doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .Select(vp => vp.ViewId)
                .ToHashSet();

            try
            {
                var scheduledIds = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ScheduleSheetInstance))
                    .Cast<ScheduleSheetInstance>()
                    .Select(ssi => ssi.ScheduleId);
                foreach (var id in scheduledIds) placedViewIds.Add(id);
            }
            catch { /* Non-critical */ }

            // Views included in any publication set (ViewSheetSet) must not be purged
            try
            {
                foreach (ViewSheetSet vss in new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSheetSet)).Cast<ViewSheetSet>())
                    foreach (View v in vss.Views)
                        placedViewIds.Add(v.Id);
            }
            catch { /* Non-critical */ }

            // Dependent views (crop-region sub-views) and callout targets — single pass over all views.
            try
            {
                foreach (View v in new FilteredElementCollector(_doc).OfClass(typeof(View)).Cast<View>())
                {
                    if (v.GetPrimaryViewId() != ElementId.InvalidElementId)
                        placedViewIds.Add(v.Id);
                    foreach (var refId in v.GetReferenceCallouts())
                        placedViewIds.Add(refId);
                }
            }
            catch { /* Non-critical */ }

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
                    0,
                    DetectionConfidence.Exact,
                    RiskLevel.Low));
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
                    .Where(t => !usedTextTypeIds.Contains(t.Id) && !defaultTypeIds.Contains(t.Id))
                    .Where(t => { try { return t.GetDependentElements(null).Count == 0; } catch { return true; } })
                    .ToList();

                foreach (var t in textTypes)
                    purgeable.Add(new PurgeableItem(
                        GetElementIdValue(t.Id), t.Name,
                        "Estilos de texto", "Estilo texto", 0,
                        DetectionConfidence.Exact, RiskLevel.Medium));
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
                    .Where(t => !usedDimTypeIds.Contains(t.Id) && !defaultTypeIds.Contains(t.Id))
                    .Where(t => { try { return t.GetDependentElements(null).Count == 0; } catch { return true; } })
                    .ToList();

                foreach (var t in dimTypes)
                    purgeable.Add(new PurgeableItem(
                        GetElementIdValue(t.Id), t.Name,
                        "Tipos de cota", "Tipo cota", 0,
                        DetectionConfidence.Exact, RiskLevel.Medium));
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
                    .Where(t => !usedFilledRegionTypeIds.Contains(t.Id) && !defaultTypeIds.Contains(t.Id))
                    .ToList();

                foreach (var t in filledRegionTypes)
                    purgeable.Add(new PurgeableItem(
                        GetElementIdValue(t.Id), t.Name,
                        "Regiones rellenas", "Patron relleno", 0,
                        DetectionConfidence.Exact, RiskLevel.Medium));
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
                        0,
                        DetectionConfidence.Exact,
                        RiskLevel.Low));
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
                        0,
                        DetectionConfidence.Exact,
                        RiskLevel.Low));
            }
            catch { /* No crítico */ }

            ReportProgress(90, 100, "Finalizando análisis...");
            WriteDiagnosticLog(inUse, families, purgeable);
            ReportProgress(100, 100, "Análisis completado");

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
                var projectParams = GetProjectParameterValues();
                return new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Where(s => !s.IsPlaceholder)
                    .Select(s =>
                    {
                        var values = GetAllParameterValues(s);
                        // Merge project parameters (sheet params take precedence on name collision)
                        foreach (var kv in projectParams)
                            if (!values.ContainsKey(kv.Key)) values[kv.Key] = kv.Value;
                        return new SheetExportInfo(
                            GetElementIdValue(s.Id),
                            s.SheetNumber,
                            s.Name,
                            s.get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION)?.AsString() ?? "",
                            s.LookupParameter("Discipline")?.AsString() ?? "",
                            values);
                    })
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

                var projectParams = GetProjectParameterValues();

                // Sheets
                var sheets = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Where(s => !s.IsPlaceholder)
                    .Select(s =>
                    {
                        var values = GetAllParameterValues(s);
                        foreach (var kv in projectParams)
                            if (!values.ContainsKey(kv.Key)) values[kv.Key] = kv.Value;
                        return new ExportableViewInfo(
                            GetElementIdValue(s.Id),
                            s.UniqueId,
                            s.Name,
                            ExportableItemType.Sheet,
                            s.SheetNumber,
                            s.get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION)?.AsString() ?? "",
                            s.LookupParameter("Discipline")?.AsString() ?? "",
                            values);
                    })
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

                // Collect from ProjectInformation (project parameters: client, number, name, etc.)
                try
                {
                    if (_doc.ProjectInformation != null)
                        CollectParameterNames(_doc.ProjectInformation, names);
                }
                catch { }

                return names.ToList();
            }
            catch { return new List<string>(); }
        }

        /// <summary>
        /// Returns all parameter values from Document.ProjectInformation.
        /// These are merged into each sheet/view's ParameterValues so that
        /// project parameters can be used in file naming patterns.
        /// </summary>
        private Dictionary<string, string> GetProjectParameterValues()
        {
            try
            {
                return _doc.ProjectInformation != null
                    ? GetAllParameterValues(_doc.ProjectInformation)
                    : new Dictionary<string, string>();
            }
            catch { return new Dictionary<string, string>(); }
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

        public ScheduleData GetScheduleData(long scheduleId) =>
            GetScheduleData(scheduleId, includeLinks: false);

        public ScheduleData GetScheduleData(long scheduleId, bool includeLinks)
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

                // ── Linked-model rows ────────────────────────────────────────────
                // Host rows are all non-linked.
                var isLinkedRow    = Enumerable.Repeat(false, rows.Count).ToList();
                var linkSourceName = Enumerable.Repeat(string.Empty, rows.Count).ToList();

                if (includeLinks && vs.Definition.CategoryId != ElementId.InvalidElementId)
                {
                    try
                    {
                        var linkInstances = new FilteredElementCollector(_doc)
                            .OfClass(typeof(RevitLinkInstance))
                            .Cast<RevitLinkInstance>()
                            .ToList();

                        foreach (var linkInst in linkInstances)
                        {
                            var linkDoc = linkInst.GetLinkDocument();
                            if (linkDoc == null) continue;

                            var linkName = Path.GetFileNameWithoutExtension(linkDoc.Title);

                            // Collect elements of the same category from the linked document.
                            var linkedElements = new List<Element>();
                            try
                            {
                                linkedElements = new FilteredElementCollector(linkDoc)
                                    .OfCategoryId(vs.Definition.CategoryId)
                                    .WhereElementIsNotElementType()
                                    .ToList();
                            }
                            catch { continue; }

                            foreach (var element in linkedElements)
                            {
                                Element? typeElem = null;
                                try
                                {
                                    var typeId = element.GetTypeId();
                                    if (typeId != ElementId.InvalidElementId)
                                        typeElem = linkDoc.GetElement(typeId);
                                }
                                catch { }

                                var cellValues = new List<string>();
                                foreach (var field in fields)
                                {
                                    string val = "";
                                    try
                                    {
                                        if (!field.IsCalculatedField)
                                        {
                                            Parameter? param = null;
                                            var fieldName = field.GetName();

                                            // Linked docs may have different ParameterIds —
                                            // use name-based lookup as primary strategy.
                                            param = element.LookupParameter(fieldName)
                                                 ?? typeElem?.LookupParameter(fieldName);

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
                                isLinkedRow.Add(true);
                                linkSourceName.Add(linkName);
                            }
                        }
                    }
                    catch { /* Non-critical — linked-model extraction is best-effort */ }
                }

                var schedules = GetSchedules();
                var info = schedules.FirstOrDefault(s => s.Id == scheduleId)
                           ?? new ScheduleInfo { Id = scheduleId, Name = vs.Name };

                return new ScheduleData
                {
                    Schedule       = info,
                    Columns        = columns,
                    ElementIds     = elementIds,
                    Rows           = rows,
                    IsLinkedRow    = isLinkedRow,
                    LinkSourceName = linkSourceName
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

        // Returns the set of IDs of all Revit project-default element types.
        // Default types (default dimension style, text style, etc.) cannot be deleted —
        // Revit hangs instead of raising a FailureMessage when doc.Delete() is called on them.
        private HashSet<ElementId> BuildDefaultTypeIdSet()
        {
            var ids = new HashSet<ElementId>();
            foreach (ElementTypeGroup group in Enum.GetValues(typeof(ElementTypeGroup)))
            {
                try
                {
                    var id = _doc.GetDefaultElementTypeId(group);
                    if (id != ElementId.InvalidElementId) ids.Add(id);
                }
                catch { }
            }
            return ids;
        }

        // Builds the complete set of "in-use" FamilySymbol IDs in a single pass.
        // Sources:
        //   A) Type IDs of ALL placed FamilyInstance elements (doors, windows, furniture, equipment…)
        //   B) Type IDs of ALL IndependentTag elements (annotation tags — NOT FamilyInstance)
        //   C) Type IDs of ALL curtain wall mullions (Mullion may not inherit FamilyInstance)
        //   D) Type IDs of ALL curtain wall panels (system panels / custom panel families)
        //   E) ElementId parameter values on every ElementType (grid heads, section marks,
        //      level heads, profile families in sweeps, etc.)
        //
        // The per-family/per-symbol check is then a simple O(1) HashSet.Contains() call.
        private HashSet<ElementId> BuildInUseTypeIdSet()
        {
            var ids = new HashSet<ElementId>();

            static void AddTypeId(HashSet<ElementId> set, ElementId id)
            {
                if (id != ElementId.InvalidElementId) set.Add(id);
            }

            // A) FamilyInstance elements (one collector call covers all model element types)
            try
            {
                foreach (var fi in new FilteredElementCollector(_doc)
                    .OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>())
                    AddTypeId(ids, fi.GetTypeId());
            }
            catch { }

            // B) IndependentTag (annotation tags are not FamilyInstance)
            try
            {
                foreach (var tag in new FilteredElementCollector(_doc)
                    .OfClass(typeof(IndependentTag)).Cast<IndependentTag>())
                    AddTypeId(ids, tag.GetTypeId());
            }
            catch { }

            // B2) SpatialElementTag — AreaTag, RoomTag, SpaceTag; these are NOT IndependentTag
            try
            {
                foreach (var tag in new FilteredElementCollector(_doc)
                    .OfClass(typeof(SpatialElementTag)).Cast<SpatialElementTag>())
                    AddTypeId(ids, tag.GetTypeId());
            }
            catch { }

            // C) Curtain wall mullions
            try
            {
                foreach (var m in new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_CurtainWallMullions)
                    .WhereElementIsNotElementType().ToElements())
                    AddTypeId(ids, m.GetTypeId());
            }
            catch { }

            // D) Curtain wall panels (system panel + custom panel families)
            try
            {
                foreach (var p in new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_CurtainWallPanels)
                    .WhereElementIsNotElementType().ToElements())
                    AddTypeId(ids, p.GetTypeId());
            }
            catch { }

            // E) Targeted parameter scans on small, specific element type collections.
            //    Covers families referenced by system types without direct FamilyInstance
            //    placements — both annotation families and model families.
            //    Each collection is deliberately small (never "all element types").
            void ScanParams(IEnumerable<Element> elements)
            {
                foreach (var elem in elements)
                    foreach (Parameter param in elem.Parameters)
                    {
                        if (param.StorageType != StorageType.ElementId || !param.HasValue) continue;
                        var id = param.AsElementId();
                        if (id == ElementId.InvalidElementId) continue;
                        ids.Add(id);
                        // If the parameter points to a Family (not a FamilySymbol), also mark all its
                        // symbols as in-use. Some Revit parameters store Family.Id, not FamilySymbol.Id.
                        try
                        {
                            if (_doc.GetElement(id) is Family fam)
                                foreach (var symId in fam.GetFamilySymbolIds())
                                    ids.Add(symId);
                        }
                        catch { }
                    }
            }

            // Deep scan: like ScanParams but also resolves StorageType.Integer values as potential
            // element IDs. Used for ViewFamilyType and ElevationMarkerType because Revit stores
            // section head and elevation mark symbol references as raw integer element IDs,
            // not as proper StorageType.ElementId parameters.
            void ScanParamsDeep(IEnumerable<Element> elements)
            {
                ScanParams(elements); // catch all normal ElementId params first
                foreach (var elem in elements)
                    foreach (Parameter param in elem.Parameters)
                    {
                        if (param.StorageType != StorageType.Integer || !param.HasValue) continue;
                        var intVal = param.AsInteger();
                        if (intVal <= 0) continue;
                        try
                        {
#if REVIT2024
#pragma warning disable CS0618
                            var id = new ElementId(intVal);
#pragma warning restore CS0618
#else
                            var id = new ElementId((long)intVal);
#endif
                            var referenced = _doc.GetElement(id);
                            if (referenced is FamilySymbol sym)
                            {
                                ids.Add(id);
                                ids.Add(sym.Family.Id);
                                foreach (var symId in sym.Family.GetFamilySymbolIds())
                                    ids.Add(symId);
                            }
                            else if (referenced is Family fam)
                            {
                                ids.Add(id);
                                foreach (var symId in fam.GetFamilySymbolIds())
                                    ids.Add(symId);
                            }
                        }
                        catch { }
                    }
            }

            // ── Comprehensive deep element type parameter scan ────────────────────
            // Scan ALL non-FamilySymbol element types with ScanParamsDeep, which checks
            // BOTH StorageType.ElementId AND StorageType.Integer parameters in one pass.
            // This covers every system type that can reference a family:
            //   · WallType, FloorType, RoofType, CeilingType → panel/edge profiles
            //   · RailingType, TopRailType, HandRailType, ContinuousRailType → baluster/profile families
            //   · ViewFamilyType → section head, elevation mark, callout head symbols
            //   · DimensionType, SpotElevationType → arrowhead symbols
            //   · GridType, LevelType → head symbols
            //   · MullionType, CurtainWallType → mullion/panel families
            //   · StairsRunType, StairsLandingType → stringer/component families
            //   · PipeType, DuctType, CableTrayType, ConduitType → segment/fitting refs
            //   · Any other type added in future Revit versions
            // Integer-stored references (e.g. section heads, railing profiles) are caught
            // because ScanParamsDeep checks both storage types.
            // FamilySymbol is excluded to avoid false positives from nested-family params.
            try
            {
                var allTypes = new FilteredElementCollector(_doc)
                    .WhereElementIsElementType()
                    .Where(e => e is not FamilySymbol)
                    .ToList();
                ScanParamsDeep(allTypes);
            }
            catch { }

            // ── Instance GetTypeId scan for non-FamilyInstance placed elements ────
            // Marks type IDs as in-use for elements whose types are not captured
            // by WhereElementIsElementType alone (e.g. WallSweep, SlabEdge).
            try
            {
                foreach (var e in new FilteredElementCollector(_doc)
                    .OfClass(typeof(WallSweep)).Cast<WallSweep>())
                    AddTypeId(ids, e.GetTypeId());
            }
            catch { }
            try
            {
                foreach (var e in new FilteredElementCollector(_doc)
                    .OfClass(typeof(SlabEdge)).Cast<SlabEdge>())
                    AddTypeId(ids, e.GetTypeId());
            }
            catch { }

            // ── Railing & stair sub-categories: balusters, posts, rails, stringers ─────────
            // These FamilyInstances are sub-components of railing/stair systems. They appear
            // in their own sub-categories rather than as top-level elements, so a plain
            // FamilyInstance collector may miss them on some Revit versions.
            // Scanning by sub-category ensures we mark their families as in-use.
            var railingStairSubcats = new[]
            {
                BuiltInCategory.OST_StairsRailing,          // railing systems (host)
                BuiltInCategory.OST_RailingSystemBaluster,  // individual baluster FamilyInstances
                BuiltInCategory.OST_RailingSystemRail,      // rail caps, hand rails, etc.
                BuiltInCategory.OST_Stairs,                 // stair runs (system family instances)
                BuiltInCategory.OST_StairsStringerCarriage, // stringer FamilyInstances
            };
            foreach (var cat in railingStairSubcats)
            {
                try
                {
                    foreach (var elem in new FilteredElementCollector(_doc)
                        .OfCategory(cat)
                        .WhereElementIsNotElementType()
                        .ToElements())
                    {
                        try
                        {
                            var typeId = elem.GetTypeId();
                            if (typeId == ElementId.InvalidElementId) continue;
                            ids.Add(typeId);
                            // Also mark the owning family so family-level check passes
                            var sym = _doc.GetElement(typeId) as FamilySymbol;
                            if (sym?.Family?.Id != null) ids.Add(sym.Family.Id);
                        }
                        catch { /* individual element non-critical */ }
                    }
                }
                catch { /* category may not exist in this project */ }
            }

            // ── MEP routing preferences: pipe, duct, cable tray, conduit types ──────────────
            // PipeType / DuctType / CableTrayType / ConduitType store their default fitting
            // families (elbows, tees, transitions, unions, etc.) via RoutingPreferenceManager.
            // These references are NOT exposed as standard Parameters — we must read them
            // through the dedicated API so that configured-but-unplaced fittings are detected.
            ScanMepRoutingPreferences(ids);

            return ids;
        }

        // Profile families are referenced by railing component types (TopRailType, HandRailType,
        // ContinuousRailType), wall sweep types, stair stringer types, etc. Revit stores those
        // references internally — they are NOT accessible via elem.Parameters, so no amount
        // of parameter scanning can detect them. Excluding by category is the only safe guard.
        private static bool IsProfileFamily(Family family)
        {
            try
            {
                var catId = family.FamilyCategory?.Id;
                if (catId == null) return false;
                long val;
#if REVIT2024
#pragma warning disable CS0618
                val = catId.IntegerValue;
#pragma warning restore CS0618
#else
                val = catId.Value;
#endif
                return val == (long)BuiltInCategory.OST_ProfileFamilies;
            }
            catch { return false; }
        }

        private void ScanMepRoutingPreferences(HashSet<ElementId> ids)
        {
            // MEP system type class names — loaded via reflection to avoid compile-time
            // dependencies on MEP assemblies that may not exist in all Revit installations.
            var mepTypeClassNames = new[]
            {
                "Autodesk.Revit.DB.Plumbing.PipeType",
                "Autodesk.Revit.DB.Mechanical.DuctType",
                "Autodesk.Revit.DB.Electrical.CableTrayType",
                "Autodesk.Revit.DB.Electrical.ConduitType",
            };

            foreach (var className in mepTypeClassNames)
            {
                try
                {
                    // Resolve the type from loaded assemblies
                    var typeClass = AppDomain.CurrentDomain.GetAssemblies()
                        .Select(a => { try { return a.GetType(className); } catch { return null; } })
                        .FirstOrDefault(t => t != null);
                    if (typeClass == null) continue;

                    foreach (var elem in new FilteredElementCollector(_doc).OfClass(typeClass).ToElements())
                    {
                        try { ScanRoutingPreferenceManagerViaReflection(elem, ids); }
                        catch { }
                    }
                }
                catch { }
            }
        }

        private void ScanRoutingPreferenceManagerViaReflection(Element elem, HashSet<ElementId> ids)
        {
            // Get RoutingPreferenceManager via reflection
            var rpmProp = elem.GetType().GetProperty("RoutingPreferenceManager");
            if (rpmProp == null) return;
            var rpm = rpmProp.GetValue(elem);
            if (rpm == null) return;

            var rpmType = rpm.GetType();
            var getNumberOfRules = rpmType.GetMethod("GetNumberOfRules");
            var getRule          = rpmType.GetMethod("GetRule");
            if (getNumberOfRules == null || getRule == null) return;

            // Get the RoutingPreferenceRuleGroupType enum values
            var groupTypeProp = rpmType.GetMethod("GetNumberOfRules")?.GetParameters().FirstOrDefault()?.ParameterType;
            if (groupTypeProp == null || !groupTypeProp.IsEnum) return;
            var allGroups = Enum.GetValues(groupTypeProp);

            foreach (var group in allGroups)
            {
                try
                {
                    var ruleCount = (int)getNumberOfRules.Invoke(rpm, new[] { group })!;
                    for (int i = 0; i < ruleCount; i++)
                    {
                        try
                        {
                            var rule    = getRule.Invoke(rpm, new object[] { group, i });
                            if (rule == null) continue;
                            var partId  = rule.GetType().GetProperty("MEPPartId")?.GetValue(rule) as ElementId;
                            if (partId == null || partId == ElementId.InvalidElementId) continue;

                            ids.Add(partId);
                            if (_doc.GetElement(partId) is FamilySymbol sym)
                            {
                                ids.Add(sym.Family.Id);
                                foreach (var sid in sym.Family.GetFamilySymbolIds())
                                    ids.Add(sid);
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Dry-run: tries to delete each candidate inside a SubTransaction that is always rolled back.
        /// Returns only the IDs whose deletion Revit accepted — zero permanent changes to the model.
        /// </summary>
        public IReadOnlyList<long> VerifyPurgeable(IReadOnlyList<long> candidateIds, Action<int, int>? onProgress = null)
        {
            var verified = new List<long>(candidateIds.Count);
            if (candidateIds.Count == 0) return verified;

            using var outerTx = new Transaction(_doc, "BIMPills - Verificación (dry-run)");
            try
            {
                var fo = outerTx.GetFailureHandlingOptions();
                fo.SetClearAfterRollback(true);
                outerTx.SetFailureHandlingOptions(fo);
                if (outerTx.Start() != TransactionStatus.Started) return verified;

                int done = 0;
                foreach (var id in candidateIds)
                {
                    var elementId = new ElementId(id);
                    if (_doc.GetElement(elementId) == null) { done++; onProgress?.Invoke(done, candidateIds.Count); continue; }

                    using var subTx = new SubTransaction(_doc);
                    try
                    {
                        if (subTx.Start() != TransactionStatus.Started) { done++; onProgress?.Invoke(done, candidateIds.Count); continue; }
                        _doc.Delete(elementId);
                        var status = subTx.Commit();

                        if (status == TransactionStatus.Committed)
                            verified.Add(id);
                        // else: SubTransaction rolled back — element has live dependencies
                    }
                    catch
                    {
                        try { subTx.RollBack(); } catch { }
                        // not verified — element has dependencies or can't be deleted
                    }
                    done++;
                    onProgress?.Invoke(done, candidateIds.Count);
                }
            }
            catch { /* dry-run failure — return whatever we have */ }
            finally
            {
                try
                {
                    if (outerTx.GetStatus() == TransactionStatus.Started)
                        outerTx.RollBack(); // ALWAYS — zero permanent changes
                }
                catch { }
            }

            return verified;
        }

        private void WriteDiagnosticLog(
            HashSet<ElementId> inUse,
            IReadOnlyList<Family> allFamilies,
            IReadOnlyList<PurgeableItem> purgeable)
        {
            try
            {
                var path = Path.Combine(
                    Path.GetTempPath(),
                    $"BIMPills_DiagAudit_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"=== BIMPills Model Audit Diagnostics ===");
                sb.AppendLine($"Fecha   : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Modelo  : {_doc.Title}");
                sb.AppendLine($"inUse IDs count: {inUse.Count}");
                sb.AppendLine();

                // 1. All families evaluated and outcome
                sb.AppendLine("── FAMILIAS EVALUADAS ──────────────────────────────────────────");
                sb.AppendLine($"{"Estado",-12} {"FamilyId",-10} {"Categoria",-30} {"Nombre"}");
                sb.AppendLine(new string('-', 90));
                foreach (var fam in allFamilies.OrderBy(f => f.FamilyCategory?.Name).ThenBy(f => f.Name))
                {
                    try
                    {
                        if (fam.IsInPlace) continue;
                        if (!fam.IsEditable) continue;

                        var famIdVal = GetElementIdValue(fam.Id);
                        var cat = fam.FamilyCategory?.Name ?? "Sin categoría";
                        var symIds = fam.GetFamilySymbolIds();
                        bool byFamId   = inUse.Contains(fam.Id);
                        bool bySymId   = symIds.Any(tid => inUse.Contains(tid));
                        string status  = (byFamId || bySymId) ? "EN USO" : "PURGABLE";
                        string reason  = byFamId ? "(by family.Id)"
                                       : bySymId ? $"(by symbolId: {symIds.First(tid => inUse.Contains(tid))})"
                                       : "";
                        sb.AppendLine($"{status,-12} {famIdVal,-10} {cat,-30} {fam.Name} {reason}");

                        // For purgeable families: list all symbol IDs (none were in inUse)
                        if (status == "PURGABLE")
                        {
                            foreach (var sid in symIds)
                                sb.AppendLine($"{"  sym",-12} {GetElementIdValue(sid),-10}");
                        }
                    }
                    catch { }
                }

                sb.AppendLine();
                sb.AppendLine("── PURGABLES FINALES ───────────────────────────────────────────");
                foreach (var p in purgeable.Where(p => p.ItemType == "Familia").OrderBy(p => p.Category))
                    sb.AppendLine($"  [{p.Id}] {p.Category} — {p.Name}");

                sb.AppendLine();
                sb.AppendLine("── inUse SET (primeros 200 IDs resueltos) ──────────────────────");
                int count = 0;
                foreach (var id in inUse.Take(200))
                {
                    try
                    {
                        var elem = _doc.GetElement(id);
                        var name = elem?.Name ?? "(null)";
                        var cls  = elem?.GetType().Name ?? "?";
                        sb.AppendLine($"  {GetElementIdValue(id),-10} {cls,-35} {name}");
                    }
                    catch { sb.AppendLine($"  {GetElementIdValue(id),-10} (error)"); }
                    if (++count >= 200) break;
                }
                if (inUse.Count > 200)
                    sb.AppendLine($"  ... y {inUse.Count - 200} más");

                File.WriteAllText(path, sb.ToString(), System.Text.Encoding.UTF8);
            }
            catch { /* diagnóstico no crítico */ }
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
