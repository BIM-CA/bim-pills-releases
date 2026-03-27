namespace BIMPills.Core.Documentacion
{
    /// <summary>
    /// Resultado de la ejecución del acotado automático.
    /// </summary>
    public sealed class AcotadoVanosResult
    {
        /// <summary>Cantidad de cotas creadas exitosamente.</summary>
        public int DimensionsCreated { get; }

        /// <summary>Cantidad de puertas procesadas.</summary>
        public int DoorsProcessed { get; }

        /// <summary>Cantidad de puertas que ya tenían cota (omitidas).</summary>
        public int DoorsSkipped { get; }

        /// <summary>Mensaje descriptivo del resultado.</summary>
        public string Message { get; }

        public AcotadoVanosResult(int dimensionsCreated, int doorsProcessed, int doorsSkipped, string message)
        {
            DimensionsCreated = dimensionsCreated;
            DoorsProcessed = doorsProcessed;
            DoorsSkipped = doorsSkipped;
            Message = message;
        }
    }
}
