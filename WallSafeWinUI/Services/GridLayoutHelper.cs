using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace WallSafeWinUI.Services
{
    /// <summary>
    /// Computes ItemsWrapGrid cell sizes so wallpaper cards fill the available
    /// width evenly (no dead space on the right). Targets a preferred card width and
    /// distributes the remaining width across the resulting column count, keeping a
    /// 16:10 aspect ratio.
    /// </summary>
    public static class GridLayoutHelper
    {
        public static void FitColumns(ItemsWrapGrid wrap, double availableWidth,
            double preferredWidth = 340, double margin = 12, double aspect = 0.625)
        {
            if (wrap == null || availableWidth <= 0) return;

            // availableWidth already excludes the page padding. Each cell includes the
            // card's own margin (margin/2 on each side => `margin` total per cell).
            double usable = Math.Max(1, availableWidth);
            int columns = Math.Max(1, (int)Math.Floor(usable / preferredWidth));
            double cellWidth = Math.Floor(usable / columns);
            if (cellWidth <= margin + 40) cellWidth = margin + 40;

            wrap.ItemWidth = cellWidth;
            wrap.ItemHeight = Math.Round((cellWidth - margin) * aspect) + margin;
        }

        /// <summary>
        /// Walks the visual tree depth-first to find the first child of the given type.
        /// Use this when ItemsPanelRoot returns null (e.g. during initial page load).
        /// </summary>
        public static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T found) return found;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        /// <summary>
        /// Resolves the ItemsWrapGrid from a GridView using ItemsPanelRoot first,
        /// then falling back to a visual tree walk.
        /// </summary>
        public static ItemsWrapGrid? GetWrapGrid(GridView gridView)
        {
            if (gridView == null) return null;
            return gridView.ItemsPanelRoot as ItemsWrapGrid
                   ?? FindVisualChild<ItemsWrapGrid>(gridView);
        }

        /// <summary>
        /// Convenience: resolve the wrap grid from a GridView and fit columns.
        /// </summary>
        public static void UpdateLayout(GridView gridView)
        {
            if (gridView == null) return;
            var wrap = GetWrapGrid(gridView);
            if (wrap != null)
                FitColumns(wrap, gridView.ActualWidth - gridView.Padding.Left - gridView.Padding.Right);
        }
    }
}
