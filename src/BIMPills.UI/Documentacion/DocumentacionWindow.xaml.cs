using BIMPills.Core.LegendFromExcel;
using BIMPills.Core.Documentacion;
using BIMPills.Core.Services;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BIMPills.UI.Documentacion
{
    public partial class DocumentacionWindow : Window
    {
        private const string TabAcotado = "acotado";
        private const string TabLeyenda = "leyenda";
        private string _activeTab = TabAcotado;

        public DocumentacionWindow()
        {
            InitializeComponent();
            BIMPills.UI.Shared.ThemeHelper.Apply(this);
        }

        public void SetDocumentName(string name) => DocumentName.Text = name;

        public void InitializeAcotado(
            AcotadoVanosData data,
            Func<AcotadoVanosSettings, AcotadoVanosResult>? executeCallback = null,
            ILogger? logger = null)
        {
            AcotadoPanel.Initialize(data, executeCallback, logger);
        }

        public void InitializeDibujar(
            IReadOnlyList<RevitStyleInfo> textStyles,
            IReadOnlyList<RevitStyleInfo> lineStyles,
            IReadOnlyList<RevitStyleInfo> fillTypes,
            Func<string, LegendDrawOptions, bool>? drawCallback = null)
        {
            DibujarPanel.Initialize(textStyles, lineStyles, fillTypes, drawCallback);
        }

        public void NavigateToTab(string tab)
        {
            _activeTab = tab;
            UpdateTabVisualState();
        }

        // ── Tab click handlers ──────────────────────────────────────────────

        private void TabAcotado_Click(object sender, MouseButtonEventArgs e)
        {
            _activeTab = TabAcotado;
            UpdateTabVisualState();
        }

        private void TabLeyenda_Click(object sender, MouseButtonEventArgs e)
        {
            _activeTab = TabLeyenda;
            UpdateTabVisualState();
        }

        // ── Tab visual state ────────────────────────────────────────────────

        private void UpdateTabVisualState()
        {
            var activeTabStyle    = (Style)FindResource("TabHeaderActive");
            var inactiveTabStyle  = (Style)FindResource("TabHeader");
            var activeTextStyle   = (Style)FindResource("TabHeaderTextActive");
            var inactiveTextStyle = (Style)FindResource("TabHeaderText");

            TabAcotadoBorder.Style = inactiveTabStyle;
            TabAcotadoText.Style   = inactiveTextStyle;
            TabLeyendaBorder.Style = inactiveTabStyle;
            TabLeyendaText.Style   = inactiveTextStyle;

            AcotadoPanel.Visibility = Visibility.Collapsed;
            DibujarPanel.Visibility = Visibility.Collapsed;

            switch (_activeTab)
            {
                case TabAcotado:
                    TabAcotadoBorder.Style  = activeTabStyle;
                    TabAcotadoText.Style    = activeTextStyle;
                    AcotadoPanel.Visibility = Visibility.Visible;
                    ToolHeaderIcon.Slug     = "ruler";
                    ToolHeaderTitle.Text    = "Acotado";
                    ToolHeaderSubtitle.Text = "Acotado automático de elementos";
                    break;

                case TabLeyenda:
                    TabLeyendaBorder.Style  = activeTabStyle;
                    TabLeyendaText.Style    = activeTextStyle;
                    DibujarPanel.Visibility = Visibility.Visible;
                    ToolHeaderIcon.Slug     = "attach-excel";
                    ToolHeaderTitle.Text    = "Leyenda desde Excel";
                    ToolHeaderSubtitle.Text = "Dibuja tablas y leyendas en vistas de Revit a partir de un .xlsx";
                    break;
            }
        }
    }
}
