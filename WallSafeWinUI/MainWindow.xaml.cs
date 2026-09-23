using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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

            try
            {
                IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                ShutdownProtectionService.HookWindow(hWnd);
            }
            catch { }

            // Theme + backdrop.
            if (RootGrid is FrameworkElement fe)
                fe.RequestedTheme = ThemeService.CurrentTheme;
            ThemeService.ApplyBackdrop(this, Settings.Instance.WindowMicaEnabled ? Settings.Instance.BackdropMaterial : "None");
            SetImmersiveDarkTitleBar(IsEffectiveDark());

            SyncRatingModeUi();
            RootGrid.Loaded += (_, _) => SyncRatingModeUi();
            AppEvents.RatingModeChanged += _ => SyncRatingModeUi();

            SyncSlideshowUi();
            SyncDiscretionUi();
            AppEvents.DiscretionBlurChanged += _ => SyncDiscretionUi();

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

            RootGrid.Loaded += (_, _) => _ = CheckForUpdatesOnStartupAsync();
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
            SetTitleBar(TitleBarDragArea);
            if (_appWindow.TitleBar is { } tb)
            {
                tb.ButtonBackgroundColor = Colors.Transparent;
                tb.ButtonInactiveBackgroundColor = Colors.Transparent;
                tb.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(25, 128, 128, 128);
                tb.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(40, 128, 128, 128);
            }

            UpdateCaptionPadding();
            _appWindow.Changed += (s, e) =>
            {
                if (e.DidPositionChange || e.DidSizeChange)
                {
                    DispatcherQueue.TryEnqueue(UpdateCaptionPadding);
                }
            };
        }

        private void UpdateCaptionPadding()
        {
            try
            {
                if (_appWindow?.TitleBar is { } tb)
                {
                    double scale = RootGrid?.XamlRoot?.RasterizationScale ?? 1.0;
                    int rightInset = tb.RightInset;
                    if (rightInset > 0)
                    {
                        CaptionPaddingArea.Width = Math.Max(100, rightInset / scale);
                    }
                }
            }
            catch { }
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

        private void SyncRatingModeUi()
        {
            _suppressToggleEvent = true;
            switch (Settings.Instance.RatingMode)
            {
                case ContentRatingMode.Questionable:
                    ModeQuestionable.IsChecked = true;
                    break;
                case ContentRatingMode.Explicit:
                    ModeExplicit.IsChecked = true;
                    break;
                default:
                    ModeSfw.IsChecked = true;
                    break;
            }
            _suppressToggleEvent = false;
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

        // ── Slideshow TitleBar Control ──
        private void SyncSlideshowUi()
        {
            var s = Settings.Instance;
            bool enabled = s.SlideshowEnabled;
            SlideshowLabel.Text = enabled ? "Slideshow: On" : "Slideshow: Off";
            SlideshowIcon.Glyph = enabled ? "\uE768" : "\uE71A";

            var accentBrush = (Brush)Application.Current.Resources["WallSafeAccentBrush"];
            var activeFillBrush = (Brush)Application.Current.Resources["WallSafeActivePillFillBrush"];
            var activeStrokeBrush = (Brush)Application.Current.Resources["WallSafeActivePillStrokeBrush"];
            var defaultFillBrush = (Brush)Application.Current.Resources["ControlFillColorDefaultBrush"];
            var borderBrush = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"];
            var textPrimary = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
            var textSecondary = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];

            if (enabled)
            {
                SlideshowBtn.Background = activeFillBrush;
                SlideshowBtn.BorderBrush = activeStrokeBrush;
                SlideshowLabel.Foreground = textPrimary;
                SlideshowLabel.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
                SlideshowIcon.Foreground = accentBrush;
                SlideshowChevron.Foreground = textSecondary;
            }
            else
            {
                SlideshowBtn.Background = defaultFillBrush;
                SlideshowBtn.BorderBrush = borderBrush;
                SlideshowLabel.Foreground = textSecondary;
                SlideshowLabel.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
                SlideshowIcon.Foreground = textSecondary;
                SlideshowChevron.Foreground = textSecondary;
            }

            _suppressToggleEvent = true;
            FlyoutSlideshowSwitch.IsOn = enabled;

            string src = s.SlideshowSource?.ToLowerInvariant() ?? "favorites";
            foreach (var item in FlyoutSourceCombo.Items)
            {
                if (item is ComboBoxItem cbi && (cbi.Tag as string) == src)
                {
                    FlyoutSourceCombo.SelectedItem = cbi;
                    break;
                }
            }

            int sec = s.SlideshowIntervalSeconds;
            int min = s.SlideshowIntervalMinutes;
            string targetTag = "15m";
            if (sec == 30) targetTag = "30s";
            else if (min == 1 && sec == 0) targetTag = "1m";
            else if (min == 5) targetTag = "5m";
            else if (min == 15) targetTag = "15m";
            else if (min == 30) targetTag = "30m";
            else if (min >= 60) targetTag = "1h";

            foreach (var item in FlyoutIntervalCombo.Items)
            {
                if (item is ComboBoxItem cbi && (cbi.Tag as string) == targetTag)
                {
                    FlyoutIntervalCombo.SelectedItem = cbi;
                    break;
                }
            }
            _suppressToggleEvent = false;
        }

        private void FlyoutSlideshow_Toggled(object sender, RoutedEventArgs e)
        {
            if (_suppressToggleEvent) return;
            var s = Settings.Instance;
            s.SlideshowEnabled = FlyoutSlideshowSwitch.IsOn;
            s.Save();

            if (s.SlideshowEnabled)
                WallpaperManager.Instance.StartSlideshowFromSettings(advanceImmediately: true);
            else
                WallpaperManager.Instance.StopSlideshow();

            SyncSlideshowUi();
        }

        private void FlyoutSource_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressToggleEvent) return;
            if (FlyoutSourceCombo.SelectedItem is ComboBoxItem cbi && cbi.Tag is string tag)
            {
                Settings.Instance.SlideshowSource = tag;
                Settings.Instance.Save();
                WallpaperManager.Instance.ResetSlideshowQueue();
            }
        }

        private void FlyoutInterval_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressToggleEvent) return;
            if (FlyoutIntervalCombo.SelectedItem is ComboBoxItem cbi && cbi.Tag is string tag)
            {
                var s = Settings.Instance;
                switch (tag)
                {
                    case "30s": s.SlideshowIntervalSeconds = 30; s.SlideshowIntervalMinutes = 0; break;
                    case "1m": s.SlideshowIntervalSeconds = 0; s.SlideshowIntervalMinutes = 1; break;
                    case "5m": s.SlideshowIntervalSeconds = 0; s.SlideshowIntervalMinutes = 5; break;
                    case "15m": s.SlideshowIntervalSeconds = 0; s.SlideshowIntervalMinutes = 15; break;
                    case "30m": s.SlideshowIntervalSeconds = 0; s.SlideshowIntervalMinutes = 30; break;
                    case "1h": s.SlideshowIntervalSeconds = 0; s.SlideshowIntervalMinutes = 60; break;
                }
                s.Save();
                if (s.SlideshowEnabled)
                    WallpaperManager.Instance.StartSlideshowFromSettings();
            }
        }

        private void FlyoutNext_Click(object sender, RoutedEventArgs e)
        {
            WallpaperManager.Instance.NextSlideshowWallpaper();
        }

        // ── Discretion Blur TitleBar Control ──
        private void SyncDiscretionUi()
        {
            bool enabled = Settings.Instance.DiscretionBlur;
            TitleDiscretionLabel.Text = enabled ? "Blur: On" : "Blur: Off";
            TitleDiscretionIcon.Glyph = enabled ? "\uED1A" : "\uF78D";

            var accentBrush = (Brush)Application.Current.Resources["WallSafeAccentBrush"];
            var activeFillBrush = (Brush)Application.Current.Resources["WallSafeActivePillFillBrush"];
            var activeStrokeBrush = (Brush)Application.Current.Resources["WallSafeActivePillStrokeBrush"];
            var defaultFillBrush = (Brush)Application.Current.Resources["ControlFillColorDefaultBrush"];
            var borderBrush = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"];
            var textPrimary = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
            var textSecondary = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];

            if (enabled)
            {
                TitleDiscretionBtn.Background = activeFillBrush;
                TitleDiscretionBtn.BorderBrush = activeStrokeBrush;
                TitleDiscretionLabel.Foreground = textPrimary;
                TitleDiscretionLabel.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
                TitleDiscretionIcon.Foreground = accentBrush;
            }
            else
            {
                TitleDiscretionBtn.Background = defaultFillBrush;
                TitleDiscretionBtn.BorderBrush = borderBrush;
                TitleDiscretionLabel.Foreground = textSecondary;
                TitleDiscretionLabel.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
                TitleDiscretionIcon.Foreground = textSecondary;
            }
        }

        private void TitleDiscretionBtn_Click(object sender, RoutedEventArgs e)
        {
            bool nowEnabled = !Settings.Instance.DiscretionBlur;
            Settings.Instance.DiscretionBlur = nowEnabled;
            Settings.Instance.Save();
            SyncDiscretionUi();
            AppEvents.RaiseDiscretionBlurChanged(nowEnabled);
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

        // ── Automatic In-App Updates ──────────────────────────────
        private async System.Threading.Tasks.Task CheckForUpdatesOnStartupAsync()
        {
            try
            {
                await System.Threading.Tasks.Task.Delay(3500);
                var info = await UpdateService.CheckForUpdateAsync();
                if (info != null && info.IsUpdateAvailable && !string.IsNullOrEmpty(info.DownloadUrl))
                {
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        await PromptAndInstallUpdateAsync(info);
                    });
                }
            }
            catch
            {
                // Silent fail on startup check (e.g. offline, rate-limited)
            }
        }

        private async System.Threading.Tasks.Task PromptAndInstallUpdateAsync(UpdateInfo info)
        {
            if (Content?.XamlRoot == null) return;

            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = $"Update Available: WallSafe v{info.LatestVersionString}",
                Content = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = $"A new update (v{info.LatestVersionString}) is available! You are currently running v{info.CurrentVersion}.",
                            TextWrapping = TextWrapping.Wrap,
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                        },
                        new TextBlock
                        {
                            Text = string.IsNullOrWhiteSpace(info.ReleaseNotes)
                                ? "Would you like to download and install this update automatically now?"
                                : info.ReleaseNotes,
                            TextWrapping = TextWrapping.Wrap,
                            MaxHeight = 160
                        }
                    }
                },
                PrimaryButtonText = "Update Now",
                CloseButtonText = "Later",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await PerformAutoUpdateAsync(info);
            }
        }

        private async System.Threading.Tasks.Task PerformAutoUpdateAsync(UpdateInfo info)
        {
            if (Content?.XamlRoot == null) return;

            var progressBar = new ProgressBar { Maximum = 1, Value = 0, IsIndeterminate = true };
            var statusText = new TextBlock
            {
                Text = "Downloading update package...",
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };

            var progressDialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Updating WallSafe...",
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children = { statusText, progressBar }
                }
            };

            var progress = new Progress<double>(p =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    progressBar.IsIndeterminate = false;
                    progressBar.Value = p;
                    statusText.Text = $"Downloading update: {(int)(p * 100)}%...";
                });
            });

            _ = progressDialog.ShowAsync();

            try
            {
                await UpdateService.DownloadAndInstallUpdateAsync(info.DownloadUrl, progress);
            }
            catch (Exception ex)
            {
                progressDialog.Hide();
                var errDialog = new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Update Failed",
                    Content = $"Could not complete automatic update: {ex.Message}",
                    CloseButtonText = "OK"
                };
                await errDialog.ShowAsync();
            }
        }
    }
}
