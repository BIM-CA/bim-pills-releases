using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BIMPills.Core.ParameterExtractor;
using BIMPills.Infrastructure.Persistence;
using BIMPills.UI.ParameterExtractor;
using BIMPills.UI.Shared;

namespace BIMPills.UI.Export.Parameters
{
    public partial class ParameterExtractorPanel : UserControl
    {
        // One brush per group; cycles through palette when more than 8 groups.
        private static readonly Brush[] TagPalette =
        {
            new SolidColorBrush(Color.FromRgb(0x42, 0x85, 0xF4)), // blue
            new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35)), // red
            new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)), // green
            new SolidColorBrush(Color.FromRgb(0xF5, 0x7C, 0x00)), // orange
            new SolidColorBrush(Color.FromRgb(0x6A, 0x1B, 0x9A)), // purple
            new SolidColorBrush(Color.FromRgb(0x00, 0x83, 0x8F)), // teal
            new SolidColorBrush(Color.FromRgb(0xAD, 0x14, 0x57)), // pink
            new SolidColorBrush(Color.FromRgb(0x37, 0x47, 0x4F)), // blue-grey
        };

        public ObservableCollection<SourceMappingVM> Sources { get; } = new();
        public ObservableCollection<TargetMappingVM> Targets { get; } = new();

        private Func<ExtractionConfig, bool>? _applyCallback;
        private JsonExtractionPresetRepository? _presetRepo;
        private readonly ObservableCollection<ExtractionPreset> _presets = new();
        private bool _suppressPresetEvent;
        private bool _syncingSelection;

        private IReadOnlyList<string>? _availableCategories;
        private IReadOnlyList<string>? _availableParameters;

        public event EventHandler<bool>? ExportEnabledChanged;
        public string ExportLabel => "Aplicar extracción";

        public ParameterExtractorPanel()
        {
            InitializeComponent();
            SourceGrid.ItemsSource = Sources;
            TargetGrid.ItemsSource = Targets;
            PresetCombo.ItemsSource = _presets;

            Sources.CollectionChanged += Sources_CollectionChanged;
            Targets.CollectionChanged += (_, _) => RaiseEnabled();

            UnitsCombo.SelectedItem    = ExtractionLengthUnits.Meters;
            DecimalsCombo.SelectedItem = 3;

            SeedSampleRules();
        }

        public void Initialize(
            int selectedElementCount,
            Func<ExtractionConfig, bool>? applyCallback = null,
            JsonExtractionPresetRepository? presetRepository = null,
            IReadOnlyList<string>? availableCategories = null,
            IReadOnlyList<string>? availableParameters = null)
        {
            _applyCallback         = applyCallback;
            _availableCategories   = availableCategories;
            _availableParameters   = availableParameters;
            _presetRepo = presetRepository ?? new JsonExtractionPresetRepository();
            SelectionSummaryText.Text = selectedElementCount == 1
                ? "1 elemento seleccionado"
                : $"{selectedElementCount} elementos seleccionados";

            LoadPresets();
            RaiseEnabled();
        }

        public void TriggerExport()
        {
            var config = BuildConfig();
            if (config.Rules.Count == 0)
            {
                BimPillsDialog.Warning(
                    "Extractor de Parámetros",
                    "Agrega al menos una regla con nombre de parámetro destino.",
                    owner: Window.GetWindow(this));
                return;
            }

            if (_applyCallback == null)
            {
                BimPillsDialog.Info(
                    "Extractor de Parámetros",
                    $"Se aplicarían {config.Rules.Count} reglas a los elementos seleccionados.",
                    detail: $"Unidades: {config.LengthUnits} · Decimales: {config.Decimals}",
                    owner: Window.GetWindow(this));
                return;
            }

            _applyCallback(config);
        }

        // ── Units / Rounding ────────────────────────────────────────────────────

        private void Units_Changed(object sender, SelectionChangedEventArgs e) => RaiseEnabled();
        private void Decimals_Changed(object sender, SelectionChangedEventArgs e) => RaiseEnabled();

        // ── Selection sync between grids ────────────────────────────────────────

        private void SourceGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncingSelection) return;
            _syncingSelection = true;
            try
            {
                if (SourceGrid.SelectedItem is SourceMappingVM src)
                {
                    // Select first target belonging to this group
                    var first = Targets.FirstOrDefault(t => t.GroupId == src.Id);
                    TargetGrid.SelectedItem = first;
                }
            }
            finally { _syncingSelection = false; }
        }

        private void TargetGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncingSelection) return;
            _syncingSelection = true;
            try
            {
                if (TargetGrid.SelectedItem is TargetMappingVM tgt)
                {
                    var src = Sources.FirstOrDefault(s => s.Id == tgt.GroupId);
                    SourceGrid.SelectedItem = src;
                }
            }
            finally { _syncingSelection = false; }
        }

        // ── Source collection changes ───────────────────────────────────────────

        private void Sources_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ReassignTags();
            RaiseEnabled();
        }

        /// <summary>
        /// Recalcula tags: Source A, B, C, ... · Target "A" (simple) o "B1"/"B2" (dual).
        /// </summary>
        private void ReassignTags()
        {
            for (int i = 0; i < Sources.Count; i++)
            {
                string letter = IndexToLetter(i);
                var brush    = TagPalette[i % TagPalette.Length];

                Sources[i].Tag      = letter;
                Sources[i].TagBrush = brush;

                var groupTargets = Targets.Where(t => t.GroupId == Sources[i].Id).ToList();
                if (groupTargets.Count == 1)
                {
                    groupTargets[0].Tag      = letter;
                    groupTargets[0].TagBrush = brush;
                }
                else
                {
                    for (int j = 0; j < groupTargets.Count; j++)
                    {
                        groupTargets[j].Tag      = letter + (j + 1);
                        groupTargets[j].TagBrush = brush;
                    }
                }
            }
        }

        private static string IndexToLetter(int i)
        {
            // 0→A, 25→Z, 26→AA, 27→AB, ...
            string s = string.Empty;
            i++;
            while (i > 0)
            {
                i--;
                s = (char)('A' + (i % 26)) + s;
                i /= 26;
            }
            return s;
        }

        // ── Presets ─────────────────────────────────────────────────────────────

        private void LoadPresets()
        {
            if (_presetRepo == null) return;
            _presets.Clear();
            foreach (var p in _presetRepo.GetAll().OrderBy(p => p.Name))
                _presets.Add(p);

            UpdatePresetButtonState();
        }

        private void UpdatePresetButtonState()
        {
            bool hasSelection = PresetCombo.SelectedItem is ExtractionPreset;
            UpdatePresetButton.IsEnabled = hasSelection;
            DeletePresetButton.IsEnabled = hasSelection;
        }

        private void Preset_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressPresetEvent) return;
            UpdatePresetButtonState();

            if (PresetCombo.SelectedItem is ExtractionPreset preset)
                ApplyConfigToGrid(preset.Config);
        }

        private void ApplyConfigToGrid(ExtractionConfig config)
        {
            Sources.Clear();
            Targets.Clear();

            // Flat load: each ExtractionRule → 1 source + 1 target. Dual pairs
            // can be recreated by the user via "+ Dual" on load.
            foreach (var rule in config.Rules)
            {
                var src = new SourceMappingVM
                {
                    Source = rule.Source,
                    SourceParameterName = rule.SourceParameterName,
                    IsDual = false,
                };
                Sources.Add(src);
                Targets.Add(new TargetMappingVM
                {
                    GroupId = src.Id,
                    TargetParameterName = rule.Target.ParameterName,
                    DataType = rule.Target.DataType,
                    CreateIfMissing = rule.Target.CreateIfMissing,
                    CoordinateOrigin = rule.CoordinateOrigin,
                    GeoFormat = rule.GeoFormat,
                    DualRole = "Single",
                });
            }
            ReassignTags();

            UnitsCombo.SelectedItem = config.LengthUnits;
            DecimalsCombo.SelectedItem = Math.Max(0, Math.Min(6, config.Decimals));
        }

        private void SavePreset_Click(object sender, RoutedEventArgs e)
        {
            if (_presetRepo == null) return;

            var config = BuildConfig();
            if (config.Rules.Count == 0)
            {
                BimPillsDialog.Warning(
                    "Guardar perfil",
                    "No hay reglas que guardar. Agrega al menos una regla.",
                    owner: Window.GetWindow(this));
                return;
            }

            var name = PromptForName(
                title: "Guardar perfil",
                label: "Nombre del perfil:",
                defaultValue: $"Extractor {DateTime.Now:yyyy-MM-dd HH:mm}");

            if (string.IsNullOrWhiteSpace(name))
                return;

            var preset = new ExtractionPreset { Name = name.Trim(), Config = config };
            _presetRepo.Create(preset);
            LoadPresets();

            _suppressPresetEvent = true;
            try { PresetCombo.SelectedItem = _presets.FirstOrDefault(p => p.Id == preset.Id); }
            finally { _suppressPresetEvent = false; }
            UpdatePresetButtonState();
        }

        private void UpdatePreset_Click(object sender, RoutedEventArgs e)
        {
            if (_presetRepo == null) return;
            if (PresetCombo.SelectedItem is not ExtractionPreset preset) return;

            bool confirmed = BimPillsDialog.Confirm(
                "Actualizar perfil",
                $"¿Sobrescribir el perfil \"{preset.Name}\" con las reglas actuales?",
                owner: Window.GetWindow(this),
                yesText: "Sobrescribir",
                noText: "Cancelar");
            if (!confirmed) return;

            preset.Config = BuildConfig();
            _presetRepo.Update(preset);
            LoadPresets();

            _suppressPresetEvent = true;
            try { PresetCombo.SelectedItem = _presets.FirstOrDefault(p => p.Id == preset.Id); }
            finally { _suppressPresetEvent = false; }
            UpdatePresetButtonState();
        }

        private void DeletePreset_Click(object sender, RoutedEventArgs e)
        {
            if (_presetRepo == null) return;
            if (PresetCombo.SelectedItem is not ExtractionPreset preset) return;

            bool confirmed = BimPillsDialog.Confirm(
                "Eliminar perfil",
                $"¿Eliminar el perfil \"{preset.Name}\"?",
                detail: "Esta acción no se puede deshacer.",
                owner: Window.GetWindow(this),
                yesText: "Eliminar",
                noText: "Cancelar",
                kind: BimPillsDialog.DialogKind.Warning);
            if (!confirmed) return;

            _presetRepo.Delete(preset.Id);
            LoadPresets();
        }

        // ── Rule edit callbacks ─────────────────────────────────────────────────

        private void RaiseEnabled()
        {
            bool hasValidRule = Targets.Any(t => !string.IsNullOrWhiteSpace(t.TargetParameterName));
            ExportEnabledChanged?.Invoke(this, hasValidRule);
        }

        private ExtractionConfig BuildConfig()
        {
            var config = new ExtractionConfig
            {
                LengthUnits = UnitsCombo.SelectedItem is ExtractionLengthUnits u
                    ? u
                    : ExtractionLengthUnits.Meters,
                Decimals = DecimalsCombo.SelectedItem is int d ? d : 3
            };

            foreach (var src in Sources)
            {
                var groupTargets = Targets.Where(t => t.GroupId == src.Id);
                foreach (var tgt in groupTargets)
                {
                    if (string.IsNullOrWhiteSpace(tgt.TargetParameterName))
                        continue;

                    config.Rules.Add(new ExtractionRule
                    {
                        Source = src.Source,
                        SourceParameterName = src.SourceParameterName ?? string.Empty,
                        CoordinateOrigin = tgt.CoordinateOrigin,
                        GeoFormat = tgt.GeoFormat,
                        Target = new ExtractionTarget
                        {
                            ParameterName = tgt.TargetParameterName,
                            DataType = tgt.DataType,
                            CreateIfMissing = tgt.CreateIfMissing
                        }
                    });
                }
            }
            return config;
        }

        private void SeedSampleRules()
        {
            // Simple: Category → BP_Categoria
            AddSimple(ExtractionSourceKind.Category, "BP_Categoria", ExtractionDataType.Text);

            // Dual: LocationX → crudo (Internal) + convertido (ProjectBase)
            AddDual(
                ExtractionSourceKind.LocationX,
                rawName: "BP_X_Crudo",       rawOrigin: CoordinateOrigin.Internal,
                convName: "BP_X_Convertido", convOrigin: CoordinateOrigin.ProjectBase,
                dataType: ExtractionDataType.Length);

            // Dual: Latitude → decimal + DMS
            AddDual(
                ExtractionSourceKind.Latitude,
                rawName: "BP_Lat_Decimal", rawOrigin: CoordinateOrigin.ProjectBase,
                convName: "BP_Lat_DMS",    convOrigin: CoordinateOrigin.ProjectBase,
                dataType: ExtractionDataType.Number,
                rawGeo: GeoFormat.Decimal, convGeo: GeoFormat.Dms,
                convDataType: ExtractionDataType.Text);
        }

        private SourceMappingVM AddSimple(
            ExtractionSourceKind source,
            string targetName,
            ExtractionDataType dataType,
            CoordinateOrigin origin = CoordinateOrigin.Internal,
            GeoFormat geo = GeoFormat.Decimal)
        {
            var src = new SourceMappingVM { Source = source, IsDual = false };
            Sources.Add(src);
            Targets.Add(new TargetMappingVM
            {
                GroupId = src.Id,
                TargetParameterName = targetName,
                DataType = dataType,
                CoordinateOrigin = origin,
                GeoFormat = geo,
                DualRole = "Single",
            });
            ReassignTags();
            return src;
        }

        private SourceMappingVM AddDual(
            ExtractionSourceKind source,
            string rawName, CoordinateOrigin rawOrigin,
            string convName, CoordinateOrigin convOrigin,
            ExtractionDataType dataType,
            GeoFormat rawGeo = GeoFormat.Decimal,
            GeoFormat convGeo = GeoFormat.Decimal,
            ExtractionDataType? convDataType = null)
        {
            var src = new SourceMappingVM { Source = source, IsDual = true };
            Sources.Add(src);
            Targets.Add(new TargetMappingVM
            {
                GroupId = src.Id,
                TargetParameterName = rawName,
                DataType = dataType,
                CoordinateOrigin = rawOrigin,
                GeoFormat = rawGeo,
                DualRole = "Raw",
            });
            Targets.Add(new TargetMappingVM
            {
                GroupId = src.Id,
                TargetParameterName = convName,
                DataType = convDataType ?? dataType,
                CoordinateOrigin = convOrigin,
                GeoFormat = convGeo,
                DualRole = "Converted",
            });
            ReassignTags();
            return src;
        }

        private void AddSources_Click(object sender, RoutedEventArgs e)
        {
            var selected = ParameterPickerModal.Show(
                Window.GetWindow(this),
                _availableCategories,
                _availableParameters);

            if (selected == null || selected.Count == 0) return;

            SourceMappingVM? lastAdded = null;
            foreach (var item in selected)
            {
                if (item.IsDual)
                {
                    lastAdded = AddDual(
                        item.Source,
                        rawName:      item.DefaultTargetName,
                        rawOrigin:    item.CoordinateOrigin,
                        convName:     item.SecondaryTargetName,
                        convOrigin:   item.SecondaryOrigin,
                        dataType:     item.DefaultDataType,
                        rawGeo:       item.GeoFormat,
                        convGeo:      item.SecondaryGeoFormat,
                        convDataType: item.SecondaryDataType);
                }
                else
                {
                    lastAdded = AddSimple(
                        item.Source,
                        targetName: item.DefaultTargetName,
                        dataType:   item.DefaultDataType,
                        origin:     item.CoordinateOrigin,
                        geo:        item.GeoFormat);
                }
            }

            if (lastAdded != null)
            {
                SourceGrid.SelectedItem = lastAdded;
                SourceGrid.ScrollIntoView(lastAdded);
            }
        }

        private void RemoveRule_Click(object sender, RoutedEventArgs e)
        {
            // Remove the selected source and all its targets.
            SourceMappingVM? src = SourceGrid.SelectedItem as SourceMappingVM;
            if (src == null && TargetGrid.SelectedItem is TargetMappingVM tgtSel)
                src = Sources.FirstOrDefault(s => s.Id == tgtSel.GroupId);
            if (src == null) return;

            var toRemove = Targets.Where(t => t.GroupId == src.Id).ToList();
            foreach (var t in toRemove)
                Targets.Remove(t);
            Sources.Remove(src);
            ReassignTags();
        }

        private void RenameTarget_Click(object sender, RoutedEventArgs e)
        {
            if (TargetGrid.SelectedItem is not TargetMappingVM tgt)
            {
                BimPillsDialog.Info(
                    "Renombrar parámetro",
                    "Selecciona primero una fila en la tabla DESTINO.",
                    owner: Window.GetWindow(this));
                return;
            }

            var newName = PromptForName(
                title: "Renombrar parámetro destino",
                label: "Nombre del parámetro:",
                defaultValue: tgt.TargetParameterName);

            if (!string.IsNullOrWhiteSpace(newName))
                tgt.TargetParameterName = newName.Trim();
        }

        // ── Prompt dialog ───────────────────────────────────────────────────────

        private string? PromptForName(string title, string label, string defaultValue = "")
        {
            var dlg = new Window
            {
                Title                 = title,
                Width                 = 380,
                SizeToContent         = SizeToContent.Height,
                Owner                 = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode            = ResizeMode.NoResize,
                Background            = System.Windows.Media.Brushes.White,
                WindowStyle           = WindowStyle.ToolWindow
            };

            var grid = new Grid { Margin = new Thickness(18) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lbl = new TextBlock
            {
                Text = label, FontSize = 12,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(lbl, 0);

            var tb = new TextBox
            {
                Height = 30, FontSize = 12,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                Padding = new Thickness(6, 4, 6, 4),
                BorderBrush = System.Windows.Media.Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Text = defaultValue
            };
            Grid.SetRow(tb, 1);

            var btns = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(btns, 3);

            string? result = null;
            var ok = new Button
            {
                Content = "Guardar", Width = 80, Height = 28,
                Margin = new Thickness(0, 0, 8, 0), IsDefault = true,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                Background = System.Windows.Media.Brushes.White
            };
            ok.Click += (_, __) => { result = tb.Text; dlg.DialogResult = true; };

            var cancel = new Button
            {
                Content = "Cancelar", Width = 80, Height = 28,
                IsCancel = true,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
            };

            btns.Children.Add(ok);
            btns.Children.Add(cancel);
            grid.Children.Add(lbl);
            grid.Children.Add(tb);
            grid.Children.Add(btns);
            dlg.Content = grid;

            tb.Loaded += (_, __) => { tb.Focus(); tb.SelectAll(); };
            return dlg.ShowDialog() == true ? result : null;
        }
    }
}
