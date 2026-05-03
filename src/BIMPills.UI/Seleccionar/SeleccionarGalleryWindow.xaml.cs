using BIMPills.Core.Seleccionar;
using BIMPills.UI.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace BIMPills.UI.Seleccionar
{
    public partial class SeleccionarGalleryWindow : Window
    {
        private readonly IReadOnlyList<string>                                _categories;
        private readonly IReadOnlyList<ParamInfo>                            _allParamInfos;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<ParamInfo>> _paramsByCategory;
        private readonly IReadOnlyList<CategoryElementSummary>              _selectionSummary;
        private readonly IFilterPresetRepository                            _presetRepo;
        private readonly Action<SelectionFilterConfig>                      _raiseApply;
        private readonly Action                                             _raiseOpenAssign;
        private readonly Action                                             _raiseOrdenar;
        private readonly Action<Action<BIMPills.Core.Seleccionar.EyedropperData>>  _onEyedropperReady;
        private readonly Action<Action<IReadOnlyList<string>>>                     _onRectSelectReady;

        /// <summary>Instancia activa de FindSelectModal (modeless). Null si está cerrado.</summary>
        private FindSelectModal? _findSelectModal;

        public SeleccionarGalleryWindow(
            IReadOnlyList<string>                                categories,
            IReadOnlyList<ParamInfo>                             allParamInfos,
            IReadOnlyDictionary<string, IReadOnlyList<ParamInfo>> paramsByCategory,
            IReadOnlyList<CategoryElementSummary>              selectionSummary,
            IFilterPresetRepository                            presetRepo,
            Action<SelectionFilterConfig>                      raiseApply,
            Action                                            raiseOpenAssign,
            Action                                            raiseOrdenar,
            Action<Action<int>>                               onApplyDone,
            Action<Action<SubprojectAssignResult>>            onAssignDone,
            Action<Action<BIMPills.Core.Seleccionar.EyedropperData>>  onEyedropperReady,
            Action<Action<IReadOnlyList<string>>>                      onRectSelectReady)
        {
            InitializeComponent();

            _categories        = categories;
            _allParamInfos     = allParamInfos;
            _paramsByCategory  = paramsByCategory;
            _selectionSummary  = selectionSummary;
            _presetRepo        = presetRepo;
            _raiseApply        = raiseApply;
            _raiseOpenAssign   = raiseOpenAssign;
            _raiseOrdenar      = raiseOrdenar;
            _onEyedropperReady = onEyedropperReady;
            _onRectSelectReady = onRectSelectReady;

            // Position top-right (like OrdenarSessionWindow)
            var screen = SystemParameters.WorkArea;
            Left = screen.Right - Width - 24;
            Top  = screen.Top + 60;

            onApplyDone(count => Dispatcher.Invoke(() =>
            {
                // Si FindSelectModal está abierto, actualizar su contador en lugar de mostrar diálogo
                if (_findSelectModal != null)
                    _findSelectModal.UpdateSelectionCount(count);
                else
                    BimPillsDialog.Success("BIM Pills — Seleccionar",
                        $"Se seleccionaron {count} elemento{(count != 1 ? "s" : "")} en la vista activa.");
            }));

            onAssignDone(result => Dispatcher.Invoke(() =>
            {
                if (result.Errors.Count == 0)
                    BimPillsDialog.Success("BIM Pills — Seleccionar",
                        $"Valores asignados a {result.ElementsAssigned} elemento{(result.ElementsAssigned != 1 ? "s" : "")}.");
                else
                    BimPillsDialog.Warning("BIM Pills — Seleccionar",
                        $"Asignados: {result.ElementsAssigned}. Errores: {result.Errors.Count}.");
            }));
        }

        private void CloseHeader_Click(object sender, MouseButtonEventArgs e) => Close();

        private void OrdenarCard_Click(object sender, MouseButtonEventArgs e)
            => _raiseOrdenar();

        private void FilterCard_Click(object sender, MouseButtonEventArgs e)
        {
            // Si ya hay un modal abierto, traerlo al frente en lugar de abrir otro
            if (_findSelectModal != null)
            {
                try
                {
                    _findSelectModal.Activate();
                    _findSelectModal.WindowState = WindowState.Normal;
                    return;
                }
                catch { _findSelectModal = null; }
            }

            var total    = _selectionSummary.Sum(s => s.TotalCount);
            var editable = _selectionSummary.Sum(s => s.EditableCount);

            _findSelectModal = new FindSelectModal(
                _categories, _allParamInfos, _paramsByCategory, _presetRepo, total, editable);

            _findSelectModal.OnApplyFilter += filter => _raiseApply(filter);
            _findSelectModal.Closed += (_, __) =>
            {
                _findSelectModal = null;
                // Restaurar galería cuando se cierre la herramienta
                WindowState = WindowState.Normal;
                Activate();
            };

            // Wire eyedropper
            _findSelectModal.RaiseEyedropper = () => _onEyedropperReady(
                data => Dispatcher.Invoke(() => _findSelectModal?.ApplyEyedropperData(data)));

            // Wire rect-select
            _findSelectModal.RaiseRectSelect = () => _onRectSelectReady(
                cats => Dispatcher.Invoke(() => _findSelectModal?.ApplyRectSelectCategories(cats)));

            // Minimizar galería mientras la herramienta está abierta
            WindowState = WindowState.Minimized;

            // Modeless: abre flotante sobre Revit
            _findSelectModal.ShowOverRevit();
        }

        private void AssignCard_Click(object sender, MouseButtonEventArgs e)
        {
            _raiseOpenAssign();
            // La galería se minimiza; AssignValuesOpenHandler llama OnModalOpened para restaurarla
            // cuando el modal se cierre — gestionado en SeleccionarRevitCommand.
            WindowState = WindowState.Minimized;
        }

        private void Card_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
                border.Background = new SolidColorBrush(Color.FromRgb(249, 249, 251));
        }

        private void Card_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
                border.Background = Brushes.White;
        }

        // La galería cierra con ESC solo si tanto el KeyDown como el KeyUp
        // ocurrieron en esta misma ventana.
        private bool _escDownInThisWindow = false;

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape && !e.IsRepeat)
                _escDownInThisWindow = true;
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (e.Key == Key.Escape && _escDownInThisWindow)
                Close();
            _escDownInThisWindow = false;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Cerrar el modal de filtro si está abierto al cerrar la galería
            try { _findSelectModal?.Close(); } catch { }
        }
    }
}
