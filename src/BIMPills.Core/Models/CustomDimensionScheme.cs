using System;
using System.Collections.Generic;

namespace BIMPills.Core.Models
{
    // ─────────────────────────────────────────────────────────────────────────────
    //  Enums — no WPF / System.Windows.Media dependencies allowed in Core
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Revit element categories that can be targeted by a dimension scheme.
    /// Values map directly to the Revit API BuiltInCategory names used when
    /// collecting elements with a FilteredElementCollector.
    /// </summary>
    public enum DimensionSchemeElementType
    {
        Walls,
        Doors,
        Windows,
        Columns,
        StructuralFraming,
        Rooms,
        Grids
    }

    /// <summary>
    /// The face/line reference on a wall used as a dimension endpoint.
    /// Maps to Autodesk.Revit.DB.WallLocationLine values and to the
    /// Reference objects returned by HostObjectUtils / wall geometry.
    /// </summary>
    public enum WallReferenceType
    {
        /// <summary>Wall centerline — WallLocationLine.WallCenterline</summary>
        WallCenterline,
        /// <summary>Core layer centerline — WallLocationLine.CoreCenterline</summary>
        CoreCenterline,
        /// <summary>Exterior finish face — WallLocationLine.FinishFaceExterior</summary>
        FinishFaceExterior,
        /// <summary>Interior finish face — WallLocationLine.FinishFaceInterior</summary>
        FinishFaceInterior,
        /// <summary>Exterior core face — WallLocationLine.CoreExterior</summary>
        CoreFaceExterior,
        /// <summary>Interior core face — WallLocationLine.CoreInterior</summary>
        CoreFaceInterior
    }

    /// <summary>
    /// Defines how the element filter matches elements in the model.
    /// </summary>
    public enum ElementFilterType
    {
        /// <summary>No filter — all elements of the target category are included.</summary>
        All,
        /// <summary>
        /// Filter by the type/family name (uses BuiltInParameter.ALL_MODEL_TYPE_NAME or
        /// FamilySymbol.Name depending on Condition).
        /// </summary>
        ByTypeName,
        /// <summary>
        /// Filter by an arbitrary element or type parameter value.
        /// ParameterName must be a valid BuiltInParameter name or shared-parameter GUID.
        /// </summary>
        ByParameter
    }

    /// <summary>
    /// Comparison operator used in an ElementFilter.
    /// </summary>
    public enum FilterCondition
    {
        Contains,
        Equals,
        StartsWith,
        EndsWith
    }

    /// <summary>
    /// The logical property / Revit API parameter being measured by a DimensionRule.
    /// </summary>
    public enum DimensionRuleParameter
    {
        /// <summary>
        /// Opening width — FamilyInstance width parameter (doors / windows).
        /// Maps to BuiltInParameter.CASEWORK_WIDTH / FAMILY_WIDTH_PARAM.
        /// </summary>
        Width,
        /// <summary>
        /// Opening or element height.
        /// Maps to BuiltInParameter.CASEWORK_HEIGHT / FAMILY_HEIGHT_PARAM.
        /// </summary>
        Height,
        /// <summary>
        /// Element length (walls, beams, columns).
        /// Maps to BuiltInParameter.CURVE_ELEM_LENGTH.
        /// </summary>
        Length,
        /// <summary>
        /// Wall / floor thickness.
        /// Maps to BuiltInParameter.WALL_ATTR_WIDTH_PARAM / FLOOR_ATTR_THICKNESS_PARAM.
        /// </summary>
        Thickness,
        /// <summary>
        /// Room / space area.
        /// Maps to BuiltInParameter.ROOM_AREA.
        /// </summary>
        Area,
        /// <summary>
        /// Room / space perimeter.
        /// Maps to BuiltInParameter.ROOM_PERIMETER.
        /// </summary>
        Perimeter,
        /// <summary>
        /// Rough-opening width for doors and windows.
        /// Maps to BuiltInParameter.FAMILY_ROUGH_WIDTH_PARAM.
        /// </summary>
        RoughOpeningWidth,
        /// <summary>
        /// Rough-opening height for doors and windows.
        /// Maps to BuiltInParameter.FAMILY_ROUGH_HEIGHT_PARAM.
        /// </summary>
        RoughOpeningHeight,
        /// <summary>
        /// A custom parameter not covered by the predefined options.
        /// Requires CustomParameterName to be set on the DimensionRule.
        /// </summary>
        Custom
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Supporting classes
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Describes how to filter elements within a target category before dimensioning.
    /// </summary>
    public class ElementFilter
    {
        /// <summary>Filtering strategy. Defaults to All (no filter).</summary>
        public ElementFilterType FilterType { get; set; } = ElementFilterType.All;

        /// <summary>
        /// Parameter name used when FilterType == ByParameter.
        /// Can be a BuiltInParameter enum name (e.g. "WALL_ATTR_WIDTH_PARAM") or a
        /// shared-parameter GUID string.
        /// </summary>
        public string ParameterName { get; set; } = "";

        /// <summary>Comparison operator applied to the parameter or type-name value.</summary>
        public FilterCondition Condition { get; set; } = FilterCondition.Contains;

        /// <summary>The value to match against.</summary>
        public string Value { get; set; } = "";
    }

    /// <summary>
    /// A single measurement rule within a CustomDimensionScheme.
    /// Specifies what Revit API reference to measure, how to format the result, and
    /// — for walls — which face references to use at the start and end of the dimension chain.
    /// </summary>
    public class DimensionRule
    {
        /// <summary>Stable identifier for the rule (GUID string).</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>The logical property / built-in parameter to measure.</summary>
        public DimensionRuleParameter Parameter { get; set; } = DimensionRuleParameter.Width;

        /// <summary>
        /// Required when Parameter == Custom.
        /// Must be a valid BuiltInParameter enum name or shared-parameter GUID.
        /// </summary>
        public string CustomParameterName { get; set; } = "";

        // ── Wall-specific references ──────────────────────────────────────────

        /// <summary>
        /// Start-side wall reference used when building a Revit Dimension for walls.
        /// Corresponds to the Reference obtained via HostObjectUtils or wall geometry.
        /// </summary>
        public WallReferenceType WallStartReference { get; set; } = WallReferenceType.FinishFaceExterior;

        /// <summary>End-side wall reference (opposite face).</summary>
        public WallReferenceType WallEndReference { get; set; } = WallReferenceType.FinishFaceInterior;

        // ── Presentation ──────────────────────────────────────────────────────

        /// <summary>
        /// Format string applied to the measured value.
        /// Use "{value}" as the placeholder, e.g. "{value} mm" or "Ancho: {value}".
        /// When empty the raw Revit-formatted value is used.
        /// </summary>
        public string Format { get; set; } = "{value}";

        /// <summary>
        /// Zero-based order in which this rule appears in the dimension annotation.
        /// Lower numbers are placed first.
        /// </summary>
        public int DisplayOrder { get; set; }

        /// <summary>When false the rule is skipped during dimensioning.</summary>
        public bool IsActive { get; set; } = true;

        // ── Legacy / backward-compatibility ───────────────────────────────────

        /// <summary>
        /// Deprecated — use Parameter instead.
        /// Retained for JSON deserialization of schemes saved before the model
        /// expansion (v1.0.0-alpha.2 and earlier).
        /// </summary>
        public string ElementProperty { get; set; } = "";

        /// <summary>
        /// Deprecated — use Format instead.
        /// Retained for JSON deserialization of schemes saved before the model
        /// expansion (v1.0.0-alpha.2 and earlier).
        /// </summary>
        public string DimensionFormat { get; set; } = "";
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Main aggregate
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A user-defined dimension scheme that controls how BIMPills annotates
    /// elements with dimensions.
    /// </summary>
    public class CustomDimensionScheme
    {
        // ── Identity ──────────────────────────────────────────────────────────

        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";

        // ── Targeting ─────────────────────────────────────────────────────────

        /// <summary>
        /// Rich enum list of Revit element categories this scheme applies to.
        /// Populated in new/edited schemes; older persisted schemes may have an
        /// empty list and rely on ApplicableElementTypes instead.
        /// </summary>
        public List<DimensionSchemeElementType> ElementTypes { get; set; } = new List<DimensionSchemeElementType>();

        /// <summary>
        /// Optional filter applied after category collection to narrow down elements.
        /// Defaults to All (no filter).
        /// </summary>
        public ElementFilter ElementFilter { get; set; } = new ElementFilter();

        // ── Rules ─────────────────────────────────────────────────────────────

        /// <summary>Ordered list of measurement rules.</summary>
        public List<DimensionRule> Rules { get; set; } = new List<DimensionRule>();

        // ── Audit ─────────────────────────────────────────────────────────────

        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }

        // ── Display properties for UI binding ─────────────────────────────────

        /// <summary>Total number of rules (active + inactive).</summary>
        public int RulesCount => Rules.Count;

        /// <summary>
        /// Human-readable comma-separated list of target element types.
        /// Prefers ElementTypes when populated, falls back to ApplicableElementTypes
        /// so that legacy-persisted schemes display correctly.
        /// </summary>
        public string ApplicableTypes
        {
            get
            {
                if (ElementTypes != null && ElementTypes.Count > 0)
                    return string.Join(", ", ElementTypes);
                return string.Join(", ", ApplicableElementTypes);
            }
        }

        // ── Legacy / backward-compatibility ───────────────────────────────────

        /// <summary>
        /// Deprecated — use ElementTypes instead.
        /// Retained for JSON round-trip compatibility with schemes persisted before
        /// the model expansion and for the UI layer that still operates on string
        /// type names via DimensionSchemeElementTypes constants.
        /// </summary>
        public List<string> ApplicableElementTypes { get; set; } = new List<string>();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Backward-compatible static class (kept for existing UI code)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// String constants for element type names used by the legacy UI layer.
    /// New code should prefer <see cref="DimensionSchemeElementType"/> instead.
    /// </summary>
    public static class DimensionSchemeElementTypes
    {
        public const string Door   = "Door";
        public const string Window = "Window";
        public const string Wall   = "Wall";
        public const string Floor  = "Floor";
        public const string Roof   = "Roof";
        public const string Room   = "Room";

        /// <summary>All legacy string constants as an array.</summary>
        public static readonly string[] All = { Door, Window, Wall, Floor, Roof, Room };

        /// <summary>
        /// Converts a legacy string element-type name to the new
        /// <see cref="DimensionSchemeElementType"/> enum, or null if not mapped.
        /// </summary>
        public static DimensionSchemeElementType? ToEnum(string legacyType)
        {
            switch (legacyType)
            {
                case Door:   return DimensionSchemeElementType.Doors;
                case Window: return DimensionSchemeElementType.Windows;
                case Wall:   return DimensionSchemeElementType.Walls;
                case Room:   return DimensionSchemeElementType.Rooms;
                default:     return null;
            }
        }

        /// <summary>
        /// Converts a <see cref="DimensionSchemeElementType"/> enum value back to
        /// the legacy string constant, or the enum name if not mapped.
        /// </summary>
        public static string FromEnum(DimensionSchemeElementType type)
        {
            switch (type)
            {
                case DimensionSchemeElementType.Doors:   return Door;
                case DimensionSchemeElementType.Windows: return Window;
                case DimensionSchemeElementType.Walls:   return Wall;
                case DimensionSchemeElementType.Rooms:   return Room;
                default: return type.ToString();
            }
        }
    }
}
