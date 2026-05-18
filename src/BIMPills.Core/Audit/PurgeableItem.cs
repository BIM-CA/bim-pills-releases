namespace BIMPills.Core.Audit
{
    /// <summary>
    /// Cómo se determinó que el elemento no está en uso.
    /// Exact = Revit lo confirmó directamente; Heuristic = inferido por análisis propio.
    /// </summary>
    public enum DetectionConfidence { Exact, Heuristic }

    /// <summary>Nivel de riesgo asociado a eliminar este elemento.</summary>
    public enum RiskLevel { Low, Medium, High }

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

        /// <summary>Cómo se determinó que el elemento no está en uso.</summary>
        public DetectionConfidence Confidence { get; }

        /// <summary>Nivel de riesgo si se elimina.</summary>
        public RiskLevel Risk { get; }

        public PurgeableItem(
            long id,
            string name,
            string category,
            string itemType,
            long sizeBytes = 0,
            DetectionConfidence confidence = DetectionConfidence.Heuristic,
            RiskLevel risk = RiskLevel.High)
        {
            Id         = id;
            Name       = name;
            Category   = category;
            ItemType   = itemType;
            SizeBytes  = sizeBytes;
            Confidence = confidence;
            Risk       = risk;
        }

        public string SizeLabel =>
            SizeBytes >= 1_048_576 ? $"{SizeBytes / 1_048_576.0:F1} MB" :
            SizeBytes >= 1_024    ? $"{SizeBytes / 1_024.0:F1} KB" :
            SizeBytes > 0         ? $"{SizeBytes} B" :
                                    "—";
    }
}
