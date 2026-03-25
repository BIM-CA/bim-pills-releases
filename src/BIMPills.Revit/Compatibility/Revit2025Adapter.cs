#if REVIT2025
using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace BIMPills.Revit.Compatibility
{
    internal sealed class RevitVersionAdapterImpl : IRevitVersionAdapter
    {
        public string VersionLabel => "Revit 2025";

        public string GetUnitLabel(ForgeTypeId unitTypeId)
            => LabelUtils.GetLabelForUnit(unitTypeId);

        public IList<Material> GetMaterials(Document doc)
            => new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .ToList();
    }
}
#endif
