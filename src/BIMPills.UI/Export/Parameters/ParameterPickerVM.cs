using System.ComponentModel;
using BIMPills.Core.ParameterExtractor;

namespace BIMPills.UI.Export.Parameters
{
    /// <summary>
    /// Una categoría de Revit en el panel izquierdo del picker.
    /// </summary>
    public class PickerCategoryVM : INotifyPropertyChanged
    {
        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set { if (_isChecked != value) { _isChecked = value; OnPropertyChanged(nameof(IsChecked)); } }
        }

        public string Name { get; set; } = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Una fuente disponible en el panel derecho del picker.
    /// Cada ítem puede generar una regla simple o dual (Raw + Converted).
    /// </summary>
    public class PickerSourceItemVM : INotifyPropertyChanged
    {
        // ── Selección ────────────────────────────────────────────────────────────

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set { if (_isChecked != value) { _isChecked = value; OnPropertyChanged(nameof(IsChecked)); } }
        }

        // ── Descripción ──────────────────────────────────────────────────────────

        public string DisplayName { get; set; } = string.Empty;
        public ExtractionSourceKind Source { get; set; }
        public string DefaultTargetName { get; set; } = string.Empty;
        public ExtractionDataType DefaultDataType { get; set; } = ExtractionDataType.Text;

        // ── Opciones de coordenada (LocationX/Y/Z) ───────────────────────────────

        public bool ShowCoordinateOrigin { get; set; }

        private CoordinateOrigin _coordinateOrigin = CoordinateOrigin.Internal;
        public CoordinateOrigin CoordinateOrigin
        {
            get => _coordinateOrigin;
            set { if (_coordinateOrigin != value) { _coordinateOrigin = value; OnPropertyChanged(nameof(CoordinateOrigin)); } }
        }

        // ── Opciones geográficas (Latitude/Longitude) ────────────────────────────

        public bool ShowGeoFormat { get; set; }

        private GeoFormat _geoFormat = GeoFormat.Decimal;
        public GeoFormat GeoFormat
        {
            get => _geoFormat;
            set { if (_geoFormat != value) { _geoFormat = value; OnPropertyChanged(nameof(GeoFormat)); } }
        }

        // ── Dual (raw + convertido) ───────────────────────────────────────────────

        public bool SupportsDual { get; set; }

        private bool _isDual;
        public bool IsDual
        {
            get => _isDual;
            set { if (_isDual != value) { _isDual = value; OnPropertyChanged(nameof(IsDual)); } }
        }

        /// <summary>Nombre del destino secundario (convertido). Pre-rellenado con valor por defecto.</summary>
        public string SecondaryTargetName { get; set; } = string.Empty;

        /// <summary>Sistema de coordenadas del destino convertido.</summary>
        public CoordinateOrigin SecondaryOrigin { get; set; } = CoordinateOrigin.ProjectBase;

        /// <summary>Formato geográfico del destino convertido (DMS por defecto para Lat/Lon).</summary>
        public GeoFormat SecondaryGeoFormat { get; set; } = GeoFormat.Dms;

        /// <summary>Tipo de dato del destino secundario. Si null, hereda DefaultDataType.</summary>
        public ExtractionDataType? SecondaryDataType { get; set; }

        // ── Opciones de conversión geo (RAW) ─────────────────────────────────────

        public GeoConversionMethod GeoConversionMethod { get; set; } = GeoConversionMethod.RevitProjectLocation;
        public int  UtmZone              { get; set; } = 19;
        public bool UtmIsNorthHemisphere { get; set; } = false;

        // ── Opciones de conversión geo (Convertido) ───────────────────────────────

        public GeoConversionMethod SecondaryConversionMethod  { get; set; } = GeoConversionMethod.RevitProjectLocation;
        public int  SecondaryUtmZone              { get; set; } = 19;
        public bool SecondaryUtmIsNorthHemisphere { get; set; } = false;

        // ── INotifyPropertyChanged ────────────────────────────────────────────────

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
