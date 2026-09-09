using System;
using System.Windows;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfApplication = System.Windows.Application;

namespace WallSafe
{
    public static class ThemeManager
    {
        public static bool IsDark => !string.Equals(Settings.Instance.Theme, "Light", StringComparison.OrdinalIgnoreCase);

        public static event Action<bool>? ThemeChanged;

        public static void Initialize()
        {
            ApplyTheme(Settings.Instance.Theme);
        }

        public static void ToggleTheme()
        {
            ApplyTheme(IsDark ? "Light" : "Dark");
        }

        public static void ApplyTheme(string themeName)
        {
            bool isDark = !string.Equals(themeName, "Light", StringComparison.OrdinalIgnoreCase);
            Settings.Instance.Theme = isDark ? "Dark" : "Light";
            Settings.Instance.Save();

            var app = WpfApplication.Current;
            if (app == null) return;

            var res = app.Resources;

            if (isDark)
            {
                // Dark Theme — Clean Windows 11 Fluent Slate (No AI neon glow)
                SetColor(res, "BgColor", "#0F1117");
                SetColor(res, "SurfaceColor", "#181B24");
                SetColor(res, "SurfaceHoverColor", "#222634");
                SetColor(res, "CardBgColor", "#181B24");
                SetColor(res, "BorderColor", "#282D3D");
                SetColor(res, "BorderHoverColor", "#3D455C");

                SetColor(res, "TextColor", "#F8FAFC");
                SetColor(res, "SubtextColor", "#94A3B8");
                SetColor(res, "MutedColor", "#64748B");

                SetColor(res, "AccentColor", "#6366F1");
                SetColor(res, "AccentHoverColor", "#818CF8");
                SetColor(res, "AccentGlowColor", "#4F46E5");

                SetColor(res, "CardOverlayFadeColor", "#E60F1117");
                SetColor(res, "CardBottomBarColor", "#D90F1117");
                SetColor(res, "CardActionBtnBgColor", "#33FFFFFF");
                SetColor(res, "CardActionBtnBorderColor", "#26FFFFFF");
                SetColor(res, "CardActionBtnFgColor", "#CBD5E1");
                SetColor(res, "NavActiveBgColor", "#25293C");
                SetColor(res, "ScrollThumbColor", "#3D455C");
            }
            else
            {
                // Light Theme — Clean, Crisp, High-Contrast Windows 11 Light (Zero Dark Elements)
                SetColor(res, "BgColor", "#F8FAFC");
                SetColor(res, "SurfaceColor", "#FFFFFF");
                SetColor(res, "SurfaceHoverColor", "#F1F5F9");
                SetColor(res, "CardBgColor", "#FFFFFF");
                SetColor(res, "BorderColor", "#E2E8F0");
                SetColor(res, "BorderHoverColor", "#CBD5E1");

                SetColor(res, "TextColor", "#0F172A");
                SetColor(res, "SubtextColor", "#475569");
                SetColor(res, "MutedColor", "#94A3B8");

                SetColor(res, "AccentColor", "#4F46E5");
                SetColor(res, "AccentHoverColor", "#4338CA");
                SetColor(res, "AccentGlowColor", "#3730A3");

                SetColor(res, "CardOverlayFadeColor", "#E6FFFFFF");
                SetColor(res, "CardBottomBarColor", "#F2FFFFFF");
                SetColor(res, "CardActionBtnBgColor", "#E2E8F0");
                SetColor(res, "CardActionBtnBorderColor", "#CBD5E1");
                SetColor(res, "CardActionBtnFgColor", "#334155");
                SetColor(res, "NavActiveBgColor", "#E2E8F0");
                SetColor(res, "ScrollThumbColor", "#CBD5E1");
            }

            ThemeChanged?.Invoke(isDark);
        }

        private static void SetColor(ResourceDictionary res, string colorKey, string hex)
        {
            var color = (MediaColor)MediaColorConverter.ConvertFromString(hex);
            res[colorKey] = color;

            string brushKey = colorKey.Replace("Color", "Brush");
            if (res.Contains(brushKey) && res[brushKey] is MediaSolidColorBrush scb && !scb.IsFrozen)
            {
                scb.Color = color;
            }
            else
            {
                res[brushKey] = new MediaSolidColorBrush(color);
            }
        }
    }
}
