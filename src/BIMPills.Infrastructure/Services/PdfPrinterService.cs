using System;
using System.Collections.Generic;
using System.Drawing.Printing;

namespace BIMPills.Infrastructure.Services
{
    /// <summary>
    /// Detects PDF printer drivers installed on the system (PDF24, Microsoft Print
    /// to PDF, Adobe PDF, Bullzip, doPDF, CutePDF). Returns a ranked list so the
    /// "best" printer is first — PDF24 is preferred because it supports silent
    /// printing and we ship its installer alongside BIM Pills.
    /// </summary>
    public static class PdfPrinterService
    {
        /// <summary>
        /// Known PDF printer name patterns, ordered by preference (first = best).
        /// </summary>
        private static readonly (string Pattern, string DisplayName, bool SupportsSilent)[] KnownPrinters =
        {
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
            /// <summary>
            /// True if the printer is known to support fully-silent printing
            /// (no dialog box when called from Revit's PrintManager with
            /// PrintToFile = true). PDF24 and Bullzip qualify.
            /// </summary>
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
            var result = new List<PdfPrinterInfo>();
            try
            {
                foreach (string installed in PrinterSettings.InstalledPrinters)
                {
                    if (string.IsNullOrWhiteSpace(installed)) continue;
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
                        result.Add(new PdfPrinterInfo(installed, display, silent, rank));
                }
            }
            catch
            {
                // InstalledPrinters can throw in locked-down environments. Silently
                // return whatever we collected so far (possibly empty).
            }

            result.Sort((a, b) => a.Rank.CompareTo(b.Rank));
            return result;
        }
    }
}
