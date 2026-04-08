using BIMPills.Core.Models;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace BIMPills.UI.Transfer
{
    public partial class TransferWindow : Window
    {
        private int _activeTab = 0;

        public TransferWindow()
        {
            InitializeComponent();
            BIMPills.UI.Shared.ThemeHelper.Apply(this);

            TemplatesPanel.TransferEnabledChanged += (_, _) => { if (_activeTab == 0) UpdateTransferButton(); };
            FiltersPanel.TransferEnabledChanged   += (_, _) => { if (_activeTab == 1) UpdateTransferButton(); };
            StandardsPanel.TransferEnabledChanged += (_, _) => { if (_activeTab == 2) UpdateTransferButton(); };
        }

        public void SetModelName(string modelName) => ModelNameLabel.Text = modelName;

        public void InitializeViewTemplates(
            IReadOnlyList<OpenDocumentInfo> openDocs,
            Func<string, IReadOnlyList<ViewTemplateInfo>>? getTemplatesCallback = null,
            Func<string, long, ViewTemplateDetail?>? getDetailCallback = null,
            Func<string, IReadOnlyList<long>, ConflictResolution, TransferResult>? transferCallback = null)
        {
            TemplatesPanel.Initialize(openDocs, getTemplatesCallback, getDetailCallback, transferCallback);
        }

        public void InitializeViewFilters(
            IReadOnlyList<OpenDocumentInfo> openDocs,
            Func<string, IReadOnlyList<TransferableFilterInfo>>? getFiltersCallback = null,
            Func<string, long, FilterDetail?>? getDetailCallback = null,
            Func<string, IReadOnlyList<long>, ConflictResolution, TransferResult>? transferCallback = null)
        {
            FiltersPanel.Initialize(openDocs, getFiltersCallback, getDetailCallback, transferCallback);
        }

        public void InitializeProjectStandards(
            IReadOnlyList<OpenDocumentInfo> openDocs,
            Func<string, string, IReadOnlyList<ProjectStandardItem>>? getItemsCallback = null,
            Func<string, IReadOnlyList<long>, ConflictResolution, Action<int, int, string>?, ProjectStandardTransferResult>? transferCallback = null)
        {
            StandardsPanel.Initialize(openDocs, getItemsCallback, transferCallback);
        }

        // ── Tab switching ─────────────────────────────────────────────────────

        private void TabPlantillas_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_activeTab == 0) return;
            _activeTab = 0;
            UpdateTabVisualState();
        }

        private void TabFiltros_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_activeTab == 1) return;
            _activeTab = 1;
            UpdateTabVisualState();
        }

        private void TabEstandares_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_activeTab == 2) return;
            _activeTab = 2;
            UpdateTabVisualState();
        }

        private void UpdateTabVisualState()
        {
            var activeStyle   = (Style)FindResource("TabHeaderActive");
            var inactiveStyle = (Style)FindResource("TabHeader");
            var activeText    = (Style)FindResource("TabHeaderTextActive");
            var inactiveText  = (Style)FindResource("TabHeaderText");

            Tab1Border.Style = _activeTab == 0 ? activeStyle : inactiveStyle;
            Tab1Text.Style   = _activeTab == 0 ? activeText  : inactiveText;
            Tab2Border.Style = _activeTab == 1 ? activeStyle : inactiveStyle;
            Tab2Text.Style   = _activeTab == 1 ? activeText  : inactiveText;
            Tab3Border.Style = _activeTab == 2 ? activeStyle : inactiveStyle;
            Tab3Text.Style   = _activeTab == 2 ? activeText  : inactiveText;

            TemplatesPanel.Visibility = _activeTab == 0 ? Visibility.Visible : Visibility.Collapsed;
            FiltersPanel.Visibility   = _activeTab == 1 ? Visibility.Visible : Visibility.Collapsed;
            StandardsPanel.Visibility = _activeTab == 2 ? Visibility.Visible : Visibility.Collapsed;

            switch (_activeTab)
            {
                case 0:
                    ToolHeaderIconBorder.Background = HexBrush("#E3F2FD");
                    ToolHeaderIcon.Text             = "\uE8A1";
                    ToolHeaderIcon.Foreground       = HexBrush("#1565C0");
                    ToolHeaderTitle.Text            = "Plantillas de Vista";
                    ToolHeaderSubtitle.Text         = "Transfiere plantillas de vista desde otros proyectos abiertos";
                    break;
                case 1:
                    ToolHeaderIconBorder.Background = HexBrush("#F3E5F5");
                    ToolHeaderIcon.Text             = "\uE74C";
                    ToolHeaderIcon.Foreground       = HexBrush("#6A1B9A");
                    ToolHeaderTitle.Text            = "Filtros de Vista";
                    ToolHeaderSubtitle.Text         = "Transfiere filtros de vista desde otros proyectos abiertos";
                    break;
                case 2:
                    ToolHeaderIconBorder.Background = HexBrush("#E8F5E9");
                    ToolHeaderIcon.Text             = "\uE8AB";
                    ToolHeaderIcon.Foreground       = HexBrush("#2E7D32");
                    ToolHeaderTitle.Text            = "Otros Est\u00e1ndares";
                    ToolHeaderSubtitle.Text         = "Transfiere tipos, estilos y configuraciones desde otro proyecto abierto";
                    break;
            }

            UpdateTransferButton();
        }

        private static SolidColorBrush HexBrush(string hex)
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            return new SolidColorBrush(c);
        }

        // ── Transfer button ───────────────────────────────────────────────────

        private void UpdateTransferButton()
        {
            var label = _activeTab == 0 ? TemplatesPanel.TransferLabel
                      : _activeTab == 1 ? FiltersPanel.TransferLabel
                      : StandardsPanel.TransferLabel;

            TransferButtonLabel.Text       = label;
            FooterTransferButton.IsEnabled = label != "Trasladar";
        }

        private void Transfer_Click(object sender, RoutedEventArgs e)
        {
            if      (_activeTab == 0) TemplatesPanel.TriggerTransfer();
            else if (_activeTab == 1) FiltersPanel.TriggerTransfer();
            else if (_activeTab == 2) StandardsPanel.TriggerTransfer();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
