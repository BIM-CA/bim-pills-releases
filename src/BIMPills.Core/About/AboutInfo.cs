using System;
using System.Reflection;

namespace BIMPills.Core.About
{
    public sealed class AboutInfo
    {
        public string PluginName => "BIMPills";
        public string Version =>
            typeof(AboutInfo).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
                ?? "0.0.0";
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
