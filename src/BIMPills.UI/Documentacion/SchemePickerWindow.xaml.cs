using BIMPills.UI.CustomDimensionSchemes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BIMPills.UI.Documentacion
{
    public partial class SchemePickerWindow : Window
    {
        public string? SelectedSchemeId { get; private set; }

        private Border? _selectedCard;
        private readonly (Border card, TextBlock check)[] _cards;

        public SchemePickerWindow(string currentSchemeId)
        {
            InitializeComponent();
            BIMPills.UI.Shared.ThemeHelper.Apply(this);

            _cards = new[]
            {
                (CardOpeningWidth,   CheckOpeningWidth),
                (CardGridCombined,   CheckGridCombined),
                (CardInteriorSpaces, CheckInteriorSpaces),
                (CardArqLevels,      CheckArqLevels),
                (CardCustom,         CheckCustom),
            };

            // Pre-select current scheme
            foreach (var (card, _) in _cards)
            {
                if (card.Tag?.ToString() == currentSchemeId)
                {
                    SelectCard(card);
                    break;
                }
            }
        }

        private void Card_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border card)
            {
                if (card == CardCustom)
                {
                    // Open scheme manager
                    var mgr = new CustomDimensionSchemesWindow();
                    mgr.Owner = this;
                    mgr.ShowDialog();
                }
                SelectCard(card);
            }
        }

        private void SelectCard(Border card)
        {
            // Deselect previous
            if (_selectedCard != null)
                SetCardInactive(_selectedCard);

            _selectedCard = card;
            SetCardActive(card);
            ConfirmBtn.IsEnabled = true;
        }

        private void SetCardActive(Border card)
        {
            card.BorderBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x63, 0x37));
            card.Background = new SolidColorBrush(Color.FromArgb(0x08, 0xEF, 0x63, 0x37));

            // Show checkmark
            foreach (var (c, check) in _cards)
                if (c == card)
                    check.Visibility = Visibility.Visible;
        }

        private void SetCardInactive(Border card)
        {
            card.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xEA));
            card.Background = Brushes.White;

            foreach (var (c, check) in _cards)
                if (c == card)
                    check.Visibility = Visibility.Collapsed;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            SelectedSchemeId = _selectedCard?.Tag?.ToString();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
