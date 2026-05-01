using BIMPills.Core.Updates;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace BIMPills.Infrastructure.Updates
{
    /// <summary>
    /// Lee el manifest público de versiones desde bim-pills-releases y compara
    /// con la versión instalada actual.
    /// Formato de versión esperado: "1.0.0-beta.X.Y" (ej. "1.0.0-beta.3.2").
    /// También acepta el formato legacy "beta-X.Y" / "beta X.Y".
    /// Este checker nunca lanza excepción — falla silenciosamente.
    /// </summary>
    public sealed class GitHubUpdateChecker
    {
        // Nota: el parámetro ?t= es un cache-buster para evitar que el CDN de
        // raw.githubusercontent.com sirva una versión vieja del manifest. Sin él,
        // el CDN puede cachear el archivo hasta 5 minutos y el check de versión
        // devolvería un resultado obsoleto.
        private static string ManifestUrl =>
            $"https://raw.githubusercontent.com/BIM-CA/bim-pills-releases/main/version.json?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        private static readonly HttpClient _http         = BuildApiClient();
        private static readonly HttpClient _downloadHttp = BuildDownloadClient();

        /// <summary>
        /// Comprueba si hay una versión más nueva en el manifest público.
        /// Retorna <see cref="UpdateInfo"/> si hay actualización, null si está al día o falla.
        /// </summary>
        /// <summary>
        /// Último diagnóstico del check — disponible para que la capa Revit lo loguee.
        /// </summary>
        public string LastDiagnostic { get; private set; } = string.Empty;

        public async Task<UpdateInfo?> CheckAsync(string currentVersionRaw)
        {
            try
            {
                var url  = ManifestUrl;
                var json = await _http.GetStringAsync(url);
                var manifest = JsonConvert.DeserializeObject<VersionManifest>(json);
                if (manifest == null || string.IsNullOrEmpty(manifest.Version))
                {
                    LastDiagnostic = "Manifest vacío o inválido.";
                    return null;
                }

                LastDiagnostic = $"Manifest OK — versión remota: {manifest.Version}";

                if (!IsNewer(manifest.Version!, currentVersionRaw))
                {
                    LastDiagnostic += " (sin actualización disponible)";
                    return null;
                }

                return new UpdateInfo
                {
                    TagName              = manifest.Version!,
                    ReleaseNotes         = TrimReleaseNotes(manifest.Notes ?? string.Empty),
                    InstallerDownloadUrl = manifest.InstallerUrl,
                };
            }
            catch (Exception ex)
            {
                LastDiagnostic = $"Error de red: {ex.GetType().Name} — {ex.Message}";
                return null;
            }
        }

        /// <summary>
        /// Descarga el instalador a una ruta temporal y retorna la ruta local.
        /// Retorna null si falla.
        /// </summary>
        public async Task<string?> DownloadInstallerAsync(UpdateInfo update)
        {
            if (string.IsNullOrEmpty(update.InstallerDownloadUrl)) return null;
            try
            {
                var dir  = Path.Combine(Path.GetTempPath(), "BIMPills");
                Directory.CreateDirectory(dir);
                var dest = Path.Combine(dir, "BIMPills_update_setup.exe");

                using var stream = await _downloadHttp.GetStreamAsync(update.InstallerDownloadUrl);
                using var file   = File.Create(dest);
                await stream.CopyToAsync(file);
                return dest;
            }
            catch
            {
                return null;
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static bool IsNewer(string latestRaw, string currentRaw)
        {
            var latest  = ParseVersion(latestRaw);
            var current = ParseVersion(currentRaw);
            if (latest == null || current == null) return false;
            return latest > current;
        }

        /// <summary>
        /// Parsea versiones en los formatos:
        ///   "1.0.0-beta.3.3"       → Version(1, 0, 3, 3)
        ///   "v1.0.0-beta.3.2-hotfix1" → Version(1, 0, 3, 2)  (hotfix ignorado)
        ///   "beta-1.0" / "beta 1.0"   → Version(1, 0, 1, 0)  (formato legacy)
        /// Retorna null si no puede parsear.
        /// </summary>
        private static Version? ParseVersion(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            var s = raw.ToLowerInvariant().Trim();

            // Strip leading 'v'
            if (s.Length > 0 && s[0] == 'v') s = s.Substring(1);

            // Strip -hotfixN suffix
            var hotfixIdx = s.IndexOf("-hotfix", StringComparison.Ordinal);
            if (hotfixIdx >= 0) s = s.Substring(0, hotfixIdx);

            // Format: "1.0.0-beta.X.Y"
            var betaIdx = s.IndexOf("-beta.", StringComparison.Ordinal);
            if (betaIdx >= 0)
            {
                var betaStr = s.Substring(betaIdx + 6); // e.g. "3.2"
                if (Version.TryParse(betaStr, out var betaVer))
                    return new Version(1, 0, betaVer.Major, betaVer.Minor >= 0 ? betaVer.Minor : 0);
                return null;
            }

            // Legacy format: "beta-X.Y" or "beta X.Y"
            if (s.StartsWith("beta-", StringComparison.Ordinal) ||
                s.StartsWith("beta ", StringComparison.Ordinal))
            {
                var numeric = s.Substring(5);
                return Version.TryParse(numeric, out var v)
                    ? new Version(1, 0, v.Major, v.Minor >= 0 ? v.Minor : 0)
                    : null;
            }

            return null;
        }

        private static string TrimReleaseNotes(string body)
        {
            const int MaxChars = 600;
            if (string.IsNullOrWhiteSpace(body)) return string.Empty;
            var trimmed = body.Trim();
            return trimmed.Length > MaxChars
                ? trimmed.Substring(0, MaxChars) + "…"
                : trimmed;
        }

        private static HttpClient BuildApiClient()
        {
            EnsureTls12();
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("BIMPills-Plugin", "1.0"));
            client.Timeout = TimeSpan.FromSeconds(10);
            return client;
        }

        private static HttpClient BuildDownloadClient()
        {
            EnsureTls12();
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("BIMPills-Plugin", "1.0"));
            client.Timeout = TimeSpan.FromMinutes(30);
            return client;
        }

        /// <summary>
        /// En .NET Framework 4.8 (Revit 2024) TLS 1.2 no siempre está habilitado
        /// por defecto, lo que hace que las peticiones HTTPS a GitHub fallen
        /// silenciosamente. Esta llamada garantiza TLS 1.2 en todas las versiones.
        /// En .NET 8+ (Revit 2025+) la propiedad está obsoleta pero es inofensiva.
        /// </summary>
        private static void EnsureTls12()
        {
            try
            {
                const System.Net.SecurityProtocolType Tls12 =
                    (System.Net.SecurityProtocolType)3072; // 0xC00 — TLS 1.2
                const System.Net.SecurityProtocolType Tls13 =
                    (System.Net.SecurityProtocolType)12288; // 0x3000 — TLS 1.3
                System.Net.ServicePointManager.SecurityProtocol |= Tls12 | Tls13;
            }
            catch
            {
                // Ignorar — si falla, el sistema usará su protocolo por defecto
            }
        }

        // ── Manifest JSON ──────────────────────────────────────────────────────

        private sealed class VersionManifest
        {
            [JsonProperty("version")]
            public string? Version { get; set; }

            [JsonProperty("notes")]
            public string? Notes { get; set; }

            [JsonProperty("installer_url")]
            public string? InstallerUrl { get; set; }
        }
    }
}
