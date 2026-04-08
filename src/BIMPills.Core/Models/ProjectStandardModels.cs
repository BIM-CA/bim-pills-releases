using System.Collections.Generic;

namespace BIMPills.Core.Models
{
    /// <summary>
    /// Category keys for transferable project standards.
    /// String constants to avoid enum versioning issues across assemblies.
    /// </summary>
    public static class ProjectStandardKeys
    {
        public const string DimensionTypes  = "DimensionTypes";
        public const string TextNoteTypes   = "TextNoteTypes";
        public const string LineStyles      = "LineStyles";
        public const string WallTypes       = "WallTypes";
        public const string FloorTypes      = "FloorTypes";
        public const string CeilingTypes    = "CeilingTypes";
        public const string RoofTypes       = "RoofTypes";
        public const string SpotDimTypes    = "SpotDimensionTypes";
        public const string FillPatterns    = "FillPatterns";
    }

    /// <summary>Metadata for a transferable category shown in the left panel.</summary>
    public class ProjectStandardCategoryInfo
    {
        public string Key         { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Icon        { get; set; } = "";  // Segoe MDL2 glyph
        public string Group       { get; set; } = "";  // Group header (e.g. "Anotación")
        public int    ItemCount   { get; set; }        // Populated after loading source doc
    }

    /// <summary>One transferable item within a category.</summary>
    public class ProjectStandardItem
    {
        public long   Id         { get; set; }
        public string Name       { get; set; } = "";
        public string Detail     { get; set; } = "";  // Secondary info (e.g., structure layers)
        public bool   IsSelected { get; set; }
    }

    /// <summary>Result of a batch transfer operation.</summary>
    public class ProjectStandardTransferResult
    {
        public int          Transferred { get; set; }
        public int          Skipped     { get; set; }
        public int          Conflicts   { get; set; }
        public List<string> Errors       { get; set; } = new List<string>();
        public List<string> SkippedNames { get; set; } = new List<string>();
    }
}
