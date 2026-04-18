using BIMPills.Core.Models;
using BIMPills.UI.Shared;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace BIMPills.UI.Ordering
{
    public partial class OrdenarSessionWindow : Window
    {
        private readonly OrderingSessionState _session;
        private readonly Action               _raisePick;
        private readonly Action               _raiseUndo;
        private bool _finishedNormally;

        public OrdenarSessionWindow(
            OrderingSessionState session,
            Action raisePick,
            Action raiseUndo,
            Action<Action<int>> onPickDone,
            Action<Action<int>> onUndoDone,
            Action<Action>      onPickCancelled)
        {
            InitializeComponent();

            _session   = session;
            _raisePick = raisePick;
            _raiseUndo = raiseUndo;

            // Set header route label
            HeaderRoute.Text = $"ORDENAR · {session.Config.CategoryName} → {session.Config.ParameterName}";

            // Position top-right
            var screen = SystemParameters.WorkArea;
            Left = screen.Right - Width - 24;
            Top  = screen.Top + 60;

            onPickDone(_ => Dispatcher.Invoke(() =>
            {
                RefreshUI();
                if (AutoContinueCheck.IsChecked == true && _session.IsActive)
                    Pick_Click(null!, null!);
            }));
            onUndoDone(_ => Dispatcher.Invoke(RefreshUI));
            onPickCancelled(() => Dispatcher.Invoke(SetIdleState));

            RefreshUI();
        }

        private void RefreshUI()
        {
            var history = _session.History;

            NextValueLabel.Text  = _session.Config.FormatValue(_session.CurrentValue);
            CountLabel.Text      = history.Count.ToString();
            UndoButton.IsEnabled = history.Count > 0;

            if (history.Count > 0)
            {
                FirstValueLabel.Text = history.First().AssignedValue;
                LastValueLabel.Text  = history.Last().AssignedValue;
            }
            else
            {
                FirstValueLabel.Text = "—";
                LastValueLabel.Text  = "—";
            }

            // Reverse history for display (newest first)
            var displayItems = history
                .AsEnumerable()
                .Reverse()
                .Select(h => new HistoryDisplayItem
                {
                    AssignedValue    = h.AssignedValue,
                    ElementIdDisplay = $"ID {h.ElementId}"
                })
                .ToList();

            HistoryList.ItemsSource     = displayItems;
            EmptyHistoryLabel.Visibility = displayItems.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            SetIdleState();
        }

        private void SetIdleState()
        {
            PickButton.IsEnabled = true;
            PickButton.Content   = BuildPickContent("↑", "Seleccionar siguiente elemento");
        }

        private object BuildPickContent(string icon, string text)
        {
            var panel = new System.Windows.Controls.StackPanel
            { Orientation = System.Windows.Controls.Orientation.Horizontal };
            panel.Children.Add(new System.Windows.Controls.TextBlock
            { Text = icon, FontSize = 13, Margin = new Thickness(0,0,6,0),
              VerticalAlignment = VerticalAlignment.Center });
            panel.Children.Add(new System.Windows.Controls.TextBlock
            { Text = text, FontSize = 12, FontWeight = FontWeights.SemiBold });
            return panel;
        }

        private void CloseHeader_Click(object sender, MouseButtonEventArgs e)
        {
            // Treat header X as cancel
            Cancel_Click(sender, new RoutedEventArgs());
        }

        private void Pick_Click(object sender, RoutedEventArgs e)
        {
            PickButton.Content   = BuildPickContent("⌛", "Esperando selección...");
            PickButton.IsEnabled = false;
            _raisePick();
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (_session.History.Count == 0) return;
            _raiseUndo();
        }

        private void Finish_Click(object sender, RoutedEventArgs e)
        {
            _finishedNormally = true;
            _session.IsActive = false;
            int count = _session.History.Count;
            Close();
            BimPillsDialog.Info("BIMPills — Ordenar", $"Sesión finalizada. Se asignaron {count} valores.");
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (_session.History.Count > 0)
            {
                var result = BimPillsDialog.Confirm(
                    "BIMPills — Ordenar",
                    $"Se desharán los {_session.History.Count} valores asignados. ¿Continuar?",
                    kind: BimPillsDialog.DialogKind.Question);
                if (!result) return;
            }
            _finishedNormally = true;
            _session.IsActive = false;
            for (int i = _session.History.Count - 1; i >= 0; i--)
                _raiseUndo();
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Cancel_Click(sender, new RoutedEventArgs());
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_finishedNormally && _session.History.Count > 0)
            {
                var result = BimPillsDialog.Confirm(
                    "BIMPills — Ordenar",
                    $"Se desharán los {_session.History.Count} valores asignados. ¿Cerrar de todas formas?",
                    kind: BimPillsDialog.DialogKind.Warning);
                if (!result) { e.Cancel = true; return; }
                _session.IsActive = false;
                for (int i = _session.History.Count - 1; i >= 0; i--)
                    _raiseUndo();
            }
        }

        private class HistoryDisplayItem
        {
            public string AssignedValue    { get; set; } = "";
            public string ElementIdDisplay { get; set; } = "";
        }
    }
}
