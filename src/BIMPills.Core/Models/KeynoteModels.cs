using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BIMPills.Core.Models
{
    public class KeynoteEntry : INotifyPropertyChanged
    {
        private string _key         = "";
        private string _description = "";
        private string _parentKey   = "";
        private bool   _isModified;
        private bool   _isNew;

        public string Key
        {
            get => _key;
            set
            {
                if (_key == value) return;
                _key = value;
                if (!_isNew) _isModified = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsGroup));
                OnPropertyChanged(nameof(Level));
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                if (_description == value) return;
                _description = value;
                if (!_isNew) _isModified = true;
                OnPropertyChanged();
            }
        }

        public string ParentKey
        {
            get => _parentKey;
            set
            {
                if (_parentKey == value) return;
                _parentKey = value;
                if (!_isNew) _isModified = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsGroup));
                OnPropertyChanged(nameof(Level));
            }
        }

        public bool IsModified
        {
            get => _isModified;
            set { _isModified = value; OnPropertyChanged(); }
        }

        public bool IsNew
        {
            get => _isNew;
            set { _isNew = value; OnPropertyChanged(); }
        }

        /// <summary>True when this entry has no parent — it is a top-level group.</summary>
        public bool IsGroup => string.IsNullOrWhiteSpace(_parentKey);

        /// <summary>Nesting depth: 0 = root, 1 = first sub-level, etc.</summary>
        public int Level => string.IsNullOrWhiteSpace(_parentKey)
            ? 0
            : _parentKey.Split('.').Length;

        /// <summary>UI state: whether this group's children are visible.</summary>
        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        /// <summary>UI drag-drop: highlights this row as the current drop target.</summary>
        private bool _isDropTarget;
        public bool IsDropTarget
        {
            get => _isDropTarget;
            set { _isDropTarget = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
