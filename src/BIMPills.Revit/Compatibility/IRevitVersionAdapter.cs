using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace BIMPills.Revit.Compatibility
{
    /// <summary>
    /// Isolates API surface that differs between Revit versions.
    /// Add new members here when a version introduces a breaking change.
    /// </summary>
    public interface IRevitVersionAdapter
    {
        string VersionLabel { get; }

        /// <summary>Returns the display label for a unit type.</summary>
        string GetUnitLabel(ForgeTypeId unitTypeId);

        /// <summary>Returns all materials in the document.</summary>
        IList<Material> GetMaterials(Document doc);
    }
}
