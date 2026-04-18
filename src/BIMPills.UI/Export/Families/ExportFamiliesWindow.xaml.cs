using BIMPills.Core.Audit;
using BIMPills.UI.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace BIMPills.UI.Export.Families
{
    public partial class ExportFamiliesWindow : Window
    {
        private readonly IReadOnlyList<FamilyExportInfo> _families;
        private readonly Func<long, string, bool>? _exportCallback;
        private readonly string _documentTitle;
        private readonly int _revitVersion;
        private string? _selectedFolder;

        public ExportFamiliesWindow(
            IReadOnlyList<FamilyExportInfo> families,
            Func<long, string, bool>? exportCallback = null,
            string documentTitle = "",
            int revitVersion = 0)
        {
            _families = families;
            _exportCallback = exportCallback;
            _documentTitle = documentTitle;
            _revitVersion = revitVersion;
            InitializeComponent();
            BIMPills.UI.Shared.ThemeHelper.Apply(this);
            Populate();
        }

        private void Populate()
        {
            // Header
            DocumentName.Text = !string.IsNullOrEmpty(_documentTitle)
                ? _documentTitle
                : $"{_families.Count} familias disponibles";
            var categoryCount = _families.Select(f => f.Category).Distinct().Count();
            SubtitleText.Text = $"Modelo con {categoryCount} categorías";

            // Summary
            SummaryText.Text = $"{_families.Count} familias en {categoryCount} categorías";

            // Grid
            FamiliesGrid.ItemsSource = _families;
            Shared.ScrollHintHelper.Attach(FamiliesGrid, ExportScrollUpHint, ExportScrollDownHint);

            // Disable export if no callback
            if (_exportCallback == null)
                ExportButton.Visibility = Visibility.Collapsed;

            // Version suffix label
            string versionSuffix = _revitVersion > 0 ? $"-{_revitVersion % 100}" : "";
            VersionSuffixLabel.Text = $"Agregar sufijo de versión al nombre de la familia (ej: \"{versionSuffix}\")";
            VersionSuffixInfoText.Text = _revitVersion > 0
                ? $"Se agregará el sufijo \"{versionSuffix}\" basado en la versión de Revit activa. Si la familia ya posee un sufijo de versión, será reemplazado por el actual."
                : "No se detectó la versión de Revit.";

            UpdateSelection();
            UpdateFolderPreview();
        }

        private void UpdateSelection()
        {
            int selected = _families.Count(f => f.IsSelected);
            int total = _families.Count;
            SelectionText.Text = $"{selected} de {total} seleccionadas";

            SelectAllButton.Content = selected == total && total > 0
                ? "Deseleccionar todo"
                : "Seleccionar todo";

            ExportButton.IsEnabled = selected > 0
                && !string.IsNullOrEmpty(_selectedFolder)
                && _exportCallback != null;
        }

        private void UpdateFolderPreview()
        {
            string basePath = _selectedFolder ?? "C:\\...";
            string projectFolder = SanitizeFileName(
                !string.IsNullOrEmpty(_documentTitle) ? _documentTitle : "Proyecto");
            var distinctCategories = _families.Select(f => f.Category).Distinct().ToList();
            var categories = distinctCategories.OrderBy(c => c).Take(4).ToList();
            int totalCategories = distinctCategories.Count;

            var lines = new List<string>
            {
                $"\U0001F4C1 {Path.GetFileName(basePath)}/",
                $"    \U0001F4C1 {projectFolder}/"
            };

            foreach (var cat in categories)
            {
                int count = _families.Count(f => f.Category == cat);
                lines.Add($"        \U0001F4C1 {cat}/ \u2014 {count} familias");
            }

            if (totalCategories > 4)
                lines.Add($"        \u2026 y {totalCategories - 4} categor\u00EDas m\u00E1s");

            FolderPreviewText.Text = string.Join("\n", lines);
        }

        private void VersionSuffixCheck_Changed(object sender, RoutedEventArgs e)
        {
            VersionSuffixInfo.Visibility = VersionSuffixCheck.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool allSelected = _families.All(f => f.IsSelected);
            bool newValue = !allSelected;
            foreach (var family in _families)
                family.IsSelected = newValue;

            FamiliesGrid.Items.Refresh();
            UpdateSelection();
        }

        private void FamilyCheckBox_Click(object sender, RoutedEventArgs e)
        {
            UpdateSelection();
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Seleccionar carpeta de destino",
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _selectedFolder = dialog.SelectedPath;
                DestinationPath.Text = _selectedFolder;
                DestinationPath.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x21, 0x2B, 0x37));
                UpdateSelection();
                UpdateFolderPreview();
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (_exportCallback == null || string.IsNullOrEmpty(_selectedFolder)) return;

            var selectedFamilies = _families.Where(f => f.IsSelected).ToList();
            if (selectedFamilies.Count == 0) return;

            bool addVersionSuffix = VersionSuffixCheck.IsChecked == true && _revitVersion > 0;
            string versionSuffix = addVersionSuffix ? $"-{_revitVersion % 100}" : "";
            string projectFolder = SanitizeFileName(
                !string.IsNullOrEmpty(_documentTitle) ? _documentTitle : "Proyecto");
            int categoryCount = selectedFamilies.Select(f => f.Category).Distinct().Count();

            // Confirmation message
            string message = $"Se exportar\u00E1n {selectedFamilies.Count} familias organizadas por categor\u00EDa en:\n\n" +
                $"\U0001F4C1 {_selectedFolder}\\{projectFolder}\\\n\n" +
                $"Categor\u00EDas: {categoryCount} subcarpetas\n";

            if (addVersionSuffix)
                message += $"Sufijo de versi\u00F3n: S\u00ED \u2014 \"{versionSuffix}\" (Revit {_revitVersion})\n";

            message += "\nEste proceso puede tomar varios minutos. \u00BFDesea continuar?";

            var confirm = BimPillsDialog.Confirm(
                "BIMPills — Confirmar exportación",
                message,
                kind: BimPillsDialog.DialogKind.Question);

            if (!confirm) return;

            // Show progress
            ProgressOverlay.Visibility = Visibility.Visible;
            ProgressBar.Maximum = selectedFamilies.Count;
            ProgressBar.Value = 0;

            int exported = 0;
            int failed = 0;
            var errors = new List<string>();

            try
            {
                for (int i = 0; i < selectedFamilies.Count; i++)
                {
                    var family = selectedFamilies[i];

                    ProgressText.Text = $"Exportando {i + 1} de {selectedFamilies.Count}...";
                    ProgressDetail.Text = family.Name;
                    ProgressBar.Value = i;

                    // Force UI update
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Render,
                        new Action(() => { }));

                    // Build filename with optional version suffix
                    string familyName = family.Name;
                    if (addVersionSuffix)
                        familyName = ApplyVersionSuffix(familyName, _revitVersion % 100);

                    // Build path: {destFolder}/{projectName}/{Category}/{FamilyName}.rfa
                    var safeCategory = SanitizeFileName(family.Category);
                    var safeName = SanitizeFileName(familyName);
                    var destPath = Path.Combine(_selectedFolder, projectFolder, safeCategory, $"{safeName}.rfa");

                    bool success = _exportCallback(family.Id, destPath);
                    if (success)
                        exported++;
                    else
                    {
                        failed++;
                        errors.Add(family.Name);
                    }
                }

                ProgressBar.Value = selectedFamilies.Count;
            }
            finally
            {
                ProgressOverlay.Visibility = Visibility.Collapsed;
            }

            string summary = $"Exportadas {exported} de {selectedFamilies.Count} familias.";
            if (failed > 0)
            {
                summary += $"\n\n{failed} familias no pudieron exportarse:";
                foreach (var name in errors.Take(10))
                    summary += $"\n  - {name}";
                if (errors.Count > 10)
                    summary += $"\n  ... y {errors.Count - 10} m\u00E1s";
            }

            if (failed > 0)
                BimPillsDialog.Warning("BIMPills — Exportación completada", summary);
            else
                BimPillsDialog.Info("BIMPills — Exportación completada", summary);
        }

        /// <summary>
        /// Applies version suffix to a family name.
        /// If the name already ends with a version suffix like "-24", "-25", "-26", replaces it.
        /// Otherwise appends the suffix.
        /// </summary>
        internal static string ApplyVersionSuffix(string familyName, int versionShort)
        {
            string suffix = $"-{versionShort}";

            // Match existing version suffix: dash followed by 2 digits at end
            var match = Regex.Match(familyName, @"-(\d{2})$");
            if (match.Success)
                return familyName.Substring(0, match.Index) + suffix;

            return familyName + suffix;
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "_";

            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new char[name.Length];
            for (int i = 0; i < name.Length; i++)
            {
                sanitized[i] = Array.IndexOf(invalidChars, name[i]) >= 0 ? '_' : name[i];
            }
            var result = new string(sanitized).Trim();
            return string.IsNullOrEmpty(result) ? "_" : result;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
