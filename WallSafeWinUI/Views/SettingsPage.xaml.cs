using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WallSafeWinUI.Services;
using WinRT.Interop;

namespace WallSafeWinUI.Views
{
    public sealed partial class SettingsPage : Page
    {
        private bool _loading = true;

        public SettingsPage()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                LoadState();
                WifiWatcher.Instance.StatusChanged += UpdateWifiStatusUi;
            };
            Unloaded += (_, _) =>
            {
                WifiWatcher.Instance.StatusChanged -= UpdateWifiStatusUi;
            };
        }

        private void LoadState()
        {
            _loading = true;
            var s = Settings.Instance;

            UserNameBox.Text = s.UserName;
            GreetingSwitch.IsOn = s.ShowGreeting;

            SelectTag(ThemeCombo, s.Theme);
            SelectTag(BackdropCombo, s.WindowMicaEnabled ? s.BackdropMaterial : "None");

            // Hotkey (recorder-based)
            UpdateHotkeyLabel();

            PanicHide.IsOn = s.PanicHideApp;
            PanicMinimize.IsOn = s.PanicMinimizeAllWindows;
            PanicMute.IsOn = s.PanicMuteAudio;
            PanicRestore.IsOn = s.PanicRestoreToggle;

            SafeWpText.Text = string.IsNullOrEmpty(s.SafeWallpaperPath) ? "Not set (using Windows default)." : s.SafeWallpaperPath;

            SlideshowSwitch.IsOn = s.SlideshowEnabled;
            IntervalBox.Value = Math.Round(s.SlideshowIntervalEffectiveSeconds / 60.0, 2);
            PopulateSlideshowSources();
            UpdateIntervalHint();

            AutoStart.IsOn = StartupManager.IsStartupEnabled();
            StartMinimized.IsOn = s.StartMinimized;

            DownloadFolderText.Text = DownloadsManager.Instance.DownloadFolder;

            WifiSwitch.IsOn = s.WifiAutoWallpaperEnabled;
            WifiSsidBox.Text = s.WifiTriggerSsid;
            WifiRestore.IsOn = s.WifiRestoreOnDisconnect;
            SafeOnShutdownSwitch.IsOn = s.SafeWallpaperOnShutdown;

            string? currentNet = WifiWatcher.GetPrimaryConnectedNetworkName();
            CurrentWifiNetworkText.Text = !string.IsNullOrEmpty(currentNet) ? currentNet : "None detected";
            UpdateWifiStatusUi(currentNet, WifiWatcher.Instance.IsTriggerActive, null);

            LocationSwitch.IsOn = s.LocationAutoWallpaperEnabled;
            LatBox.Text = s.LocationLatitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            LonBox.Text = s.LocationLongitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            RadiusBox.Text = s.LocationRadiusMeters.ToString();
            LocationRestore.IsOn = s.LocationRestoreOnLeave;

            DiscretionSwitch.IsOn = s.DiscretionBlur;
            BlacklistBox.Text = s.TagBlacklist;
            BlurStrengthSlider.Value = s.BlurStrength;
            UpdateBlurStrengthLabel(s.BlurStrength);

            HomeStatsToggle.IsOn = s.ShowHomeStats;
            HomeHero.IsOn = s.ShowHomeHero;
            HomeQuick.IsOn = s.ShowHomeQuickControls;
            HomeTrending.IsOn = s.ShowHomeTrending;
            HomeFranchises.IsOn = s.ShowHomeFranchises;
            HomeShelf.IsOn = s.ShowHomeQuickShelf;

            if (AppVersionText != null)
                AppVersionText.Text = $"Version {UpdateService.CurrentVersion} · Rebuilt on Windows App SDK 1.6 with Fluent 11 design.";

            _loading = false;
        }

        private static void SelectTag(ComboBox combo, string tag)
        {
            foreach (var o in combo.Items)
                if (o is ComboBoxItem cbi && (cbi.Tag?.ToString() == tag)) { combo.SelectedItem = cbi; return; }
            if (combo.SelectedItem == null && combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        private void UpdateHotkeyLabel() => HotkeyLabel.Text = "Panic hotkey: " + Settings.Instance.GetHotkeyDisplayName();

        // ── Personalization ──
        private void UserName_Changed(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;
            Settings.Instance.UserName = UserNameBox.Text;
            Settings.Instance.Save();
        }

        private void Greeting_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            Settings.Instance.ShowGreeting = GreetingSwitch.IsOn;
            Settings.Instance.Save();
        }

        // ── Appearance ──
        private void Theme_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            string tag = (ThemeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Dark";
            var theme = ThemeService.Parse(tag);
            if (App.RootWindow?.Content is FrameworkElement root)
                ThemeService.ApplyTheme(root, theme);
        }

        private void Backdrop_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            string tag = (BackdropCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Mica";
            Settings.Instance.BackdropMaterial = tag == "None" ? "Mica" : tag;
            Settings.Instance.WindowMicaEnabled = tag != "None";
            Settings.Instance.Save();
            if (App.RootWindow != null) ThemeService.ApplyBackdrop(App.RootWindow, tag);
        }

        // ── Hotkey recorder (max 3 keys) ──
        private bool _recording;

        private void RecordHotkey_Click(object sender, RoutedEventArgs e)
        {
            if (_recording)
            {
                StopRecording();
                return;
            }
            _recording = true;
            RecordHotkeyBtn.Content = "Press keys… (click to stop)";
            RecordHint.Text = "Hold up to 2 modifiers + 1 key, then release.";
            RecordHotkeyBtn.Focus(FocusState.Programmatic);
            // Capture at the page level so modifier+key combos are seen.
            KeyDown += Recorder_KeyDown;
        }

        private void StopRecording()
        {
            _recording = false;
            KeyDown -= Recorder_KeyDown;
            RecordHotkeyBtn.Content = "Record shortcut";
            RecordHint.Text = "Up to 3 keys, e.g. Ctrl + Shift + W";
        }

        private void Recorder_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            var vk = (int)e.Key;
            // Ignore standalone modifier presses; wait for the final non-modifier key.
            if (IsModifierVk(vk)) { e.Handled = true; return; }

            var mods = ModifierKeys.None;
            if (IsDown(Windows.System.VirtualKey.Control)) mods |= ModifierKeys.Control;
            if (IsDown(Windows.System.VirtualKey.Shift)) mods |= ModifierKeys.Shift;
            if (IsDown(Windows.System.VirtualKey.Menu)) mods |= ModifierKeys.Alt;
            if (IsDown(Windows.System.VirtualKey.LeftWindows) || IsDown(Windows.System.VirtualKey.RightWindows))
                mods |= ModifierKeys.Win;

            // Enforce "max 3 keys": at most 2 modifiers + the 1 final key.
            int modCount = CountBits((int)mods);
            if (modCount > 2)
            {
                // Trim to the first two modifiers in a stable priority order.
                var trimmed = ModifierKeys.None;
                if (mods.HasFlag(ModifierKeys.Control) && CountBits((int)trimmed) < 2) trimmed |= ModifierKeys.Control;
                if (mods.HasFlag(ModifierKeys.Shift) && CountBits((int)trimmed) < 2) trimmed |= ModifierKeys.Shift;
                if (mods.HasFlag(ModifierKeys.Alt) && CountBits((int)trimmed) < 2) trimmed |= ModifierKeys.Alt;
                if (mods.HasFlag(ModifierKeys.Win) && CountBits((int)trimmed) < 2) trimmed |= ModifierKeys.Win;
                mods = trimmed;
            }

            var s = Settings.Instance;
            s.HotkeyModifiers = (int)mods;
            s.HotkeyKey = vk;
            s.Save();
            UpdateHotkeyLabel();
            try { App.ReRegisterHotkey(); } catch { }

            e.Handled = true;
            StopRecording();
        }

        private static bool IsModifierVk(int vk)
        {
            // VK_SHIFT/CONTROL/MENU and L/R variants, plus Windows keys.
            return vk is 0x10 or 0x11 or 0x12 or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5 or 0x5B or 0x5C;
        }

        private static bool IsDown(Windows.System.VirtualKey key)
        {
            var state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key);
            return state.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        }

        private static int CountBits(int v)
        {
            int c = 0;
            while (v != 0) { c += v & 1; v >>= 1; }
            return c;
        }

        // ── Panic ──
        private void Panic_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            var s = Settings.Instance;
            s.PanicHideApp = PanicHide.IsOn;
            s.PanicMinimizeAllWindows = PanicMinimize.IsOn;
            s.PanicMuteAudio = PanicMute.IsOn;
            s.PanicRestoreToggle = PanicRestore.IsOn;
            s.Save();
        }

        // ── Safe wallpaper ──
        private async void PickSafeWallpaper_Click(object sender, RoutedEventArgs e)
        {
            var file = await PickFileAsync(new[] { ".jpg", ".jpeg", ".png", ".bmp" });
            if (file != null)
            {
                WallpaperManager.Instance.SetSafeWallpaper(file);
                SafeWpText.Text = file;
            }
        }

        // ── Slideshow ──
        private void Slideshow_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            Settings.Instance.SlideshowEnabled = SlideshowSwitch.IsOn;
            Settings.Instance.Save();
            if (SlideshowSwitch.IsOn)
                WallpaperManager.Instance.StartSlideshowFromSettings(true);
            else
                WallpaperManager.Instance.StopSlideshow();
        }

        private void Interval_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_loading) return;
            double minutes = double.IsNaN(sender.Value) ? 0 : sender.Value;
            if (minutes < 0.1) minutes = 0.1;
            ApplyInterval(minutes);
        }

        private void IntervalPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string tag &&
                double.TryParse(tag, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var minutes))
            {
                IntervalBox.Value = minutes; // triggers Interval_ValueChanged
            }
        }

        private void ApplyInterval(double minutes)
        {
            int totalSeconds = Math.Max(6, (int)Math.Round(minutes * 60));
            Settings.Instance.SlideshowIntervalSeconds = totalSeconds;
            Settings.Instance.SlideshowIntervalMinutes = Math.Max(1, (int)Math.Round(minutes));
            Settings.Instance.Save();
            UpdateIntervalHint();
            if (Settings.Instance.SlideshowEnabled)
                WallpaperManager.Instance.StartSlideshowFromSettings();
        }

        private void UpdateIntervalHint()
        {
            double secs = Settings.Instance.SlideshowIntervalEffectiveSeconds;
            IntervalHint.Text = secs < 60
                ? $"Changes wallpaper every {secs:0.#} seconds."
                : $"Changes wallpaper every {secs / 60.0:0.##} minutes.";
        }

        private void PopulateSlideshowSources()
        {
            SlideshowSourceCombo.Items.Clear();

            var favItem = new ComboBoxItem { Content = "All Favorites", Tag = "favorites" };
            SlideshowSourceCombo.Items.Add(favItem);

            var dlItem = new ComboBoxItem { Content = "Downloads folder", Tag = "downloads" };
            SlideshowSourceCombo.Items.Add(dlItem);

            var collections = FavoritesManager.Instance.GetCollections();
            foreach (var col in collections)
            {
                if (string.IsNullOrWhiteSpace(col)) continue;
                int count = FavoritesManager.Instance.GetFavoritesByCollection(col).Count;
                SlideshowSourceCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"📁 {col} ({count})",
                    Tag = $"collection:{col}"
                });
            }

            string currentSrc = Settings.Instance.SlideshowSource ?? "favorites";
            ComboBoxItem? matched = null;
            foreach (var it in SlideshowSourceCombo.Items)
            {
                if (it is ComboBoxItem cbi && string.Equals(cbi.Tag as string, currentSrc, StringComparison.OrdinalIgnoreCase))
                {
                    matched = cbi;
                    break;
                }
            }
            SlideshowSourceCombo.SelectedItem = matched ?? favItem;
        }

        private void Slideshow_Config(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            Settings.Instance.SlideshowSource = (SlideshowSourceCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "favorites";
            Settings.Instance.Save();
            WallpaperManager.Instance.ResetSlideshowQueue();
            if (Settings.Instance.SlideshowEnabled)
                WallpaperManager.Instance.StartSlideshowFromSettings(advanceImmediately: true);
            App.RootWindow?.SyncSlideshowUi();
        }

        // ── Startup ──
        private void AutoStart_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            StartupManager.SetStartup(AutoStart.IsOn);
            Settings.Instance.AutoStartWithWindows = AutoStart.IsOn;
            Settings.Instance.Save();
        }

        private void StartMin_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            Settings.Instance.StartMinimized = StartMinimized.IsOn;
            Settings.Instance.Save();
        }

        // ── Downloads folder ──
        private async void PickDownloadFolder_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.RootWindow));
            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                DownloadsManager.Instance.DownloadFolder = folder.Path;
                DownloadFolderText.Text = folder.Path;
            }
        }

        // ── Wi-Fi ──
        private void Wifi_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            Settings.Instance.WifiAutoWallpaperEnabled = WifiSwitch.IsOn;
            Settings.Instance.WifiRestoreOnDisconnect = WifiRestore.IsOn;
            Settings.Instance.Save();
            WifiWatcher.Instance.Start(immediate: true);
            string? currentNet = WifiWatcher.GetPrimaryConnectedNetworkName();
            UpdateWifiStatusUi(currentNet, WifiWatcher.Instance.IsTriggerActive, null);
        }

        private void SafeOnShutdown_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            Settings.Instance.SafeWallpaperOnShutdown = SafeOnShutdownSwitch.IsOn;
            Settings.Instance.Save();
        }

        private void Wifi_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;
            Settings.Instance.WifiTriggerSsid = WifiSsidBox.Text;
            Settings.Instance.Save();
            if (Settings.Instance.WifiAutoWallpaperEnabled)
            {
                WifiWatcher.Instance.TriggerImmediatePoll();
            }
        }

        private void AddCurrentNetwork_Click(object sender, RoutedEventArgs e)
        {
            string? current = WifiWatcher.GetPrimaryConnectedNetworkName();
            if (string.IsNullOrWhiteSpace(current)) return;

            string existing = WifiSsidBox.Text ?? "";
            if (!existing.Contains(current, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(existing))
                    WifiSsidBox.Text = current;
                else
                    WifiSsidBox.Text = existing.TrimEnd() + Environment.NewLine + current;
            }
        }

        private void UpdateWifiStatusUi(string? currentNetwork, bool isTriggerActive, string? matchedTrigger)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                CurrentWifiNetworkText.Text = !string.IsNullOrEmpty(currentNetwork) ? currentNetwork : "None detected";

                if (!Settings.Instance.WifiAutoWallpaperEnabled)
                {
                    WifiStatusInfoBar.IsOpen = false;
                    return;
                }

                if (isTriggerActive)
                {
                    WifiStatusInfoBar.IsOpen = true;
                    if (WifiWatcher.Instance.UserBypassedTrigger)
                    {
                        WifiStatusInfoBar.Severity = InfoBarSeverity.Warning;
                        WifiStatusInfoBar.Title = "Trigger Bypassed (Normal Wallpaper Visible)";
                        WifiStatusInfoBar.Message = $"Connected to trigger network '{(matchedTrigger ?? currentNetwork)}', but safe wallpaper was temporarily bypassed via the panic shortcut. Press the panic shortcut to re-hide.";
                    }
                    else
                    {
                        WifiStatusInfoBar.Severity = InfoBarSeverity.Success;
                        WifiStatusInfoBar.Title = "Safe Wallpaper Active";
                        WifiStatusInfoBar.Message = $"Connected to trigger network '{(matchedTrigger ?? currentNetwork)}'. Safe wallpaper applied. Press the panic shortcut to temporarily view your normal wallpaper.";
                    }
                }
                else
                {
                    WifiStatusInfoBar.IsOpen = true;
                    WifiStatusInfoBar.Severity = InfoBarSeverity.Informational;
                    WifiStatusInfoBar.Title = "Monitoring Network";
                    WifiStatusInfoBar.Message = !string.IsNullOrEmpty(currentNetwork)
                        ? $"Connected to '{currentNetwork}'. Not matching any trigger network."
                        : "No active network connection detected.";
                }
            });
        }

        // ── Location ──
        private void Location_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            Settings.Instance.LocationAutoWallpaperEnabled = LocationSwitch.IsOn;
            Settings.Instance.LocationRestoreOnLeave = LocationRestore.IsOn;
            Settings.Instance.Save();
            _ = LocationWatcher.Instance.StartAsync();
        }

        private void Location_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            if (double.TryParse(LatBox.Text, System.Globalization.NumberStyles.Float, ci, out var lat))
                Settings.Instance.LocationLatitude = lat;
            if (double.TryParse(LonBox.Text, System.Globalization.NumberStyles.Float, ci, out var lon))
                Settings.Instance.LocationLongitude = lon;
            if (int.TryParse(RadiusBox.Text, out var r))
                Settings.Instance.LocationRadiusMeters = Math.Max(20, r);
            Settings.Instance.Save();
        }

        private async void UseCurrentLocation_Click(object sender, RoutedEventArgs e)
        {
            LocationStatus.Text = "Getting location…";
            var pos = await LocationWatcher.GetCurrentAsync();
            if (pos is { } p)
            {
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                LatBox.Text = p.Lat.ToString("0.######", ci);
                LonBox.Text = p.Lon.ToString("0.######", ci);
                Settings.Instance.LocationLatitude = p.Lat;
                Settings.Instance.LocationLongitude = p.Lon;
                Settings.Instance.Save();
                LocationStatus.Text = "Captured current location.";
            }
            else
            {
                LocationStatus.Text = "Location unavailable (permission denied or disabled).";
            }
        }

        // ── Taskbar ──
        // ── Discretion ──
        private void Discretion_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            Settings.Instance.DiscretionBlur = DiscretionSwitch.IsOn;
            Settings.Instance.Save();
            AppEvents.RaiseDiscretionBlurChanged(DiscretionSwitch.IsOn);
        }

        private void Blacklist_Changed(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;
            Settings.Instance.TagBlacklist = BlacklistBox.Text;
            Settings.Instance.Save();
        }

        private void BlurStrength_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_loading) return;
            int val = (int)e.NewValue;
            Settings.Instance.BlurStrength = val;
            Settings.Instance.Save();
            UpdateBlurStrengthLabel(val);
            AppEvents.RaiseDiscretionBlurChanged(Settings.Instance.DiscretionBlur);
        }

        private void UpdateBlurStrengthLabel(int value)
        {
            string desc = value switch
            {
                <= 5 => "Subtle",
                <= 8 => "Balanced",
                <= 12 => "Heavy",
                _ => "Maximum"
            };
            if (BlurStrengthDesc != null) BlurStrengthDesc.Text = $"Controls how much sensitive thumbnails are blurred ({desc})";
            if (BlurStrengthValue != null) BlurStrengthValue.Text = $"{value}";
        }

        // ── Home dashboard sections ──
        private void Home_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            var s = Settings.Instance;
            s.ShowHomeStats = HomeStatsToggle.IsOn;
            s.ShowHomeHero = HomeHero.IsOn;
            s.ShowHomeQuickControls = HomeQuick.IsOn;
            s.ShowHomeTrending = HomeTrending.IsOn;
            s.ShowHomeFranchises = HomeFranchises.IsOn;
            s.ShowHomeQuickShelf = HomeShelf.IsOn;
            s.Save();
        }

        // ── File picker helper ──
        private static async System.Threading.Tasks.Task<string?> PickFileAsync(string[] extensions)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            foreach (var ext in extensions) picker.FileTypeFilter.Add(ext);
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.RootWindow));
            var file = await picker.PickSingleFileAsync();
            return file?.Path;
        }

        // ── OTA Updates ──
        private UpdateInfo? _latestUpdateInfo;

        private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            CheckUpdateBtn.IsEnabled = false;
            UpdateLoader.Visibility = Visibility.Visible;
            UpdateLoader.IsActive = true;
            UpdateStatusText.Text = "Checking GitHub for updates...";
            UpdateAvailableCard.Visibility = Visibility.Collapsed;

            try
            {
                var info = await UpdateService.CheckForUpdateAsync();
                _latestUpdateInfo = info;

                if (info.IsUpdateAvailable)
                {
                    UpdateStatusText.Text = $"Update available: v{info.LatestVersionString}!";
                    UpdateTitleText.Text = info.ReleaseTitle;
                    UpdateNotesText.Text = string.IsNullOrWhiteSpace(info.ReleaseNotes) ? "New performance and feature updates." : info.ReleaseNotes;
                    InstallUpdateBtn.Visibility = string.IsNullOrEmpty(info.DownloadUrl) ? Visibility.Collapsed : Visibility.Visible;
                    UpdateAvailableCard.Visibility = Visibility.Visible;
                }
                else
                {
                    UpdateStatusText.Text = $"You're up to date! WallSafe v{info.CurrentVersion} is the latest version.";
                }
            }
            catch (Exception ex)
            {
                UpdateStatusText.Text = $"Check failed: {ex.Message}";
            }
            finally
            {
                UpdateLoader.IsActive = false;
                UpdateLoader.Visibility = Visibility.Collapsed;
                CheckUpdateBtn.IsEnabled = true;
            }
        }

        private async void InstallUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_latestUpdateInfo == null || string.IsNullOrEmpty(_latestUpdateInfo.DownloadUrl))
                return;

            InstallUpdateBtn.IsEnabled = false;
            UpdateProgressBar.Visibility = Visibility.Visible;
            UpdateProgressBar.Value = 0;
            UpdateStatusText.Text = "Downloading update package...";

            var progress = new Progress<double>(p =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    UpdateProgressBar.Value = p;
                    UpdateStatusText.Text = $"Downloading update: {(int)(p * 100)}%...";
                });
            });

            try
            {
                await UpdateService.DownloadAndInstallUpdateAsync(_latestUpdateInfo.DownloadUrl, progress);
            }
            catch (Exception ex)
            {
                UpdateStatusText.Text = $"Installation failed: {ex.Message}";
                InstallUpdateBtn.IsEnabled = true;
                UpdateProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private async void ViewRelease_Click(object sender, RoutedEventArgs e)
        {
            string url = _latestUpdateInfo?.ReleaseUrl ?? "https://github.com/Kwan-desu/wallsafe/releases";
            try { await Windows.System.Launcher.LaunchUriAsync(new Uri(url)); }
            catch { }
        }
    }
}
