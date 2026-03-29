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

        /// <summary>Cantidad de rejillas (grids) detectadas en la vista activa.</summary>
        public int GridCount { get; }

        /// <summary>Cantidad de muros detectados en la vista activa.</summary>
        public int WallCount { get; }

        /// <summary>Cantidad de niveles ARQ detectados en el modelo.</summary>
        public int LevelCount { get; }

        /// <summary>Tipos de cota disponibles en el proyecto.</summary>
        public IReadOnlyList<DimensionTypeInfo> DimensionTypes { get; }

        /// <summary>Nombre de la vista activa.</summary>
        public string ActiveViewName { get; }

        /// <summary>Esquemas de acotado disponibles.</summary>
        public static IReadOnlyList<SchemeOptionInfo> AvailableSchemes { get; } = new List<SchemeOptionInfo>
        {
            new("opening-width", "Anchos de vanos", "Cota el ancho de cada vano de puerta visible en la vista"),
            new("grid-combined", "Cotas a ejes", "Cotas totales y parciales entre rejillas en una acción"),
            new("interior-spaces", "Cotas espacios interiores", "Dimensiones H y V del espacio usando contornos de habitación"),
            new("arq-levels", "Niveles ARQ", "Cotas totales y parciales entre niveles cuyo tipo empieza con ARQ")
        };

        public AcotadoVanosData(
            int doorCount,
            IReadOnlyList<DimensionTypeInfo> dimensionTypes,
            string activeViewName,
            int gridCount = 0,
            int wallCount = 0,
            int levelCount = 0)
        {
            DoorCount = doorCount;
            DimensionTypes = dimensionTypes;
            ActiveViewName = activeViewName;
            GridCount = gridCount;
            WallCount = wallCount;
            LevelCount = levelCount;
        }
    }

    /// <summary>
    /// Información de un esquema de acotado disponible.
    /// </summary>
    public sealed class SchemeOptionInfo
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }

        public SchemeOptionInfo(string id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }
    }
}
