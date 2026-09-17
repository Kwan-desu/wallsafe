using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WallSafeWinUI.Services;
using WallSafeWinUI.Views;

namespace WallSafeWinUI
{
    public sealed partial class MainWindow : Window
    {
        private readonly AppWindow _appWindow;
        private bool _suppressToggleEvent;

        public MainWindow()
        {
            InitializeComponent();

            _appWindow = GetAppWindow();
            Title = "WallSafe";

            ConfigureTitleBar();
            ConfigureWindowChrome();

            // Theme + backdrop.
            if (RootGrid is FrameworkElement fe)
                fe.RequestedTheme = ThemeService.CurrentTheme;
            ThemeService.ApplyBackdrop(this, Settings.Instance.WindowMicaEnabled ? Settings.Instance.BackdropMaterial : "None");
            SetImmersiveDarkTitleBar(IsEffectiveDark());

            _suppressToggleEvent = true;
            switch (Settings.Instance.RatingMode)
            {
                case ContentRatingMode.Questionable: ModeQuestionable.IsChecked = true; break;
                case ContentRatingMode.Explicit: ModeExplicit.IsChecked = true; break;
                default: ModeSfw.IsChecked = true; break;
            }
            _suppressToggleEvent = false;

            ContentFrame.Navigate(typeof(HomePage));

            try
            {
                _appWindow.Resize(new Windows.Graphics.SizeInt32(1280, 820));
            }
            catch { }

            ThemeService.ThemeChanged += t =>
            {
                if (RootGrid is FrameworkElement root) root.RequestedTheme = t;
                SetImmersiveDarkTitleBar(IsEffectiveDark());
            };

            // Closing the window hides to the system tray (background mode) instead of
            // terminating the process — the app keeps running for the panic hotkey,
            // slideshow, Wi-Fi watcher and tray menu. Real exit goes through the tray's
            // "Exit WallSafe" item. This matches the old WPF build's behavior.
            _appWindow.Closing += AppWindow_Closing;
        }

        public bool ForceClosing { get; set; }

        private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            if (ForceClosing) return; // real exit requested via tray
            args.Cancel = true;
            AppSuspensionManager.EnterBackgroundMode(this);
        }

        public bool IsVisibleWindow => _appWindow.IsVisible;

        public void HideWindow() => _appWindow.Hide();

        public void ShowWindow()
        {
            _appWindow.Show();
            SetForegroundWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
        }

        public void NavigateToSettings()
        {
            ContentFrame.Navigate(typeof(SettingsPage));
        }

        public void NavigateFromHomeToExplore()
        {
            foreach (var obj in RootNav.MenuItems)
                if (obj is NavigationViewItem { Tag: "Explore" } item) { RootNav.SelectedItem = item; return; }
            ContentFrame.Navigate(typeof(ExplorePage));
        }

        public void NavigateToFavorites()
        {
            foreach (var obj in RootNav.MenuItems)
                if (obj is NavigationViewItem { Tag: "Favorites" } item) { RootNav.SelectedItem = item; return; }
            ContentFrame.Navigate(typeof(FavoritesPage));
        }

        public void NavigateToDownloads()
        {
            foreach (var obj in RootNav.MenuItems)
                if (obj is NavigationViewItem { Tag: "Downloads" } item) { RootNav.SelectedItem = item; return; }
            ContentFrame.Navigate(typeof(DownloadsPage));
        }

        public void NavigateToHistory()
        {
            foreach (var obj in RootNav.MenuItems)
                if (obj is NavigationViewItem { Tag: "History" } item) { RootNav.SelectedItem = item; return; }
            ContentFrame.Navigate(typeof(HistoryPage));
        }

        public void NavigateToExploreWithTag(string tag)
        {
            foreach (var obj in RootNav.MenuItems)
                if (obj is NavigationViewItem { Tag: "Explore" } item)
                {
                    _suppressToggleEvent = true; // avoid re-entrant selection side effects
                    RootNav.SelectedItem = item;
                    _suppressToggleEvent = false;
                    break;
                }
            ContentFrame.Navigate(typeof(ExplorePage), tag);
        }

        // ── Title bar ─────────────────────────────────────────────
        private void ConfigureTitleBar()
        {
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            if (_appWindow.TitleBar is { } tb)
            {
                tb.ButtonBackgroundColor = Colors.Transparent;
                tb.ButtonInactiveBackgroundColor = Colors.Transparent;
            }
        }

        private AppWindow GetAppWindow()
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var id = Win32Interop.GetWindowIdFromWindow(hWnd);
            return AppWindow.GetFromWindowId(id);
        }

        private bool IsEffectiveDark()
        {
            return ThemeService.CurrentTheme switch
            {
                ElementTheme.Dark => true,
                ElementTheme.Light => false,
                _ => Application.Current.RequestedTheme == ApplicationTheme.Dark
            };
        }

        // ── Navigation ────────────────────────────────────────────
        private void RootNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                ContentFrame.Navigate(typeof(SettingsPage));
                return;
            }

            if (args.SelectedItemContainer is NavigationViewItem { Tag: string tag })
            {
                Type page = tag switch
                {
                    "Explore" => typeof(ExplorePage),
                    "Favorites" => typeof(FavoritesPage),
                    "Downloads" => typeof(DownloadsPage),
                    "History" => typeof(HistoryPage),
                    _ => typeof(HomePage)
                };
                ContentFrame.Navigate(page);
            }
        }

        private void RatingMode_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressToggleEvent) return;
            if (sender is not RadioButton rb || rb.Tag is not string tag) return;
            var mode = tag switch
            {
                "Questionable" => ContentRatingMode.Questionable,
                "Explicit" => ContentRatingMode.Explicit,
                _ => ContentRatingMode.SfwOnly
            };
            if (Settings.Instance.RatingMode == mode) return;
            Settings.Instance.RatingMode = mode;
            Settings.Instance.Save();
            AppEvents.RaiseRatingModeChanged(mode);
        }

        // ── DWM interop ───────────────────────────────────────────
        [DllImport("dwmapi.dll", PreserveSig = false)]
        private static extern void DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        private void ConfigureWindowChrome()
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            try { int pref = DWMWCP_ROUND; DwmSetWindowAttribute(hWnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int)); }
            catch { }
        }

        private void SetImmersiveDarkTitleBar(bool enabled)
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            try { int v = enabled ? 1 : 0; DwmSetWindowAttribute(hWnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref v, sizeof(int)); }
            catch { }
        }
    }
}
