using System;
using System.Windows;

namespace BIMPills.UI.Shared
{
    /// <summary>
    /// Detects Revit's UI theme and applies the matching WPF resource dictionary to a window.
    /// </summary>
    public static class ThemeHelper
    {
        private static bool _isDark;
        private static bool _initialized;

        private static readonly Uri DarkThemeUri =
            new Uri("/BIMPills.UI;component/Shared/DarkTheme.xaml", UriKind.Relative);

        /// <summary>
        /// Call once from RevitApplication with the detected theme.
        /// </summary>
        public static void Initialize(bool isDark)
        {
            _isDark      = isDark;
            _initialized = true;
        }

        public static bool IsDark => _isDark;

        /// <summary>
        /// Merges DarkTheme.xaml into the window's resources if dark mode is active.
        /// Call after InitializeComponent().
        /// </summary>
        public static void Apply(Window window)
        {
            if (!_initialized || !_isDark) return;

            try
            {
                var dark = new ResourceDictionary { Source = DarkThemeUri };
                window.Resources.MergedDictionaries.Add(dark);
            }
            catch (Exception)
            {
                // Non-critical — fallback to light theme silently
            }
        }
    }
}
