using BIMPills.Core.Seleccionar;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Xml.Linq;

namespace BIMPills.UI.Seleccionar
{
    public partial class FindSelectModal : Window
    {
        private readonly IReadOnlyList<string>                              _categories;
        private readonly IReadOnlyList<ParamInfo>                           _allParamInfos;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<ParamInfo>> _paramsByCategory;
        private readonly IFilterPresetRepository                            _presetRepo;
        private readonly ObservableCollection<CriterionRow>                 _criteria = new();
        private bool _refreshingPresets = false;

        /// <summary>Último filtro aplicado (solo para interop legacy ShowDialog).</summary>
        public SelectionFilterConfig? ResultFilter { get; private set; }

        /// <summary>Disparado cuando el usuario aplica (+/-) un filtro. No cierra la ventana.</summary>
        public event Action<SelectionFilterConfig>? OnApplyFilter;

        public FindSelectModal(
            IReadOnlyList<string>                                   categories,
            IReadOnlyList<ParamInfo>                                allParamInfos,
            IReadOnlyDictionary<string, IReadOnlyList<ParamInfo>>   paramsByCategory,
            IFilterPresetRepository                                 presetRepo,
            int                                                     selectedCount = 0,
            int                                                     editableCount = 0)
        {
            InitializeComponent();

            _categories        = categories;
            _allParamInfos     = allParamInfos;
            _paramsByCategory  = paramsByCategory;
            _presetRepo        = presetRepo;

            CriteriaList.ItemsSource = _criteria;
            _criteria.CollectionChanged += (_, __) =>
            {
                RefreshEmptyState();
                RefreshParamItems();
            };

            SelectedCountLabel.Text = selectedCount.ToString();
            EditableCountLabel.Text = editableCount.ToString();

            RefreshPresets();
            RefreshEmptyState();
        }

        /// <summary>
        /// Actualiza el contador de seleccionados tras aplicar un filtro.
        /// Llamado desde SeleccionarGalleryWindow cuando llega el resultado.
        /// </summary>
        public void UpdateSelectionCount(int count)
        {
            SelectedCountLabel.Text = count.ToString();
            EditableCountLabel.Text = count.ToString();
        }

        // ── Empty state ───────────────────────────────────────────────

        private void RefreshEmptyState()
        {
            EmptyPlaceholder.Visibility = _criteria.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // ── Dynamic params based on selected categories ───────────────

        /// <summary>
        /// Re-calcula los parámetros disponibles en función de las categorías
        /// seleccionadas y actualiza los CriterioParamItems de todas las filas de parámetro.
        /// Cada fila aplica su propio filtro de instancia/tipo internamente.
        /// </summary>
        private void RefreshParamItems()
        {
            var availableParamInfos = GetCurrentAvailableParamInfos();
            foreach (var row in _criteria.Where(r => !r.IsCategory))
                row.SetCriterioParamItems(availableParamInfos);  // row applies its own typeFilter
        }

        // ── Preset management ─────────────────────────────────────────

        private void RefreshPresets()
        {
            _refreshingPresets = true;
            try
            {
                PresetCombo.Items.Clear();
                foreach (var p in _presetRepo.LoadAll())
                    PresetCombo.Items.Add(p);
            }
            finally
            {
                _refreshingPresets = false;
            }
        }

        private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshingPresets) return;
            if (PresetCombo.SelectedItem is not FilterPreset preset) return;

            _criteria.Clear();

            // Multi-category support (new) + legacy single-category fallback
            var cats = preset.Filter.EffectiveCategoryNames;
            foreach (var cat in cats)
                AddCriterionRow("Categoría", cat, isParam: false);

            foreach (var cond in preset.Filter.Conditions)
            {
                // Restaurar typeOnly desde la condición guardada (null = mostrar todos)
                bool? typeOnly = cond.IsTypeParam.HasValue
                    ? cond.IsTypeParam
                    : (bool?)null;
                AddCriterionRow(cond.ParameterName, cond.Value, cond.Operator, isParam: true, typeOnly: typeOnly);
            }

            AndRadio.IsChecked = preset.Filter.Logic == FilterLogic.And;
            OrRadio.IsChecked  = preset.Filter.Logic == FilterLogic.Or;
        }

        private void PresetMenuBtn_Click(object sender, RoutedEventArgs e)
        {
            var allPresets = _presetRepo.LoadAll();  // leer una sola vez
            var menu = new ContextMenu();

            var saveItem = new MenuItem { Header = "Guardar como nuevo conjunto…" };
            saveItem.Click += (_, __) => SavePreset();

            var exportActiveItem = new MenuItem { Header = "Exportar conjunto activo a XML…" };
            exportActiveItem.IsEnabled = PresetCombo.SelectedItem != null;
            exportActiveItem.Click += (_, __) => ExportPresets(activeOnly: true);

            var exportAllItem = new MenuItem { Header = "Exportar todos los conjuntos a XML…" };
            exportAllItem.IsEnabled = allPresets.Count > 0;
            exportAllItem.Click += (_, __) => ExportPresets(activeOnly: false);

            var importItem = new MenuItem { Header = "Importar conjuntos desde XML…" };
            importItem.Click += (_, __) => ImportPresets();

            var deleteItem = new MenuItem { Header = "Eliminar conjunto seleccionado" };
            deleteItem.IsEnabled = PresetCombo.SelectedItem != null;
            deleteItem.Click += (_, __) => DeletePreset();

            menu.Items.Add(saveItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(exportActiveItem);
            menu.Items.Add(exportAllItem);
            menu.Items.Add(importItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(deleteItem);
            menu.PlacementTarget = PresetMenuBtn;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private void SavePreset()
        {
            var filter = BuildFilter(SelectionAction.Replace);
            if (filter == null) return;

            var name = BIMPills.UI.Shared.BimPillsDialog.Prompt(
                "Nombre del conjunto de criterios:", "Guardar conjunto", "Nuevo conjunto", this);
            if (string.IsNullOrWhiteSpace(name)) return;

            var trimmedName = name!.Trim();
            _presetRepo.Save(new FilterPreset { Name = trimmedName, Filter = filter });
            RefreshPresets();

            // Seleccionar el preset guardado en el combo de forma SILENCIOSA:
            // los criterios ya muestran exactamente lo que se guardó, no hace falta
            // recargarlos. Si no suprimimos SelectionChanged aquí, el Clear+reload
            // introduce filas de categoría duplicadas por el ciclo de reentrada.
            _refreshingPresets = true;
            try
            {
                for (int i = 0; i < PresetCombo.Items.Count; i++)
                {
                    if (PresetCombo.Items[i] is FilterPreset fp &&
                        string.Equals(fp.Name.Trim(), trimmedName, StringComparison.OrdinalIgnoreCase))
                    {
                        PresetCombo.SelectedIndex = i;
                        break;
                    }
                }
            }
            finally
            {
                _refreshingPresets = false;
            }
        }

        private void DeletePreset()
        {
            if (PresetCombo.SelectedItem is not FilterPreset preset) return;
            _presetRepo.Delete(preset.Id);
            RefreshPresets();
        }

        // ── XML Export / Import ───────────────────────────────────────

        private void ExportPresets(bool activeOnly)
        {
            var presets = activeOnly
                ? (PresetCombo.SelectedItem is FilterPreset p ? new[] { p } : Array.Empty<FilterPreset>())
                : _presetRepo.LoadAll().ToArray();

            if (presets.Length == 0) return;

            var dlg = new SaveFileDialog
            {
                Title      = "Exportar conjuntos de selección",
                Filter     = "Archivos XML (*.xml)|*.xml",
                DefaultExt = ".xml",
                FileName   = activeOnly && presets.Length == 1
                    ? $"Filtro_{presets[0].Name}.xml"
                    : "ConjuntosSeleccion.xml"
            };

            if (dlg.ShowDialog() != true) return;

            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("BIMPillsSelectionPresets",
                    new XAttribute("version", "1.1"),
                    new XAttribute("exportedAt", DateTime.UtcNow.ToString("o")),
                    presets.Select(PresetToXml)));

            doc.Save(dlg.FileName);

            BIMPills.UI.Shared.BimPillsDialog.Success(
                "BIM Pills — Exportar",
                $"Se exportaron {presets.Length} conjunto{(presets.Length != 1 ? "s" : "")} correctamente.");
        }

        private void ImportPresets()
        {
            var dlg = new OpenFileDialog
            {
                Title  = "Importar conjuntos de selección",
                Filter = "Archivos XML (*.xml)|*.xml"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var doc  = XDocument.Load(dlg.FileName);
                var root = doc.Root;
                if (root == null || root.Name.LocalName != "BIMPillsSelectionPresets")
                {
                    BIMPills.UI.Shared.BimPillsDialog.Warning(
                        "BIM Pills — Error al importar",
                        "El archivo no es un conjunto de selección válido de BIM Pills.");
                    return;
                }

                var imported = 0;
                foreach (var elem in root.Elements("Preset"))
                {
                    var preset = PresetFromXml(elem);
                    _presetRepo.Save(preset);
                    imported++;
                }

                RefreshPresets();
                BIMPills.UI.Shared.BimPillsDialog.Success(
                    "BIM Pills — Importar",
                    $"Se importaron {imported} conjunto{(imported != 1 ? "s" : "")} correctamente.");
            }
            catch (Exception ex)
            {
                BIMPills.UI.Shared.BimPillsDialog.Error(
                    "BIM Pills — Error al importar",
                    $"Error al leer el archivo:\n{ex.Message}");
            }
        }

        // ── XML serialization helpers ─────────────────────────────────

        private static XElement PresetToXml(FilterPreset p)
        {
            var effectiveCats = p.Filter.EffectiveCategoryNames;
            var filterElem = new XElement("Filter",
                new XAttribute("logic", p.Filter.Logic.ToString()),
                // Multi-category (v1.1)
                new XElement("Categories",
                    effectiveCats.Select(c => new XElement("Category", new XAttribute("name", c)))),
                // Legacy single-category attribute for old readers
                new XAttribute("category", effectiveCats.Count > 0 ? effectiveCats[0] : string.Empty),
                p.Filter.Conditions.Select(c =>
                    new XElement("Condition",
                        new XAttribute("param", c.ParameterName),
                        new XAttribute("op",    c.Operator.ToString()),
                        new XAttribute("value", c.Value))));

            return new XElement("Preset",
                new XAttribute("id",         p.Id),
                new XAttribute("name",       p.Name),
                new XAttribute("createdAt",  p.CreatedAt.ToString("o")),
                new XAttribute("modifiedAt", p.ModifiedAt.ToString("o")),
                filterElem);
        }

        private static FilterPreset PresetFromXml(XElement elem)
        {
            var filterEl = elem.Element("Filter");
            var conditions = filterEl?.Elements("Condition").Select(c => new FilterCondition
            {
                ParameterName = (string?)c.Attribute("param") ?? string.Empty,
                Operator      = Enum.TryParse<FilterOperator>((string?)c.Attribute("op"), out var op) ? op : FilterOperator.Equals,
                Value         = (string?)c.Attribute("value") ?? string.Empty
            }).ToList() ?? new List<FilterCondition>();

            // Read multi-category (v1.1) or fall back to single-category attribute (v1.0)
            var catNames = filterEl?.Element("Categories")
                ?.Elements("Category")
                .Select(c => (string?)c.Attribute("name") ?? string.Empty)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();

            if (catNames == null || catNames.Count == 0)
            {
                var legacyCat = (string?)filterEl?.Attribute("category") ?? string.Empty;
                catNames = string.IsNullOrEmpty(legacyCat)
                    ? new List<string>()
                    : new List<string> { legacyCat };
            }

            return new FilterPreset
            {
                Id         = (string?)elem.Attribute("id")   ?? Guid.NewGuid().ToString(),
                Name       = (string?)elem.Attribute("name") ?? "Importado",
                CreatedAt  = DateTime.TryParse((string?)elem.Attribute("createdAt"),  out var ca) ? ca : DateTime.UtcNow,
                ModifiedAt = DateTime.TryParse((string?)elem.Attribute("modifiedAt"), out var ma) ? ma : DateTime.UtcNow,
                Filter     = new SelectionFilterConfig
                {
                    CategoryName  = catNames.Count > 0 ? catNames[0] : string.Empty,
                    CategoryNames = catNames,
                    Logic         = Enum.TryParse<FilterLogic>((string?)filterEl?.Attribute("logic"), out var logic) ? logic : FilterLogic.And,
                    Conditions    = conditions
                }
            };
        }

        // ── Criteria management ───────────────────────────────────────

        private void AddBtn_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();

            var catItem = new MenuItem { Header = "Categoría" };
            catItem.Click += (_, __) => AddCriterionRow("Categoría", isParam: false);

            var paramInstItem = new MenuItem { Header = "Parámetro de instancia" };
            paramInstItem.Click += (_, __) => AddCriterionRow(string.Empty, isParam: true, typeOnly: false);

            var paramTypeItem = new MenuItem { Header = "Parámetro de tipo" };
            paramTypeItem.Click += (_, __) => AddCriterionRow(string.Empty, isParam: true, typeOnly: true);

            menu.Items.Add(catItem);
            menu.Items.Add(paramInstItem);
            menu.Items.Add(paramTypeItem);
            menu.PlacementTarget = AddBtn;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private void AddCriterionRow(string criterio, string value = "",
            FilterOperator op = FilterOperator.Equals, bool isParam = false, bool? typeOnly = null)
        {
            CriterionRow row;
            if (isParam)
            {
                row = new CriterionRow(_categories, GetCurrentAvailableParamInfos(), typeOnly);
            }
            else
            {
                row = new CriterionRow(_categories, new List<string> { "Categoría" });
            }
            row.Criterio              = criterio;
            row.Value                 = value;
            row.SelectedOperatorLabel = CriterionRow.AllOperatorLabels[Array.IndexOf(CriterionRow.OperatorOrder, op).NormalizeIndex()];

            // When category value changes → refresh param items for all param rows
            row.PropertyChanged += (_, args) =>
            {
                if (row.IsCategory && (args.PropertyName == nameof(CriterionRow.Value) || args.PropertyName == nameof(CriterionRow.Criterio)))
                    RefreshParamItems();
            };

            _criteria.Add(row);
        }

        private IReadOnlyList<ParamInfo> GetCurrentAvailableParamInfos()
        {
            var selectedCats = _criteria
                .Where(r => r.IsCategory && !string.IsNullOrWhiteSpace(r.Value))
                .Select(r => r.Value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (selectedCats.Count == 0) return _allParamInfos;

            // Usar directamente los params de la categoría para preservar IsTypeParam correcto.
            // _allParamInfos tiene una entrada por nombre (la primera categoría que lo añadió),
            // lo que puede dar IsTypeParam incorrecto para otras categorías.
            Dictionary<string, ParamInfo>? intersect = null;
            foreach (var cat in selectedCats)
            {
                if (!_paramsByCategory.TryGetValue(cat, out var catParams)) continue;
                var catDict = catParams.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);
                if (intersect == null)
                    intersect = catDict;
                else
                {
                    foreach (var key in intersect.Keys.Except(catDict.Keys, StringComparer.OrdinalIgnoreCase).ToList())
                        intersect.Remove(key);
                }
            }

            if (intersect == null || intersect.Count == 0) return _allParamInfos;

            return intersect.Values
                .OrderBy(p => p.Group)
                .ThenBy(p => p.Name)
                .ToList();
        }

        private void RemoveCriterion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is CriterionRow row)
                _criteria.Remove(row);
        }

        private void DeleteRowBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_criteria.Count > 0)
                _criteria.RemoveAt(_criteria.Count - 1);
        }

        /// <summary>
        /// Invocado por SeleccionarRevitCommand cuando el EyedropperHandler obtiene datos.
        /// Rellena criterios de categoría + valores de parámetro del elemento cuentagotas.
        /// </summary>
        public Action? RaiseEyedropper { get; set; }

        /// <summary>
        /// Invocado por SeleccionarRevitCommand cuando el RectSelectHandler obtiene datos.
        /// Rellena criterios de categoría con las categorías de la selección activa.
        /// </summary>
        public Action? RaiseRectSelect { get; set; }

        private void EyedropperBtn_Click(object sender, RoutedEventArgs e)
        {
            if (RaiseEyedropper == null)
            {
                BIMPills.UI.Shared.BimPillsDialog.Info(
                    "BIM Pills — Cuentagotas",
                    "El cuentagotas no está disponible en este contexto.");
                return;
            }
            WindowState = WindowState.Minimized;
            RaiseEyedropper.Invoke();
        }

        private void RectSelectBtn_Click(object sender, RoutedEventArgs e)
        {
            if (RaiseRectSelect == null)
            {
                BIMPills.UI.Shared.BimPillsDialog.Info(
                    "BIM Pills — Selección activa",
                    "La selección activa no está disponible en este contexto.");
                return;
            }
            // Lee categorías de la selección actual en Revit — no necesita minimizar
            RaiseRectSelect.Invoke();
        }

        /// <summary>
        /// Aplica los datos del cuentagotas a los criterios EXISTENTES:
        /// rellena el Valor de cada fila según el elemento leído.
        /// Si no hay ningún criterio todavía, añade solo la fila de Categoría.
        /// </summary>
        public void ApplyEyedropperData(BIMPills.Core.Seleccionar.EyedropperData data)
        {
            WindowState = WindowState.Normal;
            Activate();
            if (_criteria.Count == 0)
            {
                // Sin criterios → añadir solo la categoría como punto de partida
                if (!string.IsNullOrWhiteSpace(data.CategoryName))
                    AddCriterionRow("Categoría", data.CategoryName, isParam: false);
                return;
            }

            // Rellenar valores en los criterios que ya existen
            foreach (var row in _criteria)
            {
                if (row.IsCategory)
                {
                    // Fila de categoría → poner la categoría del elemento
                    if (!string.IsNullOrWhiteSpace(data.CategoryName))
                        row.Value = data.CategoryName;
                }
                else
                {
                    // Fila de parámetro → buscar el valor en los datos del elemento
                    if (!string.IsNullOrWhiteSpace(row.Criterio)
                        && data.ParamValues.TryGetValue(row.Criterio, out var val)
                        && !string.IsNullOrWhiteSpace(val))
                    {
                        row.Value = val;
                    }
                }
            }
        }

        /// <summary>
        /// Aplica las categorías de la selección activa: limpia los criterios actuales
        /// y añade una fila de categoría por cada categoría distinta encontrada.
        /// </summary>
        public void ApplyRectSelectCategories(IReadOnlyList<string> categories)
        {
            WindowState = WindowState.Normal;
            Activate();
            if (categories.Count == 0)
            {
                BIMPills.UI.Shared.BimPillsDialog.Info(
                    "BIM Pills — Selección activa",
                    "No hay elementos seleccionados en Revit.");
                return;
            }

            _criteria.Clear();

            foreach (var cat in categories)
                AddCriterionRow("Categoría", cat, isParam: false);
        }

        // ── Actions (modeless — NO cierran la ventana) ───────────────

        private void Select_Click(object sender, RoutedEventArgs e)
            => CommitWithAction(SelectionAction.Replace);

        private void AddToSelection_Click(object sender, RoutedEventArgs e)
            => CommitWithAction(SelectionAction.Add);

        private void Deselect_Click(object sender, RoutedEventArgs e)
            => CommitWithAction(SelectionAction.Remove);

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape) Close();
        }

        /// <summary>
        /// Aplica el filtro disparando el evento — NO cierra la ventana.
        /// La ventana permanece flotante para seguir ajustando criterios.
        /// </summary>
        private void CommitWithAction(SelectionAction action)
        {
            var filter = BuildFilter(action);
            if (filter == null)
            {
                BIMPills.UI.Shared.BimPillsDialog.Warning(
                    "BIM Pills — Sin criterios",
                    "Agrega al menos un criterio antes de buscar.");
                return;
            }
            ResultFilter = filter;
            OnApplyFilter?.Invoke(filter);
        }

        // ── Build filter ──────────────────────────────────────────────

        private SelectionFilterConfig? BuildFilter(SelectionAction action)
        {
            if (_criteria.Count == 0) return null;

            var conditions = new List<FilterCondition>();
            var categories = new List<string>();

            foreach (var row in _criteria)
            {
                if (row.IsCategory)
                {
                    if (!string.IsNullOrWhiteSpace(row.Value))
                        categories.Add(row.Value.Trim());
                }
                else if (!string.IsNullOrWhiteSpace(row.Criterio))
                {
                    conditions.Add(new FilterCondition
                    {
                        ParameterName = row.Criterio,
                        Operator      = row.Operator,
                        Value         = row.Value,
                        IsTypeParam   = row.TypeFilter   // preservar tipo/instancia para round-trip
                    });
                }
            }

            if (categories.Count == 0 && conditions.Count == 0) return null;

            return new SelectionFilterConfig
            {
                CategoryName  = categories.FirstOrDefault() ?? string.Empty,
                CategoryNames = categories,
                Logic         = OrRadio.IsChecked == true ? FilterLogic.Or : FilterLogic.And,
                Conditions    = conditions,
                Action        = action
            };
        }
    }

    // ── CriterionRow ViewModel ────────────────────────────────────────

    internal sealed class CriterionRow : INotifyPropertyChanged
    {
        private readonly IReadOnlyList<string> _allCategories;
        /// <summary>null = todos, false = solo instancia, true = solo tipo.</summary>
        private readonly bool? _typeFilter;

        public static readonly string[] AllOperatorLabels =
        {
            "= es igual a",
            "≠ no es igual a",
            "∋ contiene",
            "∌ no contiene",
            "⊂ empieza por",
            "⊃ termina en",
            "∅ está vacío",
            "≠∅ no está vacío",
            "> mayor que",
            "< menor que",
        };

        public static readonly FilterOperator[] OperatorOrder =
        {
            FilterOperator.Equals,      FilterOperator.NotEquals,
            FilterOperator.Contains,    FilterOperator.NotContains,
            FilterOperator.StartsWith,  FilterOperator.EndsWith,
            FilterOperator.IsEmpty,     FilterOperator.IsNotEmpty,
            FilterOperator.GreaterThan, FilterOperator.LessThan,
        };

        // ── Category row fields ───────────────────────────────────────

        private List<string> _criterioItemsList;
        /// <summary>Items del combo Criterio para filas de categoría (string list).</summary>
        public IReadOnlyList<string> CriterioItems => _criterioItemsList;
        public IReadOnlyList<string> OperatorLabels => AllOperatorLabels;

        /// <summary>Actualiza los items del combo Criterio (para filas de categoría).</summary>
        public void SetCriterioItems(IReadOnlyList<string> items)
        {
            _criterioItemsList = items.ToList();
            OnPropertyChanged(nameof(CriterioItems));
        }

        // ── Param row fields ─────────────────────────────────────────

        /// <summary>Vista agrupada por Group de parámetros disponibles (filas de parámetro).</summary>
        public ICollectionView? CriterioParamView { get; private set; }

        /// <summary>Lista plana de ParamInfo para búsqueda por nombre al resolver AllowedValues.</summary>
        private List<ParamInfo> _paramInfoFlat = new();

        /// <summary>Valores sugeridos del parámetro actualmente seleccionado (null = campo libre).</summary>
        private IReadOnlyList<string>? _currentParamAllowedValues;

        public bool IsParamRow => !IsCategory;

        /// <summary>Filtro de tipo persistido: null = todos, false = instancia, true = tipo.</summary>
        public bool? TypeFilter => _typeFilter;

        /// <summary>
        /// Reemplaza los items del combo de parámetros y recrea la vista agrupada.
        /// Si esta fila tiene un filtro de tipo (instancia/tipo), lo aplica aquí.
        /// </summary>
        public void SetCriterioParamItems(IReadOnlyList<ParamInfo> items)
        {
            // Apply instance/type filter if set
            var filtered = _typeFilter.HasValue
                ? items.Where(p => p.IsTypeParam == _typeFilter.Value).ToList()
                : items.ToList();

            _paramInfoFlat = filtered;   // guardar lista plana para lookup de AllowedValues

            // Save current selection — WPF resets editable ComboBox Text when ItemsSource changes
            var savedCriterio = _criterio;

            var lcv = new ListCollectionView(filtered);
            lcv.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ParamInfo.Group)));
            lcv.SortDescriptions.Add(new SortDescription(nameof(ParamInfo.Group), ListSortDirection.Ascending));
            lcv.SortDescriptions.Add(new SortDescription(nameof(ParamInfo.Name),  ListSortDirection.Ascending));
            CriterioParamView = lcv;
            OnPropertyChanged(nameof(CriterioParamView));

            // Restore Criterio if WPF cleared it via TwoWay binding on Text (synchronous path)
            if (!string.IsNullOrEmpty(savedCriterio) && _criterio != savedCriterio)
            {
                _criterio = savedCriterio;                // write backing field directly (no Value clear)
                OnPropertyChanged(nameof(Criterio));
            }

            // Re-resolver AllowedValues para el criterio actual tras actualizar la lista
            ResolveParamAllowedValues();
        }

        /// <summary>Actualiza _currentParamAllowedValues según el Criterio actualmente escrito.</summary>
        private void ResolveParamAllowedValues()
        {
            IReadOnlyList<string>? resolved = null;
            if (!string.IsNullOrEmpty(_criterio))
            {
                var pi = _paramInfoFlat.FirstOrDefault(
                    p => string.Equals(p.Name, _criterio, StringComparison.OrdinalIgnoreCase));
                if (pi?.AllowedValues?.Count > 0)
                    resolved = pi.AllowedValues;
            }
            _currentParamAllowedValues = resolved;
            OnPropertyChanged(nameof(ValueItems));
        }

        // ── Common properties ────────────────────────────────────────

        private string _criterio;
        public string Criterio
        {
            get => _criterio;
            set
            {
                _criterio = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCategory));
                OnPropertyChanged(nameof(IsParamRow));
                Value = string.Empty;
                ResolveParamAllowedValues();   // actualiza ValueItems (incluye OnPropertyChanged)
            }
        }

        private string _selectedOperatorLabel;
        public string SelectedOperatorLabel
        {
            get => _selectedOperatorLabel;
            set { _selectedOperatorLabel = value; OnPropertyChanged(); }
        }

        private string _value = string.Empty;
        public string Value
        {
            get => _value;
            set
            {
                var normalized = value ?? string.Empty;
                if (_value == normalized) return;   // no change → evita RefreshParamItems innecesario
                _value = normalized;
                OnPropertyChanged();
            }
        }

        public bool IsCategory => _criterio == "Categoría";

        public IReadOnlyList<string> ValueItems
            => IsCategory ? _allCategories : (_currentParamAllowedValues ?? Array.Empty<string>());

        public FilterOperator Operator
        {
            get
            {
                var idx = Array.IndexOf(AllOperatorLabels, _selectedOperatorLabel);
                return idx >= 0 && idx < OperatorOrder.Length ? OperatorOrder[idx] : FilterOperator.Equals;
            }
        }

        /// <summary>Constructor para filas de categoría (string items).</summary>
        public CriterionRow(IReadOnlyList<string> categories, IReadOnlyList<string> criterioItems)
        {
            _allCategories         = categories;
            _criterioItemsList     = criterioItems.ToList();
            _criterio              = criterioItems.Count > 0 ? criterioItems[0] : string.Empty;
            _selectedOperatorLabel = AllOperatorLabels[0];
        }

        /// <summary>Constructor para filas de parámetro (ParamInfo items con agrupación).
        /// <paramref name="typeOnly"/>: null=todos, false=solo instancia, true=solo tipo.</summary>
        public CriterionRow(IReadOnlyList<string> categories, IReadOnlyList<ParamInfo> paramItems, bool? typeOnly = null)
        {
            _allCategories         = categories;
            _criterioItemsList     = new List<string>();
            _criterio              = string.Empty;
            _selectedOperatorLabel = AllOperatorLabels[0];
            _typeFilter            = typeOnly;
            SetCriterioParamItems(paramItems);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    internal static class IndexExtensions
    {
        public static int NormalizeIndex(this int idx) => idx < 0 ? 0 : idx;
    }
}
