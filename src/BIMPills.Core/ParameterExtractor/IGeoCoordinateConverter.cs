namespace BIMPills.Core.ParameterExtractor
{
    /// <summary>
    /// Convierte desplazamientos XY en metros desde un punto de referencia
    /// (latitud/longitud base del proyecto) a coordenadas geográficas.
    /// </summary>
    public interface IGeoCoordinateConverter
    {
        /// <summary>
        /// Retorna (lat, lon) en grados decimales para un punto desplazado
        /// <paramref name="deltaEastMeters"/> al Este y <paramref name="deltaNorthMeters"/> al Norte
        /// del origen geográfico (<paramref name="baseLatDeg"/>, <paramref name="baseLonDeg"/>).
        /// Usa aproximación equirectangular — error &lt;1m @ 1km, válido para sitios BIM.
        /// </summary>
        (double latDeg, double lonDeg) Offset(
            double baseLatDeg,
            double baseLonDeg,
            double deltaEastMeters,
            double deltaNorthMeters);
    }

    /// <summary>
    /// Formatea y parsea ángulos geográficos en notación grados-minutos-segundos.
    /// </summary>
    public interface IDmsFormatter
    {
        /// <summary>
        /// Formatea <paramref name="decimalDeg"/> como DMS. <paramref name="isLatitude"/>
        /// determina sufijo N/S vs E/W. Ejemplo: 40.4168 → 40°25'00.48"N.
        /// </summary>
        string Format(double decimalDeg, bool isLatitude, int secondsDecimals = 2);

        /// <summary>
        /// Parsea una cadena DMS de vuelta a grados decimales. Retorna false si la
        /// cadena no se puede interpretar.
        /// </summary>
        bool TryParse(string dms, out double decimalDeg);
    }
}
