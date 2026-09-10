using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WallSafe
{
    /// <summary>
    /// A modern, theme-aware renderer for the system-tray context menu so it matches
    /// WallSafe's Fluent dark/light design instead of the flat default Win32 menu.
    ///
    /// Palette is pulled to mirror ThemeManager: dark slate surfaces, indigo accent,
    /// rounded hover highlight, subtle separators and custom check glyphs.
    /// </summary>
    public sealed class TrayMenuRenderer : ToolStripProfessionalRenderer
    {
        public TrayMenuRenderer() : base(new TrayColorTable()) { }

        private static bool IsDark => ThemeManager.IsDark;

        private static Color Surface => IsDark ? ColorTranslator.FromHtml("#181B24") : ColorTranslator.FromHtml("#FFFFFF");
        private static Color Border => IsDark ? ColorTranslator.FromHtml("#282D3D") : ColorTranslator.FromHtml("#E2E8F0");
        private static Color Text => IsDark ? ColorTranslator.FromHtml("#F8FAFC") : ColorTranslator.FromHtml("#0F172A");
        private static Color Subtext => IsDark ? ColorTranslator.FromHtml("#94A3B8") : ColorTranslator.FromHtml("#475569");
        private static Color Accent => IsDark ? ColorTranslator.FromHtml("#6366F1") : ColorTranslator.FromHtml("#4F46E5");
        private static Color Hover => IsDark ? ColorTranslator.FromHtml("#25293C") : ColorTranslator.FromHtml("#F1F5F9");
        private static Color SeparatorColor => IsDark ? ColorTranslator.FromHtml("#282D3D") : ColorTranslator.FromHtml("#E2E8F0");

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var bg = new SolidBrush(Surface);
            var rect = new Rectangle(Point.Empty, e.ToolStrip.Size);
            using var path = RoundedRect(rect, 10);
            e.Graphics.FillPath(bg, path);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            using var pen = new Pen(Border, 1f);
            using var path = RoundedRect(rect, 10);
            e.Graphics.DrawPath(pen, path);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var item = e.Item;
            var rect = new Rectangle(3, 1, item.Width - 6, item.Height - 2);

            if (item.Selected && item.Enabled)
            {
                using var hover = new SolidBrush(Hover);
                using var path = RoundedRect(rect, 6);
                e.Graphics.FillPath(hover, path);

                // Accent bar on the left edge of the highlighted item.
                using var accent = new SolidBrush(Accent);
                using var bar = RoundedRect(new Rectangle(rect.X, rect.Y + 4, 3, rect.Height - 8), 2);
                e.Graphics.FillPath(accent, bar);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Text : Subtext;
            e.TextFont = new Font("Segoe UI Semibold", 9.25f, FontStyle.Regular, GraphicsUnit.Point);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int mid = e.Item.Height / 2;
            using var pen = new Pen(SeparatorColor, 1f);
            e.Graphics.DrawLine(pen, 12, mid, e.Item.Width - 12, mid);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            // Draw a rounded accent check chip instead of the default Win32 checkbox.
            if (e.Item is ToolStripMenuItem mi && mi.Checked)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var r = e.ImageRectangle;
                var chip = new Rectangle(r.X + 2, r.Y + (r.Height - 16) / 2, 16, 16);
                using var fill = new SolidBrush(Accent);
                using var path = RoundedRect(chip, 4);
                e.Graphics.FillPath(fill, path);

                // Draw the check mark.
                using var pen = new Pen(Color.White, 1.8f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                var p1 = new Point(chip.X + 4, chip.Y + 8);
                var p2 = new Point(chip.X + 7, chip.Y + 11);
                var p3 = new Point(chip.X + 12, chip.Y + 5);
                e.Graphics.DrawLines(pen, new[] { p1, p2, p3 });
            }
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            if (d <= 0 || r.Width <= 0 || r.Height <= 0)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>Backing color table so the professional renderer's own fills match too.</summary>
        private sealed class TrayColorTable : ProfessionalColorTable
        {
            public TrayColorTable() { UseSystemColors = false; }
            public override Color ToolStripDropDownBackground => Surface;
            public override Color ImageMarginGradientBegin => Surface;
            public override Color ImageMarginGradientMiddle => Surface;
            public override Color ImageMarginGradientEnd => Surface;
            public override Color MenuItemSelected => Hover;
            public override Color MenuItemSelectedGradientBegin => Hover;
            public override Color MenuItemSelectedGradientEnd => Hover;
            public override Color MenuItemBorder => Hover;
            public override Color MenuBorder => Border;
            public override Color SeparatorDark => SeparatorColor;
            public override Color SeparatorLight => SeparatorColor;
        }
    }
}
