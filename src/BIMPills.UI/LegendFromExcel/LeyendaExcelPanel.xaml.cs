using BIMPills.Core.LegendFromExcel;
using BIMPills.Infrastructure.LegendFromExcel;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace BIMPills.UI.LegendFromExcel
{
    public partial class LeyendaExcelPanel : UserControl
    {
        private Func<string, LegendDrawOptions, bool>? _drawCallback;
        private string? _selectedFilePath;
        private ExcelTableModel? _parsedTable;
        private ExcelPreviewWindow? _previewWindow;
        private int _currentStep = 1;

        public LeyendaExcelPanel()
        {
            InitializeComponent();
        }

        private void GoToStep(int step)
        {
            _currentStep = step;

            Step1Content.Visibility = step == 1 ? Visibility.Visible  : Visibility.Collapsed;
            Step2Content.Visibility = step == 2 ? Visibility.Visible  : Visibility.Collapsed;

            NextButton.Visibility    = step == 1 ? Visibility.Visible  : Visibility.Collapsed;
            PreviewButton.Visibility = step == 2 ? Visibility.Visible  : Visibility.Collapsed;
            DrawButton.Visibility    = step == 2 ? Visibility.Visible  : Visibility.Collapsed;

            // Step indicator: active badge = AccentBrush, inactive = grey
            Badge1.Background  = step == 1
                ? (System.Windows.Media.Brush)FindResource("AccentBrush")
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAE, 0xAE, 0xB2));
            Badge2.Background  = step == 2
                ? (System.Windows.Media.Brush)FindResource("AccentBrush")
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAE, 0xAE, 0xB2));

            StepLabel1.FontWeight = step == 1 ? FontWeights.SemiBold : FontWeights.Normal;
            StepLabel1.Foreground = step == 1
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1D, 0x1D, 0x1F))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAE, 0xAE, 0xB2));
            StepLabel2.FontWeight = step == 2 ? FontWeights.SemiBold : FontWeights.Normal;
            StepLabel2.Foreground = step == 2
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1D, 0x1D, 0x1F))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAE, 0xAE, 0xB2));

            UpdateDrawEnabled();
        }

        private void Next_Click(object sender, RoutedEventArgs e) => GoToStep(2);

        public void Initialize(
            IReadOnlyList<RevitStyleInfo> textStyles,
            IReadOnlyList<RevitStyleInfo> lineStyles,
            IReadOnlyList<RevitStyleInfo> fillTypes,
            Func<string, LegendDrawOptions, bool>? drawCallback)
        {
            _drawCallback = drawCallback;

            TextStyleValuesCombo.ItemsSource  = textStyles;
            TextStyleHeader1Combo.ItemsSource = textStyles;
            TextStyleHeader2Combo.ItemsSource = textStyles;
            if (textStyles.Count > 0)
            {
                TextStyleValuesCombo.SelectedIndex  = 0;
                TextStyleHeader1Combo.SelectedIndex = 0;
                TextStyleHeader2Combo.SelectedIndex = 0;
            }

            LineStyleCombo.ItemsSource = lineStyles;
            if (lineStyles.Count > 0) LineStyleCombo.SelectedIndex = 0;

            var fillList = new List<RevitStyleInfo?> { null };
            foreach (var f in fillTypes) fillList.Add(f);
            FillType1Combo.ItemsSource = fillList;
            FillType2Combo.ItemsSource = fillList;
            FillType1Combo.SelectedIndex = 0;
            FillType2Combo.SelectedIndex = 0;

            UpdateSectionVisibility();
            UpdateDrawEnabled();
        }

        public void TriggerDraw()
        {
            if (_selectedFilePath == null || _drawCallback == null) return;
            _drawCallback(_selectedFilePath, BuildOptions());
        }

        private LegendDrawOptions BuildOptions()
        {
            double.TryParse(CellWidthBox.Text,  out var w); if (w <= 0) w = 50;
            double.TryParse(CellHeightBox.Text, out var h); if (h <= 0) h = 8;

            bool customize  = CustomizeHeadersCheck.IsChecked == true;
            int  rows       = !customize ? 0 : (HeaderRows2.IsChecked == true ? 2 : 1);
            bool diffText   = customize && DifferentiateHeaderCheck.IsChecked == true;
            bool applyFill  = customize && ApplyFillCheck.IsChecked == true;

            return new LegendDrawOptions
            {
                ViewName           = string.IsNullOrWhiteSpace(ViewNameBox.Text) ? "Leyenda" : ViewNameBox.Text.Trim(),
                LineStyleId        = (LineStyleCombo.SelectedItem  as RevitStyleInfo)?.Id ?? 0,
                TextStyleIdValues  = (TextStyleValuesCombo.SelectedItem  as RevitStyleInfo)?.Id ?? 0,
                HeaderRowsCount    = rows,
                DifferentiateHeader= diffText,
                TextStyleIdHeader1 = diffText         ? (TextStyleHeader1Combo.SelectedItem as RevitStyleInfo)?.Id ?? 0 : 0,
                TextStyleIdHeader2 = diffText && rows == 2 ? (TextStyleHeader2Combo.SelectedItem as RevitStyleInfo)?.Id ?? 0 : 0,
                ApplyFill          = applyFill,
                FillRegionTypeId1  = applyFill         ? (FillType1Combo.SelectedItem as RevitStyleInfo)?.Id ?? 0 : 0,
                FillRegionTypeId2  = applyFill && rows == 2 ? (FillType2Combo.SelectedItem as RevitStyleInfo)?.Id ?? 0 : 0,
                CellWidthMm        = w,
                CellHeightMm       = h,
            };
        }

        // ── File ─────────────────────────────────────────────────────────

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title  = "Seleccionar archivo Excel",
                Filter = "Excel (*.xlsx)|*.xlsx|Todos los archivos (*.*)|*.*",
            };
            if (dlg.ShowDialog() != true) return;

            _selectedFilePath = dlg.FileName;
            FilePathBox.Text  = dlg.FileName;
            TableInfoBorder.Visibility = Visibility.Collapsed;
            _parsedTable = null;

            try
            {
                _parsedTable = ExcelTableParser.Parse(dlg.FileName);
                TableInfoText.Text = $"{System.IO.Path.GetFileName(dlg.FileName)} " +
                                     $"— {_parsedTable.RowCount} filas × {_parsedTable.ColumnCount} columnas";
                TableInfoBorder.Visibility = Visibility.Visible;
                RefreshPreviewIfOpen();
            }
            catch (Exception ex)
            {
                TableInfoText.Text = $"Error al leer: {ex.Message}";
                TableInfoBorder.Visibility = Visibility.Visible;
            }

            UpdateDrawEnabled();
        }

        // ── Toggles ──────────────────────────────────────────────────────

        private void ToggleSection_Changed(object sender, RoutedEventArgs e)
        {
            UpdateSectionVisibility();
            RefreshPreviewIfOpen();
        }

        private void UpdateSectionVisibility()
        {
            // Guard contra eventos que se disparan durante la carga del BAML
            if (FillHintText == null) return;

            bool customize = CustomizeHeadersCheck.IsChecked == true;
            bool is2Rows   = HeaderRows2.IsChecked == true;
            bool diffText  = DifferentiateHeaderCheck.IsChecked == true;
            bool applyFill = ApplyFillCheck.IsChecked == true;

            HeaderSection.Visibility = customize ? Visibility.Visible : Visibility.Collapsed;

            // Texto diferenciado
            HeaderTextSection.Visibility = diffText ? Visibility.Visible : Visibility.Collapsed;
            Header2TextPanel.Visibility  = diffText && is2Rows ? Visibility.Visible : Visibility.Collapsed;

            // Relleno
            FillSection.Visibility   = applyFill ? Visibility.Visible : Visibility.Collapsed;
            FillType2Panel.Visibility = applyFill && is2Rows ? Visibility.Visible : Visibility.Collapsed;

            // Hint solo visible cuando hay relleno activo
            FillHintText.Visibility = customize && applyFill ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Cell size ────────────────────────────────────────────────────

        private void CellSize_TextChanged(object sender, TextChangedEventArgs e)
            => RefreshPreviewIfOpen();

        // ── Preview ──────────────────────────────────────────────────────

        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            if (_parsedTable == null || _selectedFilePath == null) return;

            if (_previewWindow == null || !_previewWindow.IsLoaded)
            {
                _previewWindow = new ExcelPreviewWindow();
                _previewWindow.Owner  = Window.GetWindow(this);
                _previewWindow.Closed += (_, __) => _previewWindow = null;
                PositionPreviewWindowToLeft(_previewWindow);
                _previewWindow.Show();
            }
            else
            {
                _previewWindow.Activate();
            }

            RenderPreview();
        }

        private void RefreshPreviewIfOpen()
        {
            if (_previewWindow != null && _previewWindow.IsLoaded)
                RenderPreview();
        }

        private void RenderPreview()
        {
            if (_previewWindow == null || _parsedTable == null || _selectedFilePath == null) return;

            double.TryParse(CellWidthBox.Text,  out var w); if (w <= 0) w = 50;
            double.TryParse(CellHeightBox.Text, out var h); if (h <= 0) h = 8;

            _previewWindow.Render(_parsedTable, _selectedFilePath, w, h);
        }

        private void PositionPreviewWindowToLeft(Window previewWin)
        {
            var parent = Window.GetWindow(this);
            if (parent == null) return;
            previewWin.Left = Math.Max(parent.Left - previewWin.Width - 8, 0);
            previewWin.Top  = parent.Top;
        }

        // ── View name / draw ─────────────────────────────────────────────

        private void ViewNameBox_TextChanged(object sender, TextChangedEventArgs e)
            => UpdateDrawEnabled();

        private void Draw_Click(object sender, RoutedEventArgs e)   => TriggerDraw();
        private void Cancel_Click(object sender, RoutedEventArgs e) => Window.GetWindow(this)?.Close();

        private void UpdateDrawEnabled()
        {
            bool hasFile = !string.IsNullOrWhiteSpace(_selectedFilePath);
            bool hasName = !string.IsNullOrWhiteSpace(ViewNameBox?.Text);
            if (NextButton    != null) NextButton.IsEnabled    = hasFile && hasName;
            if (DrawButton    != null) DrawButton.IsEnabled    = hasFile && hasName;
            if (PreviewButton != null) PreviewButton.IsEnabled = hasFile && _parsedTable != null;
        }
    }
}
