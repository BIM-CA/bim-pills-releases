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

        private readonly ObservableCollection<PickerCategoryVM>    _categories    = new();
        private readonly ObservableCollection<PickerSourceItemVM>  _coordSources  = new();
        private readonly ObservableCollection<PickerSourceItemVM>  _identSources  = new();
        private readonly ObservableCollection<PickerSourceItemVM>  _elementParams = new();

        private ICollectionView? _categoriesView;

        // ── Constructor ──────────────────────────────────────────────────────

        public ParameterPickerModal()
        {
            InitializeComponent();
            BIMPills.UI.Shared.ThemeHelper.Apply(this);
        }

        // ── Static factory ───────────────────────────────────────────────────

        /// <summary>
        /// Abre el modal y devuelve la lista de fuentes seleccionadas, o null si se canceló.
        /// </summary>
        public static IReadOnlyList<PickerSourceItemVM>? Show(
            Window? owner,
            IReadOnlyList<string>? categories       = null,
            IReadOnlyList<string>? elementParameters = null)
        {
            var dlg = new ParameterPickerModal { Owner = owner };
            dlg.Populate(categories, elementParameters);
            return dlg.ShowDialog() == true ? dlg.GetSelected() : null;
        }

        // ── Data population ──────────────────────────────────────────────────

        private void Populate(
            IReadOnlyList<string>? categories,
            IReadOnlyList<string>? elementParameters)
        {
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
                vm.PropertyChanged += Item_PropertyChanged;
                _categories.Add(vm);
            }

            _categoriesView = CollectionViewSource.GetDefaultView(_categories);
            _categoriesView.Filter = o =>
                o is PickerCategoryVM cat &&
                (string.IsNullOrEmpty(CategorySearch.Text) ||
                 cat.Name.IndexOf(CategorySearch.Text, StringComparison.OrdinalIgnoreCase) >= 0);

            CategoryList.ItemsSource = _categoriesView;

            // ── Coordinate sources ────────────────────────────────────────────
            AddCoord("X",            ExtractionSourceKind.LocationX, "BP_X",        ExtractionDataType.Length, "BP_X_Conv",   CoordinateOrigin.ProjectBase);
            AddCoord("Y",            ExtractionSourceKind.LocationY, "BP_Y",        ExtractionDataType.Length, "BP_Y_Conv",   CoordinateOrigin.ProjectBase);
            AddCoord("Z (Elevación)",ExtractionSourceKind.LocationZ, "BP_Z",        ExtractionDataType.Length, "BP_Z_Conv",   CoordinateOrigin.ProjectBase);
            AddGeo("Latitud",  ExtractionSourceKind.Latitude,  "BP_Latitud",  ExtractionDataType.Number, "BP_Latitud_DMS");
            AddGeo("Longitud", ExtractionSourceKind.Longitude, "BP_Longitud", ExtractionDataType.Number, "BP_Longitud_DMS");

            CoordList.ItemsSource = _coordSources;

            // ── Identifier sources ────────────────────────────────────────────
            AddIdent("Categoría",  ExtractionSourceKind.Category,   "BP_Categoria");
            AddIdent("Familia",    ExtractionSourceKind.FamilyName,  "BP_Familia");
            AddIdent("Tipo",       ExtractionSourceKind.TypeName,    "BP_Tipo");
            AddIdent("Nivel",      ExtractionSourceKind.LevelName,   "BP_Nivel");
            AddIdent("ElementId",  ExtractionSourceKind.ElementId,   "BP_ElementId");
            AddIdent("UniqueId",   ExtractionSourceKind.UniqueId,    "BP_UniqueId");

            IdentList.ItemsSource = _identSources;

            // ── Element parameters (from Revit) ───────────────────────────────
            if (elementParameters?.Count > 0)
            {
                foreach (var p in elementParameters)
                {
                    var vm = new PickerSourceItemVM
                    {
                        DisplayName     = p,
                        Source          = ExtractionSourceKind.ElementProperty,
                        DefaultTargetName = $"BP_{p}",
                        DefaultDataType = ExtractionDataType.Text,
                    };
                    vm.PropertyChanged += Item_PropertyChanged;
                    _elementParams.Add(vm);
                }
                ElementParamsEmpty.Visibility = Visibility.Collapsed;
            }

            ElementParamList.ItemsSource = _elementParams;
            UpdateSelectionCount();
        }

        // ── Helpers to build source items ────────────────────────────────────

        private void AddCoord(
            string label, ExtractionSourceKind kind,
            string primary, ExtractionDataType dtype,
            string secondary, CoordinateOrigin secOrigin)
        {
            var vm = new PickerSourceItemVM
            {
                DisplayName        = label,
                Source             = kind,
                DefaultTargetName  = primary,
                DefaultDataType    = dtype,
                ShowCoordinateOrigin = true,
                SupportsDual       = true,
                SecondaryTargetName = secondary,
                SecondaryOrigin    = secOrigin,
            };
            vm.PropertyChanged += Item_PropertyChanged;
            _coordSources.Add(vm);
        }

        private void AddGeo(
            string label, ExtractionSourceKind kind,
            string primary, ExtractionDataType dtype,
            string secondary)
        {
            var vm = new PickerSourceItemVM
            {
                DisplayName         = label,
                Source              = kind,
                DefaultTargetName   = primary,
                DefaultDataType     = dtype,
                ShowGeoFormat       = true,
                SupportsDual        = true,
                SecondaryTargetName = secondary,
                SecondaryGeoFormat  = GeoFormat.Dms,
                SecondaryDataType   = ExtractionDataType.Text,
                CoordinateOrigin    = CoordinateOrigin.Survey,
                SecondaryOrigin     = CoordinateOrigin.Survey,
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

        // ── Events ───────────────────────────────────────────────────────────

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PickerSourceItemVM.IsChecked))
                UpdateSelectionCount();
        }

        private void CategorySearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _categoriesView?.Refresh();
        }

        private void AllCategories_Click(object sender, RoutedEventArgs e)
        {
            bool check = AllCategoriesCheck.IsChecked == true;
            foreach (var cat in _categories)
                cat.IsChecked = check;
        }

        private void Category_CheckChanged(object sender, RoutedEventArgs e)
        {
            // Sync "Todas" tri-state
            int total   = _categories.Count;
            int checked_ = _categories.Count(c => c.IsChecked);
            AllCategoriesCheck.IsChecked =
                checked_ == total  ? (bool?)true  :
                checked_ == 0      ? (bool?)false  :
                                     (bool?)null;    // indeterminate
        }

        private void UpdateSelectionCount()
        {
            int n = AllSources().Count(s => s.IsChecked);
            SelectionCountText.Text = n == 0
                ? "0 fuentes seleccionadas"
                : n == 1 ? "1 fuente seleccionada"
                : $"{n} fuentes seleccionadas";
            ConfirmButton.IsEnabled = n > 0;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        // ── Result ───────────────────────────────────────────────────────────

        private IEnumerable<PickerSourceItemVM> AllSources() =>
            _coordSources.Concat(_identSources).Concat(_elementParams);

        private IReadOnlyList<PickerSourceItemVM> GetSelected() =>
            AllSources().Where(s => s.IsChecked).ToList();
    }
}
