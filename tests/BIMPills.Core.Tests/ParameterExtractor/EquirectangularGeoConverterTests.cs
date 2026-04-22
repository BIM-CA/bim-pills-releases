using System;
using BIMPills.Commands.ParameterExtractor;
using Xunit;

namespace BIMPills.Core.Tests.ParameterExtractor
{
    public class EquirectangularGeoConverterTests
    {
        private readonly EquirectangularGeoConverter _sut = new();

        [Fact]
        public void Offset_ZeroDelta_ReturnsBaseCoords()
        {
            var (lat, lon) = _sut.Offset(40.4168, -3.7038, 0.0, 0.0);
            Assert.Equal(40.4168, lat, 6);
            Assert.Equal(-3.7038, lon, 6);
        }

        [Fact]
        public void Offset_NorthDelta_IncrementsLatitude()
        {
            var (lat, lon) = _sut.Offset(40.0, -3.0, 0.0, 1000.0);
            Assert.True(lat > 40.0);
            Assert.Equal(-3.0, lon, 6);
        }

        [Fact]
        public void Offset_EastDelta_IncrementsLongitude()
        {
            var (lat, lon) = _sut.Offset(40.0, -3.0, 1000.0, 0.0);
            Assert.Equal(40.0, lat, 6);
            Assert.True(lon > -3.0);
        }

        [Fact]
        public void Offset_OneKilometer_ErrorUnderOneMeter()
        {
            // Comparamos con la fórmula de Haversine como referencia de alta precisión.
            // Error esperado < 1m para radios de 1 km en latitudes medias.
            double baseLat = 40.0;
            double baseLon = -3.0;
            double dE = 1000.0;
            double dN = 1000.0;

            var (lat, lon) = _sut.Offset(baseLat, baseLon, dE, dN);

            double haversineDist = HaversineMeters(baseLat, baseLon, lat, lon);
            double cartesianDist = Math.Sqrt(dE * dE + dN * dN);

            double errorMeters = Math.Abs(haversineDist - cartesianDist);
            Assert.True(errorMeters < 1.0, $"Error fue {errorMeters:F3} m");
        }

        [Fact]
        public void Offset_SymmetricalRoundTrip()
        {
            // Ir y volver debe recuperar el punto base (dentro de tolerancia).
            double baseLat = 40.0, baseLon = -3.0;
            var (lat1, lon1) = _sut.Offset(baseLat, baseLon, 500.0, -500.0);
            var (lat2, lon2) = _sut.Offset(lat1, lon1, -500.0, 500.0);

            Assert.Equal(baseLat, lat2, 4);
            Assert.Equal(baseLon, lon2, 4);
        }

        private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6378137.0;
            double p1 = lat1 * Math.PI / 180.0;
            double p2 = lat2 * Math.PI / 180.0;
            double dp = (lat2 - lat1) * Math.PI / 180.0;
            double dl = (lon2 - lon1) * Math.PI / 180.0;

            double a = Math.Sin(dp / 2) * Math.Sin(dp / 2)
                     + Math.Cos(p1) * Math.Cos(p2)
                     * Math.Sin(dl / 2) * Math.Sin(dl / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
    }
}
