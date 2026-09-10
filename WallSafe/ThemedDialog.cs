using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Control = System.Windows.Controls.Control;
using Orientation = System.Windows.Controls.Orientation;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using TextBox = System.Windows.Controls.TextBox;
using Application = System.Windows.Application;
using Cursors = System.Windows.Input.Cursors;

namespace WallSafe
{
    /// <summary>
    /// A small borderless, theme-synced modal dialog used for text input and yes/no
    /// confirmations, so WallSafe never falls back to the old-school system dialogs.
    /// Colors are pulled from the app's DynamicResource theme brushes.
    /// </summary>
    public static class ThemedDialog
    {
        private static Brush B(string key) =>
            (Brush)(Application.Current.TryFindResource(key) ?? Brushes.Gray);

        /// <summary>Prompt for a single line of text. Returns null if cancelled.</summary>
        public static string? Prompt(Window owner, string title, string message, string initial = "", string okText = "Create")
        {
            var (win, panel) = BuildShell(owner, title);

            panel.Children.Add(new TextBlock
            {
                Text = message,
                Foreground = B("SubtextBrush"),
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var box = new TextBox
            {
                Text = initial,
                FontSize = 13,
                Padding = new Thickness(8, 6, 8, 6),
                Background = B("CardBgBrush"),
                Foreground = B("TextBrush"),
                CaretBrush = B("TextBrush"),
                BorderBrush = B("BorderBrush"),
                BorderThickness = new Thickness(1)
            };
            panel.Children.Add(box);

            string? result = null;
            var buttons = BuildButtonRow(win,
                okText, () => { result = box.Text; win.DialogResult = true; },
                "Cancel");
            panel.Children.Add(buttons);

            win.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
            return win.ShowDialog() == true ? result : null;
        }

        /// <summary>Yes/No confirmation. Returns true if confirmed.</summary>
        public static bool Confirm(Window owner, string title, string message, string okText = "Delete")
        {
            var (win, panel) = BuildShell(owner, title);

            panel.Children.Add(new TextBlock
            {
                Text = message,
                Foreground = B("SubtextBrush"),
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
            });

            bool confirmed = false;
            var buttons = BuildButtonRow(win,
                okText, () => { confirmed = true; win.DialogResult = true; },
                "Cancel");
            panel.Children.Add(buttons);

            win.ShowDialog();
            return confirmed;
        }

        // ── Shared chrome ──

        private static (Window win, StackPanel panel) BuildShell(Window owner, string title)
        {
            var win = new Window
            {
                Width = 380,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false
            };

            var outer = new Border
            {
                Background = B("SurfaceBrush"),
                BorderBrush = B("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black, BlurRadius = 20, ShadowDepth = 4, Opacity = 0.4
                }
            };

            var root = new StackPanel { Margin = new Thickness(18) };

            // Title row (also draggable)
            var titleText = new TextBlock
            {
                Text = title,
                Foreground = B("TextBrush"),
                FontFamily = new FontFamily("Segoe UI Semibold"),
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 12)
            };
            root.Children.Add(titleText);

            outer.Child = root;
            win.Content = outer;

            // Allow dragging the frameless dialog + Esc to cancel.
            win.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) win.DragMove(); };
            win.KeyDown += (_, e) => { if (e.Key == Key.Escape) { win.DialogResult = false; } };

            return (win, root);
        }

        private static StackPanel BuildButtonRow(Window win, string okText, System.Action onOk, string cancelText)
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };

            var cancel = MakeButton(cancelText, accent: false);
            cancel.IsCancel = true;
            cancel.Margin = new Thickness(0, 0, 8, 0);
            cancel.Click += (_, _) => win.DialogResult = false;

            var ok = MakeButton(okText, accent: true);
            ok.IsDefault = true;
            ok.Click += (_, _) => onOk();

            row.Children.Add(cancel);
            row.Children.Add(ok);
            return row;
        }

        private static Button MakeButton(string text, bool accent)
        {
            var btn = new Button
            {
                Content = text,
                MinWidth = 84,
                Height = 34,
                Padding = new Thickness(14, 0, 14, 0),
                Cursor = Cursors.Hand,
                Foreground = accent ? Brushes.White : B("TextBrush"),
                BorderThickness = new Thickness(accent ? 0 : 1),
                BorderBrush = B("BorderBrush"),
                FontFamily = new FontFamily("Segoe UI Semibold"),
                FontSize = 12
            };

            // Rounded template with hover.
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "bd";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(cp);
            template.VisualTree = border;

            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BackgroundProperty,
                accent ? B("AccentHoverBrush") : B("SurfaceHoverBrush"), "bd"));
            template.Triggers.Add(hover);
            btn.Template = template;

            btn.Background = accent ? B("AccentBrush") : B("CardBgBrush");
            return btn;
        }
    }
}
