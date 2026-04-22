using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;
using BIMPills.Core.ParameterExtractor;

namespace BIMPills.UI.ParameterExtractor
{
    /// <summary>
    /// View-model de una fila en el panel ORIGEN. Un origen produce 1 destino
    /// (simple) o 2 destinos (dual: crudo + convertido). El tag es una letra
    /// (A, B, C, ...) que identifica el grupo.
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

        private bool _isDual;
        public bool IsDual
        {
            get => _isDual;
            set
            {
                if (_isDual != value)
                {
                    _isDual = value;
                    OnPropertyChanged(nameof(IsDual));
                }
            }
        }

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
    /// View-model de una fila en el panel DESTINO. Referencia el grupo origen
    /// por GroupId. Tag es letra sola (origen simple) o letra+número
    /// (origen dual: "B1" = crudo, "B2" = convertido).
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

        /// <summary>"Single", "Raw" (dual crudo), "Converted" (dual convertido).</summary>
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

        private CoordinateOrigin _coordinateOrigin = CoordinateOrigin.Internal;
        public CoordinateOrigin CoordinateOrigin
        {
            get => _coordinateOrigin;
            set { if (_coordinateOrigin != value) { _coordinateOrigin = value; OnPropertyChanged(nameof(CoordinateOrigin)); } }
        }

        private GeoFormat _geoFormat = GeoFormat.Decimal;
        public GeoFormat GeoFormat
        {
            get => _geoFormat;
            set
            {
                if (_geoFormat != value)
                {
                    _geoFormat = value;
                    OnPropertyChanged(nameof(GeoFormat));
                }
            }
        }

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
