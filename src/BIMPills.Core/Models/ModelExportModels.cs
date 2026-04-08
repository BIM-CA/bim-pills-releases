namespace BIMPills.Core.Models
{
    /// <summary>Supported model export formats.</summary>
    public enum ModelExportFormat
    {
        NWC
        // Future: IFC, FBX, etc.
    }

    /// <summary>Scope of NWC export.</summary>
    public enum NwcExportScope
    {
        Model,
        SpecificView,
        Selection
    }

    /// <summary>A 3D view available for NWC export scoping.</summary>
    public class NwcViewInfo
    {
        public long ElementId { get; set; }
        public string Name { get; set; } = "";
    }

    /// <summary>Coordinate system for NWC export.</summary>
    public enum NwcCoordinates
    {
        Shared,
        Internal
    }

    /// <summary>Parameter export level for NWC.</summary>
    public enum NwcParameters
    {
        All,
        Elements,
        None
    }

    /// <summary>Geometry precision for NWC faceting.</summary>
    public enum NwcFacetingPrecision
    {
        Low,
        Medium,
        High
    }

    /// <summary>Named preset storing a full NWC export configuration.</summary>
    public class NwcExportPreset
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public NwcExportConfig Config { get; set; } = new();
        public System.DateTime CreatedAt { get; set; } = System.DateTime.UtcNow;
    }

    /// <summary>
    /// Configuration for NWC (Navisworks) model export.
    /// Maps to Revit's NavisworksExportOptions.
    /// </summary>
    public class NwcExportConfig
    {
        public NwcExportScope Scope { get; set; } = NwcExportScope.Model;
        public long? ViewId { get; set; }
        public bool ExportLinks { get; set; } = true;
        public NwcCoordinates Coordinates { get; set; } = NwcCoordinates.Shared;
        public NwcParameters Parameters { get; set; } = NwcParameters.All;
        public NwcFacetingPrecision FacetingPrecision { get; set; } = NwcFacetingPrecision.Medium;
        public bool ConvertElementProperties { get; set; } = true;
        public bool ExportRoomAsAttribute { get; set; } = true;
        public bool ExportRoomGeometry { get; set; }
        public bool DivideFileIntoLevels { get; set; }
        public bool ExportUrls { get; set; }
        public bool FindMissingMaterials { get; set; }
        public string DestinationFolder { get; set; } = "";
        public string FileName { get; set; } = "";
        /// <summary>Raw template (may contain {Token} placeholders). Stored for preset recall.</summary>
        public string FileNameTemplate { get; set; } = "";
    }
}
