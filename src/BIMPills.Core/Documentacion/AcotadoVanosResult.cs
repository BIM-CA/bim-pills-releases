using System.Collections.Generic;

namespace BIMPills.Core.Documentacion
{
    /// <summary>
    /// Resultado de la ejecución del acotado automático.
    /// </summary>
    public sealed class AcotadoVanosResult
    {
        /// <summary>Cantidad de cotas creadas exitosamente.</summary>
        public int DimensionsCreated { get; }

        /// <summary>Alias de DimensionsCreated para compatibilidad con la UI.</summary>
        public int CreatedCount => DimensionsCreated;

        /// <summary>Cantidad de puertas procesadas.</summary>
        public int DoorsProcessed { get; }

        /// <summary>Cantidad de puertas que ya tenían cota (omitidas).</summary>
        public int DoorsSkipped { get; }

        /// <summary>Mensaje descriptivo del resultado (resumen general).</summary>
        public string Message { get; }

        /// <summary>
        /// Mensaje de error fatal, si lo hubo. Null o vacío si no hubo error fatal.
        /// Cuando está establecido, la operación falló y no se crearon cotas.
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Lista de elementos omitidos con su motivo (ej. "Baño 101: Sin contorno válido").
        /// Null o vacía si no hubo elementos omitidos.
        /// </summary>
        public IReadOnlyList<string>? SkippedItems { get; }

        /// <summary>Constructor principal para resultado exitoso.</summary>
        public AcotadoVanosResult(int dimensionsCreated, int doorsProcessed, int doorsSkipped, string message)
        {
            DimensionsCreated = dimensionsCreated;
            DoorsProcessed = doorsProcessed;
            DoorsSkipped = doorsSkipped;
            Message = message;
            ErrorMessage = null;
            SkippedItems = null;
        }

        /// <summary>Constructor para resultado exitoso con elementos omitidos detallados.</summary>
        public AcotadoVanosResult(
            int dimensionsCreated,
            int doorsProcessed,
            int doorsSkipped,
            string message,
            IReadOnlyList<string>? skippedItems)
        {
            DimensionsCreated = dimensionsCreated;
            DoorsProcessed = doorsProcessed;
            DoorsSkipped = doorsSkipped;
            Message = message;
            ErrorMessage = null;
            SkippedItems = skippedItems;
        }

        // Private full constructor used by factory methods.
        private AcotadoVanosResult(
            int dimensionsCreated,
            int doorsProcessed,
            int doorsSkipped,
            string message,
            IReadOnlyList<string>? skippedItems,
            string? errorMessage)
        {
            DimensionsCreated = dimensionsCreated;
            DoorsProcessed = doorsProcessed;
            DoorsSkipped = doorsSkipped;
            Message = message;
            SkippedItems = skippedItems;
            ErrorMessage = errorMessage;
        }

        /// <summary>Crea un resultado de error fatal.</summary>
        public static AcotadoVanosResult CreateError(string errorMessage)
            => new AcotadoVanosResult(0, 0, 0, errorMessage, null, errorMessage);

        /// <summary>Crea un resultado exitoso con elementos omitidos detallados.</summary>
        public static AcotadoVanosResult CreateWithSkipped(
            int dimensionsCreated,
            int doorsProcessed,
            string message,
            IReadOnlyList<string> skippedItems)
            => new AcotadoVanosResult(
                dimensionsCreated,
                doorsProcessed,
                skippedItems?.Count ?? 0,
                message,
                skippedItems,
                null);
    }
}
