using System;
using System.Globalization;
using System.Text.RegularExpressions;
using BIMPills.Core.ParameterExtractor;

namespace BIMPills.Commands.ParameterExtractor
{
    /// <summary>
    /// Convierte entre grados decimales y notación grados-minutos-segundos.
    /// Formato emitido: D°MM'SS.ss"H (H = N/S/E/W).
    /// </summary>
    public sealed class DmsFormatter : IDmsFormatter
    {
        // Regex tolerante: acepta símbolos ° ' " o letras d m s, separadores con/sin espacio.
        private static readonly Regex DmsRegex = new Regex(
            @"^\s*(?<deg>-?\d+(?:[.,]\d+)?)\s*[°dº]\s*" +
            @"(?:(?<min>\d+(?:[.,]\d+)?)\s*['m′]\s*)?" +
            @"(?:(?<sec>\d+(?:[.,]\d+)?)\s*[""s″]?\s*)?" +
            @"(?<hem>[NSEWnsew])?\s*$",
            RegexOptions.Compiled);

        public string Format(double decimalDeg, bool isLatitude, int secondsDecimals = 2)
        {
            char hemisphere = isLatitude
                ? (decimalDeg >= 0 ? 'N' : 'S')
                : (decimalDeg >= 0 ? 'E' : 'W');

            double abs = Math.Abs(decimalDeg);
            int degrees = (int)Math.Floor(abs);
            double minutesFull = (abs - degrees) * 60.0;
            int minutes = (int)Math.Floor(minutesFull);
            double seconds = (minutesFull - minutes) * 60.0;

            // Corrección de redondeo: si seconds se redondea a 60, súbelo a minutos.
            int decs = Math.Max(0, secondsDecimals);
            string secFmt = decs > 0 ? "00." + new string('0', decs) : "00";
            string secString = seconds.ToString(secFmt, CultureInfo.InvariantCulture);
            if (double.Parse(secString, CultureInfo.InvariantCulture) >= 60.0)
            {
                seconds = 0.0;
                minutes += 1;
                secString = (0.0).ToString(secFmt, CultureInfo.InvariantCulture);
            }
            if (minutes >= 60)
            {
                minutes -= 60;
                degrees += 1;
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}°{1:D2}'{2}\"{3}",
                degrees, minutes, secString, hemisphere);
        }

        public bool TryParse(string dms, out double decimalDeg)
        {
            decimalDeg = 0.0;
            if (string.IsNullOrWhiteSpace(dms)) return false;

            var m = DmsRegex.Match(dms);
            if (!m.Success) return false;

            if (!TryParseDouble(m.Groups["deg"].Value, out double deg)) return false;
            double min = 0.0, sec = 0.0;
            if (m.Groups["min"].Success && !TryParseDouble(m.Groups["min"].Value, out min)) return false;
            if (m.Groups["sec"].Success && !TryParseDouble(m.Groups["sec"].Value, out sec)) return false;

            double sign = deg < 0 ? -1.0 : 1.0;
            double abs = Math.Abs(deg) + (min / 60.0) + (sec / 3600.0);
            decimalDeg = sign * abs;

            if (m.Groups["hem"].Success)
            {
                char h = char.ToUpperInvariant(m.Groups["hem"].Value[0]);
                if (h == 'S' || h == 'W') decimalDeg = -Math.Abs(decimalDeg);
                else if (h == 'N' || h == 'E') decimalDeg = Math.Abs(decimalDeg);
            }

            return true;
        }

        private static bool TryParseDouble(string value, out double result)
        {
            var normalized = value.Replace(',', '.');
            return double.TryParse(
                normalized,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out result);
        }
    }
}
