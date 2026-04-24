using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;
using BIMPills.Core.ParameterExtractor;

namespace BIMPills.UI.ParameterExtractor
{
    /// <summary>
    /// View-model de una fila en el panel ORIGEN.
    /// IsConversionEnabled genera una fila convertida adicional en DESTINO.
    /// Los settings de conversión viven aquí — no en el DESTINO.
    /// </summary>
    public class SourceMappingVM : INotifyPropertyChanged
    {
        public Guid Id { get; } = Guid.NewGuid();

        private string _tag = "A";
        public string Tag
        {
            get => _tag;
            set { if (_tag != value) { _tag = value; OnPropertyChanged(nameof(Tag)); } }
        }

        private ExtractionSourceKind _source;
        public ExtractionSourceKind Source
        {
            get => _source;
            set
            {
                if (_source != value)
                {
                    _source = value;
                    OnPropertyChanged(nameof(Source));
                    OnPropertyChanged(nameof(ShowSourceParameterName));
                }
            }
        }

        private string _sourceParameterName = string.Empty;
        public string SourceParameterName
        {
            get => _sourceParameterName;
            set { if (_sourceParameterName != value) { _sourceParameterName = value; OnPropertyChanged(nameof(SourceParameterName)); } }
        }

        // ── Conversión ───────────────────────────────────────────────────────────

        private bool _isConversionEnabled;
        public bool IsConversionEnabled
        {
            get => _isConversionEnabled;
            set
            {
                if (_isConversionEnabled != value)
                {
                    _isConversionEnabled = value;
                    OnPropertyChanged(nameof(IsConversionEnabled));
                    OnPropertyChanged(nameof(ConversionSummary));
                }
            }
        }

        /// <summary>Origen de coord. para la salida CONVERTIDA.</summary>
        public CoordinateOrigin ConversionOrigin { get; set; } = CoordinateOrigin.Survey;

        /// <summary>Formato geográfico de la salida convertida.</summary>
        public GeoFormat ConversionGeoFormat { get; set; } = GeoFormat.Dms;

        /// <summary>Método de conversión (equirectangular o UTM).</summary>
        public GeoConversionMethod ConversionMethod { get; set; } = GeoConversionMethod.RevitProjectLocation;

        /// <summary>Zona UTM (1–60), solo relevante si ConversionMethod = UTM.</summary>
        public int ConversionUtmZone { get; set; } = 19;

        /// <summary>True = hemisferio norte, false = sur.</summary>
        public bool ConversionUtmIsNorth { get; set; } = false;

        public string ConversionSummary
        {
            get
            {
                if (!IsConversionEnabled) return "Sin conversión";
                string method = ConversionMethod == GeoConversionMethod.UTM
                    ? $"UTM Z{ConversionUtmZone}{(ConversionUtmIsNorth ? "N" : "S")}"
                    : "RevitGeo";
                return $"{ConversionOrigin} · {ConversionGeoFormat} · {method}";
            }
        }

        // ── Apariencia ───────────────────────────────────────────────────────────

        private Brush _tagBrush = Brushes.Gray;
        public Brush TagBrush
        {
            get => _tagBrush;
            set { if (!ReferenceEquals(_tagBrush, value)) { _tagBrush = value; OnPropertyChanged(nameof(TagBrush)); } }
        }

        public bool ShowSourceParameterName => Source == ExtractionSourceKind.ElementProperty;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public static IReadOnlyList<ExtractionSourceKind> AllSources => new[]
        {
            ExtractionSourceKind.ElementProperty,
            ExtractionSourceKind.LocationX,
            ExtractionSourceKind.LocationY,
            ExtractionSourceKind.LocationZ,
            ExtractionSourceKind.Latitude,
            ExtractionSourceKind.Longitude,
            ExtractionSourceKind.Category,
            ExtractionSourceKind.FamilyName,
            ExtractionSourceKind.TypeName,
            ExtractionSourceKind.LevelName,
            ExtractionSourceKind.ElementId,
            ExtractionSourceKind.UniqueId,
        };
    }

    /// <summary>
    /// View-model de una fila en el panel DESTINO.
    /// DualRole: "Single" | "Raw" | "Converted".
    /// Los settings de conversión los lee BuildConfig() desde el SourceMappingVM padre.
    /// </summary>
    public class TargetMappingVM : INotifyPropertyChanged
    {
        public Guid GroupId { get; set; }

        private string _tag = "A";
        public string Tag
        {
            get => _tag;
            set { if (_tag != value) { _tag = value; OnPropertyChanged(nameof(Tag)); } }
        }

        public string DualRole { get; set; } = "Single";

        public bool IsPaired => DualRole != "Single";

        private Brush _tagBrush = Brushes.Gray;
        public Brush TagBrush
        {
            get => _tagBrush;
            set { if (!ReferenceEquals(_tagBrush, value)) { _tagBrush = value; OnPropertyChanged(nameof(TagBrush)); } }
        }

        private string _targetParameterName = string.Empty;
        public string TargetParameterName
        {
            get => _targetParameterName;
            set { if (_targetParameterName != value) { _targetParameterName = value; OnPropertyChanged(nameof(TargetParameterName)); } }
        }

        private ExtractionDataType _dataType = ExtractionDataType.Text;
        public ExtractionDataType DataType
        {
            get => _dataType;
            set { if (_dataType != value) { _dataType = value; OnPropertyChanged(nameof(DataType)); } }
        }

        // Mantenidos para que BuildConfig/ApplyConfigToGrid sigan funcionando
        // y como fallback para filas Single/Raw.
        public CoordinateOrigin CoordinateOrigin { get; set; } = CoordinateOrigin.Internal;
        public GeoFormat GeoFormat { get; set; } = GeoFormat.Decimal;

        private bool _createIfMissing = true;
        public bool CreateIfMissing
        {
            get => _createIfMissing;
            set { if (_createIfMissing != value) { _createIfMissing = value; OnPropertyChanged(nameof(CreateIfMissing)); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public static IReadOnlyList<ExtractionDataType> AllDataTypes => new[]
        {
            ExtractionDataType.Text,
            ExtractionDataType.Number,
            ExtractionDataType.Length,
            ExtractionDataType.Angle,
        };

        public static IReadOnlyList<CoordinateOrigin> AllOrigins => new[]
        {
            CoordinateOrigin.Internal,
            CoordinateOrigin.ProjectBase,
            CoordinateOrigin.Survey,
        };

        public static IReadOnlyList<GeoFormat> AllGeoFormats => new[]
        {
            GeoFormat.Decimal,
            GeoFormat.Dms,
        };

        public static IReadOnlyList<GeoConversionMethod> AllConversionMethods => new[]
        {
            GeoConversionMethod.RevitProjectLocation,
            GeoConversionMethod.UTM,
        };

        public static IReadOnlyList<ExtractionLengthUnits> AllLengthUnits => new[]
        {
            ExtractionLengthUnits.Meters,
            ExtractionLengthUnits.Centimeters,
            ExtractionLengthUnits.Millimeters,
            ExtractionLengthUnits.Feet,
        };

        public static IReadOnlyList<int> AllDecimalOptions => new[] { 0, 1, 2, 3, 4, 5, 6 };
    }
}
