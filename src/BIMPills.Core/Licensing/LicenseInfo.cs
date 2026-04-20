using System;

namespace BIMPills.Core.Licensing
{
    public class LicenseInfo
    {
        public string LicenseKey { get; set; } = "";
        public string Software { get; set; } = "";
        public string Plan { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime? ExpiresAt { get; set; }
        public string MachineId { get; set; } = "";
        public string HolderName { get; set; } = "";
        public string Email { get; set; } = "";
        public DateTime ValidatedAt { get; set; }
        public string AirtableRecordId { get; set; } = "";
    }
}
