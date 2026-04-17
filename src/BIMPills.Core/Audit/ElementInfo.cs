namespace BIMPills.Core.Audit
{
    public sealed class ElementInfo
    {
        public int Id { get; }
        public string Name { get; }
        public string? Category { get; }
        /// <summary>Nombre de clase de Revit del elemento (ej. "ImportInstance", "Group").</summary>
        public string? ClassName { get; }
        /// <summary>True si Revit confirmó que el elemento se puede eliminar.</summary>
        public bool CanDelete { get; }
        /// <summary>Descripción legible explicando qué es este elemento y si es seguro borrarlo.</summary>
        public string? Description { get; }

        public ElementInfo(int id, string name, string? category, string? className = null, bool canDelete = true, string? description = null)
        {
            Id = id;
            Name = name;
            Category = category;
            ClassName = className;
            CanDelete = canDelete;
            Description = description;
        }
    }
}
