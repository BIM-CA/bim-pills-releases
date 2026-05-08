using BIMPills.Core.Documentacion;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BIMPills.UI.Documentacion
{
    public partial class AcotadoVanosWindow : Window
    {
        private readonly AcotadoVanosData _data;
        private readonly Func<AcotadoVanosSettings, AcotadoVanosResult>? _executeCallback;
        private string _selectedScheme = "opening-width";
        private bool _useActiveView = true;

        public AcotadoVanosWindow(
            AcotadoVanosData data,
            Func<AcotadoVanosSettings, AcotadoVanosResult>? executeCallback = null)
        {
            _data = data;
            _executeCallback = executeCallback;

            InitializeComponent();
            BIMPills.UI.Shared.ThemeHelper.Apply(this);
            Populate();
        }

        private void Populate()
        {
            // Poblar ComboBox de tipos de cota
            DimTypeCombo.ItemsSource = _data.DimensionTypes;
            DimTypeCombo.DisplayMemberPath = "Name";
            if (_data.DimensionTypes.Count > 0)
                DimTypeCombo.SelectedIndex = 0;

            // Status bar
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            var count = _data.DoorCount;
            StatusText.Text = count > 0
                ? $"{count} puertas detectadas en la vista"
                : "0 puertas detectadas";

            ExecuteBtn.IsEnabled = count > 0 && DimTypeCombo.SelectedItem != null;
        }

        // ── Scope chips ──

        private void ScopeChip_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border chip) return;
            var tag = chip.Tag?.ToString();

            _useActiveView = tag == "active-view";

            // Visual: reset both
            SetChipInactive(ChipActiveView);
            SetChipInactive(ChipSelection);

            // Activate selected
            SetChipActive(chip);

            // Update info text
            InfoText.Inlines.Clear();
            if (_useActiveView)
            {
                InfoText.Inlines.Add(new System.Windows.Documents.Run("Se crearán cotas en la "));
                InfoText.Inlines.Add(new System.Windows.Documents.Bold(
                    new System.Windows.Documents.Run("vista activa")));
                InfoText.Inlines.Add(new System.Windows.Documents.Run(
                    " para cada puerta visible. Las cotas existentes no serán duplicadas."));
            }
            else
            {
                InfoText.Inlines.Add(new System.Windows.Documents.Run("Se crearán cotas solo para las "));
                InfoText.Inlines.Add(new System.Windows.Documents.Bold(
                    new System.Windows.Documents.Run("puertas seleccionadas")));
                InfoText.Inlines.Add(new System.Windows.Documents.Run(
                    ". Las cotas existentes no serán duplicadas."));
            }
        }

        private static void SetChipActive(Border chip)
        {
            chip.Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xEB, 0xE6));
            chip.BorderBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x63, 0x37));
            var tb = chip.Child as TextBlock;
            if (tb != null)
            {
                tb.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x63, 0x37));
                tb.FontWeight = FontWeights.Medium;
            }
        }

        private static void SetChipInactive(Border chip)
        {
            chip.Background = Brushes.White;
            chip.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xEA));
            var tb = chip.Child as TextBlock;
            if (tb != null)
            {
                tb.Foreground = new SolidColorBrush(Color.FromRgb(0x86, 0x86, 0x8B));
                tb.FontWeight = FontWeights.Normal;
            }
        }

        // ── Scheme cards ──

        private void SchemeCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border card) return;
            var tag = card.Tag?.ToString();
            if (string.IsNullOrEmpty(tag)) return;

            _selectedScheme = tag!;

            // Toggle radio buttons
            if (tag == "opening-width")
                RadioOpening.IsChecked = true;
            else if (tag == "wall-chain")
                RadioChain.IsChecked = true;

            UpdateSchemeVisuals();
        }

        private void SchemeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton rb) return;
            _selectedScheme = rb.Tag?.ToString() ?? "opening-width";
            UpdateSchemeVisuals();
        }

        private void UpdateSchemeVisuals()
        {
            var accent = new SolidColorBrush(Color.FromRgb(0xEF, 0x63, 0x37));
            var separator = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xEA));
            var activeBg = new SolidColorBrush(Color.FromRgb(0xFA, 0xF7, 0xF5));

            // Reset all
            if (SchemeOpeningWidth != null)
            {
                SchemeOpeningWidth.BorderBrush = separator;
                SchemeOpeningWidth.Background = Brushes.White;
            }
            if (SchemeWallChain != null)
            {
                SchemeWallChain.BorderBrush = separator;
                SchemeWallChain.Background = Brushes.White;
            }

            // Activate selected
            Border? active = _selectedScheme switch
            {
                "opening-width" => SchemeOpeningWidth,
                "wall-chain" => SchemeWallChain,
                _ => null
            };

            if (active != null)
            {
                active.BorderBrush = accent;
                active.Background = activeBg;
            }
        }

        // ── Offset slider ──

        private void OffsetSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (OffsetValueText == null) return;
            var val = (int)OffsetSlider.Value;
            OffsetValueText.Text = val.ToString();
            UpdatePresetHighlight(val);
        }

        private void PresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (int.TryParse(btn.Tag?.ToString(), out var val))
            {
                OffsetSlider.Value = val;
            }
        }

        private void UpdatePresetHighlight(int currentValue)
        {
            // Find preset buttons in the visual tree and highlight matching one
            // This is handled via the Tag matching in code-behind
        }

        // ── Actions ──

        private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

        private void ExecuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_executeCallback == null)
            {
                MessageBox.Show(
                    "La función de acotado solo está disponible dentro de Revit.",
                    "BIM Pills", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedDimType = DimTypeCombo.SelectedItem as DimensionTypeInfo;
            if (selectedDimType == null)
            {
                MessageBox.Show(
                    "Selecciona un tipo de cota antes de ejecutar.",
                    "BIM Pills", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var settings = new AcotadoVanosSettings
            {
                Scheme = _selectedScheme,
                DimensionTypeId = selectedDimType.Id,
                OffsetMm = OffsetSlider.Value,
                UseActiveView = _useActiveView
            };

            ExecuteBtn.IsEnabled = false;
            ExecuteBtn.Content = "Procesando...";

            try
            {
                var result = _executeCallback(settings);

                // Mostrar resultado
                var icon = result.DimensionsCreated > 0
                    ? MessageBoxImage.Information
                    : MessageBoxImage.Warning;

                MessageBox.Show(
                    result.Message,
                    "BIMPills — Acotado de Vanos",
                    MessageBoxButton.OK,
                    icon);

                if (result.DimensionsCreated > 0)
                {
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error durante el acotado:\n{ex.Message}",
                    "BIMPills — Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                ExecuteBtn.IsEnabled = true;
                ExecuteBtn.Content = CreateExecuteContent();
            }
        }

        private object CreateExecuteContent()
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            var canvas = new Canvas { Width = 14, Height = 14, Margin = new Thickness(0, 0, 6, 0) };
            var polyline = new System.Windows.Shapes.Polyline
            {
                Points = PointCollection.Parse("3,7 6,10 11,4"),
                Stroke = Brushes.White,
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            canvas.Children.Add(polyline);
            sp.Children.Add(canvas);
            sp.Children.Add(new TextBlock { Text = "Acotar", VerticalAlignment = VerticalAlignment.Center });
            return sp;
        }
    }
}
