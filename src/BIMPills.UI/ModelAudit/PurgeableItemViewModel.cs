using BIMPills.Core.Audit;
using System.ComponentModel;

namespace BIMPills.UI.ModelAudit
{
    /// <summary>
    /// ViewModel wrapper for PurgeableItem that adds selection support for the DataGrid checkbox.
    /// Lives in the UI layer so Core stays free of UI concerns (INotifyPropertyChanged).
    /// </summary>
    public sealed class PurgeableItemViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;

        public PurgeableItem Item { get; }

        public long Id => Item.Id;
        public string Name => Item.Name;
        public string Category => Item.Category;
        public string ItemType => Item.ItemType;
        public long SizeBytes => Item.SizeBytes;
        public string SizeLabel => Item.SizeLabel;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public PurgeableItemViewModel(PurgeableItem item)
        {
            Item = item;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
