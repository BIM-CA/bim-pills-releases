using BIMPills.Core.Audit;
using BIMPills.Core.Models;
using BIMPills.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace BIMPills.UI.ExportFamilies
{
    public partial class ExportarWindow : Window
    {
        private string _familiesSubtitle = "";
        private string _sheetsSubtitle = "";
        private string _modelSubtitle = "";
        private int _activeTab = 0;

        private bool _familiesCanExport;
        private bool _sheetsCanExport;
        private bool _modelCanExport;

        public ExportarWindow()
        {
            InitializeComponent();
            BIMPills.UI.Shared.ThemeHelper.Apply(this);

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
            ModelPanel.ExportEnabledChanged += (_, canExport) =>
            {
                _modelCanExport = canExport;
                if (_activeTab == 2) UpdateActionButton();
            };
        }

        private void UpdateActionButton()
        {
            switch (_activeTab)
            {
                case 0:
                    ActionButton.IsEnabled = _familiesCanExport;
                    ActionButton.Content = ExportPanel.ExportLabel;
                    break;
                case 1:
                    ActionButton.IsEnabled = _sheetsCanExport;
                    ActionButton.Content = SheetsPanel.ExportLabel;
                    break;
                case 2:
                    ActionButton.IsEnabled = _modelCanExport;
                    ActionButton.Content = ModelPanel.ExportLabel;
                    break;
            }
        }

        private void Action_Click(object sender, RoutedEventArgs e)
        {
            switch (_activeTab)
            {
                case 0: ExportPanel.TriggerExport(); break;
                case 1: SheetsPanel.TriggerExport(); break;
                case 2: ModelPanel.TriggerExport(); break;
            }
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
            _familiesSubtitle = $"{families.Count} familias en {categoryCount} categor\u00edas";

            if (_activeTab == 0)
                SubtitleText.Text = _familiesSubtitle;
        }

        /// <summary>
        /// Initializes the Export Sheets tab with data and export callbacks (backward compat).
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

        /// <summary>
        /// Initializes the Planos y Vistas tab with unified exportable views.
        /// </summary>
        public void InitializeExportViews(
            IReadOnlyList<ExportableViewInfo> items,
            Func<long, string, string, PdfExportSettings, bool>? pdfExportCallback = null,
            Func<long, string, string, DwgExportConfig?, bool>? dwgExportCallback = null,
            string projectName = "",
            ILogger? logger = null,
            IReadOnlyList<string>? availableParameters = null,
            IReadOnlyList<string>? dwgPresetNames = null)
        {
            SheetsPanel.InitializeViews(items, pdfExportCallback, dwgExportCallback, projectName, logger, availableParameters, dwgPresetNames);
            var sheetCount = items.Count(i => i.ItemType == ExportableItemType.Sheet);
            var viewCount = items.Count - sheetCount;
            _sheetsSubtitle = $"{sheetCount} planos + {viewCount} vistas disponibles";
        }

        /// <summary>
        /// Initializes the Export Model (NWC) tab.
        /// </summary>
        public void InitializeExportModel(
            string modelTitle,
            string? activeViewName = null,
            bool nwcAvailable = true,
            Func<NwcExportConfig, bool>? nwcExportCallback = null,
            ILogger? logger = null,
            IReadOnlyList<string>? availableParameters = null,
            IReadOnlyDictionary<string, string>? parameterValues = null,
            IReadOnlyList<NwcExportPreset>? presets = null,
            IReadOnlyList<NwcViewInfo>? availableViews = null)
        {
            ModelPanel.Initialize(modelTitle, activeViewName, nwcAvailable, nwcExportCallback, logger,
                availableParameters, parameterValues, presets, availableViews);
            _modelSubtitle = "Exportar modelo a formato NWC";
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

        private void TabModelo_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_activeTab == 2) return;
            _activeTab = 2;
            UpdateTabVisualState();
        }

        private void UpdateTabVisualState()
        {
            var activeTabStyle    = (Style)FindResource("TabHeaderActive");
            var inactiveTabStyle  = (Style)FindResource("TabHeader");
            var activeTextStyle   = (Style)FindResource("TabHeaderTextActive");
            var inactiveTextStyle = (Style)FindResource("TabHeaderText");

            // Reset all tabs to inactive
            TabFamiliasBorder.Style = inactiveTabStyle;
            TabFamiliasText.Style   = inactiveTextStyle;
            TabPlanosBorder.Style   = inactiveTabStyle;
            TabPlanosText.Style     = inactiveTextStyle;
            TabModeloBorder.Style   = inactiveTabStyle;
            TabModeloText.Style     = inactiveTextStyle;

            ExportPanel.Visibility  = Visibility.Collapsed;
            SheetsPanel.Visibility  = Visibility.Collapsed;
            ModelPanel.Visibility   = Visibility.Collapsed;

            switch (_activeTab)
            {
                case 0:
                    TabFamiliasBorder.Style = activeTabStyle;
                    TabFamiliasText.Style   = activeTextStyle;
                    ExportPanel.Visibility  = Visibility.Visible;
                    ToolHeaderTitle.Text    = "Exportar Familias";
                    ToolHeaderSubtitle.Text = "Exporta familias del modelo a carpeta local";
                    SubtitleText.Text       = _familiesSubtitle;
                    break;
                case 1:
                    TabPlanosBorder.Style   = activeTabStyle;
                    TabPlanosText.Style     = activeTextStyle;
                    SheetsPanel.Visibility  = Visibility.Visible;
                    ToolHeaderTitle.Text    = "Exportar Planos y Vistas";
                    ToolHeaderSubtitle.Text = "Exporta planos y vistas a PDF y DWG en lote";
                    SubtitleText.Text       = _sheetsSubtitle;
                    break;
                case 2:
                    TabModeloBorder.Style   = activeTabStyle;
                    TabModeloText.Style     = activeTextStyle;
                    ModelPanel.Visibility   = Visibility.Visible;
                    ToolHeaderTitle.Text    = "Exportar Modelo";
                    ToolHeaderSubtitle.Text = "Exporta el modelo completo a NWC u otros formatos";
                    SubtitleText.Text       = _modelSubtitle;
                    break;
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
