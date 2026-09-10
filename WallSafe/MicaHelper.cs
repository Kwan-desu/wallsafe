using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace WallSafe
{
    /// <summary>
    /// Applies the Windows 11 "Mica" system backdrop to a WPF window via DWM.
    ///
    /// Mica tints the window with the desktop wallpaper's dominant color without
    /// showing live content behind it, so it stays legible — unlike Acrylic which
    /// we reserve for transient popups. Requires Windows 11 build 22621+; on older
    /// systems this is a no-op and the window keeps its opaque themed background.
    /// </summary>
    public static class MicaHelper
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        // DWMWA_SYSTEMBACKDROP_TYPE = 38 (Win11 22621+)
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        // DWMWA_USE_IMMERSIVE_DARK_MODE = 20
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        // DWMWA_WINDOW_CORNER_PREFERENCE = 33 (Win11)
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_DEFAULT = 0;
        private const int DWMWCP_ROUND = 2;

        /// <summary>Ask DWM to render rounded window corners (Windows 11). No-op elsewhere.</summary>
        public static void EnableRoundedCorners(Window window)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;
                int pref = DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
            }
            catch { }
        }

        /// <summary>Sync the DWM title-bar (caption) dark/light appearance to the app theme.</summary>
        public static void SyncCaptionTheme(Window window)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;
                int dark = ThemeManager.IsDark ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
            }
            catch { }
        }

        // DWM_SYSTEMBACKDROP_TYPE values
        private const int DWMSBT_AUTO = 0;
        private const int DWMSBT_NONE = 1;
        private const int DWMSBT_MAINWINDOW = 2;   // Mica
        private const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic
        private const int DWMSBT_TABBEDWINDOW = 4; // Mica Alt

        /// <summary>True on Windows 11 build 22621 or newer, where Mica is supported.</summary>
        public static bool IsMicaSupported
        {
            get
            {
                var v = Environment.OSVersion.Version;
                return v.Major >= 10 && v.Build >= 22621;
            }
        }

        /// <summary>
        /// Enable or disable the Mica backdrop for a window. When enabling, the window's
        /// background and (optionally) the root panel must be transparent for the backdrop
        /// to show through. Returns true if Mica was applied.
        /// </summary>
        public static bool Apply(Window window, bool enable)
        {
            if (window == null) return false;
            if (enable && !IsMicaSupported) return false;

            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return false;

            try
            {
                // Match the DWM dark-mode flag to the app theme so the non-client area blends.
                int dark = ThemeManager.IsDark ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

                int backdrop = enable ? DWMSBT_MAINWINDOW : DWMSBT_NONE;
                int hr = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
                return hr == 0 && enable;
            }
            catch
            {
                return false;
            }
        }
    }
}
