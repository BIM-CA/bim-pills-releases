using System.Threading.Tasks;

namespace BIMPills.Core.Licensing
{
    public interface ILicenseService
    {
        Task<LicenseInfo?> ValidateAsync(string licenseKey, bool forceRefresh = false);
        Task<bool> ActivateAsync(string licenseKey, string machineId);
        Task<bool> DeactivateAsync();
        LicenseInfo? GetCachedLicense();
        bool IsValid { get; }
        bool IsExpired { get; }
        bool IsGracePeriod { get; }
        bool IsActivated { get; }
    }
}
