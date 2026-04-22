using System;
using System.Collections.Generic;

namespace BIMPills.Core.ParameterExtractor
{
    /// <summary>
    /// Origen desde donde se calcula la posición XYZ al extraer coordenadas.
    /// </summary>
    public enum CoordinateOrigin
    {
        Internal,      // Internal Origin (Revit)
        ProjectBase,   // Project Base Point
        Survey         // Survey Point
    }

    /// <summary>
    /// Formato de salida para latitud / longitud (y otros ángulos geográficos).
    /// El formato determina el tipo del parámetro destino:
    ///  - Decimal → Number (double)
    ///  - Dms     → Text (string "40°25'00.48"N")
    /// </summary>
    public enum GeoFormat
    {
        Decimal,
        Dms
    }

    /// <summary>
    /// Tipo de dato esperado del destino. Se mapea a SpecTypeId en la capa Revit.
    /// </summary>
    public enum ExtractionDataType
    {
        Text,
        Number,
        Length,
        Angle
    }

    /// <summary>
    /// Qué dato queremos extraer del elemento.
    /// </summary>
    public enum ExtractionSourceKind
    {
        ElementProperty,   // parámetro existente del elemento
        LocationX,
        LocationY,
        LocationZ,
        Latitude,
        Longitude,
        Category,
        FamilyName,
        TypeName,
        LevelName,
        ElementId,
        UniqueId
    }

    /// <summary>
    /// Especifica cómo localizar el destino (parámetro del elemento).
    /// El destino puede ser un parámetro compartido creado por la herramienta
    /// o un parámetro de proyecto/built-in ya existente.
    /// </summary>
    public class ExtractionTarget
    {
        public string ParameterName { get; set; } = string.Empty;
        public ExtractionDataType DataType { get; set; } = ExtractionDataType.Text;

        /// <summary>
        /// Si es true y el parámetro no existe, se crea como SharedParameter
        /// en el grupo "Datos" (GroupTypeId.Data) con InstanceBinding.
        /// </summary>
        public bool CreateIfMissing { get; set; } = true;
    }

    /// <summary>
    /// Regla de extracción sencilla: una fuente, un destino.
    /// Opcionalmente especifica el parámetro fuente cuando Source = ElementProperty.
    /// </summary>
    public class ExtractionRule
    {
        public ExtractionSourceKind Source { get; set; }
        public string SourceParameterName { get; set; } = string.Empty;
        public ExtractionTarget Target { get; set; } = new();

        /// <summary>
        /// Opcional: origen de coordenadas para sources Location* / Latitude / Longitude.
        /// </summary>
        public CoordinateOrigin CoordinateOrigin { get; set; } = CoordinateOrigin.Internal;

        /// <summary>
        /// Opcional: formato para sources Latitude/Longitude.
        /// </summary>
        public GeoFormat GeoFormat { get; set; } = GeoFormat.Decimal;
    }

    /// <summary>
    /// Regla dual-output: extrae el mismo dato dos veces — una cruda y otra convertida.
    /// Ejemplo: X Internal (cruda, Length) + X Project (convertida restando PBP offset, Length).
    /// </summary>
    public class DualOutputRule
    {
        public ExtractionSourceKind Source { get; set; }
        public ExtractionTarget RawTarget { get; set; } = new();
        public ExtractionTarget ConvertedTarget { get; set; } = new();
        public CoordinateOrigin ConvertedOrigin { get; set; } = CoordinateOrigin.ProjectBase;
        public GeoFormat ConvertedGeoFormat { get; set; } = GeoFormat.Decimal;
    }

    /// <summary>
    /// Unidad de longitud en la que se escriben los valores Length extraídos
    /// (LocationX/Y/Z). El writer convierte desde internal units (ft) a esta unidad.
    /// </summary>
    public enum ExtractionLengthUnits
    {
        Meters,
        Centimeters,
        Millimeters,
        Feet
    }

    /// <summary>
    /// Configuración completa de una extracción.
    /// </summary>
    public class ExtractionConfig
    {
        public List<ExtractionRule> Rules { get; set; } = new();
        public List<DualOutputRule> DualRules { get; set; } = new();

        /// <summary>Unidad para valores Length (X/Y/Z). Default: metros.</summary>
        public ExtractionLengthUnits LengthUnits { get; set; } = ExtractionLengthUnits.Meters;

        /// <summary>Número de decimales para Length y Latitud/Longitud decimal. 0-6.</summary>
        public int Decimals { get; set; } = 3;
    }

    /// <summary>
    /// Perfil guardado: un conjunto de reglas con nombre, persistido a JSON.
    /// </summary>
    public class ExtractionPreset
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public ExtractionConfig Config { get; set; } = new();
    }

    /// <summary>
    /// Resultado de aplicar reglas a un conjunto de elementos.
    /// </summary>
    public class ExtractionResult
    {
        public int ElementsProcessed { get; set; }
        public int ParametersWritten { get; set; }
        public int ParametersCreated { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
