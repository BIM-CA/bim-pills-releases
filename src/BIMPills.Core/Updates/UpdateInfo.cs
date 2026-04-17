using System;

namespace BIMPills.Core.Updates
{
    /// <summary>
    /// Información de una actualización disponible obtenida desde el manifest público.
    /// </summary>
    public sealed class UpdateInfo
    {
        /// <summary>Versión del release — ej. "1.0.0-beta.3.3"</summary>
        public string TagName { get; set; } = string.Empty;

        /// <summary>Versión para mostrar al usuario — ej. "beta 3.3" (sin "1.0.0").</summary>
        public string DisplayVersion
        {
            get
            {
                // "1.0.0-beta.3.3" → "beta 3.3"
                var betaIdx = TagName.IndexOf("-beta.", StringComparison.OrdinalIgnoreCase);
                if (betaIdx >= 0) return "beta " + TagName.Substring(betaIdx + 6);
                return TagName;
            }
        }

        /// <summary>Notas del release (markdown, puede estar vacío).</summary>
        public string ReleaseNotes { get; set; } = string.Empty;

        /// <summary>URL directa al instalador (.exe) en GitHub Assets.</summary>
        public string? InstallerDownloadUrl { get; set; }
    }
}
