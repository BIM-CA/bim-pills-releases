namespace BIMPills.Core.Documentacion
{
    /// <summary>
    /// Configuración seleccionada por el usuario para el acotado automático de vanos.
    /// </summary>
    public sealed class AcotadoVanosSettings
    {
        /// <summary>Esquema de acotado: "opening-width", "grid-combined" o "interior-spaces".</summary>
        public string Scheme { get; set; } = "opening-width";

        /// <summary>ID del DimensionType seleccionado.</summary>
        public long DimensionTypeId { get; set; }

        /// <summary>Distancia de offset en milímetros (separación cota-elemento).</summary>
        public double OffsetMm { get; set; } = 150;

        /// <summary>True = vista activa, False = selección actual.</summary>
        public bool UseActiveView { get; set; } = true;

        /// <summary>
        /// Para esquemas de ejes: en qué extremo(s) colocar la cota.
        /// "both" = ambos extremos, "start" = inicio, "end" = fin.
        /// </summary>
        public string GridEndpoint { get; set; } = "end";
    }
}
