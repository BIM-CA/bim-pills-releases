using System;
using System.Reflection;

namespace BIMPills.Core.About
{
    public sealed class AboutInfo
    {
        public string PluginName => "BIMPills";
        public string Version
        {
            get
            {
                var full = typeof(AboutInfo).Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion ?? "0.0.0";
                // Strip build metadata (+commitHash) — show only semver portion
                var plusIdx = full.IndexOf('+');
                return plusIdx >= 0 ? full.Substring(0, plusIdx) : full;
            }
        }
        public string Developer => "Rodrigo Flores + BIM-CA Team";
        public string Company => "BIM-CA";
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
