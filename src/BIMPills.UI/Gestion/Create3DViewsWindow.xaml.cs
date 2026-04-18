using BIMPills.Core.Gestion;
using BIMPills.UI.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace BIMPills.UI.Gestion
{
    public partial class Create3DViewsWindow : Window
    {
        private readonly ObservableCollection<WorksetCheckItem> _items;
        private readonly Func<View3DCreationConfig, View3DCreationResult>? _createViewsCallback;

        public View3DCreationConfig? ResultConfig { get; private set; }

        public Create3DViewsWindow(
            IReadOnlyList<WorksetViewModel> worksets,
            IReadOnlyList<WorksetViewModel>? preselected = null,
            Func<View3DCreationConfig, View3DCreationResult>? createViewsCallback = null)
        {
            InitializeComponent();
            BIMPills.UI.Shared.ThemeHelper.Apply(this);
            _createViewsCallback = createViewsCallback;

            var preselectedIds = preselected?.Select(w => w.Id).ToHashSet() ?? new HashSet<long>();

            _items = new ObservableCollection<WorksetCheckItem>(
                worksets.Select(w => new WorksetCheckItem
                {
                    Id        = w.Id,
                    Name      = w.Name,
                    IsChecked = preselected != null ? preselectedIds.Contains(w.Id) : true
                }));

            WorksetsList.ItemsSource = _items;
            foreach (var item in _items)
                item.PropertyChanged += (_, _) => UpdateUI();

            UpdateUI();
        }

        private void UpdateUI()
        {
            int count = _items.Count(i => i.IsChecked);
            SelectionText.Text  = count > 0 ? $"{count} seleccionados" : "";
            CreateBtnLabel.Text = count > 0 ? $"Crear {count} vistas" : "Crear vistas";
            BtnCreate.IsEnabled = count > 0;
        }

        private void WorksetCheck_Click(object sender, RoutedEventArgs e) => UpdateUI();

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool allChecked = _items.All(i => i.IsChecked);
            foreach (var item in _items)
                item.IsChecked = !allChecked;
            UpdateUI();
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            var selected = _items.Where(i => i.IsChecked).ToList();
            if (selected.Count == 0) return;

            var conflictTag = (ConflictCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString() ?? "Skip";
            var detailTag   = (DetailLevelCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString() ?? "Medium";

            var config = new View3DCreationConfig
            {
                ViewNameTemplate  = ViewNameBox.Text.Trim().Length > 0 ? ViewNameBox.Text : "{nombre}",
                Visibility        = WorksetViewVisibility.HideOthers,
                ConflictResolution = conflictTag == "Overwrite" ? ViewConflictResolution.Overwrite : ViewConflictResolution.Skip,
                DetailLevel                = detailTag == "Coarse" ? ViewDetailLevel.Coarse : detailTag == "Fine" ? ViewDetailLevel.Fine : ViewDetailLevel.Medium,
                HideAnnotationCategories  = ChkHideAnnotations.IsChecked == true,
                SetCoordinationDiscipline = ChkCoordination.IsChecked == true,
                WorksetIds                = selected.Select(i => i.Id).ToList(),
                WorksetNames              = selected.Select(i => i.Name).ToList()
            };

            if (_createViewsCallback != null)
            {
                var result = _createViewsCallback(config);
                ResultConfig = config;
                var msg = $"Operación completada.\n\n• Creadas: {result.Created}\n• Omitidas: {result.Skipped}\n• Errores: {result.Failed}";
                if (result.Errors.Count > 0) msg += $"\n\n{string.Join("\n", result.Errors.Take(5))}";
                if (result.Failed > 0)
                    BimPillsDialog.Warning("BIM Pills — Crear Vistas 3D", msg);
                else
                    BimPillsDialog.Info("BIM Pills — Crear Vistas 3D", msg);
                Close();
            }
            else
            {
                // Sandbox: show mock result
                ResultConfig = config;
                BimPillsDialog.Info("BIM Pills — Crear Vistas 3D",
                    $"[Sandbox] Se crearían {selected.Count} vistas 3D:\n\n" +
                    string.Join("\n", selected.Take(5).Select(i => $"• {config.ViewNameTemplate.Replace("{nombre}", i.Name)}")) +
                    (selected.Count > 5 ? $"\n… y {selected.Count - 5} más" : ""));
                Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }

    internal class WorksetCheckItem : INotifyPropertyChanged
    {
        private bool _isChecked;
        public long   Id   { get; set; }
        public string Name { get; set; } = "";
        public bool IsChecked
        {
            get => _isChecked;
            set { _isChecked = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked))); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
