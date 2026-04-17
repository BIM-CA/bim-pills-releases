using System;
using System.Reflection;

namespace BIMPills.Core.About
{
    public sealed class AboutInfo
    {
        public string PluginName => "BIM Pills";
        /// <summary>
        /// User-facing version. InformationalVersion format: "1.0.0-beta.X.Y"
        /// (ej. "1.0.0-beta.3.3"). Retorna la versión limpia sin metadata de build.
        /// </summary>
        public string Version
        {
            get
            {
                var raw = typeof(AboutInfo).Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion ?? "1.0.0-beta.3.3";
                // Strip build metadata (+commitHash) added by SDK
                var plusIdx = raw.IndexOf('+');
                var clean = plusIdx >= 0 ? raw.Substring(0, plusIdx) : raw;
                // "1.0.0-beta.3.3" → "beta 3.3"  (user never sees "1.0.0")
                var betaIdx = clean.IndexOf("-beta.", StringComparison.OrdinalIgnoreCase);
                if (betaIdx >= 0) return "beta " + clean.Substring(betaIdx + 6);
                return clean;
            }
        }
        public string Developer => "MBA Arq. Rodrigo Flores";
        public string Company => "BIM-CA (Prototype, S.A.)";
        public string Website => "https://bim-ca.com";
        public string SupportEmail => "soporte@bim-ca.com";
        public string Description => "Herramientas inteligentes para optimizar tu flujo de trabajo en Revit.";
        public string Copyright => "© 2026 BIM-CA. Todos los derechos reservados.";

        // License fields — populated from ILicenseService at runtime
        public string? LicensePlan { get; set; }
        public string? LicenseStatus { get; set; }
        public DateTime? LicenseExpiry { get; set; }
        public string? LicenseHolder { get; set; }
    }
}
