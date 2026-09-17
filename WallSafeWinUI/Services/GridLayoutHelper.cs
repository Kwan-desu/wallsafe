using System;
using Microsoft.UI.Xaml.Controls;

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
    }
}
