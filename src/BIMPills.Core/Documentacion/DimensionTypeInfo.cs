namespace BIMPills.Core.Documentacion
{
    /// <summary>
    /// Información básica de un tipo de cota del proyecto Revit.
    /// </summary>
    public sealed class DimensionTypeInfo
    {
        public long Id { get; }
        public string Name { get; }

        public DimensionTypeInfo(long id, string name)
        {
            Id = id;
            Name = name;
        }

        public override string ToString() => Name;
    }
}
