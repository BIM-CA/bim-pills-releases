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
