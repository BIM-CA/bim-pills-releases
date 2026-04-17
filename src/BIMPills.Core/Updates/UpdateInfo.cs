namespace BIMPills.Core.Updates
{
    /// <summary>
    /// Información de una actualización disponible obtenida desde el manifest público.
    /// </summary>
    public sealed class UpdateInfo
    {
        /// <summary>Versión del release — ej. "1.0.0-beta.3.3"</summary>
        public string TagName { get; set; } = string.Empty;

        /// <summary>Versión para mostrar al usuario — igual que TagName.</summary>
        public string DisplayVersion => TagName;

        /// <summary>Notas del release (markdown, puede estar vacío).</summary>
        public string ReleaseNotes { get; set; } = string.Empty;

        /// <summary>URL directa al instalador (.exe) en GitHub Assets.</summary>
        public string? InstallerDownloadUrl { get; set; }
    }
}
