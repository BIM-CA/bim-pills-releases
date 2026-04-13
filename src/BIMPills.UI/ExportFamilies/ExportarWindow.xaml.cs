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
        // Tab indexes (new order): 0 = Planos y Vistas, 1 = Modelo, 2 = Familias
        private const int TabPlanos = 0;
        private const int TabModelo = 1;
        private const int TabFamilias = 2;

        private string _familiesSubtitle = "";
        private string _sheetsSubtitle = "";
        private string _modelSubtitle = "";
        private int _activeTab = TabPlanos;

        private bool _familiesCanExport;
        private bool _sheetsCanExport;
        private bool _modelCanExport;

        /// <summary>Export queue built by SheetsPanel for non-blocking processing by the command.</summary>
        public List<ExportQueueItem>? PendingExportQueue => SheetsPanel.PendingExportQueue;

        /// <summary>
        /// Global PDF engine settings (Native vs SystemPrinter) chosen by the user.
        /// Read by the Revit command right after the dialog closes so the export queue
        /// can route each item to the right pipeline.
        /// </summary>
        public PdfEngineSettings GetPdfEngineSettings() => SheetsPanel.GetPdfEngineSettings();

        public ExportarWindow()
        {
            InitializeComponent();
            BIMPills.UI.Shared.ThemeHelper.Apply(this);

            // Wire panel events to footer action button
            ExportPanel.ExportEnabledChanged += (_, canExport) =>
            {
                _familiesCanExport = canExport;
                if (_activeTab == TabFamilias) UpdateActionButton();
            };
            SheetsPanel.ExportEnabledChanged += (_, canExport) =>
            {
                _sheetsCanExport = canExport;
                if (_activeTab == TabPlanos) UpdateActionButton();
            };
            ModelPanel.ExportEnabledChanged += (_, canExport) =>
            {
                _modelCanExport = canExport;
                if (_activeTab == TabModelo) UpdateActionButton();
            };

            // Wire step-change events to update Next/Action button visibility
            SheetsPanel.StepChanged += (_, step) =>
            {
                if (_activeTab == TabPlanos) UpdateFooterButtons();
            };
            ModelPanel.StepChanged += (_, step) =>
            {
                if (_activeTab == TabModelo) UpdateFooterButtons();
            };
        }

        private void UpdateActionButton()
        {
            switch (_activeTab)
            {
                case TabPlanos:
                    ActionButton.IsEnabled = _sheetsCanExport;
                    ActionButton.Content = SheetsPanel.ExportLabel;
                    break;
                case TabModelo:
                    ActionButton.IsEnabled = _modelCanExport;
                    ActionButton.Content = ModelPanel.ExportLabel;
                    break;
                case TabFamilias:
                    ActionButton.IsEnabled = _familiesCanExport;
                    ActionButton.Content = ExportPanel.ExportLabel;
                    break;
            }
            UpdateFooterButtons();
        }

        /// <summary>
        /// Shows "Siguiente →" on wizard steps 1 and 2; shows the action button on the last step
        /// (or when the active tab has no step wizard, e.g. Familias).
        /// </summary>
        private void UpdateFooterButtons()
        {
            bool onLastStep;
            switch (_activeTab)
            {
                case TabPlanos:
                    onLastStep = SheetsPanel.CurrentStep >= SheetsPanel.StepCount;
                    break;
                case TabModelo:
                    onLastStep = ModelPanel.CurrentStep >= ModelPanel.StepCount;
                    break;
                default: // TabFamilias — single step, always show action button
                    onLastStep = true;
                    break;
            }

            NextButton.Visibility  = onLastStep ? Visibility.Collapsed : Visibility.Visible;
            ActionButton.Visibility = onLastStep ? Visibility.Visible  : Visibility.Collapsed;
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            switch (_activeTab)
            {
                case TabPlanos:  SheetsPanel.NextStep(); break;
                case TabModelo:  ModelPanel.NextStep();  break;
            }
        }

        private void Action_Click(object sender, RoutedEventArgs e)
        {
            switch (_activeTab)
            {
                case TabPlanos:   SheetsPanel.TriggerExport(); break;
                case TabModelo:   ModelPanel.TriggerExport(); break;
                case TabFamilias: ExportPanel.TriggerExport(); break;
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

            if (_activeTab == TabFamilias)
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

            if (_activeTab == TabPlanos)
                SubtitleText.Text = _sheetsSubtitle;
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

        private void TabPlanos_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_activeTab == TabPlanos) return;
            _activeTab = TabPlanos;
            UpdateTabVisualState();
        }

        private void TabModelo_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_activeTab == TabModelo) return;
            _activeTab = TabModelo;
            UpdateTabVisualState();
        }

        private void TabFamilias_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_activeTab == TabFamilias) return;
            _activeTab = TabFamilias;
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
                case TabPlanos:
                    TabPlanosBorder.Style   = activeTabStyle;
                    TabPlanosText.Style     = activeTextStyle;
                    SheetsPanel.Visibility  = Visibility.Visible;
                    ToolHeaderTitle.Text    = "Exportar Planos y Vistas";
                    ToolHeaderSubtitle.Text = "Exporta planos y vistas a PDF y DWG en lote";
                    SubtitleText.Text       = _sheetsSubtitle;
                    break;
                case TabModelo:
                    TabModeloBorder.Style   = activeTabStyle;
                    TabModeloText.Style     = activeTextStyle;
                    ModelPanel.Visibility   = Visibility.Visible;
                    ToolHeaderTitle.Text    = "Exportar Modelo";
                    ToolHeaderSubtitle.Text = "Exporta el modelo completo a NWC u otros formatos";
                    SubtitleText.Text       = _modelSubtitle;
                    break;
                case TabFamilias:
                    TabFamiliasBorder.Style = activeTabStyle;
                    TabFamiliasText.Style   = activeTextStyle;
                    ExportPanel.Visibility  = Visibility.Visible;
                    ToolHeaderTitle.Text    = "Exportar Familias";
                    ToolHeaderSubtitle.Text = "Exporta familias del modelo a carpeta local";
                    SubtitleText.Text       = _familiesSubtitle;
                    break;
            }

            UpdateActionButton();
            UpdateFooterButtons();
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
