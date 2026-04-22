using System;
using BIMPills.Core.ParameterExtractor;

namespace BIMPills.Commands.ParameterExtractor
{
    /// <summary>
    /// Aproximación equirectangular para convertir desplazamientos cartesianos
    /// en metros a latitud / longitud. Fórmula:
    ///   deltaLat = deltaN / R
    ///   deltaLon = deltaE / (R · cos(lat))
    /// Con R = 6_378_137 m (radio ecuatorial WGS84).
    /// Error &lt; 1 m para radios de hasta ~1 km, aceptable para el ámbito BIM.
    /// </summary>
    public sealed class EquirectangularGeoConverter : IGeoCoordinateConverter
    {
        private const double EarthRadiusMeters = 6378137.0;

        public (double latDeg, double lonDeg) Offset(
            double baseLatDeg,
            double baseLonDeg,
            double deltaEastMeters,
            double deltaNorthMeters)
        {
            double baseLatRad = baseLatDeg * Math.PI / 180.0;

            double deltaLatRad = deltaNorthMeters / EarthRadiusMeters;
            double deltaLonRad = deltaEastMeters / (EarthRadiusMeters * Math.Cos(baseLatRad));

            double latDeg = baseLatDeg + (deltaLatRad * 180.0 / Math.PI);
            double lonDeg = baseLonDeg + (deltaLonRad * 180.0 / Math.PI);

            return (latDeg, lonDeg);
        }
    }
}
