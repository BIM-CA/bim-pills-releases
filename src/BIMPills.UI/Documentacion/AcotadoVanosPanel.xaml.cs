using BIMPills.Core.Documentacion;
using BIMPills.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BIMPills.UI.Documentacion
{
    public partial class AcotadoVanosPanel : UserControl
    {
        private AcotadoVanosData? _data;
        private Func<AcotadoVanosSettings, AcotadoVanosResult>? _executeCallback;
        private ILogger? _logger;
        private string _selectedScheme = "opening-width";
        private string _selectedGridEndpoint = "end";
        private bool _useActiveView = true;

        public AcotadoVanosPanel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Initializes the panel with data from Revit, execution callback and optional logger.
        /// </summary>
        public void Initialize(
            AcotadoVanosData data,
            Func<AcotadoVanosSettings, AcotadoVanosResult>? executeCallback = null,
            ILogger? logger = null)
        {
            _data = data;
            _executeCallback = executeCallback;
            _logger = logger;
            Populate();
        }

        private static readonly List<SchemeOption> _schemes = new()
        {
            new("opening-width",   "Anchos de vanos",           "Cota el ancho de cada vano de puerta visible en la vista"),
            new("grid-combined",   "Cotas a ejes",              "Cotas totales y parciales entre rejillas en una acción"),
            new("interior-spaces", "Cotas espacios interiores", "Dimensiones H y V del espacio usando contornos de habitación"),
            new("arq-levels",      "Niveles ARQ",               "Cotas totales y parciales entre niveles cuyo tipo contiene ARQ"),
        };

        private void Populate()
        {
            if (_data == null) return;

            // Set default scheme display
            ApplySchemeDisplay(_selectedScheme);

            // Populate dimension types
            DimTypeCombo.ItemsSource = _data.DimensionTypes;
            DimTypeCombo.DisplayMemberPath = "Name";
            if (_data.DimensionTypes.Count > 0)
                DimTypeCombo.SelectedIndex = 0;

            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (StatusText == null) return;

            switch (_selectedScheme)
            {
                case "grid-combined":
                    var gridCount = _data?.GridCount ?? 0;
                    StatusText.Text = gridCount > 0
                        ? $"{gridCount} ejes detectados en la vista"
                        : "0 ejes detectados";
                    if (ExecuteBtn != null)
                        ExecuteBtn.IsEnabled = gridCount >= 2 && DimTypeCombo.SelectedItem != null;
                    break;
                case "interior-spaces":
                    var wallCount = _data?.WallCount ?? 0;
                    StatusText.Text = wallCount > 0
                        ? $"{wallCount} muros detectados en la vista"
                        : "0 muros detectados";
                    if (ExecuteBtn != null)
                        ExecuteBtn.IsEnabled = wallCount >= 2 && DimTypeCombo.SelectedItem != null;
                    break;
                case "arq-levels":
                    var levelCount = _data?.LevelCount ?? 0;
                    StatusText.Text = levelCount > 0
                        ? $"{levelCount} niveles ARQ detectados en el modelo"
                        : "0 niveles ARQ detectados";
                    if (ExecuteBtn != null)
                        ExecuteBtn.IsEnabled = levelCount >= 2 && DimTypeCombo.SelectedItem != null;
                    break;
                default:
                    var doorCount = _data?.DoorCount ?? 0;
                    StatusText.Text = doorCount > 0
                        ? $"{doorCount} puertas detectadas en la vista"
                        : "0 puertas detectadas";
                    if (ExecuteBtn != null)
                        ExecuteBtn.IsEnabled = doorCount > 0 && DimTypeCombo.SelectedItem != null;
                    break;
            }
        }

        // ── Scope chips ──

        private void ScopeChip_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is not Border chip) return;
                var tag = chip.Tag?.ToString();

                _useActiveView = tag == "active-view";

                SetChipInactive(ChipActiveView);
                SetChipInactive(ChipSelection);
                SetChipActive(chip);

                UpdateInfoBox();
            }
            catch (Exception ex)
            {
                _logger?.Error("Error en ScopeChip_Click", ex);
                MessageBox.Show($"Error inesperado:\n{ex.Message}\n\nRevisa el log para más detalles.",
                    "BIMPills — Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void SetChipActive(Border chip)
        {
            chip.Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xEB, 0xE6));
            chip.BorderBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x63, 0x37));
            if (chip.Child is TextBlock tb)
            {
                tb.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x63, 0x37));
                tb.FontWeight = FontWeights.Medium;
            }
        }

        private static void SetChipInactive(Border chip)
        {
            chip.Background = Brushes.White;
            chip.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xEA));
            if (chip.Child is TextBlock tb)
            {
                tb.Foreground = new SolidColorBrush(Color.FromRgb(0x86, 0x86, 0x8B));
                tb.FontWeight = FontWeights.Normal;
            }
        }

        // ── Scheme picker modal ──

        private void SchemePickerButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new SchemePickerWindow(_selectedScheme)
                {
                    Owner = Window.GetWindow(this)
                };
                if (picker.ShowDialog() == true && picker.SelectedSchemeId != null)
                {
                    _selectedScheme = picker.SelectedSchemeId;
                    ApplySchemeDisplay(_selectedScheme);
                    UpdateContextualUI();
                }
            }
            catch (Exception ex)
            {
                _logger?.Error("Error al abrir selector de esquema", ex);
                MessageBox.Show($"Error inesperado:\n{ex.Message}\n\nRevisa el log para más detalles.",
                    "BIMPills — Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplySchemeDisplay(string schemeId)
        {
            var scheme = _schemes.FirstOrDefault(s => s.Id == schemeId) ?? _schemes[0];
            if (SchemeNameText != null) SchemeNameText.Text = scheme.Name;
            if (SchemeDescText != null) SchemeDescText.Text = scheme.Description;
        }

        private void UpdateContextualUI()
        {
            // Endpoint panel: visible for grids and levels (both use start/end/both)
            var showEndpoints = _selectedScheme == "grid-combined" || _selectedScheme == "arq-levels";
            if (GridEndpointPanel != null)
                GridEndpointPanel.Visibility = showEndpoints ? Visibility.Visible : Visibility.Collapsed;

            // Step 4 description
            if (OffsetDescription != null)
            {
                OffsetDescription.Text = _selectedScheme switch
                {
                    "grid-combined"
                        => "Distancia desde el extremo de rejilla hasta la línea de cota. Valores negativos invierten la dirección (en milímetros).",
                    "interior-spaces"
                        => "Distancia desde el muro más cercano hacia el interior de la habitación. Valores negativos mueven la cota hacia el exterior (en milímetros).",
                    "arq-levels"
                        => "Distancia desde el extremo del nivel hasta la línea de cota. Valores negativos invierten la dirección (en milímetros).",
                    _ => "Separación entre la línea de cota y el elemento acotado (en milímetros).",
                };
            }

            // Info box
            UpdateInfoBox();

            // Status text
            UpdateStatus();
        }

        private void UpdateInfoBox()
        {
            if (InfoText == null) return;
            InfoText.Inlines.Clear();

            var scope = _useActiveView ? "vista activa" : "puertas seleccionadas";

            switch (_selectedScheme)
            {
                case "grid-combined":
                    InfoText.Inlines.Add(new System.Windows.Documents.Run("Se crearán cotas totales y parciales apiladas entre ejes paralelos en la "));
                    InfoText.Inlines.Add(new System.Windows.Documents.Bold(new System.Windows.Documents.Run(scope)));
                    InfoText.Inlines.Add(new System.Windows.Documents.Run("."));
                    break;
                case "interior-spaces":
                    InfoText.Inlines.Add(new System.Windows.Documents.Run("Se crearán cotas entre caras interiores de muros paralelos en la "));
                    InfoText.Inlines.Add(new System.Windows.Documents.Bold(new System.Windows.Documents.Run(scope)));
                    InfoText.Inlines.Add(new System.Windows.Documents.Run(". Se omite el espesor de muro."));
                    break;
                case "arq-levels":
                    InfoText.Inlines.Add(new System.Windows.Documents.Run("Se crearán cotas totales y parciales entre niveles cuyo tipo contiene "));
                    InfoText.Inlines.Add(new System.Windows.Documents.Bold(new System.Windows.Documents.Run("ARQ")));
                    InfoText.Inlines.Add(new System.Windows.Documents.Run(". Se colocan en los extremos seleccionados (inicio/fin/ambos). Requiere sección o alzado."));
                    break;
                default:
                    InfoText.Inlines.Add(new System.Windows.Documents.Run("Se crearán cotas en la "));
                    InfoText.Inlines.Add(new System.Windows.Documents.Bold(new System.Windows.Documents.Run(scope)));
                    InfoText.Inlines.Add(new System.Windows.Documents.Run(" para cada puerta visible. Las cotas existentes no serán duplicadas."));
                    break;
            }
        }

        // ── Grid endpoint chips ──

        private void EndpointChip_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is not Border chip) return;
                _selectedGridEndpoint = chip.Tag?.ToString() ?? "end";

                SetChipInactive(ChipEndpointStart);
                SetChipInactive(ChipEndpointEnd);
                SetChipInactive(ChipEndpointBoth);
                SetChipActive(chip);
            }
            catch (Exception ex)
            {
                _logger?.Error("Error en EndpointChip_Click", ex);
                MessageBox.Show($"Error inesperado:\n{ex.Message}\n\nRevisa el log para más detalles.",
                    "BIMPills — Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Offset slider ──

        private bool _updatingOffset;

        private void OffsetSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (OffsetValueText == null || _updatingOffset) return;
                _updatingOffset = true;
                var val = (int)OffsetSlider.Value;
                OffsetValueText.Text = val.ToString();
                _updatingOffset = false;
            }
            catch (Exception ex)
            {
                _updatingOffset = false;
                _logger?.Error("Error en OffsetSlider_ValueChanged", ex);
            }
        }

        private void OffsetInput_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyOffsetFromInput();
            }
            catch (Exception ex)
            {
                _logger?.Error("Error en OffsetInput_LostFocus", ex);
            }
        }

        private void OffsetInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            try
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                    ApplyOffsetFromInput();
            }
            catch (Exception ex)
            {
                _logger?.Error("Error en OffsetInput_KeyDown", ex);
            }
        }

        private void ApplyOffsetFromInput()
        {
            if (_updatingOffset) return;
            _updatingOffset = true;
            if (int.TryParse(OffsetValueText.Text, out var val))
            {
                OffsetSlider.Value = val;
                OffsetValueText.Text = val.ToString();
            }
            else
            {
                OffsetValueText.Text = ((int)OffsetSlider.Value).ToString();
            }
            _updatingOffset = false;
        }

        private void PresetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is not Button btn) return;
                if (int.TryParse(btn.Tag?.ToString(), out var val))
                    OffsetSlider.Value = val;
            }
            catch (Exception ex)
            {
                _logger?.Error("Error en PresetButton_Click", ex);
                MessageBox.Show($"Error inesperado:\n{ex.Message}\n\nRevisa el log para más detalles.",
                    "BIMPills — Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Actions ──

        private void MainScroll_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
        {
            if (ScrollHint == null) return;
            bool hasMore = MainScroll.VerticalOffset + MainScroll.ViewportHeight < MainScroll.ExtentHeight - 1;
            ScrollHint.Visibility = hasMore ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Window.GetWindow(this)?.Close();
            }
            catch (Exception ex)
            {
                _logger?.Error("Error en CancelButton_Click", ex);
            }
        }

        private void ExecuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_executeCallback == null)
            {
                MessageBox.Show(
                    "La función de acotado solo está disponible dentro de Revit.",
                    "BIMPills", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedDimType = DimTypeCombo.SelectedItem as DimensionTypeInfo;
            if (selectedDimType == null)
            {
                MessageBox.Show(
                    "Selecciona un tipo de cota antes de ejecutar.",
                    "BIMPills", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var settings = new AcotadoVanosSettings
            {
                Scheme = _selectedScheme,
                DimensionTypeId = selectedDimType.Id,
                OffsetMm = OffsetSlider.Value,
                UseActiveView = _useActiveView,
                GridEndpoint = _selectedGridEndpoint
            };

            // ── Confirmation modal ────────────────────────────────────────────────
            var scheme      = _schemes.FirstOrDefault(s => s.Id == _selectedScheme) ?? _schemes[0];
            var elementCount = _selectedScheme switch
            {
                "grid-combined"    => _data?.GridCount  ?? 0,
                "interior-spaces"  => _data?.WallCount  ?? 0,
                "arq-levels"       => _data?.LevelCount ?? 0,
                _                  => _data?.DoorCount  ?? 0
            };
            var elementLabel = _selectedScheme switch
            {
                "grid-combined"    => "ejes",
                "interior-spaces"  => "muros",
                "arq-levels"       => "niveles ARQ",
                _                  => "puertas"
            };

            var confirm = new ConfirmDimensionWindow(
                schemeName:    scheme.Name,
                viewName:      _data?.ActiveViewName ?? "Vista activa",
                elementCount:  elementCount,
                elementLabel:  elementLabel,
                dimTypeName:   selectedDimType.Name,
                offsetMm:      (int)OffsetSlider.Value)
            {
                Owner = Window.GetWindow(this)
            };

            if (confirm.ShowDialog() != true) return;
            // ─────────────────────────────────────────────────────────────────────

            ExecuteBtn.IsEnabled = false;
            HideResultStatus();

            try
            {
                var result = _executeCallback(settings);

                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    // ── Fatal error ──────────────────────────────────────────────────────────
                    _logger?.Error($"[AcotadoVanos] Error fatal en esquema '{settings.Scheme}': {result.ErrorMessage}");

                    MessageBox.Show(
                        $"Error durante el acotado:\n{result.ErrorMessage}\n\nRevisa el log para más detalles.",
                        "BIMPills — Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                else if (result.SkippedItems?.Any() == true)
                {
                    // ── Partial success with skipped items ───────────────────────────────────
                    _logger?.Info($"[AcotadoVanos] Acotado completado con advertencias: {result.CreatedCount} cota(s) creada(s), {result.SkippedItems.Count} elemento(s) omitido(s).");
                    foreach (var skipped in result.SkippedItems)
                        _logger?.Warning($"[AcotadoVanos] Omitido — {skipped}");

                    var skippedText = string.Join("\n", result.SkippedItems.Select(s => $"• {s}"));
                    MessageBox.Show(
                        $"✓ Acotado completado: {result.CreatedCount} cota(s) creada(s).\n\n" +
                        $"⚠ {result.SkippedItems.Count} elemento(s) omitido(s):\n{skippedText}",
                        "BIMPills — Resumen",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    ShowResultStatus($"✓ {result.CreatedCount} cota(s) creada(s) — {result.SkippedItems.Count} omitida(s).");

                    if (result.CreatedCount > 0)
                    {
                        var win = Window.GetWindow(this);
                        if (win != null) try { win.DialogResult = true; } catch (InvalidOperationException) { }
                        win?.Close();
                    }
                }
                else
                {
                    // ── Full success — no blocking dialog ────────────────────────────────────
                    _logger?.Info($"[AcotadoVanos] Acotado completado: {result.CreatedCount} cota(s) creada(s) correctamente.");

                    ShowResultStatus($"✓ {result.CreatedCount} cota(s) creada(s) correctamente.");

                    if (result.CreatedCount > 0)
                    {
                        var win = Window.GetWindow(this);
                        if (win != null) try { win.DialogResult = true; } catch (InvalidOperationException) { }
                        win?.Close();
                    }
                    else
                    {
                        // Zero cotas but no error and no skipped items — warn the user
                        _logger?.Warning($"[AcotadoVanos] El acotado no creó ninguna cota. Motivo: {result.Message}");
                        MessageBox.Show(
                            result.Message,
                            "BIMPills — Acotado",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"[AcotadoVanos] Excepción no controlada en esquema '{settings.Scheme}'", ex);

                MessageBox.Show(
                    $"Error durante el acotado:\n{ex.Message}\n\nRevisa el log para más detalles.",
                    "BIMPills — Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                ExecuteBtn.IsEnabled = true;
            }
        }

        private void ShowResultStatus(string message)
        {
            if (ResultStatusLabel == null) return;
            ResultStatusLabel.Text = message;
            ResultStatusLabel.Visibility = Visibility.Visible;
        }

        private void HideResultStatus()
        {
            if (ResultStatusLabel == null) return;
            ResultStatusLabel.Text = string.Empty;
            ResultStatusLabel.Visibility = Visibility.Collapsed;
        }

        // ── Scheme option model ──

        private sealed class SchemeOption
        {
            public string Id { get; }
            public string Name { get; }
            public string Description { get; }

            public SchemeOption(string id, string name, string description)
            {
                Id = id;
                Name = name;
                Description = description;
            }
        }
    }
}
