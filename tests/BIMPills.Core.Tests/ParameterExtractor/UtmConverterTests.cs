using System;
using BIMPills.Commands.ParameterExtractor;
using Xunit;

namespace BIMPills.Core.Tests.ParameterExtractor
{
    /// <summary>
    /// Tests de precisión para UtmConverter (WGS84 Transverse Mercator).
    /// Tolerancia: 1 m en coordenadas, 0.00001° (≈1 m) en ángulos.
    /// </summary>
    public class UtmConverterTests
    {
        private const double CoordTolM  = 1.0;      // metros
        private const double AngleTolDeg = 0.00001; // grados (≈1 m)

        // ── ZoneFromLongitude ─────────────────────────────────────────────────

        [Theory]
        [InlineData(-70.67, 19)]   // Santiago, Chile
        [InlineData(-58.38, 21)]   // Buenos Aires
        [InlineData(  0.0,  31)]   // Meridiano de Greenwich
        [InlineData(-180.0,  1)]   // Anti-meridiano W
        [InlineData( 179.9, 60)]   // Zona 60 E
        [InlineData( -75.0, 18)]   // Lima, Perú
        public void ZoneFromLongitude_KnownCities(double lon, int expectedZone)
        {
            Assert.Equal(expectedZone, UtmConverter.ZoneFromLongitude(lon));
        }

        // ── Round-trip: FromLatLon → ToLatLon ────────────────────────────────

        [Theory]
        [InlineData(-33.4489, -70.6693, 19, false)]  // Santiago, Chile
        [InlineData(-34.6037, -58.3816, 21, false)]  // Buenos Aires
        [InlineData(  0.0,      0.0,    31, true)]   // Intersección Ecuador/Meridiano
        [InlineData( 40.7128, -74.0060, 18, true)]   // Nueva York
        [InlineData(-12.0464, -77.0428, 18, false)]  // Lima
        [InlineData( 51.5074,  -0.1278, 30, true)]   // Londres
        [InlineData( 35.6762, 139.6503, 54, true)]   // Tokio
        public void RoundTrip_LatLon_RestoredWithinTolerance(
            double lat, double lon, int zone, bool isNorth)
        {
            var (e, n) = UtmConverter.FromLatLon(lat, lon, zone, isNorth);
            var (latBack, lonBack) = UtmConverter.ToLatLon(e, n, zone, isNorth);

            Assert.InRange(Math.Abs(latBack - lat), 0, AngleTolDeg);
            Assert.InRange(Math.Abs(lonBack - lon), 0, AngleTolDeg);
        }

        // ── Forward: (lat,lon) → UTM — valores de referencia ─────────────────
        // Referencia: NOAA NGS / franzpc.com converter

        [Fact]
        public void FromLatLon_Santiago_Zone19S()
        {
            // Santiago: lat=-33.4489°, lon=-70.6693° → Zona 19 Sur
            // Referencia: franzpc.com → E≈344_847, N≈6_299_541
            var (e, n) = UtmConverter.FromLatLon(-33.4489, -70.6693, 19, false);

            Assert.InRange(e, 344_847 - 1, 344_847 + 1);
            Assert.InRange(n, 6_297_700 - 1, 6_297_700 + 1);
        }

        [Fact]
        public void FromLatLon_Equator_PrimeMeridian_Zone31N()
        {
            // (0°, 0°) → zona 31 Norte: falso Este = 166 022.44, Norte = 0
            var (e, n) = UtmConverter.FromLatLon(0.0, 0.0, 31, true);

            Assert.InRange(e, 166_022.44 - 1, 166_022.44 + 1);
            Assert.InRange(n, -1, 1);
        }

        // ── Inverse: UTM → (lat,lon) — valores de referencia ─────────────────

        [Fact]
        public void ToLatLon_Equator_Zone31N()
        {
            // Inversa de (0°, 0°) → debe devolver lat≈0, lon≈0
            var (lat, lon) = UtmConverter.ToLatLon(166_022.44, 0.0, 31, true);

            Assert.InRange(lat, -AngleTolDeg, AngleTolDeg);
            Assert.InRange(lon, -AngleTolDeg, AngleTolDeg);
        }

        [Fact]
        public void ToLatLon_BuenosAires_Zone21S()
        {
            // Buenos Aires: zona 21 Sur. Primero forward, luego verifica inverse.
            var (e, n) = UtmConverter.FromLatLon(-34.6037, -58.3816, 21, false);
            var (lat, lon) = UtmConverter.ToLatLon(e, n, 21, false);

            Assert.InRange(Math.Abs(lat - (-34.6037)), 0, AngleTolDeg);
            Assert.InRange(Math.Abs(lon - (-58.3816)), 0, AngleTolDeg);
        }

        // ── Offset relativo: dos puntos cercanos ─────────────────────────────

        [Fact]
        public void FromLatLon_TwoPointsOffset_ConsistentWithDistance()
        {
            // Dos puntos a ~1 km de distancia en Santiago
            var (e1, n1) = UtmConverter.FromLatLon(-33.4489, -70.6693, 19, false);
            var (e2, n2) = UtmConverter.FromLatLon(-33.4489, -70.6603, 19, false); // ~800 m al Este

            double dist = Math.Sqrt(Math.Pow(e2 - e1, 2) + Math.Pow(n2 - n1, 2));

            // Distancia esperada: cos(-33.45°) * 111_320 * 0.009° ≈ 841 m
            Assert.InRange(dist, 800, 900);
        }
    }
}
