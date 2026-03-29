using BIMPills.Core.Audit;
using BIMPills.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace BIMPills.UI.ExportFamilies
{
    public partial class ExportFamiliesPanel : UserControl
    {
        private IReadOnlyList<FamilyExportInfo> _families = Array.Empty<FamilyExportInfo>();
        private Func<long, string, bool>? _exportCallback;
        private string _documentTitle = "";
        private int _revitVersion;
        private string? _selectedFolder;
        private ILogger? _logger;

        /// <summary>Raised when export availability changes. Arg = canExport.</summary>
        public event EventHandler<bool>? ExportEnabledChanged;

        public ExportFamiliesPanel()
        {
            InitializeComponent();
        }

        /// <summary>Trigger export from external button.</summary>
        public void TriggerExport() => Export_Click(this, new RoutedEventArgs());

        /// <summary>Get export button label.</summary>
        public string ExportLabel
        {
            get
            {
                int selected = _families.Count(f => f.IsSelected);
                return selected > 0 ? $"Exportar {selected} familias" : "Exportar familias";
            }
        }

        /// <summary>
        /// Initializes the panel with family data and export callback.
        /// </summary>
        public void Initialize(
            IReadOnlyList<FamilyExportInfo> families,
            Func<long, string, bool>? exportCallback = null,
            string documentTitle = "",
            int revitVersion = 0,
            ILogger? logger = null)
        {
            _families = families;
            _exportCallback = exportCallback;
            _documentTitle = documentTitle;
            _revitVersion = revitVersion;
            _logger = logger;
            Populate();
        }

        private void Populate()
        {
            var categoryCount = _families.Select(f => f.Category).Distinct().Count();

            // Summary
            SummaryText.Text = $"{_families.Count} familias en {categoryCount} categorías";

            // Grid
            FamiliesGrid.ItemsSource = _families;
            Shared.ScrollHintHelper.Attach(FamiliesGrid, ExportScrollUpHint, ExportScrollDownHint);

            // Notify parent of export availability
            ExportEnabledChanged?.Invoke(this, _exportCallback != null);

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

            bool canExport = selected > 0
                && !string.IsNullOrEmpty(_selectedFolder)
                && _exportCallback != null;
            ExportEnabledChanged?.Invoke(this, canExport);
        }

        private void UpdateFolderPreview()
        {
            string basePath = _selectedFolder ?? @"C:\...";
            string projectFolder = SanitizeFileName(
                !string.IsNullOrEmpty(_documentTitle) ? _documentTitle : "Proyecto");
            var categories = _families.Select(f => f.Category).Distinct().OrderBy(c => c).Take(4).ToList();
            int totalCategories = _families.Select(f => f.Category).Distinct().Count();

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
                lines.Add($"        \u2026 y {totalCategories - 4} categorías más");

            FolderPreviewText.Text = string.Join("\n", lines);
        }

        private void VersionSuffixCheck_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                VersionSuffixInfo.Visibility = VersionSuffixCheck.IsChecked == true
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                _logger?.Error("Error en VersionSuffixCheck_Changed", ex);
            }
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool allSelected = _families.All(f => f.IsSelected);
                bool newValue = !allSelected;
                foreach (var family in _families)
                    family.IsSelected = newValue;

                FamiliesGrid.Items.Refresh();
                UpdateSelection();
            }
            catch (Exception ex)
            {
                _logger?.Error("Error en SelectAll_Click", ex);
                MessageBox.Show($"Error inesperado:\n{ex.Message}\n\nRevisa el log para más detalles.",
                    "BIMPills — Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FamilyCheckBox_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateSelection();
            }
            catch (Exception ex)
            {
                _logger?.Error("Error en FamilyCheckBox_Click", ex);
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            try
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
            catch (Exception ex)
            {
                _logger?.Error("Error en Browse_Click", ex);
                MessageBox.Show($"Error inesperado:\n{ex.Message}\n\nRevisa el log para más detalles.",
                    "BIMPills — Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            try
            {
            if (_exportCallback == null || string.IsNullOrEmpty(_selectedFolder)) return;

            var selectedFamilies = _families.Where(f => f.IsSelected).ToList();
            if (selectedFamilies.Count == 0) return;

            bool addVersionSuffix = VersionSuffixCheck.IsChecked == true && _revitVersion > 0;
            string versionSuffix = addVersionSuffix ? $"-{_revitVersion % 100}" : "";
            string projectFolder = SanitizeFileName(
                !string.IsNullOrEmpty(_documentTitle) ? _documentTitle : "Proyecto");
            int categoryCount = selectedFamilies.Select(f => f.Category).Distinct().Count();

            string message = $"Se exportarán {selectedFamilies.Count} familias organizadas por categoría en:\n\n" +
                $"\U0001F4C1 {_selectedFolder}\\{projectFolder}\\\n\n" +
                $"Categorías: {categoryCount} subcarpetas\n";

            if (addVersionSuffix)
                message += $"Sufijo de versión: Sí \u2014 \"{versionSuffix}\" (Revit {_revitVersion})\n";

            message += "\nEste proceso puede tomar varios minutos. ¿Desea continuar?";

            var confirm = MessageBox.Show(
                message,
                "BIMPills \u2014 Confirmar exportación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.Yes);

            if (confirm != MessageBoxResult.Yes) return;

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

                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Render,
                        new Action(() => { }));

                    string familyName = family.Name;
                    if (addVersionSuffix)
                        familyName = ApplyVersionSuffix(familyName, _revitVersion % 100);

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
                    summary += $"\n  ... y {errors.Count - 10} más";
            }

            MessageBox.Show(
                summary,
                "BIMPills \u2014 Exportación completada",
                MessageBoxButton.OK,
                failed > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            if (exported > 0 && failed == 0)
            {
                var win = Window.GetWindow(this);
                if (win != null) try { win.DialogResult = true; } catch (InvalidOperationException) { }
                win?.Close();
            }
            }
            catch (Exception ex)
            {
                _logger?.Error("Error no controlado en Export_Click", ex);
                ProgressOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show(
                    $"Error inesperado durante la exportación:\n{ex.Message}\n\nRevisa el log para más detalles.",
                    "BIMPills — Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        internal static string ApplyVersionSuffix(string familyName, int versionShort)
        {
            string suffix = $"-{versionShort}";
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
                sanitized[i] = Array.IndexOf(invalidChars, name[i]) >= 0 ? '_' : name[i];
            var result = new string(sanitized).Trim();
            return string.IsNullOrEmpty(result) ? "_" : result;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Window.GetWindow(this)?.Close();
            }
            catch (Exception ex)
            {
                _logger?.Error("Error en Close_Click", ex);
            }
        }
    }
}
