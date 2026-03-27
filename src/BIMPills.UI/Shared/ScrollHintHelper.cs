using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BIMPills.UI.Shared
{
    /// <summary>
    /// Attaches scroll hint overlays ("más elementos arriba/abajo") to a DataGrid.
    /// Call Attach() after the DataGrid is loaded and has items.
    /// </summary>
    public static class ScrollHintHelper
    {
        /// <summary>
        /// Attaches scroll indicators to a DataGrid that is inside a Grid container.
        /// The Grid must already contain the DataGrid and two TextBlocks for hints.
        /// </summary>
        public static void Attach(DataGrid grid, TextBlock upHint, TextBlock downHint)
        {
            grid.Loaded += (s, e) =>
            {
                var scrollViewer = FindScrollViewer(grid);
                if (scrollViewer == null) return;

                scrollViewer.ScrollChanged += (sv, args) =>
                    UpdateHints(scrollViewer, upHint, downHint);

                // Initial check
                UpdateHints(scrollViewer, upHint, downHint);
            };

            // Also update when items change
            grid.LayoutUpdated += (s, e) =>
            {
                var scrollViewer = FindScrollViewer(grid);
                if (scrollViewer != null)
                    UpdateHints(scrollViewer, upHint, downHint);
            };
        }

        private static void UpdateHints(ScrollViewer sv, TextBlock upHint, TextBlock downHint)
        {
            bool canScrollUp = sv.VerticalOffset > 0;
            bool canScrollDown = sv.VerticalOffset < sv.ScrollableHeight - 1;

            upHint.Visibility = canScrollUp ? Visibility.Visible : Visibility.Collapsed;
            downHint.Visibility = canScrollDown ? Visibility.Visible : Visibility.Collapsed;
        }

        private static ScrollViewer? FindScrollViewer(DependencyObject obj)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is ScrollViewer sv)
                    return sv;

                var result = FindScrollViewer(child);
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}
