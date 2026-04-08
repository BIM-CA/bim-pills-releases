using System.Collections.Generic;

namespace BIMPills.Core.Gestion
{
    public enum WorksetViewVisibility
    {
        IsolateWorkset,  // aislar: solo este workset visible
        HideOthers,      // ocultar los demás worksets
        NoRestriction    // sin restricción de visibilidad
    }

    public enum ViewConflictResolution
    {
        Skip,      // omitir si ya existe
        Overwrite  // sobrescribir
    }

    public enum ViewDetailLevel
    {
        Coarse,  // Grueso
        Medium,  // Medio
        Fine     // Fino
    }

    public class View3DCreationConfig
    {
        public string ViewNameTemplate { get; set; } = "{nombre} — 3D";
        public WorksetViewVisibility Visibility { get; set; } = WorksetViewVisibility.IsolateWorkset;
        public ViewConflictResolution ConflictResolution { get; set; } = ViewConflictResolution.Skip;
        public ViewDetailLevel DetailLevel { get; set; } = ViewDetailLevel.Medium;
        public bool HideAnnotationCategories { get; set; } = false;
        public bool SetCoordinationDiscipline { get; set; } = false;
        public List<long> WorksetIds { get; set; } = new();
        public List<string> WorksetNames { get; set; } = new();
    }

    public class View3DCreationResult
    {
        public int Created { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
