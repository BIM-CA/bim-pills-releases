using System;

namespace BIMPills.Commands.ParameterExtractor
{
    /// <summary>
    /// Proyección UTM (Universal Transverse Mercator) bidireccional sobre el elipsoide WGS84.
    /// Precisión: &lt; 1 mm para zonas alejadas hasta ~3° del meridiano central.
    /// Series de Helmert de 6° orden — estándar para aplicaciones BIM / topografía.
    /// </summary>
    public static class UtmConverter
    {
        // ── Constantes WGS84 ────────────────────────────────────────────────────
        private const double A  = 6378137.0;               // semi-eje mayor (m)
        private const double F  = 1.0 / 298.257223563;     // achatamiento
        private const double K0 = 0.9996;                  // factor de escala UTM
        private const double E0 = 500_000.0;               // falso Este (m)

        private static readonly double E2  = 2 * F - F * F;               // e²
        private static readonly double E4  = E2 * E2;
        private static readonly double E6  = E4 * E2;
        private static readonly double Ep2 = E2 / (1 - E2);               // e'²

        // ── Zona automática ─────────────────────────────────────────────────────

        /// <summary>Calcula la zona UTM (1-60) a partir de la longitud decimal.</summary>
        public static int ZoneFromLongitude(double lonDeg) =>
            (int)Math.Floor((lonDeg + 180.0) / 6.0) % 60 + 1;

        // ── Forward: (lat, lon) → (Easting, Northing) ──────────────────────────

        /// <summary>
        /// Convierte latitud/longitud decimales a coordenadas UTM.
        /// </summary>
        /// <param name="latDeg">Latitud en grados decimales (–90 a +90).</param>
        /// <param name="lonDeg">Longitud en grados decimales (–180 a +180).</param>
        /// <param name="zone">Zona UTM (1–60).</param>
        /// <param name="isNorth">True = hemisferio Norte (falso Norte = 0 m); False = Sur (falso Norte = 10 000 000 m).</param>
        /// <returns>(Easting, Northing) en metros.</returns>
        public static (double easting, double northing) FromLatLon(
            double latDeg, double lonDeg, int zone, bool isNorth = true)
        {
            double n0       = isNorth ? 0.0 : 10_000_000.0;
            double lambda0  = CentralMeridian(zone);

            double phi    = Deg2Rad(latDeg);
            double lambda = Deg2Rad(lonDeg);
            double dL     = lambda - lambda0;

            double sinPhi = Math.Sin(phi);
            double cosPhi = Math.Cos(phi);
            double tanPhi = Math.Tan(phi);

            double T = tanPhi * tanPhi;
            double C = Ep2 * cosPhi * cosPhi;
            double Nv = A / Math.Sqrt(1 - E2 * sinPhi * sinPhi);   // radio de curvatura en N vertical
            double Al = cosPhi * dL;

            double M = MeridionalArc(phi);

            double a2 = Al * Al;
            double a3 = a2 * Al;
            double a4 = a3 * Al;
            double a5 = a4 * Al;

            double easting = E0 + K0 * Nv * (
                Al
                + (1 - T + C) * a3 / 6.0
                + (5 - 18 * T + T * T + 72 * C - 58 * Ep2) * a5 / 120.0);

            double northing = n0 + K0 * (
                M + Nv * tanPhi * (
                      a2 / 2.0
                    + (5 - T + 9 * C + 4 * C * C) * a4 / 24.0
                    + (61 - 58 * T + T * T + 600 * C - 330 * Ep2) * a4 * a2 / 720.0));

            return (easting, northing);
        }

        // ── Inverse: (Easting, Northing) → (lat, lon) ──────────────────────────

        /// <summary>
        /// Convierte coordenadas UTM a latitud/longitud decimales.
        /// </summary>
        /// <param name="easting">Este en metros.</param>
        /// <param name="northing">Norte en metros.</param>
        /// <param name="zone">Zona UTM (1–60).</param>
        /// <param name="isNorth">True = hemisferio Norte; False = Sur.</param>
        /// <returns>(latDeg, lonDeg) en grados decimales.</returns>
        public static (double latDeg, double lonDeg) ToLatLon(
            double easting, double northing, int zone, bool isNorth = true)
        {
            double n0      = isNorth ? 0.0 : 10_000_000.0;
            double lambda0 = CentralMeridian(zone);

            // Parámetro auxiliar e1 (para series de Helmert)
            double sqrtE2 = Math.Sqrt(1 - E2);
            double e1     = (1 - sqrtE2) / (1 + sqrtE2);
            double e12    = e1 * e1;
            double e13    = e12 * e1;
            double e14    = e13 * e1;

            // Arco meridional reducido
            double M  = (northing - n0) / K0;
            double mu = M / (A * (1 - E2 / 4 - 3 * E4 / 64 - 5 * E6 / 256));

            // Latitud de pie (footprint latitude phi1) — serie de Helmert
            double phi1 = mu
                + (3 * e1 / 2 - 27 * e13 / 32)   * Math.Sin(2 * mu)
                + (21 * e12 / 16 - 55 * e14 / 32) * Math.Sin(4 * mu)
                + (151 * e13 / 96)                 * Math.Sin(6 * mu)
                + (1097 * e14 / 512)               * Math.Sin(8 * mu);

            double sinPhi1 = Math.Sin(phi1);
            double cosPhi1 = Math.Cos(phi1);
            double tanPhi1 = Math.Tan(phi1);

            double T1  = tanPhi1 * tanPhi1;
            double T12 = T1 * T1;
            double C1  = Ep2 * cosPhi1 * cosPhi1;
            double N1  = A / Math.Sqrt(1 - E2 * sinPhi1 * sinPhi1);
            double R1  = A * (1 - E2) / Math.Pow(1 - E2 * sinPhi1 * sinPhi1, 1.5);

            double D   = (easting - E0) / (N1 * K0);
            double d2  = D * D;
            double d3  = d2 * D;
            double d4  = d3 * D;
            double d5  = d4 * D;
            double d6  = d5 * D;

            double latRad = phi1 - (N1 * tanPhi1 / R1) * (
                  d2 / 2.0
                - (5 + 3 * T1 + 10 * C1 - 4 * C1 * C1 - 9 * Ep2) * d4 / 24.0
                + (61 + 90 * T1 + 298 * C1 + 45 * T12 - 252 * Ep2 - 3 * C1 * C1) * d6 / 720.0);

            double lonRad = lambda0 + (
                  D
                - (1 + 2 * T1 + C1) * d3 / 6.0
                + (5 - 2 * C1 + 28 * T1 - 3 * C1 * C1 + 8 * Ep2 + 24 * T12) * d5 / 120.0)
                / cosPhi1;

            return (Rad2Deg(latRad), Rad2Deg(lonRad));
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private static double CentralMeridian(int zone) =>
            Deg2Rad((zone - 1) * 6 - 180 + 3);

        private static double MeridionalArc(double phi) =>
            A * (
                  (1 - E2 / 4 - 3 * E4 / 64 - 5 * E6 / 256) * phi
                - (3 * E2 / 8 + 3 * E4 / 32 + 45 * E6 / 1024) * Math.Sin(2 * phi)
                + (15 * E4 / 256 + 45 * E6 / 1024) * Math.Sin(4 * phi)
                - (35 * E6 / 3072) * Math.Sin(6 * phi));

        private static double Deg2Rad(double d) => d * Math.PI / 180.0;
        private static double Rad2Deg(double r) => r * 180.0 / Math.PI;
    }
}
