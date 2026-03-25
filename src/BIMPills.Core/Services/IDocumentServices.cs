using BIMPills.Core.Audit;
using System.Collections.Generic;

namespace BIMPills.Core.Services
{
    /// <summary>
    /// Abstraction over the active Revit document.
    /// Exposes only what commands need — no Revit API types leak through.
    /// </summary>
    public interface IDocumentServices
    {
        string Title { get; }
        bool IsWorkshared { get; }

        IReadOnlyList<ModelWarningInfo> GetWarnings();
        IReadOnlyList<FamilyInfo> GetFamilySizes();
        IReadOnlyList<ViewInfo> GetUnplacedViews();
        IReadOnlyList<ElementInfo> GetElementsWithoutCategory();
    }
}
