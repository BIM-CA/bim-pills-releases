namespace BIMPills.Core.Audit
{
    /// <summary>
    /// Representa un elemento purgable (familia, vista, material, etc.) no utilizado en el modelo.
    /// </summary>
    public sealed class PurgeableItem
    {
        /// <summary>ElementId del elemento. Usa long para compatibilidad con Revit 2025+ (64-bit IDs).</summary>
        public long Id { get; }
        public string Name { get; }
        public string Category { get; }
        public string ItemType { get; }
        public long SizeBytes { get; }

        public PurgeableItem(long id, string name, string category, string itemType, long sizeBytes = 0)
        {
            Id = id;
            Name = name;
            Category = category;
            ItemType = itemType;
            SizeBytes = sizeBytes;
        }

        public string SizeLabel =>
            SizeBytes >= 1_048_576 ? $"{SizeBytes / 1_048_576.0:F1} MB" :
            SizeBytes >= 1_024    ? $"{SizeBytes / 1_024.0:F1} KB" :
            SizeBytes > 0         ? $"{SizeBytes} B" :
                                    "—";
    }
}
