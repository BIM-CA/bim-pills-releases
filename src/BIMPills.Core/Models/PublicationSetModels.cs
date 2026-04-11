using System;
using System.Collections.Generic;

namespace BIMPills.Core.Models
{
    /// <summary>
    /// A saved selection of sheets and views for batch export.
    /// Persisted to JSON in %APPDATA%/BIMPills/PublicationSets/.
    /// </summary>
    public class PublicationSet
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public List<PublicationSetItem> Items { get; set; } = new List<PublicationSetItem>();
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }

        // ── Export settings (saved with the set) ──

        /// <summary>Export PDF when using this set.</summary>
        public bool ExportPdf { get; set; } = true;
        /// <summary>Export DWG when using this set.</summary>
        public bool ExportDwg { get; set; } = false;
        /// <summary>Naming pattern (e.g. "{SheetNumber}-{SheetName}").</summary>
        public string NamingPattern { get; set; } = "";
        /// <summary>Folder organization mode.</summary>
        public FolderOrganization FolderOrganization { get; set; } = FolderOrganization.ByFormat;
        /// <summary>Full PDF export settings (null = use defaults).</summary>
        public PdfExportSettings? PdfSettings { get; set; }
        /// <summary>Name of the Revit DWG export preset.</summary>
        public string DwgPresetName { get; set; } = "";
        /// <summary>Extra DWG options (linked xrefs, clean pcp).</summary>
        public bool DwgExportLinkedAsXrefs { get; set; }
        public bool DwgCleanPcpFiles { get; set; }
    }

    /// <summary>
    /// A single item (sheet or view) within a publication set.
    /// Uses UniqueId for stable references across Revit sessions.
    /// </summary>
    public class PublicationSetItem
    {
        /// <summary>Revit UniqueId (GUID-based, stable across sessions).</summary>
        public string UniqueId { get; set; } = "";
        /// <summary>Display name for UI when element is not found in current model.</summary>
        public string DisplayName { get; set; } = "";
        public ExportableItemType ItemType { get; set; }
    }
}
