using System.Collections.Generic;

namespace BIMPills.Core.Documentacion
{
    /// <summary>
    /// Datos recopilados del modelo para la herramienta de Acotado de Vanos.
    /// Se pasan a la ventana UI para poblar controles.
    /// </summary>
    public sealed class AcotadoVanosData
    {
        /// <summary>Cantidad de puertas detectadas en el alcance (vista/selección).</summary>
        public int DoorCount { get; }

        /// <summary>Tipos de cota disponibles en el proyecto.</summary>
        public IReadOnlyList<DimensionTypeInfo> DimensionTypes { get; }

        /// <summary>Nombre de la vista activa.</summary>
        public string ActiveViewName { get; }

        public AcotadoVanosData(
            int doorCount,
            IReadOnlyList<DimensionTypeInfo> dimensionTypes,
            string activeViewName)
        {
            DoorCount = doorCount;
            DimensionTypes = dimensionTypes;
            ActiveViewName = activeViewName;
        }
    }
}
