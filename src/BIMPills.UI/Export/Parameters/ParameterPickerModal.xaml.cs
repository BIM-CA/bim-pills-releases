using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using BIMPills.Core.ParameterExtractor;

namespace BIMPills.UI.Export.Parameters
{
    public partial class ParameterPickerModal : Window
    {
        // ── Collections ───────────────────────────────────────────────────────

        private readonly ObservableCollection<PickerCategoryVM>   _categories   = new();
        private readonly ObservableCollection<PickerSourceItemVM> _coordSources = new();
        private readonly ObservableCollection<PickerSourceItemVM> _identSources = new();
        private readonly ObservableCollection<PickerSourceItemVM> _elementParams = new();

        private ICollectionView? _categoriesView;
        private ICollectionView? _coordView;
        private ICollectionView? _identView;
        private ICollectionView? _paramView;

        private IReadOnlyDictionary<string, IReadOnlyList<string>>? _paramsByCategory;

        // ── Constructor ──────────────────────────────────────────────────────

        public ParameterPickerModal()
        {
            InitializeComponent();
            BIMPills.UI.Shared.ThemeHelper.Apply(this);
        }

        // ── Static factory ───────────────────────────────────────────────────

        public static (IReadOnlyList<PickerSourceItemVM>? Items, ExtractionScope Scope) Show(
            Window? owner,
            IReadOnlyList<string>? categories = null,
            IReadOnlyDictionary<string, IReadOnlyList<string>>? paramsByCategory = null)
        {
            var dlg = new ParameterPickerModal { Owner = owner };
            dlg.Populate(categories, paramsByCategory);
            if (dlg.ShowDialog() == true)
                return (dlg.GetSelected(), dlg.SelectedScope);
            return (null, ExtractionScope.CurrentSelection);
        }

        // ── Scope ────────────────────────────────────────────────────────────

        private ExtractionScope SelectedScope =>
            ScopeModelRadio?.IsChecked     == true ? ExtractionScope.WholeModel :
            ScopeViewRadio?.IsChecked      == true ? ExtractionScope.ActiveView  :
                                                     ExtractionScope.CurrentSelection;

        // ── Data population ──────────────────────────────────────────────────

        private void Populate(
            IReadOnlyList<string>? categories,
            IReadOnlyDictionary<string, IReadOnlyList<string>>? paramsByCategory)
        {
            _paramsByCategory = paramsByCategory;

            // ── Categories ───────────────────────────────────────────────────
            var cats = categories?.Count > 0
                ? categories
                : new[]
                {
                    "Aparatos sanitarios", "Elementos de detalle", "Equipos eléctricos",
                    "Equipos especializados", "Habitaciones", "Mobiliario",
                    "Modelos genéricos", "Muebles de obra", "Muros",
                    "Puertas", "Suelos", "Techos", "Vínculos RVT"
                };

            foreach (var c in cats)
            {
                var vm = new PickerCategoryVM { Name = c, IsChecked = false };
                vm.PropertyChanged += Category_PropertyChanged;
                _categories.Add(vm);
            }

            _categoriesView = CollectionViewSource.GetDefaultView(_categories);
            _categoriesView.Filter = o =>
                o is PickerCategoryVM cat &&
                (HideUncheckedCheck?.IsChecked != true || cat.IsChecked) &&
                (string.IsNullOrEmpty(CategorySearch.Text) ||
                 cat.Name.IndexOf(CategorySearch.Text, StringComparison.OrdinalIgnoreCase) >= 0);

            CategoryList.ItemsSource = _categoriesView;

            // ── Coordinate sources ─────────────────────────────────────────
            AddCoord("X",             ExtractionSourceKind.LocationX, "BP_X",       ExtractionDataType.Length);
            AddCoord("Y",             ExtractionSourceKind.LocationY, "BP_Y",       ExtractionDataType.Length);
            AddCoord("Z (Elevación)", ExtractionSourceKind.LocationZ, "BP_Z",       ExtractionDataType.Length);
            AddCoord("Latitud",       ExtractionSourceKind.Latitude,  "BP_Latitud", ExtractionDataType.Number);
            AddCoord("Longitud",      ExtractionSourceKind.Longitude, "BP_Longitud",ExtractionDataType.Number);

            _coordView = CollectionViewSource.GetDefaultView(_coordSources);
            _coordView.Filter = HideUncheckedFilter;
            CoordList.ItemsSource = _coordView;

            // ── Identifier sources ─────────────────────────────────────────
            AddIdent("Categoría",  ExtractionSourceKind.Category,   "BP_Categoria");
            AddIdent("Familia",    ExtractionSourceKind.FamilyName,  "BP_Familia");
            AddIdent("Tipo",       ExtractionSourceKind.TypeName,    "BP_Tipo");
            AddIdent("Nivel",      ExtractionSourceKind.LevelName,   "BP_Nivel");
            AddIdent("ElementId",  ExtractionSourceKind.ElementId,   "BP_ElementId");
            AddIdent("UniqueId",   ExtractionSourceKind.UniqueId,    "BP_UniqueId");

            _identView = CollectionViewSource.GetDefaultView(_identSources);
            _identView.Filter = HideUncheckedFilter;
            IdentList.ItemsSource = _identSources;

            // ── Element parameters ─────────────────────────────────────────
            var allParams = paramsByCategory != null
                ? paramsByCategory.Values.SelectMany(v => v).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p)
                : Enumerable.Empty<string>();

            foreach (var p in allParams)
            {
                var vm = new PickerSourceItemVM
                {
                    DisplayName       = p,
                    Source            = ExtractionSourceKind.ElementProperty,
                    DefaultTargetName = $"BP_{p}",
                    DefaultDataType   = ExtractionDataType.Text,
                };
                vm.PropertyChanged += Item_PropertyChanged;
                _elementParams.Add(vm);
            }

            if (_elementParams.Count > 0)
                ElementParamsEmpty.Visibility = Visibility.Collapsed;

            _paramView = CollectionViewSource.GetDefaultView(_elementParams);
            _paramView.Filter = ParamFilter;
            ElementParamList.ItemsSource = _paramView;

            UpdateSelectionCount();
            UpdateParamCount();
        }

        // ── Source item builders ──────────────────────────────────────────────

        private void AddCoord(string label, ExtractionSourceKind kind, string target, ExtractionDataType dtype)
        {
            var vm = new PickerSourceItemVM
            {
                DisplayName       = label,
                Source            = kind,
                DefaultTargetName = target,
                DefaultDataType   = dtype,
            };
            vm.PropertyChanged += Item_PropertyChanged;
            _coordSources.Add(vm);
        }

        private void AddIdent(string label, ExtractionSourceKind kind, string target)
        {
            var vm = new PickerSourceItemVM
            {
                DisplayName       = label,
                Source            = kind,
                DefaultTargetName = target,
                DefaultDataType   = ExtractionDataType.Text,
            };
            vm.PropertyChanged += Item_PropertyChanged;
            _identSources.Add(vm);
        }

        // ── Filters ──────────────────────────────────────────────────────────

        private bool HideUncheckedFilter(object o) =>
            HideUncheckedCheck?.IsChecked != true ||
            (o is PickerSourceItemVM vm && vm.IsChecked);

        private bool ParamFilter(object o)
        {
            if (o is not PickerSourceItemVM vm) return false;

            if (HideUncheckedCheck?.IsChecked == true && !vm.IsChecked) return false;

            if (!string.IsNullOrEmpty(ParamSearch?.Text) &&
                vm.DisplayName.IndexOf(ParamSearch!.Text, StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            if (_paramsByCategory == null) return true;

            var checkedCats = _categories.Where(c => c.IsChecked).Select(c => c.Name).ToList();
            if (checkedCats.Count == 0) return true;

            foreach (var cat in checkedCats)
            {
                if (_paramsByCategory.TryGetValue(cat, out var catParams) &&
                    catParams.Contains(vm.DisplayName, StringComparer.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        // ── Events ───────────────────────────────────────────────────────────

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PickerSourceItemVM.IsChecked))
            {
                UpdateSelectionCount();
                if (HideUncheckedCheck?.IsChecked == true)
                    RefreshSourceViews();
            }
        }

        private void Category_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PickerCategoryVM.IsChecked))
            {
                _paramView?.Refresh();
                UpdateParamCount();
                if (HideUncheckedCheck?.IsChecked == true)
                    _categoriesView?.Refresh();
            }
        }

        private void CategorySearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _categoriesView?.Refresh();
        }

        private void ParamSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _paramView?.Refresh();
            UpdateParamCount();
        }

        private void AllCategories_Click(object sender, RoutedEventArgs e)
        {
            bool check = AllCategoriesCheck.IsChecked == true;
            foreach (var cat in _categories) cat.IsChecked = check;
            _paramView?.Refresh();
            UpdateParamCount();
        }

        private void Category_CheckChanged(object sender, RoutedEventArgs e)
        {
            int total    = _categories.Count;
            int checked_ = _categories.Count(c => c.IsChecked);
            AllCategoriesCheck.IsChecked =
                checked_ == total ? (bool?)true  :
                checked_ == 0     ? (bool?)false  :
                                    (bool?)null;
        }

        private void HideUnchecked_Changed(object sender, RoutedEventArgs e)
        {
            _categoriesView?.Refresh();
            RefreshSourceViews();
        }

        private void RefreshSourceViews()
        {
            _coordView?.Refresh();
            _identView?.Refresh();
            _paramView?.Refresh();
            UpdateParamCount();
        }

        private void UpdateSelectionCount()
        {
            int n = AllSources().Count(s => s.IsChecked);
            SelectionCountText.Text = n == 0 ? "0 fuentes seleccionadas"
                : n == 1 ? "1 fuente seleccionada"
                : $"{n} fuentes seleccionadas";
            ConfirmButton.IsEnabled = n > 0;
        }

        private void UpdateParamCount()
        {
            if (ParamCountText == null) return;
            int visible = _paramView?.Cast<object>().Count() ?? _elementParams.Count;
            ParamCountText.Text = visible == 0 ? "" : $"{visible} parámetros";
        }

        private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
        private void Cancel_Click(object sender, RoutedEventArgs e)  => DialogResult = false;

        // ── Result ───────────────────────────────────────────────────────────

        private IEnumerable<PickerSourceItemVM> AllSources() =>
            _coordSources.Concat(_identSources).Concat(_elementParams);

        private IReadOnlyList<PickerSourceItemVM> GetSelected() =>
            AllSources().Where(s => s.IsChecked).ToList();
    }
}
