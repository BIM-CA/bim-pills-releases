using System;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;

namespace BIMPills.Infrastructure.Licensing
{
    /// <summary>
    /// Generates a deterministic, unique identifier for the current machine
    /// based on hardware attributes (machine name + processor ID + MAC address).
    /// </summary>
    public static class MachineIdProvider
    {
        private static string? _cached;

        public static string GetMachineId()
        {
            if (_cached != null) return _cached;

            var raw = new StringBuilder();
            raw.Append(Environment.MachineName);
            raw.Append('|');
            raw.Append(GetProcessorId());
            raw.Append('|');
            raw.Append(GetMacAddress());

            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw.ToString()));
                _cached = BitConverter.ToString(hash).Replace("-", "").Substring(0, 32);
            }

            return _cached;
        }

        private static string GetProcessorId()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
                {
                    foreach (var obj in searcher.Get())
                        return obj["ProcessorId"]?.ToString() ?? "";
                }
            }
            catch { }
            return "UNKNOWN";
        }

        private static string GetMacAddress()
        {
            try
            {
                var nic = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up
                             && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .OrderBy(n => n.Name)
                    .FirstOrDefault();

                return nic?.GetPhysicalAddress().ToString() ?? "NOMAC";
            }
            catch { }
            return "NOMAC";
        }
    }
}
