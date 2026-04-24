using BIMPills.Core.ParameterExtractor;
using BIMPills.Infrastructure.Persistence;
using BIMPills.UI.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace BIMPills.UI.Export.Parameters
{
    // ── Filter tree node (Category → Family → Type) ─────────────────────────

    public enum FilterNodeLevel { Category, Family, Type }

    public sealed class FilterTreeNode : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _isChecked;  // unchecked by default
        private bool _updating;
        internal FilterTreeNode? Parent;

        public string          Label    { get; init; } = "";
        public FilterNodeLevel Level    { get; init; }
        public List<FilterTreeNode> Children { get; } = new();

        public bool IsExpandedByDefault => false;  // collapsed by default
        public bool IsCategory => Level == FilterNodeLevel.Category;
        public bool IsFamily   => Level == FilterNodeLevel.Family;

        public void AddChild(FilterTreeNode child)
        {
            child.Parent = this;
            Children.Add(child);
        }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_updating || _isChecked == value) return;
                SetChecked(value, propagateDown: true, propagateUp: true);
            }
        }

        internal void SetChecked(bool value, bool propagateDown, bool propagateUp)
        {
            if (_isChecked == value) return;
            _updating = true;
            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            _updating = false;
            if (propagateDown) foreach (var c in Children) c.SetChecked(value, true, false);
            if (propagateUp) Parent?.RecalcFromChildren();
        }

        internal void RecalcFromChildren()
        {
            if (Children.Count == 0) return;
            bool newVal = Children.All(c => c._isChecked);
            if (_isChecked == newVal) return;
            _updating = true;
            _isChecked = newVal;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            _updating = false;
            Parent?.RecalcFromChildren();
        }

        public bool HasAnyChecked()
            => _isChecked || Children.Any(c => c.HasAnyChecked());
    }

    // ── Param tree node — simple bool IsChecked, no tri-state ───────────────

    public sealed class ParamTreeNode : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _isChecked;
        private bool _updating;
        internal ParamTreeNode? Parent;

        public string Label { get; init; } = "";
        public List<ParamTreeNode> Children { get; } = new();
        public bool IsLeaf    => Children.Count == 0;
        public bool IsGroup   => !IsLeaf;
        public bool IsGeoGroup => IsGroup && Label.Contains("Geogr", StringComparison.OrdinalIgnoreCase);

        // Leaf-only properties
        public ExtractionSourceKind? Source   { get; init; }
        public CoordinateOrigin      Origin   { get; init; } = CoordinateOrigin.Internal;
        public GeoFormat             GeoFmt   { get; init; } = GeoFormat.Decimal;
        public ExtractionDataType    DataType { get; init; } = ExtractionDataType.Text;
        public string DefaultParamName        { get; init; } = "";

        public void AddChild(ParamTreeNode child)
        {
            child.Parent = this;
            Children.Add(child);
        }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_updating || _isChecked == value) return;
                SetChecked(value, propagateDown: true, propagateUp: true);
            }
        }

        private void SetChecked(bool value, bool propagateDown, bool propagateUp)
        {
            if (_isChecked == value) return;
            _updating = true;
            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            _updating = false;

            if (propagateDown)
                foreach (var child in Children) child.SetChecked(value, true, false);

            if (propagateUp) Parent?.RecalcFromChildren();
        }

        internal void RecalcFromChildren()
        {
            if (Children.Count == 0) return;
            bool newVal = Children.All(c => c._isChecked);
            if (_isChecked == newVal) return;
            _updating = true;
            _isChecked = newVal;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            _updating = false;
            Parent?.RecalcFromChildren();
        }

        public IEnumerable<ParamTreeNode> AllLeaves()
        {
            if (IsLeaf) { yield return this; yield break; }
            foreach (var child in Children)
                foreach (var leaf in child.AllLeaves())
                    yield return leaf;
        }
    }

    // ── Row in Tab-2 DataGrid ─────────────────────────────────────────────────

    public sealed class ParamRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _paramName = "";
        private ExtractionDataType _dataType;

        public string ParamName
        {
            get => _paramName;
            set { _paramName = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ParamName))); }
        }

        public ExtractionDataType DataType
        {
            get => _dataType;
            set { _dataType = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DataType))); }
        }

        public string             SourceLabel      { get; init; } = "";
        public string             DefaultParamName { get; init; } = "";
        public ExtractionSourceKind Source         { get; init; }
        public CoordinateOrigin   Origin           { get; init; } = CoordinateOrigin.Internal;
        public GeoFormat          GeoFmt           { get; init; } = GeoFormat.Decimal;

        public bool IsGeoRow => Source == ExtractionSourceKind.Latitude ||
                                Source == ExtractionSourceKind.Longitude;
    }

    // ── Panel ─────────────────────────────────────────────────────────────────

    public partial class ParameterExtractorPanel : UserControl
    {
        public static ExtractionDataType[] AllDataTypes { get; } =
            (ExtractionDataType[])Enum.GetValues(typeof(ExtractionDataType));

        private ExtractionScope _scope = ExtractionScope.WholeModel;
        private Func<ExtractionConfig, bool>? _applyCallback;
        private List<ParamTreeNode>  _allTreeRoots = new();   // unfiltered
        private List<ParamTreeNode>  _treeRoots    = new();   // visible
        private List<FilterTreeNode> _filterRoots  = new();   // category/family/type filter

        private readonly ObservableCollection<ParamRow> _paramRows = new();
        private readonly Dictionary<(ExtractionSourceKind, CoordinateOrigin, GeoFormat), string> _customNames = new();
        private IReadOnlyDictionary<string, bool> _hasCurveByCategory = new Dictionary<string, bool>();

        private JsonExtractionPresetRepository? _presetRepository;
        private List<ExtractionPreset> _presets = new();

        private bool _filterUpdating;           // suppresses ApplyTreeFilter during bulk select/clear
        private bool _paramTreeInitialized;     // has ItemsSource been set at least once?
        private bool _lastHasCurve = true;      // previous hasCurve value for diffing

        public event EventHandler<bool>? ExportEnabledChanged;
        public event EventHandler<int>?  StepChanged;
        public string ExportLabel => "Aplicar extracción";

        public int CurrentStep => (WizardTabs?.SelectedIndex ?? 0) + 1;
        public int StepCount   => 2;

        public void NextStep()
        {
            if (WizardTabs.SelectedIndex < StepCount - 1)
                WizardTabs.SelectedIndex++;
        }

        public ParameterExtractorPanel()
        {
            InitializeComponent();
            ParamGrid.ItemsSource = _paramRows;

            UnitsCombo.ItemsSource    = Enum.GetValues(typeof(ExtractionLengthUnits));
            UnitsCombo.SelectedItem   = ExtractionLengthUnits.Meters;
            DecimalsCombo.ItemsSource = new[] { 0, 1, 2, 3, 4, 5, 6 };
            DecimalsCombo.SelectedItem = 3;

            WizardTabs.SelectionChanged += (_, _) => StepChanged?.Invoke(this, CurrentStep);
        }

        public void Initialize(
            int selectedElementCount,
            Func<ExtractionConfig, bool>? applyCallback = null,
            JsonExtractionPresetRepository? presetRepository = null,
            IReadOnlyList<string>? availableCategories = null,
            IReadOnlyDictionary<string, IReadOnlyList<string>>? paramsByCategory = null,
            IReadOnlyDictionary<string, bool>? hasCurveByCategory = null,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>? familyTypesByCategory = null)
        {
            // Always start at step 1 (in case the panel is re-initialized or the TabControl
            // was left on step 2 from a previous session).
            WizardTabs.SelectedIndex = 0;

            _applyCallback      = applyCallback;
            _presetRepository   = presetRepository;
            _hasCurveByCategory = hasCurveByCategory ?? new Dictionary<string, bool>();

            // Suppress filter-change callbacks while we're setting up both trees
            _filterUpdating = true;

            // Build filter tree (Category → Family → Type)
            _filterRoots = BuildFilterRoots(familyTypesByCategory ?? new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>());
            FilterTree.ItemsSource = _filterRoots;

            // Param tree — all nodes start unchecked and collapsed
            _allTreeRoots = BuildTree();

            _filterUpdating = false;

            // First apply pass now that everything is ready
            ApplyTreeFilter();

            LoadPresets();
            UpdateScopeText();
            RaiseEnabled();
        }

        // ── Filter tree builders ──────────────────────────────────────────────

        private static List<FilterTreeNode> BuildFilterRoots(
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> familyTypesByCategory)
        {
            var roots = new List<FilterTreeNode>();
            foreach (var (catName, famDict) in familyTypesByCategory.OrderBy(kv => kv.Key))
            {
                var catNode = new FilterTreeNode { Label = catName, Level = FilterNodeLevel.Category };
                foreach (var (famName, types) in famDict.OrderBy(kv => kv.Key))
                {
                    var famNode = new FilterTreeNode { Label = famName, Level = FilterNodeLevel.Family };
                    foreach (var typeName in types.OrderBy(t => t))
                        famNode.AddChild(new FilterTreeNode { Label = typeName, Level = FilterNodeLevel.Type });
                    catNode.AddChild(famNode);
                }
                roots.Add(catNode);
            }
            return roots;
        }

        /// <summary>
        /// Filters the param tree based on which categories are active in the filter tree.
        /// Start/End groups are only shown when the active categories contain curve elements.
        ///
        /// IMPORTANT: Only reassigns ParamTree.ItemsSource when the visible set actually
        /// changes (i.e. hasCurve toggles Inicio/Fin in or out).  Reassigning the same
        /// list causes WPF to destroy and recreate TreeViewItem containers, which fires
        /// CheckBox.Unchecked via the TwoWay binding and silently clears user selections.
        /// </summary>
        private void ApplyTreeFilter()
        {
            if (_filterUpdating) return;

            bool anyFiltered = _filterRoots.Any(c => !c.IsChecked);
            bool hasCurve = !anyFiltered || _filterRoots
                .Where(c => c.HasAnyChecked())
                .Any(c => _hasCurveByCategory.TryGetValue(c.Label, out var hc) && hc);

            // Only rebuild + reassign if this is the first call, or if the Inicio/Fin
            // visibility has genuinely toggled. Avoids destroying user selections.
            if (!_paramTreeInitialized || hasCurve != _lastHasCurve)
            {
                _lastHasCurve       = hasCurve;
                _paramTreeInitialized = true;

                _treeRoots = _allTreeRoots.Where(r =>
                {
                    if (r.Label.StartsWith("Inicio") || r.Label.StartsWith("Fin"))
                        return hasCurve;
                    return true;
                }).ToList();

                ParamTree.ItemsSource = _treeRoots;
            }

            RaiseEnabled();
        }

        public void TriggerExport()
        {
            RebuildParamRows();
            var config = BuildConfig();

            var ownerWin = Window.GetWindow(this);

            if (config.Rules.Count == 0)
            {
                BimPillsDialog.Warning(
                    "Extractor de Parámetros",
                    "No hay parámetros seleccionados.",
                    detail: "Vuelve al Paso 1 y marca al menos un parámetro antes de aplicar.",
                    owner: ownerWin);
                return;
            }

            if (_applyCallback == null)
            {
                BimPillsDialog.Info(
                    "Extractor de Parámetros",
                    $"Se aplicarían {config.Rules.Count} reglas a los elementos.",
                    detail: $"Unidades: {config.LengthUnits} · Decimales: {config.Decimals}",
                    owner: ownerWin);
                return;
            }

            _applyCallback(config);
        }

        // ── Tab switching ────────────────────────────────────────────────────

        private void WizardTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // IMPORTANT: SelectionChanged is a bubbling event. Child Selectors (DataGrid,
            // ComboBox) fire their own SelectionChanged which bubbles up to WizardTabs.
            // Without this guard, DataGrid row selection after RebuildParamRows() would
            // trigger RebuildParamRows() again → infinite loop → empty grid + blinking cursor.
            if (!ReferenceEquals(e.OriginalSource, WizardTabs)) return;

            if (WizardTabs.SelectedIndex == 1) RebuildParamRows();
        }

        // ── Tree ─────────────────────────────────────────────────────────────

        private static List<ParamTreeNode> BuildTree()
        {
            var roots = new List<ParamTreeNode>();

            // Geographic — first (BIM Pills premium feature, most requested)
            var geo = Grp("Coordenadas Geográficas  ★ BIM Pills");
            geo.AddChild(Leaf("Latitud (decimal)",  ExtractionSourceKind.Latitude,  CoordinateOrigin.Survey, GeoFormat.Decimal, ExtractionDataType.Number, "BP_Latitud"));
            geo.AddChild(Leaf("Latitud (GMS)",      ExtractionSourceKind.Latitude,  CoordinateOrigin.Survey, GeoFormat.Dms,     ExtractionDataType.Text,   "BP_Latitud_GMS"));
            geo.AddChild(Leaf("Longitud (decimal)", ExtractionSourceKind.Longitude, CoordinateOrigin.Survey, GeoFormat.Decimal, ExtractionDataType.Number, "BP_Longitud"));
            geo.AddChild(Leaf("Longitud (GMS)",     ExtractionSourceKind.Longitude, CoordinateOrigin.Survey, GeoFormat.Dms,     ExtractionDataType.Text,   "BP_Longitud_GMS"));
            roots.Add(geo);

            // Location
            var loc = Grp("Ubicación  (punto / centroide)");
            foreach (var (lbl, origin, abbr) in CoordOrigins())
                loc.AddChild(AxisGroup(lbl, origin,
                    ExtractionSourceKind.LocationX, ExtractionSourceKind.LocationY, ExtractionSourceKind.LocationZ,
                    "BP_X", "BP_Y", "BP_Z", abbr));
            roots.Add(loc);

            // Start
            var st = Grp("Inicio  (muros, tuberías, vigas…)");
            foreach (var (lbl, origin, abbr) in CoordOrigins())
                st.AddChild(AxisGroup(lbl, origin,
                    ExtractionSourceKind.StartX, ExtractionSourceKind.StartY, ExtractionSourceKind.StartZ,
                    "BP_Ini_X", "BP_Ini_Y", "BP_Ini_Z", abbr));
            roots.Add(st);

            // End
            var en = Grp("Fin  (muros, tuberías, vigas…)");
            foreach (var (lbl, origin, abbr) in CoordOrigins())
                en.AddChild(AxisGroup(lbl, origin,
                    ExtractionSourceKind.EndX, ExtractionSourceKind.EndY, ExtractionSourceKind.EndZ,
                    "BP_Fin_X", "BP_Fin_Y", "BP_Fin_Z", abbr));
            roots.Add(en);

            // Element properties
            var props = Grp("Datos del elemento");
            props.AddChild(Leaf("Categoría",  ExtractionSourceKind.Category,   paramName: "BP_Categoria"));
            props.AddChild(Leaf("Familia",    ExtractionSourceKind.FamilyName, paramName: "BP_Familia"));
            props.AddChild(Leaf("Tipo",       ExtractionSourceKind.TypeName,   paramName: "BP_Tipo"));
            props.AddChild(Leaf("Nivel",      ExtractionSourceKind.LevelName,  paramName: "BP_Nivel"));
            props.AddChild(Leaf("Element ID", ExtractionSourceKind.ElementId,  paramName: "BP_ElementId"));
            props.AddChild(Leaf("Unique ID",  ExtractionSourceKind.UniqueId,   paramName: "BP_UniqueId"));
            roots.Add(props);

            return roots;
        }

        private static (string lbl, CoordinateOrigin origin, string abbr)[] CoordOrigins() => new[]
        {
            ("Origen Interno (Internal)",        CoordinateOrigin.Internal,    "Int"),
            ("Punto Base Proyecto (PBP)",        CoordinateOrigin.ProjectBase, "PBP"),
            ("Punto de Levantamiento (Survey)",  CoordinateOrigin.Survey,      "Survey"),
        };

        private static ParamTreeNode Grp(string label) => new() { Label = label };

        private static ParamTreeNode AxisGroup(
            string originLabel, CoordinateOrigin origin,
            ExtractionSourceKind sx, ExtractionSourceKind sy, ExtractionSourceKind sz,
            string px, string py, string pz, string abbr)
        {
            var g = new ParamTreeNode { Label = originLabel };
            g.AddChild(CoordLeaf("X", sx, origin, $"{px}_{abbr}"));
            g.AddChild(CoordLeaf("Y", sy, origin, $"{py}_{abbr}"));
            g.AddChild(CoordLeaf("Z", sz, origin, $"{pz}_{abbr}"));
            return g;
        }

        private static ParamTreeNode CoordLeaf(string axis, ExtractionSourceKind src, CoordinateOrigin origin, string paramName) =>
            new() { Label = axis, Source = src, Origin = origin, GeoFmt = GeoFormat.Decimal, DataType = ExtractionDataType.Length, DefaultParamName = paramName };

        private static ParamTreeNode Leaf(string label, ExtractionSourceKind src,
            CoordinateOrigin origin = CoordinateOrigin.Internal,
            GeoFormat geoFmt = GeoFormat.Decimal,
            ExtractionDataType dataType = ExtractionDataType.Text,
            string paramName = "") =>
            new() { Label = label, Source = src, Origin = origin, GeoFmt = geoFmt, DataType = dataType, DefaultParamName = paramName };

        // ── Tree events ───────────────────────────────────────────────────────

        private void TreeNode_CheckChanged(object sender, RoutedEventArgs e)
        {
            e.Handled = true; // prevent bubbling through parent tree items
            RaiseEnabled();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var root in _treeRoots) root.IsChecked = true;
            RaiseEnabled();
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var root in _treeRoots) root.IsChecked = false;
            RaiseEnabled();
        }

        // ── Filter tree events ────────────────────────────────────────────────

        private void FilterNode_CheckChanged(object sender, RoutedEventArgs e)
        {
            e.Handled = true; // stop bubbling through parent TreeViewItems
            if (!_filterUpdating) ApplyTreeFilter();
        }

        private void SelectAllFilter_Click(object sender, RoutedEventArgs e)
        {
            _filterUpdating = true;
            foreach (var root in _filterRoots) root.SetChecked(true, true, false);
            _filterUpdating = false;
            ApplyTreeFilter();
        }

        private void ClearAllFilter_Click(object sender, RoutedEventArgs e)
        {
            _filterUpdating = true;
            foreach (var root in _filterRoots) root.SetChecked(false, true, false);
            _filterUpdating = false;
            ApplyTreeFilter();
        }

        // ── Geo conversion method handler ─────────────────────────────────────

        private void GeoMethod_Changed(object sender, RoutedEventArgs e)
        {
            if (UtmSettingsPanel == null) return;
            UtmSettingsPanel.Visibility = GeoMethodUtm?.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Scope radio handlers ──────────────────────────────────────────────

        private void ScopeModel_Checked(object sender, RoutedEventArgs e)
        {
            _scope = ExtractionScope.WholeModel;
            UpdateScopeText();
        }

        private void ScopeView_Checked(object sender, RoutedEventArgs e)
        {
            _scope = ExtractionScope.ActiveView;
            UpdateScopeText();
        }

        private void UpdateScopeText()
        {
            if (SelectionInfoText == null) return;
            SelectionInfoText.Text = _scope == ExtractionScope.WholeModel
                ? "Se procesarán todos los elementos del modelo"
                : "Se procesarán los elementos visibles en la vista activa";
        }

        // ── Tab-2 row building ────────────────────────────────────────────────

        private void RebuildParamRows()
        {
            // Defensive: if tree was never initialized (e.g. rare timing edge), do it now
            if (!_paramTreeInitialized && _allTreeRoots.Count > 0)
                ApplyTreeFilter();

            foreach (var row in _paramRows)
                if (row.ParamName != row.DefaultParamName)
                    _customNames[(row.Source, row.Origin, row.GeoFmt)] = row.ParamName;

            _paramRows.Clear();

            foreach (var leaf in CheckedLeaves())
            {
                var key  = (leaf.Source!.Value, leaf.Origin, leaf.GeoFmt);
                var name = _customNames.TryGetValue(key, out var n) ? n : leaf.DefaultParamName;

                _paramRows.Add(new ParamRow
                {
                    ParamName        = name,
                    DefaultParamName = leaf.DefaultParamName,
                    SourceLabel      = BuildSourceLabel(leaf),
                    DataType         = leaf.DataType,
                    Source           = leaf.Source.Value,
                    Origin           = leaf.Origin,
                    GeoFmt           = leaf.GeoFmt,
                });
            }

            bool hasRows = _paramRows.Count > 0;
            EmptyLabel.Visibility = hasRows ? Visibility.Collapsed : Visibility.Visible;
            ParamGrid.Visibility  = hasRows ? Visibility.Visible   : Visibility.Collapsed;

            // Show geo panel only when there are Lat/Lon rows
            bool hasGeoRows = hasRows && _paramRows.Any(r =>
                r.Source == ExtractionSourceKind.Latitude ||
                r.Source == ExtractionSourceKind.Longitude);
            if (GeoSettingsPanel != null)
                GeoSettingsPanel.Visibility = hasGeoRows ? Visibility.Visible : Visibility.Collapsed;

            // Update "Categorías afectadas" summary
            if (ScopeCategoriesText != null)
            {
                var activeCats = _filterRoots.Where(c => c.HasAnyChecked()).Select(c => c.Label).ToList();
                ScopeCategoriesText.Text = activeCats.Count == 0 || activeCats.Count == _filterRoots.Count
                    ? "Todas"
                    : string.Join(", ", activeCats);
            }
        }

        private static string BuildSourceLabel(ParamTreeNode leaf)
        {
            string src = leaf.Source switch
            {
                ExtractionSourceKind.LocationX  => "Ubicación X",
                ExtractionSourceKind.LocationY  => "Ubicación Y",
                ExtractionSourceKind.LocationZ  => "Ubicación Z",
                ExtractionSourceKind.StartX     => "Inicio X",
                ExtractionSourceKind.StartY     => "Inicio Y",
                ExtractionSourceKind.StartZ     => "Inicio Z",
                ExtractionSourceKind.EndX       => "Fin X",
                ExtractionSourceKind.EndY       => "Fin Y",
                ExtractionSourceKind.EndZ       => "Fin Z",
                ExtractionSourceKind.Latitude   => leaf.GeoFmt == GeoFormat.Dms ? "Latitud (GMS)"  : "Latitud",
                ExtractionSourceKind.Longitude  => leaf.GeoFmt == GeoFormat.Dms ? "Longitud (GMS)" : "Longitud",
                ExtractionSourceKind.Category   => "Categoría",
                ExtractionSourceKind.FamilyName => "Familia",
                ExtractionSourceKind.TypeName   => "Tipo",
                ExtractionSourceKind.LevelName  => "Nivel",
                ExtractionSourceKind.ElementId  => "Element ID",
                ExtractionSourceKind.UniqueId   => "Unique ID",
                _                               => leaf.Label
            };

            bool needsOrigin = leaf.Source is
                ExtractionSourceKind.LocationX or ExtractionSourceKind.LocationY or ExtractionSourceKind.LocationZ or
                ExtractionSourceKind.StartX    or ExtractionSourceKind.StartY    or ExtractionSourceKind.StartZ    or
                ExtractionSourceKind.EndX      or ExtractionSourceKind.EndY      or ExtractionSourceKind.EndZ;

            if (!needsOrigin) return src;

            return src + leaf.Origin switch
            {
                CoordinateOrigin.ProjectBase => " — PBP",
                CoordinateOrigin.Survey      => " — Survey",
                _                            => " — Internal"
            };
        }

        // ── Config building ───────────────────────────────────────────────────

        private ExtractionConfig BuildConfig()
        {
            var config = new ExtractionConfig
            {
                LengthUnits = UnitsCombo.SelectedItem is ExtractionLengthUnits u ? u : ExtractionLengthUnits.Meters,
                Decimals    = DecimalsCombo.SelectedItem is int d ? d : 3,
                Scope       = _scope,
            };

            // Build Category/Family/Type filters from the filter tree
            bool allCatsChecked = _filterRoots.All(c => c.IsChecked);
            if (!allCatsChecked)
            {
                var cats  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var fams  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var catNode in _filterRoots)
                {
                    if (!catNode.HasAnyChecked()) continue;
                    cats.Add(catNode.Label);

                    bool allFamsChecked = catNode.Children.Count == 0 || catNode.Children.All(f => f.IsChecked);
                    if (!allFamsChecked)
                    {
                        foreach (var famNode in catNode.Children)
                        {
                            if (!famNode.HasAnyChecked()) continue;
                            fams.Add(famNode.Label);

                            bool allTypesChecked = famNode.Children.Count == 0 || famNode.Children.All(t => t.IsChecked);
                            if (!allTypesChecked)
                                foreach (var typeNode in famNode.Children.Where(t => t.IsChecked))
                                    types.Add(typeNode.Label);
                        }
                    }
                }

                config.CategoryFilter = cats.ToList();
                config.FamilyFilter   = fams.ToList();
                config.TypeFilter     = types.ToList();
            }

            // Read geo conversion settings once (applied to all Lat/Lon rules)
            bool useUtm   = GeoMethodUtm?.IsChecked == true;
            int  utmZone  = int.TryParse(UtmZoneBox?.Text?.Trim(), out var z) && z >= 1 && z <= 60
                            ? z : 19;
            bool utmNorth = UtmHemiNorth?.IsChecked == true;

            foreach (var row in _paramRows)
            {
                if (string.IsNullOrWhiteSpace(row.ParamName)) continue;

                var rule = new ExtractionRule
                {
                    Source           = row.Source,
                    CoordinateOrigin = row.Origin,
                    GeoFormat        = row.GeoFmt,
                    Target = new ExtractionTarget
                    {
                        ParameterName   = row.ParamName.Trim(),
                        DataType        = row.DataType,
                        CreateIfMissing = true
                    }
                };

                // Wire geo conversion method + UTM params for geographic coordinate rows
                if (row.Source == ExtractionSourceKind.Latitude ||
                    row.Source == ExtractionSourceKind.Longitude)
                {
                    rule.GeoConversionMethod  = useUtm
                        ? GeoConversionMethod.UTM
                        : GeoConversionMethod.RevitProjectLocation;
                    rule.UtmZone              = utmZone;
                    rule.UtmIsNorthHemisphere = utmNorth;
                }

                config.Rules.Add(rule);
            }
            return config;
        }

        // ── Presets ───────────────────────────────────────────────────────────

        private void LoadPresets()
        {
            if (PresetCombo == null) return;

            _presets = _presetRepository?.GetAll() ?? new List<ExtractionPreset>();

            PresetCombo.SelectionChanged -= Preset_Changed;
            PresetCombo.Items.Clear();
            PresetCombo.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "(ninguno)", Tag = "" });
            foreach (var p in _presets)
                PresetCombo.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = p.Name, Tag = p.Id });
            PresetCombo.SelectedIndex = 0;
            PresetCombo.SelectionChanged += Preset_Changed;

            if (DeletePresetBtn != null) DeletePresetBtn.IsEnabled = false;
        }

        private void Preset_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.OriginalSource, PresetCombo)) return;
            var id = (PresetCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString();
            if (DeletePresetBtn != null) DeletePresetBtn.IsEnabled = !string.IsNullOrEmpty(id);
            if (string.IsNullOrEmpty(id)) return;
            var preset = _presets.Find(p => p.Id == id);
            if (preset != null) ApplyPreset(preset.Config);
        }

        private void SavePreset_Click(object sender, RoutedEventArgs e)
        {
            var name = PromptForPresetName();
            if (string.IsNullOrWhiteSpace(name)) return;

            // Ensure param rows are current so customNames are captured
            RebuildParamRows();
            var config = BuildConfig();

            var preset = new ExtractionPreset { Name = name, Config = config };
            if (_presetRepository != null)
                _presetRepository.Create(preset);
            else
                preset.Id = Guid.NewGuid().ToString();

            _presets.Add(preset);
            var item = new System.Windows.Controls.ComboBoxItem { Content = name, Tag = preset.Id };
            PresetCombo.Items.Add(item);
            PresetCombo.SelectedItem = item;
        }

        private void DeletePreset_Click(object sender, RoutedEventArgs e)
        {
            var id = (PresetCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString();
            if (string.IsNullOrEmpty(id)) return;

            bool confirmed = BimPillsDialog.Confirm(
                "Extractor de Parámetros",
                "¿Eliminar el perfil seleccionado?",
                owner: Window.GetWindow(this));
            if (!confirmed) return;

            _presetRepository?.Delete(id);
            _presets.RemoveAll(p => p.Id == id);

            var idx = PresetCombo.SelectedIndex;
            PresetCombo.Items.RemoveAt(idx);
            PresetCombo.SelectedIndex = 0;
        }

        private void ApplyPreset(ExtractionConfig config)
        {
            // Restore category filter tree
            _filterUpdating = true;
            if (config.CategoryFilter.Count == 0)
            {
                // No filter saved → check everything
                foreach (var root in _filterRoots)
                    root.SetChecked(true, true, false);
            }
            else
            {
                // Uncheck all, then re-check only saved categories
                foreach (var root in _filterRoots)
                    root.SetChecked(false, true, false);
                foreach (var root in _filterRoots)
                    if (config.CategoryFilter.Contains(root.Label, StringComparer.OrdinalIgnoreCase))
                        root.SetChecked(true, true, false);
            }
            _filterUpdating = false;

            // Reset all param tree selections
            foreach (var root in _allTreeRoots)
                root.IsChecked = false;

            _customNames.Clear();

            // Restore checked leaves from rules
            foreach (var rule in config.Rules)
            {
                var leaf = FindLeaf(rule.Source, rule.CoordinateOrigin, rule.GeoFormat);
                if (leaf == null) continue;
                leaf.IsChecked = true;
                if (!string.IsNullOrWhiteSpace(rule.Target?.ParameterName) &&
                    rule.Target.ParameterName != leaf.DefaultParamName)
                    _customNames[(rule.Source, rule.CoordinateOrigin, rule.GeoFormat)] = rule.Target.ParameterName;
            }

            // Restore units / decimals / scope
            if (UnitsCombo != null)    UnitsCombo.SelectedItem    = config.LengthUnits;
            if (DecimalsCombo != null) DecimalsCombo.SelectedItem = config.Decimals;

            if (config.Scope == ExtractionScope.ActiveView)
            {
                _scope = ExtractionScope.ActiveView;
                if (ScopeView != null) ScopeView.IsChecked = true;
            }
            else
            {
                _scope = ExtractionScope.WholeModel;
                if (ScopeModel != null) ScopeModel.IsChecked = true;
            }

            ApplyTreeFilter();
            RaiseEnabled();
        }

        private ParamTreeNode? FindLeaf(ExtractionSourceKind src, CoordinateOrigin origin, GeoFormat fmt)
            => _allTreeRoots
                .SelectMany(r => r.AllLeaves())
                .FirstOrDefault(l => l.Source == src && l.Origin == origin && l.GeoFmt == fmt);

        private string? PromptForPresetName()
        {
            var ownerWin = Window.GetWindow(this);
            var dlg = new Window
            {
                Title = "BIM Pills — Guardar perfil",
                Width = 340,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Owner = ownerWin
            };

            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock
            {
                Text = "Nombre del perfil:",
                FontSize = 12,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                Margin = new Thickness(0, 0, 0, 8)
            });
            var tb = new TextBox
            {
                FontSize = 12,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                Padding = new Thickness(6, 5, 6, 5),
                Margin = new Thickness(0, 0, 0, 16)
            };
            panel.Children.Add(tb);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnOk = new Button
            {
                Content = "Guardar", Width = 80, Height = 30,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 12,
                Margin = new Thickness(0, 0, 8, 0), IsDefault = true
            };
            var btnCancel = new Button
            {
                Content = "Cancelar", Width = 80, Height = 30,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 12,
                IsCancel = true
            };
            string? result = null;
            btnOk.Click     += (_, __) => { result = tb.Text.Trim(); dlg.DialogResult = true; };
            btnCancel.Click += (_, __) => { dlg.DialogResult = false; };
            btnRow.Children.Add(btnOk);
            btnRow.Children.Add(btnCancel);
            panel.Children.Add(btnRow);
            dlg.Content = panel;
            tb.Focus();
            dlg.ShowDialog();
            return result;
        }

        // ── Geo info button ───────────────────────────────────────────────────

        private void GeoInfo_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true; // prevent bubbling through TreeViewItem
            BimPillsDialog.Info(
                "Coordenadas Geográficas — BIM Pills",
                "Pobla parámetros con latitud y longitud calculadas a partir de la posición real de cada elemento en el modelo.",
                detail:
                    "Método de conversión\n" +
                    "• Revit ProjectLocation (recomendado): usa directamente la georeferenciación configurada en el proyecto " +
                    "(Gestionar → Ubicación → Sitio). Si el proyecto tiene un Survey Point vinculado a coordenadas reales, " +
                    "BIM Pills calcula la latitud y longitud de cada elemento automáticamente.\n\n" +
                    "• UTM manual: convierte coordenadas de cuadrícula UTM a latitud/longitud usando la zona y hemisferio " +
                    "que especifiques. Útil cuando el modelo no tiene ProjectLocation configurado pero conoces la zona UTM " +
                    "del proyecto (p.ej. zona 19S para Santiago de Chile).\n\n" +
                    "Formatos de salida\n" +
                    "• Decimal (ej. −33.4568): valor numérico directo, compatible con SIG, Excel y fórmulas.\n" +
                    "• GMS (ej. 33°27'24.5\"S): Grados, Minutos, Segundos — estándar cartográfico español (IGN, IGM).\n\n" +
                    "Parámetros afectados\n" +
                    "Los parámetros BP_Latitud, BP_Longitud y sus variantes GMS recibirán los valores calculados. " +
                    "En el panel de configuración (Paso 2) aparecen destacados con el fondo azul claro para " +
                    "identificarlos fácilmente junto con los ajustes de conversión.",
                owner: Window.GetWindow(this));
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private IEnumerable<ParamTreeNode> CheckedLeaves()
        {
            // Fallback to _allTreeRoots if _treeRoots is somehow empty
            var source = _treeRoots.Count > 0 ? _treeRoots : _allTreeRoots;
            return source.SelectMany(r => r.AllLeaves()).Where(l => l.IsChecked);
        }

        private void RaiseEnabled()
        {
            bool hasAny = _treeRoots.Any(r => r.AllLeaves().Any(l => l.IsChecked));
            ExportEnabledChanged?.Invoke(this, hasAny);
        }
    }
}
