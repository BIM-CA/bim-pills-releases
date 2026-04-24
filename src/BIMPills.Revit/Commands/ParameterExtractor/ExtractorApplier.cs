using Autodesk.Revit.DB;
using BIMPills.Commands.ParameterExtractor;
using BIMPills.Core.ParameterExtractor;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace BIMPills.Revit.Commands.ParameterExtractor
{
    /// <summary>
    /// Aplica una <see cref="ExtractionConfig"/> a un conjunto de elementos Revit:
    /// resuelve el valor fuente, crea el parámetro destino si falta, y escribe
    /// el valor. Toda la operación corre en una sola Transaction.
    /// </summary>
    internal static class ExtractorApplier
    {
        private const string SharedParamFileName = "BIMPills-SharedParameters.txt";
        private const string SharedParamGroup    = "BIMPills";

        private static readonly IDmsFormatter _dms = new DmsFormatter();

        public static ExtractionResult Apply(
            Document doc,
            IList<ElementId> selection,
            ExtractionConfig config)
        {
            var result = new ExtractionResult();
            if (doc == null || selection == null || selection.Count == 0 || config.Rules.Count == 0)
                return result;

            using (var tx = new Transaction(doc, "Extractor de Parámetros"))
            {
                tx.Start();
                try
                {
                    // ── 1. Asegurar que los parámetros destino existen ──────
                    var categories   = CollectCategories(doc, selection, config);
                    var createdCount = EnsureTargetParameters(doc, config, categories);
                    result.ParametersCreated = createdCount;

                    // Regenerate so newly created shared parameters are accessible
                    // on elements within the same transaction.
                    if (createdCount > 0)
                        doc.Regenerate();

                    // ── 2. Iterar elementos y aplicar cada regla ────────────
                    foreach (var id in selection)
                    {
                        var elem = doc.GetElement(id);
                        if (elem == null) continue;

                        // Category filter
                        if (config.CategoryFilter.Count > 0
                            && !config.CategoryFilter.Contains(elem.Category?.Name ?? string.Empty,
                                StringComparer.OrdinalIgnoreCase))
                            continue;

                        // Family filter
                        if (config.FamilyFilter.Count > 0)
                        {
                            var famName = GetFamilyNameStatic(doc, elem);
                            if (!config.FamilyFilter.Contains(famName, StringComparer.OrdinalIgnoreCase))
                                continue;
                        }

                        // Type filter
                        if (config.TypeFilter.Count > 0)
                        {
                            var typeName = GetTypeNameStatic(doc, elem);
                            if (!config.TypeFilter.Contains(typeName, StringComparer.OrdinalIgnoreCase))
                                continue;
                        }

                        result.ElementsProcessed++;

                        foreach (var rule in config.Rules)
                        {
                            try
                            {
                                if (ApplyRule(doc, elem, rule, config))
                                    result.ParametersWritten++;
                            }
                            catch (Exception ex)
                            {
                                result.Errors.Add(
                                    $"[{elem.Id}] {rule.Target.ParameterName}: {ex.Message}");
                            }
                        }
                    }

                    tx.Commit();
                }
                catch
                {
                    if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack();
                    throw;
                }
            }

            return result;
        }

        // ───────────────────────────────────────────────────────────────────
        // Categorías para binding
        // ───────────────────────────────────────────────────────────────────

        private static HashSet<ElementId> CollectCategories(
            Document doc, IList<ElementId> selection, ExtractionConfig config)
        {
            var set = new HashSet<ElementId>();
            foreach (var id in selection)
            {
                var e = doc.GetElement(id);
                if (e?.Category == null) continue;
                if (!e.Category.AllowsBoundParameters) continue;

                // Respect CategoryFilter: only bind to categories the user selected
                // in the filter tree (step 1). When the filter is empty → all categories.
                if (config.CategoryFilter.Count > 0 &&
                    !config.CategoryFilter.Contains(e.Category.Name, StringComparer.OrdinalIgnoreCase))
                    continue;

                set.Add(e.Category.Id);
            }
            return set;
        }

        // ───────────────────────────────────────────────────────────────────
        // Creación de parámetros destino (shared params)
        // ───────────────────────────────────────────────────────────────────

        private static int EnsureTargetParameters(
            Document doc,
            ExtractionConfig config,
            HashSet<ElementId> categoryIds)
        {
            int created = 0;

            // Reglas que piden crear si falta
            var needed = config.Rules
                .Where(r => r.Target.CreateIfMissing && !string.IsNullOrWhiteSpace(r.Target.ParameterName))
                .GroupBy(r => r.Target.ParameterName)
                .ToList();

            if (needed.Count == 0) return 0;

            var app = doc.Application;
            var sharedFile = EnsureSharedParameterFile(app);
            if (sharedFile == null) return 0;

            var group = sharedFile.Groups.get_Item(SharedParamGroup)
                        ?? sharedFile.Groups.Create(SharedParamGroup);

            var catSet = app.Create.NewCategorySet();
            foreach (var id in categoryIds)
            {
                var cat = Category.GetCategory(doc, id);
                if (cat != null && cat.AllowsBoundParameters) catSet.Insert(cat);
            }

            // Fallback only when no category filter is active.
            // With an explicit filter, respect the user's choice — don't expand to the whole model.
            if (catSet.Size == 0)
            {
                if (config.CategoryFilter.Count > 0) return 0;  // filter set but no matches → skip

                // No filter: bind to all model categories (broadest scope)
                var allCats = doc.Settings.Categories;
                foreach (Category c in allCats)
                    if (c.AllowsBoundParameters && c.CategoryType == CategoryType.Model)
                        catSet.Insert(c);
            }
            if (catSet.Size == 0) return 0;

            var bindings = doc.ParameterBindings;

            foreach (var g in needed)
            {
                var firstRule = g.First();
                var name       = firstRule.Target.ParameterName;
                var dataType   = firstRule.Target.DataType;
                var specTypeId = SpecFor(dataType, firstRule.GeoFormat);

                // Si ya existe un parámetro con ese nombre ligado al modelo, no recrear.
                if (BindingExistsByName(bindings, name)) continue;

                // Crear definición en shared param file (si no existe ya)
                var def = group.Definitions.get_Item(name);
                if (def == null)
                {
                    var opts = new ExternalDefinitionCreationOptions(name, specTypeId)
                    {
                        Visible = true,
                        UserModifiable = true
                    };
                    def = group.Definitions.Create(opts);
                }

                var binding = app.Create.NewInstanceBinding(catSet);
                bool ok = bindings.Insert(def, binding, GroupTypeId.Data);
                if (ok) created++;
            }

            return created;
        }

        private static DefinitionFile? EnsureSharedParameterFile(Autodesk.Revit.ApplicationServices.Application app)
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir  = Path.Combine(appData, "Autodesk", "Revit", "Addins", "BIMPills");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, SharedParamFileName);

            if (!File.Exists(path))
            {
                // Crear archivo vacío con la cabecera correcta
                File.WriteAllText(path,
                    "# This is a Revit shared parameter file.\n" +
                    "# Do not edit manually.\n" +
                    "*META\tVERSION\tMINVERSION\n" +
                    "META\t2\t1\n" +
                    "*GROUP\tID\tNAME\n" +
                    "*PARAM\tGUID\tNAME\tDATATYPE\tDATACATEGORY\tGROUP\tVISIBLE\tDESCRIPTION\tUSERMODIFIABLE\n");
            }

            app.SharedParametersFilename = path;
            return app.OpenSharedParameterFile();
        }

        private static bool BindingExistsByName(BindingMap bindings, string name)
        {
            var it = bindings.ForwardIterator();
            while (it.MoveNext())
            {
                if (it.Key is Definition d && string.Equals(d.Name, name, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static ForgeTypeId SpecFor(ExtractionDataType dataType, GeoFormat geoFormat) =>
            (dataType, geoFormat) switch
            {
                (ExtractionDataType.Text,   _             ) => SpecTypeId.String.Text,
                (ExtractionDataType.Number, _             ) => SpecTypeId.Number,
                (ExtractionDataType.Length, _             ) => SpecTypeId.Length,
                (ExtractionDataType.Angle,  _             ) => SpecTypeId.Angle,
                _ => SpecTypeId.String.Text
            };

        // ───────────────────────────────────────────────────────────────────
        // Aplicación de regla a un elemento
        // ───────────────────────────────────────────────────────────────────

        private static bool ApplyRule(
            Document doc,
            Element elem,
            ExtractionRule rule,
            ExtractionConfig config)
        {
            var target = elem.LookupParameter(rule.Target.ParameterName);
            if (target == null || target.IsReadOnly) return false;

            var sourceValue = ResolveSource(doc, elem, rule);
            if (sourceValue == null) return false;

            return WriteToParameter(target, sourceValue.Value, rule, config);
        }

        // ── Fuente ──────────────────────────────────────────────────────────

        /// <summary>
        /// Valor fuente resuelto. Sólo uno de los campos no-null aplica según Kind.
        /// </summary>
        private struct SourceValue
        {
            public SourceKind Kind;
            public string? Text;
            public double Number;  // para Number/Length/Angle/Geo
        }

        private enum SourceKind { Text, Length, Number, Angle, Latitude, Longitude }

        private static SourceValue? ResolveSource(
            Document doc,
            Element elem,
            ExtractionRule rule)
        {
            switch (rule.Source)
            {
                case ExtractionSourceKind.Category:
                    return new SourceValue { Kind = SourceKind.Text, Text = elem.Category?.Name };

                case ExtractionSourceKind.FamilyName:
                    return new SourceValue { Kind = SourceKind.Text, Text = GetFamilyName(doc, elem) };

                case ExtractionSourceKind.TypeName:
                    return new SourceValue { Kind = SourceKind.Text, Text = GetTypeName(doc, elem) };

                case ExtractionSourceKind.LevelName:
                    var lvl = elem.LevelId != null ? doc.GetElement(elem.LevelId) as Level : null;
                    return new SourceValue { Kind = SourceKind.Text, Text = lvl?.Name ?? string.Empty };

                case ExtractionSourceKind.ElementId:
                    return new SourceValue { Kind = SourceKind.Text, Text = ToLongId(elem.Id).ToString(CultureInfo.InvariantCulture) };

                case ExtractionSourceKind.UniqueId:
                    return new SourceValue { Kind = SourceKind.Text, Text = elem.UniqueId };

                case ExtractionSourceKind.ElementProperty:
                    if (string.IsNullOrWhiteSpace(rule.SourceParameterName)) return null;
                    var srcParam = elem.LookupParameter(rule.SourceParameterName);
                    if (srcParam == null) return null;
                    return ReadParameter(srcParam);

                case ExtractionSourceKind.LocationX:
                case ExtractionSourceKind.LocationY:
                case ExtractionSourceKind.LocationZ:
                    {
                        var pt = GetElementPoint(elem);
                        if (pt == null) return null;
                        var adjusted = ApplyOriginOffset(doc, pt, rule.CoordinateOrigin);
                        double feet =
                            rule.Source == ExtractionSourceKind.LocationX ? adjusted.X :
                            rule.Source == ExtractionSourceKind.LocationY ? adjusted.Y :
                                                                            adjusted.Z;
                        return new SourceValue { Kind = SourceKind.Length, Number = feet };
                    }

                case ExtractionSourceKind.StartX:
                case ExtractionSourceKind.StartY:
                case ExtractionSourceKind.StartZ:
                    {
                        var pt = GetStartPoint(elem);
                        if (pt == null) return null;
                        var adjusted = ApplyOriginOffset(doc, pt, rule.CoordinateOrigin);
                        double feet =
                            rule.Source == ExtractionSourceKind.StartX ? adjusted.X :
                            rule.Source == ExtractionSourceKind.StartY ? adjusted.Y :
                                                                         adjusted.Z;
                        return new SourceValue { Kind = SourceKind.Length, Number = feet };
                    }

                case ExtractionSourceKind.EndX:
                case ExtractionSourceKind.EndY:
                case ExtractionSourceKind.EndZ:
                    {
                        var pt = GetEndPoint(elem);
                        if (pt == null) return null;
                        var adjusted = ApplyOriginOffset(doc, pt, rule.CoordinateOrigin);
                        double feet =
                            rule.Source == ExtractionSourceKind.EndX ? adjusted.X :
                            rule.Source == ExtractionSourceKind.EndY ? adjusted.Y :
                                                                       adjusted.Z;
                        return new SourceValue { Kind = SourceKind.Length, Number = feet };
                    }

                case ExtractionSourceKind.Latitude:
                case ExtractionSourceKind.Longitude:
                    {
                        var pt = GetElementPoint(elem);
                        if (pt == null) return null;

                        double latDeg, lonDeg;

                        if (rule.GeoConversionMethod == GeoConversionMethod.UTM)
                        {
                            // ── UTM inverso: Survey Point lat/lon → UTM → + offset → lat/lon ──
                            var site = doc.SiteLocation;
                            if (site == null) return null;

                            double surveyLatDeg = site.Latitude  * 180.0 / Math.PI;
                            double surveyLonDeg = site.Longitude * 180.0 / Math.PI;

                            var (surveyE, surveyN) = UtmConverter.FromLatLon(
                                surveyLatDeg, surveyLonDeg, rule.UtmZone, rule.UtmIsNorthHemisphere);

                            // Offset elemento respecto al Survey Point en internal (ft)
                            var surveyPos = BasePoint.GetSurveyPoint(doc)?.Position ?? XYZ.Zero;
                            var delta     = pt - surveyPos;

                            // Convertir ft → m (se asume X=Este, Y=Norte sin corrección True North)
                            double deltaEM = UnitUtils.ConvertFromInternalUnits(delta.X, UnitTypeId.Meters);
                            double deltaNM = UnitUtils.ConvertFromInternalUnits(delta.Y, UnitTypeId.Meters);

                            (latDeg, lonDeg) = UtmConverter.ToLatLon(
                                surveyE + deltaEM, surveyN + deltaNM,
                                rule.UtmZone, rule.UtmIsNorthHemisphere);
                        }
                        else
                        {
                            // ── RevitProjectLocation: equirectangular desde Survey Point ───
                            // ProjectPosition no expone Lat/Lon; usamos SiteLocation (Survey)
                            // + offset equirectangular del elemento respecto al Survey Point.
                            var site = doc.SiteLocation;
                            if (site == null) return null;

                            double surveyLatRad = site.Latitude;
                            double surveyLonRad = site.Longitude;

                            var surveyPos = BasePoint.GetSurveyPoint(doc)?.Position ?? XYZ.Zero;
                            var delta     = pt - surveyPos;
                            double deltaEM = UnitUtils.ConvertFromInternalUnits(delta.X, UnitTypeId.Meters);
                            double deltaNM = UnitUtils.ConvertFromInternalUnits(delta.Y, UnitTypeId.Meters);

                            const double R = 6_378_137.0;
                            latDeg = surveyLatRad * 180.0 / Math.PI + (deltaNM / R) * (180.0 / Math.PI);
                            lonDeg = surveyLonRad * 180.0 / Math.PI + (deltaEM / (R * Math.Cos(surveyLatRad))) * (180.0 / Math.PI);
                        }

                        double value = rule.Source == ExtractionSourceKind.Latitude ? latDeg : lonDeg;
                        return new SourceValue
                        {
                            Kind   = rule.Source == ExtractionSourceKind.Latitude ? SourceKind.Latitude : SourceKind.Longitude,
                            Number = value
                        };
                    }
            }
            return null;
        }

        private static SourceValue? ReadParameter(Parameter p)
        {
            switch (p.StorageType)
            {
                case StorageType.String:
                    return new SourceValue { Kind = SourceKind.Text, Text = p.AsString() ?? string.Empty };
                case StorageType.Integer:
                    return new SourceValue { Kind = SourceKind.Number, Number = p.AsInteger() };
                case StorageType.Double:
                    return new SourceValue { Kind = SourceKind.Number, Number = p.AsDouble() };
                case StorageType.ElementId:
                    return new SourceValue { Kind = SourceKind.Text, Text = p.AsValueString() ?? ToLongId(p.AsElementId()).ToString(CultureInfo.InvariantCulture) };
                default:
                    return new SourceValue { Kind = SourceKind.Text, Text = p.AsValueString() ?? string.Empty };
            }
        }

        // ── Geometría ───────────────────────────────────────────────────────

        private static XYZ? GetElementPoint(Element elem)
        {
            if (elem.Location is LocationPoint lp) return lp.Point;
            if (elem.Location is LocationCurve lc) return lc.Curve.Evaluate(0.5, true);
            var bb = elem.get_BoundingBox(null);
            if (bb != null) return (bb.Min + bb.Max) * 0.5;
            return null;
        }

        private static XYZ? GetStartPoint(Element elem)
        {
            if (elem.Location is LocationCurve lc) return lc.Curve.GetEndPoint(0);
            if (elem.Location is LocationPoint lp) return lp.Point;
            return null;
        }

        private static XYZ? GetEndPoint(Element elem)
        {
            if (elem.Location is LocationCurve lc) return lc.Curve.GetEndPoint(1);
            if (elem.Location is LocationPoint lp) return lp.Point;
            return null;
        }

        private static XYZ ApplyOriginOffset(Document doc, XYZ internalPt, CoordinateOrigin origin)
        {
            switch (origin)
            {
                case CoordinateOrigin.Internal:
                    return internalPt;

                case CoordinateOrigin.ProjectBase:
                    {
                        var pbp = BasePoint.GetProjectBasePoint(doc);
                        if (pbp == null) return internalPt;
                        return internalPt - pbp.Position;
                    }

                case CoordinateOrigin.Survey:
                    {
                        var sp = BasePoint.GetSurveyPoint(doc);
                        if (sp == null) return internalPt;
                        return internalPt - sp.Position;
                    }
            }
            return internalPt;
        }

        // ── Escritura ───────────────────────────────────────────────────────

        private static bool WriteToParameter(
            Parameter target,
            SourceValue value,
            ExtractionRule rule,
            ExtractionConfig config)
        {
            switch (target.StorageType)
            {
                case StorageType.String:
                    target.Set(FormatAsText(value, rule, config));
                    return true;

                case StorageType.Double:
                    target.Set(ConvertToInternalDouble(value, rule, target, config));
                    return true;

                case StorageType.Integer:
                    if (value.Kind == SourceKind.Text)
                    {
                        if (int.TryParse(value.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                        { target.Set(i); return true; }
                        return false;
                    }
                    target.Set((int)Math.Round(value.Number));
                    return true;

                case StorageType.ElementId:
                    return false; // No soportado (escritura por lookup de id)
            }
            return false;
        }

        private static string FormatAsText(SourceValue value, ExtractionRule rule, ExtractionConfig config)
        {
            switch (value.Kind)
            {
                case SourceKind.Text:
                    return value.Text ?? string.Empty;

                case SourceKind.Length:
                    double converted = UnitUtils.ConvertFromInternalUnits(value.Number, LengthUnitId(config.LengthUnits));
                    return Math.Round(converted, Math.Max(0, Math.Min(6, config.Decimals)))
                                .ToString($"F{config.Decimals}", CultureInfo.InvariantCulture);

                case SourceKind.Latitude:
                case SourceKind.Longitude:
                    if (rule.GeoFormat == GeoFormat.Dms)
                        return _dms.Format(value.Number, isLatitude: value.Kind == SourceKind.Latitude, secondsDecimals: 2);
                    return Math.Round(value.Number, Math.Max(0, Math.Min(6, config.Decimals)))
                                .ToString($"F{config.Decimals}", CultureInfo.InvariantCulture);

                case SourceKind.Number:
                    return Math.Round(value.Number, Math.Max(0, Math.Min(6, config.Decimals)))
                                .ToString(CultureInfo.InvariantCulture);

                case SourceKind.Angle:
                    // Número en radianes — convertir a grados decimales para mostrar como texto
                    double deg = value.Number * 180.0 / Math.PI;
                    return Math.Round(deg, Math.Max(0, Math.Min(6, config.Decimals)))
                                .ToString(CultureInfo.InvariantCulture);
            }
            return string.Empty;
        }

        private static double ConvertToInternalDouble(
            SourceValue value,
            ExtractionRule rule,
            Parameter target,
            ExtractionConfig config)
        {
            switch (value.Kind)
            {
                case SourceKind.Length:
                    // LocationX/Y/Z vienen en ft internos. Si el destino es Length, Revit
                    // interpreta como ft y lo muestra en la unidad del proyecto.
                    return value.Number;

                case SourceKind.Latitude:
                case SourceKind.Longitude:
                    // Revit Angle parameters store in radians internally.
                    // Number parameters (the default for decimal lat/lon) store the
                    // value directly — do NOT convert to radians.
                    if (rule.Target.DataType == ExtractionDataType.Angle)
                        return value.Number * Math.PI / 180.0;
                    return value.Number;   // decimal degrees as-is

                case SourceKind.Number:
                    return value.Number;

                case SourceKind.Angle:
                    return value.Number;

                case SourceKind.Text:
                    if (double.TryParse(value.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                        return d;
                    return 0.0;
            }
            return 0.0;
        }

        private static ForgeTypeId LengthUnitId(ExtractionLengthUnits units) =>
            units switch
            {
                ExtractionLengthUnits.Meters      => UnitTypeId.Meters,
                ExtractionLengthUnits.Centimeters => UnitTypeId.Centimeters,
                ExtractionLengthUnits.Millimeters => UnitTypeId.Millimeters,
                ExtractionLengthUnits.Feet        => UnitTypeId.Feet,
                _                                 => UnitTypeId.Meters
            };

        // ── Helpers ─────────────────────────────────────────────────────────

        private static string GetFamilyName(Document doc, Element elem)  => GetFamilyNameStatic(doc, elem);
        private static string GetTypeName(Document doc, Element elem)    => GetTypeNameStatic(doc, elem);

        private static string GetFamilyNameStatic(Document doc, Element elem)
        {
            if (elem is FamilyInstance fi) return fi.Symbol?.Family?.Name ?? string.Empty;
            var typeId = elem.GetTypeId();
            if (typeId == ElementId.InvalidElementId) return string.Empty;
            return (doc.GetElement(typeId) as ElementType)?.FamilyName ?? string.Empty;
        }

        private static string GetTypeNameStatic(Document doc, Element elem)
        {
            var typeId = elem.GetTypeId();
            if (typeId == ElementId.InvalidElementId) return elem.Name;
            return doc.GetElement(typeId)?.Name ?? elem.Name;
        }

        private static long ToLongId(ElementId id)
        {
#if REVIT2024
#pragma warning disable CS0618
            return (long)id.IntegerValue;
#pragma warning restore CS0618
#else
            return id.Value;
#endif
        }
    }
}
