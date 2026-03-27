using BIMPills.Core.Audit;
using BIMPills.Core.Documentacion;
using BIMPills.Core.Gestion;
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

        /// <summary>Tamaño del archivo del modelo en bytes.</summary>
        long GetModelFileSize();

        /// <summary>Cantidad total de elementos en el modelo.</summary>
        int GetTotalElementCount();

        IReadOnlyList<ModelWarningInfo> GetWarnings();
        IReadOnlyList<FamilyInfo> GetFamilySizes();
        IReadOnlyList<ViewInfo> GetUnplacedViews();
        IReadOnlyList<ElementInfo> GetElementsWithoutCategory();

        /// <summary>Elementos no utilizados que pueden purgarse.</summary>
        IReadOnlyList<PurgeableItem> GetPurgeableElements();

        /// <summary>Obtiene todas las familias cargadas en el modelo con su categoría.</summary>
        IReadOnlyList<FamilyExportInfo> GetLoadedFamilies();

        /// <summary>Exporta una familia a un archivo .rfa. Retorna true si tuvo éxito.</summary>
        bool ExportFamily(long familyId, string destinationPath);

        /// <summary>Purga los elementos con los IDs especificados. Retorna cuántos se eliminaron.</summary>
        int PurgeElements(IReadOnlyList<long> elementIds);

        /// <summary>Obtiene los subproyectos (Worksets) del modelo.</summary>
        IReadOnlyList<WorksetInfo> GetWorksets();

        /// <summary>Crea un nuevo subproyecto. Retorna true si tuvo éxito.</summary>
        bool CreateWorkset(string name);

        /// <summary>Renombra un subproyecto existente.</summary>
        bool RenameWorkset(long worksetId, string newName);

        // ── Documentación: Acotado de Vanos ──

        /// <summary>Obtiene los tipos de cota (DimensionType) disponibles en el proyecto.</summary>
        IReadOnlyList<DimensionTypeInfo> GetDimensionTypes();

        /// <summary>Cantidad de puertas visibles en la vista activa.</summary>
        int GetDoorCountInActiveView();

        /// <summary>Nombre de la vista activa.</summary>
        string GetActiveViewName();
    }
}
