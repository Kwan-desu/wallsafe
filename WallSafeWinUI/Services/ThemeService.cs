using System;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace WallSafeWinUI.Services
{
    /// <summary>
    /// Theme + window-backdrop coordination for WinUI 3. Applies an ElementTheme to
    /// window roots and assigns the window SystemBackdrop (Mica / Mica Alt / Acrylic)
    /// with capability probing and a solid fallback.
    /// </summary>
    public static class ThemeService
    {
        public static event Action<ElementTheme>? ThemeChanged;

        public static ElementTheme CurrentTheme { get; private set; } = ElementTheme.Dark;

        public static void Initialize()
        {
            CurrentTheme = Parse(Settings.Instance.Theme);
        }

        public static ElementTheme Parse(string name) => name switch
        {
            "Light" => ElementTheme.Light,
            "System" => ElementTheme.Default,
            _ => ElementTheme.Dark
        };

        public static string ToName(ElementTheme theme) => theme switch
        {
            ElementTheme.Light => "Light",
            ElementTheme.Default => "System",
            _ => "Dark"
        };

        public static void ApplyTheme(FrameworkElement root, ElementTheme theme)
        {
            CurrentTheme = theme;
            root.RequestedTheme = theme;
            Settings.Instance.Theme = ToName(theme);
            Settings.Instance.Save();
            ThemeChanged?.Invoke(theme);
        }

        public static void ApplyBackdrop(Window window, string material)
        {
            switch (material)
            {
                case "MicaAlt":
                    window.SystemBackdrop = MicaController.IsSupported()
                        ? new MicaBackdrop { Kind = MicaKind.BaseAlt }
                        : null;
                    break;
                case "Acrylic":
                    window.SystemBackdrop = DesktopAcrylicController.IsSupported()
                        ? new DesktopAcrylicBackdrop()
                        : null;
                    break;
                case "None":
                    window.SystemBackdrop = null;
                    break;
                default: // Mica
                    window.SystemBackdrop = MicaController.IsSupported()
                        ? new MicaBackdrop { Kind = MicaKind.Base }
                        : null;
                    break;
            }
        }
    }
}
