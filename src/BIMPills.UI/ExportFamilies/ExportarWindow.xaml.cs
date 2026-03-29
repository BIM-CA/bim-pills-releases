using BIMPills.Core.Audit;
using BIMPills.Core.Models;
using BIMPills.Core.Services;
using System;
using System.Collections.Generic;
using System.Windows;

namespace BIMPills.UI.ExportFamilies
{
    public partial class ExportarWindow : Window
    {
        private string _familiesSubtitle = "";
        private string _sheetsSubtitle = "";
        private int _activeTab = 0;

        private bool _familiesCanExport;
        private bool _sheetsCanExport;

        public ExportarWindow()
        {
            InitializeComponent();

            // Wire panel events to footer action button
            ExportPanel.ExportEnabledChanged += (_, canExport) =>
            {
                _familiesCanExport = canExport;
                if (_activeTab == 0) UpdateActionButton();
            };
            SheetsPanel.ExportEnabledChanged += (_, canExport) =>
            {
                _sheetsCanExport = canExport;
                if (_activeTab == 1) UpdateActionButton();
            };
        }

        private void UpdateActionButton()
        {
            if (_activeTab == 0)
            {
                ActionButton.IsEnabled = _familiesCanExport;
                ActionButton.Content = ExportPanel.ExportLabel;
            }
            else
            {
                ActionButton.IsEnabled = _sheetsCanExport;
                ActionButton.Content = SheetsPanel.ExportLabel;
            }
        }

        private void Action_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTab == 0)
                ExportPanel.TriggerExport();
            else
                SheetsPanel.TriggerExport();
        }

        /// <summary>
        /// Initializes the Export Families tab with data and execution callback.
        /// </summary>
        public void InitializeExportFamilies(
            IReadOnlyList<FamilyExportInfo> families,
            Func<long, string, bool>? exportCallback = null,
            string documentTitle = "",
            int revitVersion = 0,
            ILogger? logger = null)
        {
            ExportPanel.Initialize(families, exportCallback, documentTitle, revitVersion, logger);

            var categoryCount = 0;
            var seen = new HashSet<string>();
            foreach (var f in families)
                if (seen.Add(f.Category)) categoryCount++;
            _familiesSubtitle = $"{families.Count} familias en {categoryCount} categorías";

            if (_activeTab == 0)
                SubtitleText.Text = _familiesSubtitle;
        }

        /// <summary>
        /// Initializes the Export Sheets tab with data and export callbacks.
        /// </summary>
        public void InitializeExportSheets(
            IReadOnlyList<SheetExportInfo> sheets,
            Func<long, string, string, PdfExportSettings, bool>? pdfExportCallback = null,
            Func<long, string, string, DwgExportConfig?, bool>? dwgExportCallback = null,
            string projectName = "",
            ILogger? logger = null,
            IReadOnlyList<string>? availableParameters = null,
            IReadOnlyList<string>? dwgPresetNames = null)
        {
            SheetsPanel.Initialize(sheets, pdfExportCallback, dwgExportCallback, projectName, logger, availableParameters, dwgPresetNames);
            _sheetsSubtitle = $"{sheets.Count} planos disponibles para exportar";
        }

        private void TabFamilias_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_activeTab == 0) return;
            _activeTab = 0;
            UpdateTabVisualState();
        }

        private void TabPlanos_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_activeTab == 1) return;
            _activeTab = 1;
            UpdateTabVisualState();
        }

        private void UpdateTabVisualState()
        {
            var activeTabStyle    = (Style)FindResource("TabHeaderActive");
            var inactiveTabStyle  = (Style)FindResource("TabHeader");
            var activeTextStyle   = (Style)FindResource("TabHeaderTextActive");
            var inactiveTextStyle = (Style)FindResource("TabHeaderText");

            if (_activeTab == 0)
            {
                TabFamiliasBorder.Style = activeTabStyle;
                TabFamiliasText.Style   = activeTextStyle;
                TabPlanosBorder.Style   = inactiveTabStyle;
                TabPlanosText.Style     = inactiveTextStyle;
                ExportPanel.Visibility  = Visibility.Visible;
                SheetsPanel.Visibility  = Visibility.Collapsed;
                ToolHeaderTitle.Text    = "Exportar Familias";
                ToolHeaderSubtitle.Text = "Exporta familias del modelo a carpeta local";
                SubtitleText.Text       = _familiesSubtitle;
            }
            else
            {
                TabFamiliasBorder.Style = inactiveTabStyle;
                TabFamiliasText.Style   = inactiveTextStyle;
                TabPlanosBorder.Style   = activeTabStyle;
                TabPlanosText.Style     = activeTextStyle;
                ExportPanel.Visibility  = Visibility.Collapsed;
                SheetsPanel.Visibility  = Visibility.Visible;
                ToolHeaderTitle.Text    = "Exportar Planos";
                ToolHeaderSubtitle.Text = "Exporta planos a PDF y DWG en lote";
                SubtitleText.Text       = _sheetsSubtitle;
            }

            UpdateActionButton();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        /// <summary>
        /// Sets the document name in the header (from Revit).
        /// </summary>
        public void SetDocumentName(string name)
        {
            DocumentName.Text = name;
        }
    }
}
