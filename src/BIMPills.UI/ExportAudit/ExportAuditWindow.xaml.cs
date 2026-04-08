using BIMPills.Commands.ModelAudit;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace BIMPills.UI.ExportAudit
{
    public partial class ExportAuditWindow : Window
    {
        private ModelAuditResult? _auditResult;
        private string _outputPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        public ExportAuditWindow(ModelAuditResult? auditResult = null)
        {
            _auditResult = auditResult;
            InitializeComponent();
            BIMPills.UI.Shared.ThemeHelper.Apply(this);
            OutputPathBox.Text = _outputPath;

            // Default values for header fields
            TxtReportTitle.Text = "Informe de Auditor\u00EDa BIM";
            TxtProductionDate.Text = DateTime.Now.ToString("yyyy-MM-dd");
            TxtGeneratedBy.Text = Environment.UserName;

            if (_auditResult != null) Populate();
        }

        /// <summary>Load audit data after construction (for modal usage from ModelAuditWindow).</summary>
        public void LoadAuditData(ModelAuditResult result)
        {
            _auditResult = result;
            Populate();
        }

        private void Populate()
        {
            if (_auditResult == null) return;

            HealthScoreDisplay.Text = $"{_auditResult.HealthScore.TotalScore} / 100  \u2014  {_auditResult.HealthScore.LevelLabel}";
            WarningsDisplay.Text = _auditResult.Warnings.Count.ToString("N0");
            OrphansDisplay.Text = _auditResult.OrphanElements.Count.ToString("N0");
            FileSizeDisplay.Text = _auditResult.FileSizeLabel;

            // Update title with project name
            if (!string.IsNullOrWhiteSpace(_auditResult.DocumentTitle))
                TxtReportTitle.Text = $"Informe de Auditor\u00EDa \u2014 {_auditResult.DocumentTitle}";
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (_auditResult == null)
            {
                MessageBox.Show("No hay datos de auditor\u00EDa. Ejecuta Auditar primero.", "BIM Pills");
                return;
            }

            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = $"AuditReport_{timestamp}.html";
                var outputPath = Path.Combine(_outputPath, fileName);
                Directory.CreateDirectory(_outputPath);

                var options = new HtmlReportExporter.HtmlExportOptions
                {
                    IncludeHealthScore = ChkHealthScore.IsChecked == true,
                    IncludeWarnings = ChkWarnings.IsChecked == true,
                    IncludeElementsWithoutCategory = ChkOrphans.IsChecked == true,
                    IncludeLargestFamilies = ChkFamilies.IsChecked == true,
                    IncludeUnplacedViews = ChkViews.IsChecked == true,
                    IncludePurgeableItems = ChkPurgeable.IsChecked == true,
                    IncludeRecommendations = ChkRecommendations.IsChecked == true,
                    IncludeMethodology = ChkMethodology.IsChecked == true,

                    // Header fields
                    ReportTitle = TxtReportTitle.Text.Trim(),
                    ProductionDate = TxtProductionDate.Text.Trim(),
                    GeneratedBy = TxtGeneratedBy.Text.Trim(),

                    // Signature fields
                    SignatureName = TxtSignatureName.Text.Trim(),
                    SignatureRole = TxtSignatureRole.Text.Trim()
                };

                var exporter = new HtmlReportExporter();
                exporter.Export(_auditResult, options, outputPath);

                var result = MessageBox.Show(
                    $"Informe exportado en:\n\n{outputPath}\n\n\u00BFDeseas abrirlo?",
                    "BIMPills \u2014 Exportaci\u00F3n exitosa",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    Helpers.ProcessHelper.OpenDocument(outputPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error durante la exportaci\u00F3n: {ex.Message}", "BIMPills \u2014 Error");
            }
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Selecciona la carpeta de guardado",
                SelectedPath = _outputPath
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _outputPath = dialog.SelectedPath;
                OutputPathBox.Text = _outputPath;
            }
        }
    }
}
