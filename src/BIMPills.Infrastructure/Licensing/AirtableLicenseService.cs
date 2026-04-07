using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using BIMPills.Core.Licensing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BIMPills.Infrastructure.Licensing
{
    /// <summary>
    /// Validates BIMPills licenses against the Airtable "Licencias" table.
    /// Uses local DPAPI-encrypted cache for offline support (24h validity).
    /// Grace period: 7 days after expiration before full lockout.
    /// </summary>
    public class AirtableLicenseService : ILicenseService
    {
        private const string BaseId = "app7VyLuZOE0akwDw";
        private const string TableId = "tblaCoF8RCnf8hky8";
        private const string SoftwarePrefix = "BIM Pills";   // matches "BIM Pills Pro - Mensual", "BIM Pills Pro - Anual", "BIM Pills Pro - Colaborador"
        private const int GracePeriodDays = 7;

        // Airtable read/write token — XOR-obfuscated so it doesn't appear as plaintext
        // in the compiled binary. Key = { 'B','I','M' } (0x42,0x49,0x4D).
        // To rotate: re-run the XOR transform on the new token and update _tokenData.
        private static readonly byte[] _xorKey = { 0x42, 0x49, 0x4D };
        private static readonly byte[] _tokenData =
        {
            0x32,0x28,0x39,0x2A,0x0D,0x19,0x2A,0x38,0x1C,0x0A,0x38,0x1C,0x16,0x79,0x1F,0x21,
            0x22,0x63,0x7A,0x70,0x7E,0x75,0x7F,0x7D,0x71,0x2F,0x2E,0x20,0x7E,0x2C,0x76,0x7E,
            0x7F,0x77,0x2A,0x7E,0x71,0x7D,0x2C,0x74,0x79,0x2B,0x75,0x7A,0x7C,0x77,0x78,0x79,
            0x76,0x2B,0x7F,0x74,0x2A,0x2E,0x76,0x7D,0x74,0x26,0x7F,0x28,0x24,0x79,0x7C,0x70,
            0x2D,0x29,0x7A,0x2A,0x2F,0x7B,0x7E,0x29,0x21,0x2C,0x7D,0x24,0x79,0x7F,0x7B,0x7D,
            0x7E,0x21
        };

        private static string ResolveToken()
        {
            var chars = new char[_tokenData.Length];
            for (int i = 0; i < _tokenData.Length; i++)
                chars[i] = (char)(_tokenData[i] ^ _xorKey[i % _xorKey.Length]);
            return new string(chars);
        }

        private readonly LicenseCache _cache;
        private readonly string _apiKey;
        private readonly string _machineId;

        private static readonly HttpClient _http = new HttpClient();

        public AirtableLicenseService(LicenseCache? cache = null)
        {
            _apiKey = ResolveToken();
            _cache = cache ?? new LicenseCache();
            _machineId = MachineIdProvider.GetMachineId();
        }

        // Constructor for tests — allows injecting a custom key
        internal AirtableLicenseService(string apiKey, LicenseCache? cache = null)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _cache = cache ?? new LicenseCache();
            _machineId = MachineIdProvider.GetMachineId();
        }

        public bool IsValid
        {
            get
            {
                var license = _cache.Load();
                if (license == null) return false;
                if (license.Status == "Activo") return true;
                if (license.Status == "Grace Period") return true;

                // Check grace period for recently expired licenses
                if (license.Status == "Expirado" || license.Status == "Cancelado")
                {
                    if (license.ExpiresAt.HasValue)
                    {
                        var daysSinceExpiry = (DateTime.UtcNow - license.ExpiresAt.Value).TotalDays;
                        return daysSinceExpiry <= GracePeriodDays;
                    }
                }

                return false;
            }
        }

        public bool IsActivated => _cache.Load() != null;

        public bool IsExpired
        {
            get
            {
                var license = _cache.Load();
                if (license == null) return false;  // nunca activada — no "expirada", solo sin activar
                if (license.Status == "Activo") return false;
                if (license.Status == "Grace Period") return false;

                if (license.ExpiresAt.HasValue)
                {
                    var daysSinceExpiry = (DateTime.UtcNow - license.ExpiresAt.Value).TotalDays;
                    return daysSinceExpiry > GracePeriodDays;
                }

                return license.Status == "Expirado" || license.Status == "Cancelado";
            }
        }

        public bool IsGracePeriod
        {
            get
            {
                var license = _cache.Load();
                if (license == null) return false;
                if (license.Status == "Grace Period") return true;

                if ((license.Status == "Expirado" || license.Status == "Cancelado") && license.ExpiresAt.HasValue)
                {
                    var daysSinceExpiry = (DateTime.UtcNow - license.ExpiresAt.Value).TotalDays;
                    return daysSinceExpiry > 0 && daysSinceExpiry <= GracePeriodDays;
                }

                return false;
            }
        }

        public LicenseInfo? GetCachedLicense() => _cache.Load();

        public async Task<LicenseInfo?> ValidateAsync(string licenseKey)
        {
            // Try cache first if fresh
            if (_cache.IsCacheFresh())
            {
                var cached = _cache.Load();
                if (cached != null && cached.LicenseKey == licenseKey)
                    return cached;
            }

            try
            {
                var formula = Uri.EscapeDataString($"{{License Key}}=\"{licenseKey}\"");
                var url = $"https://api.airtable.com/v0/{BaseId}/{TableId}?filterByFormula={formula}&maxRecords=1";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", $"Bearer {_apiKey}");

                var response = await _http.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;

                var body = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(body);
                var records = json["records"] as JArray;

                if (records == null || records.Count == 0) return null;

                var record = records[0];
                var recordId = record["id"]?.ToString();
                var fields = record["fields"];
                if (fields == null) return null;

                // Validate this is a BIM Pills license (not a record from another product in the same base)
                var software = fields["Software"]?.ToString() ?? "";
                if (!software.StartsWith(SoftwarePrefix, StringComparison.OrdinalIgnoreCase))
                    return null;

                var status = fields["Estado Suscripci\u00F3n"]?.ToString() ?? "";
                var remoteMachineId = fields["Machine ID"]?.ToString() ?? "";

                // Machine ID check: must match or be empty (first activation)
                if (!string.IsNullOrEmpty(remoteMachineId) && remoteMachineId != _machineId)
                    return null;

                var license = new LicenseInfo
                {
                    LicenseKey       = licenseKey,
                    Software         = software,
                    Plan             = fields["Plan"]?.ToString() ?? "",
                    Status           = status,
                    ExpiresAt        = fields["Fecha Vencimiento"]?.Type == JTokenType.Date
                        ? fields["Fecha Vencimiento"]?.ToObject<DateTime>()
                        : ParseDate(fields["Fecha Vencimiento"]?.ToString()),
                    MachineId        = _machineId,
                    HolderName       = ParseStringField(fields["Nombre Completo"]),
                    ValidatedAt      = DateTime.UtcNow,
                    AirtableRecordId = recordId ?? ""
                };

                _cache.Save(license);

                // Update "Ultima Validacion" in Airtable (fire-and-forget)
                _ = UpdateLastValidationAsync(recordId!);

                return license;
            }
            catch
            {
                // Network error — fall back to cache
                return _cache.Load();
            }
        }

        public async Task<bool> ActivateAsync(string licenseKey, string machineId)
        {
            try
            {
                // First validate the key exists
                var formula = Uri.EscapeDataString($"{{License Key}}=\"{licenseKey}\"");
                var url = $"https://api.airtable.com/v0/{BaseId}/{TableId}?filterByFormula={formula}&maxRecords=1";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", $"Bearer {_apiKey}");

                var response = await _http.SendAsync(request);
                if (!response.IsSuccessStatusCode) return false;

                var body = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(body);
                var records = json["records"] as JArray;

                if (records == null || records.Count == 0) return false;

                var record = records[0];
                var recordId = record["id"]?.ToString();
                var fields = record["fields"];
                if (fields == null || recordId == null) return false;

                // Validate this is a BIM Pills license
                var softwareValue = fields["Software"]?.ToString() ?? "";
                if (!softwareValue.StartsWith(SoftwarePrefix, StringComparison.OrdinalIgnoreCase))
                    return false;

                var remoteMachineId = fields["Machine ID"]?.ToString() ?? "";
                var status = fields["Estado Suscripci\u00F3n"]?.ToString() ?? "";

                // Already bound to another machine
                if (!string.IsNullOrEmpty(remoteMachineId) && remoteMachineId != machineId)
                    return false;

                // Status must be Activo or Grace Period to activate
                if (status != "Activo" && status != "Grace Period")
                    return false;

                // Write Machine ID + Ultima Validacion to Airtable
                var patchUrl = $"https://api.airtable.com/v0/{BaseId}/{TableId}/{recordId}";
                var patchBody = new JObject
                {
                    ["fields"] = new JObject
                    {
                        ["Machine ID"] = machineId,
                        ["\u00DAltima Validaci\u00F3n"] = DateTime.UtcNow.ToString("o")
                    }
                };

                var patchRequest = new HttpRequestMessage(new HttpMethod("PATCH"), patchUrl)
                {
                    Content = new StringContent(patchBody.ToString(), Encoding.UTF8, "application/json")
                };
                patchRequest.Headers.Add("Authorization", $"Bearer {_apiKey}");

                var patchResponse = await _http.SendAsync(patchRequest);
                if (!patchResponse.IsSuccessStatusCode) return false;

                // Cache locally
                var license = new LicenseInfo
                {
                    LicenseKey       = licenseKey,
                    Software         = softwareValue,
                    Plan             = fields["Plan"]?.ToString() ?? "",
                    Status           = status,
                    ExpiresAt        = ParseDate(fields["Fecha Vencimiento"]?.ToString()),
                    MachineId        = machineId,
                    HolderName       = ParseStringField(fields["Nombre Completo"]),
                    ValidatedAt      = DateTime.UtcNow,
                    AirtableRecordId = recordId ?? ""
                };
                _cache.Save(license);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Clears the Machine ID binding in Airtable and deletes the local cache,
        /// allowing the license key to be activated on a different machine.
        /// </summary>
        public async Task<bool> DeactivateAsync()
        {
            try
            {
                var cached = _cache.Load();

                // Clear local cache first regardless of network result
                _cache.Clear();

                if (cached == null || string.IsNullOrEmpty(cached.AirtableRecordId))
                    return true; // Nothing to deregister remotely

                var patchUrl = $"https://api.airtable.com/v0/{BaseId}/{TableId}/{cached.AirtableRecordId}";
                var patchBody = new JObject
                {
                    ["fields"] = new JObject
                    {
                        ["Machine ID"] = ""
                    }
                };

                var request = new HttpRequestMessage(new HttpMethod("PATCH"), patchUrl)
                {
                    Content = new StringContent(patchBody.ToString(), Encoding.UTF8, "application/json")
                };
                request.Headers.Add("Authorization", $"Bearer {_apiKey}");

                var response = await _http.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return true; // Cache is already cleared — consider it success
            }
        }

        private async Task UpdateLastValidationAsync(string recordId)
        {
            try
            {
                var url = $"https://api.airtable.com/v0/{BaseId}/{TableId}/{recordId}";
                var body = new JObject
                {
                    ["fields"] = new JObject
                    {
                        ["\u00DAltima Validaci\u00F3n"] = DateTime.UtcNow.ToString("o")
                    }
                };

                var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
                {
                    Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json")
                };
                request.Headers.Add("Authorization", $"Bearer {_apiKey}");

                await _http.SendAsync(request);
            }
            catch
            {
                // Best-effort update, don't fail the validation
            }
        }

        /// <summary>
        /// Reads a field that may be a plain string or a Airtable lookup array (["value"]).
        /// </summary>
        private static string ParseStringField(JToken? token)
        {
            if (token == null) return "";
            if (token.Type == JTokenType.Array)
            {
                var arr = token as JArray;
                return arr?.Count > 0 ? arr[0]?.ToString() ?? "" : "";
            }
            return token.ToString();
        }

        private static DateTime? ParseDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return DateTime.TryParse(value, out var dt) ? dt : (DateTime?)null;
        }
    }
}
