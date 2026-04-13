using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BIMPills.Infrastructure.Services
{
    /// <summary>
    /// Detects PDF printer drivers installed on the system (PDF24, Microsoft Print
    /// to PDF, Adobe PDF, Bullzip, doPDF, CutePDF). Returns a ranked list so the
    /// "best" printer is first — PDF24 is preferred because it supports silent
    /// printing and we ship its installer alongside BIM Pills.
    ///
    /// Uses a two-stage enumeration with fallback:
    ///   1) <see cref="PrinterSettings.InstalledPrinters"/> (System.Drawing) —
    ///      fast, but has been observed to return empty or throw under Revit's
    ///      .NET 8 runtime on some machines.
    ///   2) WMI <c>SELECT Name FROM Win32_Printer</c> via System.Management —
    ///      slower but far more reliable. Isolated in a separate method to avoid
    ///      JIT-loading System.Management unless actually needed (prevents
    ///      TypeLoadException when the assembly isn't resolvable).
    ///
    /// Diagnostic traces are written to
    /// <c>%APPDATA%\Autodesk\Revit\Addins\BIMPills\pdf-printer-diag.log</c>
    /// so we can see exactly what each strategy returned when users report
    /// "no printer detected".
    /// </summary>
    public static class PdfPrinterService
    {
        /// <summary>
        /// Known PDF printer name patterns, ordered by preference (first = best).
        /// </summary>
        private static readonly (string Pattern, string DisplayName, bool SupportsSilent)[] KnownPrinters =
        {
            ("pdf24 (bimpills)",               "PDF24 (BIMPills)",        true ),  // BIMPills-installed, auto-save via named pipe
            ("pdf24",                          "PDF24",                   true ),
            ("microsoft print to pdf",         "Microsoft Print to PDF",  false),
            ("adobe pdf",                      "Adobe PDF",               false),
            ("bullzip pdf printer",            "Bullzip PDF Printer",     true ),
            ("dopdf",                          "doPDF",                   false),
            ("cutepdf writer",                 "CutePDF Writer",          false),
            ("foxit reader pdf printer",       "Foxit Reader PDF Printer",false),
            ("nitro pdf creator",              "Nitro PDF Creator",       false),
        };

        /// <summary>
        /// Information about a detected PDF printer.
        /// </summary>
        public sealed class PdfPrinterInfo
        {
            public string SystemName { get; }
            public string DisplayName { get; }
            public bool SupportsSilent { get; }
            /// <summary>Lower is better. -1 = unknown/unranked.</summary>
            public int Rank { get; }

            public PdfPrinterInfo(string systemName, string displayName, bool supportsSilent, int rank)
            {
                SystemName     = systemName;
                DisplayName    = displayName;
                SupportsSilent = supportsSilent;
                Rank           = rank;
            }
        }

        /// <summary>
        /// Enumerates Windows printers and returns the ones that look like PDF
        /// printer drivers, ordered by preference.
        /// </summary>
        public static List<PdfPrinterInfo> GetInstalledPdfPrinters()
        {
            var diag = new DiagLog();
            diag.WriteLine($"--- PdfPrinterService.GetInstalledPdfPrinters @ {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---");

            var allPrinters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // ---- Strategy 1: System.Drawing.Printing ----
            // IMPORTANT: Isolated in a [NoInlining] method so the JIT does NOT
            // try to resolve System.Drawing.Common when compiling THIS method.
            // Revit's .NET 8 runtime often does not include System.Drawing.Common,
            // which causes a FileNotFoundException at JIT time — unrecoverable
            // by try/catch unless the reference is in a separate method.
            try
            {
                EnumerateViaSystemDrawing(allPrinters, diag);
            }
            catch (Exception ex)
            {
                diag.WriteLine($"  [System.Drawing] FAILED: {ex.GetType().Name}: {ex.Message}");
            }

            // ---- Strategy 2: WMI Win32_Printer ----
            // IMPORTANT: Same JIT-isolation pattern as above. System.Management
            // may not be resolvable on certain Revit configurations.
            try
            {
                EnumerateViaWmi(allPrinters, diag);
            }
            catch (Exception ex)
            {
                diag.WriteLine($"  [WMI]           FAILED: {ex.GetType().Name}: {ex.Message}");
            }

            // ---- Strategy 3: Win32 EnumPrinters (P/Invoke) ----
            // Pure Win32 fallback — no external assemblies required. Works even
            // when both System.Drawing.Common and System.Management are missing.
            try
            {
                EnumerateViaWin32(allPrinters, diag);
            }
            catch (Exception ex)
            {
                diag.WriteLine($"  [Win32]         FAILED: {ex.GetType().Name}: {ex.Message}");
            }

            diag.WriteLine($"  [merged]        total unique: {allPrinters.Count}");

            // ---- Rank and filter ----
            var result = new List<PdfPrinterInfo>();
            foreach (var installed in allPrinters)
            {
                var lower = installed.ToLowerInvariant();
                int rank = -1;
                string display = installed;
                bool silent = false;

                for (int i = 0; i < KnownPrinters.Length; i++)
                {
                    if (lower.IndexOf(KnownPrinters[i].Pattern, StringComparison.Ordinal) >= 0)
                    {
                        rank    = i;
                        display = KnownPrinters[i].DisplayName;
                        silent  = KnownPrinters[i].SupportsSilent;
                        break;
                    }
                }

                // Extra heuristic: accept anything that contains "pdf" in the name.
                if (rank < 0 && lower.Contains("pdf"))
                {
                    rank = 999;
                    display = installed;
                    silent = false;
                }

                if (rank >= 0)
                {
                    result.Add(new PdfPrinterInfo(installed, display, silent, rank));
                    diag.WriteLine($"  [match] rank={rank} display='{display}' silent={silent} system='{installed}'");
                }
            }

            result.Sort((a, b) => a.Rank.CompareTo(b.Rank));
            diag.WriteLine($"  [result]        {result.Count} PDF printer(s) returned");
            diag.Flush();
            return result;
        }

        /// <summary>
        /// System.Drawing.Printing-based printer enumeration. Isolated in its
        /// own method so the JIT only loads System.Drawing.Common when this
        /// method is actually invoked — preventing FileNotFoundException from
        /// killing <see cref="GetInstalledPdfPrinters"/> when the assembly isn't
        /// deployed (e.g. Revit's .NET 8 runtime).
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void EnumerateViaSystemDrawing(HashSet<string> allPrinters, DiagLog diag)
        {
            int count = 0;
            foreach (string installed in PrinterSettings.InstalledPrinters)
            {
                if (string.IsNullOrWhiteSpace(installed)) continue;
                allPrinters.Add(installed);
                count++;
                diag.WriteLine($"  [System.Drawing] {installed}");
            }
            diag.WriteLine($"  [System.Drawing] total: {count}");
        }

        /// <summary>
        /// WMI-based printer enumeration. Isolated in its own method so the JIT
        /// only loads System.Management when this method is actually invoked —
        /// preventing TypeLoadException from killing the entire
        /// <see cref="GetInstalledPdfPrinters"/> call when System.Management
        /// isn't resolvable.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void EnumerateViaWmi(HashSet<string> allPrinters, DiagLog diag)
        {
            int count = 0;
            using (var searcher = new System.Management.ManagementObjectSearcher("SELECT Name FROM Win32_Printer"))
            using (var results = searcher.Get())
            {
                foreach (System.Management.ManagementObject mo in results)
                {
                    try
                    {
                        var name = mo["Name"] as string;
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        allPrinters.Add(name!);
                        count++;
                        diag.WriteLine($"  [WMI]           {name}");
                    }
                    finally
                    {
                        mo.Dispose();
                    }
                }
            }
            diag.WriteLine($"  [WMI]           total: {count}");
        }

        // ────────────────────────────────────────────────────────────────
        // Win32 P/Invoke — zero external assembly dependencies.
        // Uses EnumPrintersW(PRINTER_ENUM_LOCAL | PRINTER_ENUM_CONNECTIONS)
        // with PRINTER_INFO_2 to list all printers visible to the user.
        // ────────────────────────────────────────────────────────────────

        private const int PRINTER_ENUM_LOCAL       = 0x00000002;
        private const int PRINTER_ENUM_CONNECTIONS = 0x00000004;

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool EnumPrintersW(
            int flags, string? name, int level,
            IntPtr pPrinterEnum, int cbBuf,
            out int pcbNeeded, out int pcReturned);

        /// <summary>
        /// Pure Win32 printer enumeration via <c>winspool.drv!EnumPrintersW</c>.
        /// No dependency on System.Drawing.Common or System.Management — always
        /// available on Windows.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void EnumerateViaWin32(HashSet<string> allPrinters, DiagLog diag)
        {
            int flags = PRINTER_ENUM_LOCAL | PRINTER_ENUM_CONNECTIONS;
            // First call: query required buffer size
            EnumPrintersW(flags, null, 2, IntPtr.Zero, 0, out int needed, out _);
            if (needed <= 0)
            {
                diag.WriteLine("  [Win32]         EnumPrinters returned needed=0");
                return;
            }

            IntPtr buffer = Marshal.AllocHGlobal(needed);
            try
            {
                if (!EnumPrintersW(flags, null, 2, buffer, needed, out _, out int returned))
                {
                    int err = Marshal.GetLastWin32Error();
                    diag.WriteLine($"  [Win32]         EnumPrinters failed, error={err}");
                    return;
                }

                int count = 0;
                int structSize = Marshal.SizeOf(typeof(PRINTER_INFO_2));
                for (int i = 0; i < returned; i++)
                {
                    var ptr = new IntPtr(buffer.ToInt64() + i * structSize);
                    var info = (PRINTER_INFO_2)Marshal.PtrToStructure(ptr, typeof(PRINTER_INFO_2))!;
                    var name = info.pPrinterName;
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    allPrinters.Add(name!);
                    count++;
                    diag.WriteLine($"  [Win32]         {name}");
                }
                diag.WriteLine($"  [Win32]         total: {count}");
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PRINTER_INFO_2
        {
            public string? pServerName;
            public string? pPrinterName;
            public string? pShareName;
            public string? pPortName;
            public string? pDriverName;
            public string? pComment;
            public string? pLocation;
            public IntPtr  pDevMode;
            public string? pSepFile;
            public string? pPrintProcessor;
            public string? pDatatype;
            public string? pParameters;
            public IntPtr  pSecurityDescriptor;
            public int     Attributes;
            public int     Priority;
            public int     DefaultPriority;
            public int     StartTime;
            public int     UntilTime;
            public int     Status;
            public int     cJobs;
            public int     AveragePPM;
        }

        /// <summary>
        /// Ensures <c>HKCU\SOFTWARE\PDF24\Services\bimpills</c> contains the
        /// correct auto-save configuration for the currently logged-in user.
        ///
        /// <para>
        /// The NSIS installer writes this key during installation, but when the
        /// installer is run with a different admin account (UAC elevation with a
        /// separate admin user, common in enterprise environments), the write goes
        /// to the <em>admin account's</em> HKCU — not the user who will actually
        /// run Revit.  PDF24 Agent (<c>pdf24-agent.exe</c>), which runs as the
        /// interactive user, reads <em>its own</em> HKCU to decide how to handle
        /// completed print jobs.  If the key is absent, it falls back to default
        /// behaviour (opens a save dialog) and the silent auto-save never fires.
        /// </para>
        ///
        /// <para>
        /// This method is called at ExportSheets load time, after PDF24 is
        /// detected in the printer list.  Running inside Revit guarantees the
        /// correct user context — no elevation needed, HKCU is always right.
        /// Returns <c>true</c> if values were written (first-time fix), or
        /// <c>false</c> if the key was already correctly configured (no-op).
        /// All exceptions are silently swallowed so a registry failure can never
        /// prevent the panel from opening.
        /// </para>
        /// </summary>
        public static bool EnsureBimpillsHkcuServiceConfig()
        {
            const string keyPath = @"SOFTWARE\PDF24\Services\bimpills";
            try
            {
                // Check whether HKCU is already configured correctly — fast path.
                using (var existing = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath, writable: false))
                {
                    if (existing != null &&
                        string.Equals(existing.GetValue("Handler") as string, "autoSave", StringComparison.OrdinalIgnoreCase))
                    {
                        // Already configured; nothing to do.
                        return false;
                    }
                }

                // Write (or overwrite) all required values.
                using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                    keyPath, Microsoft.Win32.RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    if (key == null) return false;

                    key.SetValue("Handler",                "autoSave",                     Microsoft.Win32.RegistryValueKind.String);
                    key.SetValue("Port",                   @"\\.\pipe\PDFPrint - bimpills",Microsoft.Win32.RegistryValueKind.String);
                    key.SetValue("AutoSaveDir",            @"%localappdata%\BIMPills\PDFTemp", Microsoft.Win32.RegistryValueKind.String);
                    key.SetValue("AutoSaveFilename",       "$fileName",                    Microsoft.Win32.RegistryValueKind.String);
                    key.SetValue("AutoSaveShowProgress",   0,                              Microsoft.Win32.RegistryValueKind.DWord);
                    key.SetValue("AutoSaveUseFileChooser", 0,                              Microsoft.Win32.RegistryValueKind.DWord);
                    key.SetValue("AutoSaveOverwriteFile",  1,                              Microsoft.Win32.RegistryValueKind.DWord);
                    key.SetValue("LoadInCreatorIfOpen",    0,                              Microsoft.Win32.RegistryValueKind.DWord);
                    key.SetValue("AutoSaveFileCmd",        "",                             Microsoft.Win32.RegistryValueKind.String);
                    key.SetValue("AutoSaveOpenDir",        0,                              Microsoft.Win32.RegistryValueKind.DWord);
                    key.SetValue("AutoSaveUseFileCmd",     0,                              Microsoft.Win32.RegistryValueKind.DWord);
                    key.SetValue("FilenameErasements",     "",                             Microsoft.Win32.RegistryValueKind.String);
                    key.SetValue("ShellCmd",               "",                             Microsoft.Win32.RegistryValueKind.String);
                }

                // Log so we know the fix fired on this machine.
                try
                {
                    var diag = new DiagLog();
                    diag.WriteLine($"--- EnsureBimpillsHkcuServiceConfig @ {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---");
                    diag.WriteLine("  HKCU\\SOFTWARE\\PDF24\\Services\\bimpills was missing or incorrect — written.");
                    diag.Flush();
                }
                catch { /* diagnostics must never break callers */ }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Lightweight diagnostic log writer that appends to
        /// <c>%APPDATA%\Autodesk\Revit\Addins\BIMPills\pdf-printer-diag.log</c>.
        /// Swallows all errors so diagnostics can never break printer enumeration.
        /// </summary>
        private sealed class DiagLog
        {
            private readonly List<string> _lines = new List<string>();

            public void WriteLine(string line) => _lines.Add(line);

            public void Flush()
            {
                try
                {
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    var dir     = Path.Combine(appData, "Autodesk", "Revit", "Addins", "BIMPills");
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    var path = Path.Combine(dir, "pdf-printer-diag.log");
                    File.AppendAllLines(path, _lines, System.Text.Encoding.UTF8);
                }
                catch
                {
                    // Diagnostics must never break the calling code.
                }
            }
        }
    }
}
