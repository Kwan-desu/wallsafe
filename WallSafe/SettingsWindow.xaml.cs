using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using WinFormsKeys = System.Windows.Forms.Keys;

namespace WallSafe
{
    public partial class SettingsWindow : Window
    {
        private bool _isInitializing = true;
        private bool _isRecordingHotkey = false;
        private int _recordedModifiers;
        private int _recordedKey;

        public SettingsWindow()
        {
            InitializeComponent();
            PopulateCombos();
            LoadCurrentSettings();
            RefreshCacheInfo();
            RefreshDownloadFolderInfo();
            _isInitializing = false;
            UpdateSafeImagePreview();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        // ─────────────── Taskbar Appearance (merged TranslucentTB engine) ───────────────

        /// <summary>Parse a #AARRGGBB or #RRGGBB hex string into a 0xAARRGGBB uint. Returns false if invalid.</summary>
        private static bool TryParseArgb(string? text, out uint argb)
        {
            argb = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var hex = text.Trim().TrimStart('#');
            if (hex.Length == 6) hex = "FF" + hex; // assume opaque if alpha omitted
            if (hex.Length != 8) return false;
            return uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out argb);
        }

        private static void UpdateSwatch(System.Windows.Controls.Border? swatch, string? colorText)
        {
            if (swatch == null) return;
            if (TryParseArgb(colorText, out uint argb))
            {
                var c = System.Windows.Media.Color.FromArgb(
                    (byte)((argb >> 24) & 0xFF), (byte)((argb >> 16) & 0xFF),
                    (byte)((argb >> 8) & 0xFF), (byte)(argb & 0xFF));
                swatch.Background = new System.Windows.Media.SolidColorBrush(c);
            }
        }

        /// <summary>Load one dynamic-state appearance into its controls.</summary>
        private static void LoadStateAppearance(TaskbarStateAppearance? a,
            System.Windows.Controls.CheckBox? chk, System.Windows.Controls.ComboBox? combo,
            System.Windows.Controls.TextBox? colorBox, System.Windows.Controls.Border? swatch,
            System.Windows.Controls.CheckBox? noTint = null)
        {
            a ??= new TaskbarStateAppearance();
            if (chk != null) chk.IsChecked = a.Enabled;
            if (combo != null) combo.SelectedIndex = System.Math.Clamp(a.AccentState, 0, 4);
            if (colorBox != null) colorBox.Text = "#" + unchecked((uint)a.ColorArgb).ToString("X8");
            if (noTint != null) noTint.IsChecked = a.NoTint;
            if (colorBox != null) colorBox.IsEnabled = !(noTint?.IsChecked ?? false);
            UpdateSwatch(swatch, colorBox?.Text);
        }

        /// <summary>Read one dynamic-state appearance from its controls back into settings.</summary>
        private static void SaveStateAppearance(TaskbarStateAppearance a,
            System.Windows.Controls.CheckBox? chk, System.Windows.Controls.ComboBox? combo,
            System.Windows.Controls.TextBox? colorBox, System.Windows.Controls.CheckBox? noTint = null)
        {
            a.Enabled = chk?.IsChecked == true;
            if (combo?.SelectedItem is ComboBoxItem item && item.Tag is string tag && int.TryParse(tag, out int sv))
                a.AccentState = System.Math.Clamp(sv, 0, 4);
            if (TryParseArgb(colorBox?.Text, out uint argb))
                a.ColorArgb = unchecked((int)argb);
            a.NoTint = noTint?.IsChecked == true;
        }

        /// <summary>Live-apply: persist all taskbar states from the UI and re-evaluate the engine.</summary>
        private void TaskbarLiveApply_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            // Grey out the color box + swatch when "No tint" is active for that state.
            if (StateDesktopColor != null) StateDesktopColor.IsEnabled = !(ChkStateDesktopNoTint?.IsChecked ?? false);
            if (StateVisibleColor != null) StateVisibleColor.IsEnabled = !(ChkStateVisibleNoTint?.IsChecked ?? false);
            if (StateMaximizedColor != null) StateMaximizedColor.IsEnabled = !(ChkStateMaximizedNoTint?.IsChecked ?? false);
            if (StateStartColor != null) StateStartColor.IsEnabled = !(ChkStateStartNoTint?.IsChecked ?? false);

            // Keep swatches in sync.
            UpdateSwatch(StateDesktopSwatch, StateDesktopColor?.Text);
            UpdateSwatch(StateVisibleSwatch, StateVisibleColor?.Text);
            UpdateSwatch(StateMaximizedSwatch, StateMaximizedColor?.Text);
            UpdateSwatch(StateStartSwatch, StateStartColor?.Text);

            var s = Settings.Instance;
            s.TaskbarTransparencyEnabled = ChkTaskbarEnabled?.IsChecked == true;
            SaveStateAppearance(s.DesktopAppearance ??= new(), ChkStateDesktop, StateDesktopCombo, StateDesktopColor, ChkStateDesktopNoTint);
            SaveStateAppearance(s.VisibleWindowAppearance ??= new(), ChkStateVisible, StateVisibleCombo, StateVisibleColor, ChkStateVisibleNoTint);
            SaveStateAppearance(s.MaximizedWindowAppearance ??= new(), ChkStateMaximized, StateMaximizedCombo, StateMaximizedColor, ChkStateMaximizedNoTint);
            SaveStateAppearance(s.StartOpenedAppearance ??= new(), ChkStateStart, StateStartCombo, StateStartColor, ChkStateStartNoTint);
            s.Save();

            TaskbarManager.Instance.ApplyFromSettings();
            (Owner as MainWindow)?.UpdateTaskbarQuickToggle();
        }

        // ─────────────── Interface: density + Mica ───────────────

        private void SlideshowInterval_Changed(object sender, SelectionChangedEventArgs e)
        {
            UpdateCustomIntervalVisibility();
        }

        private void UpdateCustomIntervalVisibility()
        {
            if (CustomIntervalPanel == null) return;
            bool custom = SlideshowIntervalCombo?.SelectedItem is ComboBoxItem ci && ci.Tag is int t && t == -1;
            CustomIntervalPanel.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
        }

        private void NumericOnly_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private void Density_Checked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            string density = "comfortable";
            if (DensityCompactRadio?.IsChecked == true) density = "compact";
            else if (DensityLargeRadio?.IsChecked == true) density = "large";

            Settings.Instance.CardDensity = density;
            Settings.Instance.Save();

            if (Owner is MainWindow mw)
                mw.RefreshGridDensity();
        }

        private void WindowMica_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            Settings.Instance.WindowMicaEnabled = ChkWindowMica?.IsChecked == true;
            Settings.Instance.Save();

            if (Owner is MainWindow mw)
                mw.ApplyWindowBackdrop();
        }

        // ─────────────── Wi-Fi auto wallpaper ───────────────

        private void WifiAuto_Changed(object sender, RoutedEventArgs e)
        {
            // Live apply happens on save/close; nothing required here beyond letting the user toggle.
        }

        private void WifiUseCurrent_Click(object sender, RoutedEventArgs e)
        {
            var ssid = WifiWatcher.GetCurrentSsid();
            if (!string.IsNullOrEmpty(ssid) && WifiSsidBox != null)
            {
                WifiSsidBox.Text = ssid;
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Couldn't detect a connected Wi-Fi network. Make sure you're connected, then try again.",
                    "Wi-Fi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void WifiBrowseWallpaper_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose a wallpaper for the trigger network",
                Filter = "Images (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true && WifiWallpaperBox != null)
            {
                WifiWallpaperBox.Text = dlg.FileName;
            }
        }

        private void SettingsRootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is UIElement elem && e.NewSize.Width > 0 && e.NewSize.Height > 0)
            {
                elem.Clip = new System.Windows.Media.RectangleGeometry(
                    new Rect(0, 0, e.NewSize.Width, e.NewSize.Height), 10, 10);
            }
        }

        private void NavSec_Checked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            // Hide all panels
            if (SecPanelHome != null) SecPanelHome.Visibility = Visibility.Collapsed;
            if (SecPanelPanic != null) SecPanelPanic.Visibility = Visibility.Collapsed;
            if (SecPanelSlideshow != null) SecPanelSlideshow.Visibility = Visibility.Collapsed;
            if (SecPanelSources != null) SecPanelSources.Visibility = Visibility.Collapsed;
            if (SecPanelGeneral != null) SecPanelGeneral.Visibility = Visibility.Collapsed;
            if (SecPanelTaskbar != null) SecPanelTaskbar.Visibility = Visibility.Collapsed;

            if (NavSecHome?.IsChecked == true)
            {
                if (SecPanelHome != null) SecPanelHome.Visibility = Visibility.Visible;
                if (SectionHeaderTitle != null) SectionHeaderTitle.Text = "HOME DASHBOARD";
                if (SectionHeaderSubtitle != null) SectionHeaderSubtitle.Text = "Customize which sections and widgets appear on your Home page.";
            }
            else if (NavSecPanic?.IsChecked == true)
            {
                if (SecPanelPanic != null) SecPanelPanic.Visibility = Visibility.Visible;
                if (SectionHeaderTitle != null) SectionHeaderTitle.Text = "PANIC & STEALTH SUITE";
                if (SectionHeaderSubtitle != null) SectionHeaderSubtitle.Text = "Configure neutral safe wallpapers, 3-key global shortcuts, and concealment actions.";
            }
            else if (NavSecSlideshow?.IsChecked == true)
            {
                if (SecPanelSlideshow != null) SecPanelSlideshow.Visibility = Visibility.Visible;
                if (SectionHeaderTitle != null) SectionHeaderTitle.Text = "AUTOMATED SLIDESHOW ENGINE";
                if (SectionHeaderSubtitle != null) SectionHeaderSubtitle.Text = "Set rotation intervals, source pools, and display monitor targets.";
            }
            else if (NavSecSources?.IsChecked == true)
            {
                if (SecPanelSources != null) SecPanelSources.Visibility = Visibility.Visible;
                if (SectionHeaderTitle != null) SectionHeaderTitle.Text = "IMAGE SOURCES & CONTENT SAFETY";
                if (SectionHeaderSubtitle != null) SectionHeaderSubtitle.Text = "Manage SFW/NSFW booru ratings, custom board endpoints, and tag blacklists.";
            }
            else if (NavSecGeneral?.IsChecked == true)
            {
                if (SecPanelGeneral != null) SecPanelGeneral.Visibility = Visibility.Visible;
                if (SectionHeaderTitle != null) SectionHeaderTitle.Text = "GENERAL & SYSTEM STORAGE";
                if (SectionHeaderSubtitle != null) SectionHeaderSubtitle.Text = "Configure theme mode, Windows startup behavior, and local wallpaper cache.";
            }
            else if (NavSecTaskbar?.IsChecked == true)
            {
                if (SecPanelTaskbar != null) SecPanelTaskbar.Visibility = Visibility.Visible;
                if (SectionHeaderTitle != null) SectionHeaderTitle.Text = "TASKBAR APPEARANCE";
                if (SectionHeaderSubtitle != null) SectionHeaderSubtitle.Text = "Make the Windows taskbar translucent, blurred or acrylic with a custom tint color.";
            }
        }

        private void ShowAllHomeSections_Click(object sender, RoutedEventArgs e)
        {
            if (ChkShowHomeHero != null) ChkShowHomeHero.IsChecked = true;
            if (ChkShowHomeQuickControls != null) ChkShowHomeQuickControls.IsChecked = true;
            if (ChkShowHomeTrending != null) ChkShowHomeTrending.IsChecked = true;
            if (ChkShowHomeFranchises != null) ChkShowHomeFranchises.IsChecked = true;
            if (ChkShowHomeQuickShelf != null) ChkShowHomeQuickShelf.IsChecked = true;
        }

        private void HideAllHomeSections_Click(object sender, RoutedEventArgs e)
        {
            if (ChkShowHomeHero != null) ChkShowHomeHero.IsChecked = false;
            if (ChkShowHomeQuickControls != null) ChkShowHomeQuickControls.IsChecked = false;
            if (ChkShowHomeTrending != null) ChkShowHomeTrending.IsChecked = false;
            if (ChkShowHomeFranchises != null) ChkShowHomeFranchises.IsChecked = false;
            if (ChkShowHomeQuickShelf != null) ChkShowHomeQuickShelf.IsChecked = false;
        }

        private void PopulateCombos()
        {
            // Slideshow intervals
            SlideshowIntervalCombo.Items.Clear();
            SlideshowIntervalCombo.Items.Add(new ComboBoxItem { Content = "5 Minutes", Tag = 5 });
            SlideshowIntervalCombo.Items.Add(new ComboBoxItem { Content = "15 Minutes (Default)", Tag = 15 });
            SlideshowIntervalCombo.Items.Add(new ComboBoxItem { Content = "30 Minutes", Tag = 30 });
            SlideshowIntervalCombo.Items.Add(new ComboBoxItem { Content = "1 Hour", Tag = 60 });
            SlideshowIntervalCombo.Items.Add(new ComboBoxItem { Content = "4 Hours", Tag = 240 });
            SlideshowIntervalCombo.Items.Add(new ComboBoxItem { Content = "Custom…", Tag = -1 });

            // Monitors
            MonitorTargetCombo.Items.Clear();
            MonitorTargetCombo.Items.Add(new ComboBoxItem { Content = "All Attached Displays", Tag = -1 });

            uint monitorCount = WallpaperManager.Instance.GetMonitorCount();
            for (int i = 0; i < monitorCount; i++)
            {
                MonitorTargetCombo.Items.Add(new ComboBoxItem { Content = $"Monitor #{i + 1}", Tag = i });
            }
        }

        private void LoadCurrentSettings()
        {
            var s = Settings.Instance;
            SafePathBox.Text = s.SafeWallpaperPath;

            _recordedModifiers = s.HotkeyModifiers;
            _recordedKey = s.HotkeyKey;
            UpdateHotkeyDisplay();

            // Home Dashboard Section Visibility
            ChkShowHomeHero.IsChecked = s.ShowHomeHero;
            ChkShowHomeQuickControls.IsChecked = s.ShowHomeQuickControls;
            ChkShowHomeTrending.IsChecked = s.ShowHomeTrending;
            ChkShowHomeFranchises.IsChecked = s.ShowHomeFranchises;
            ChkShowHomeQuickShelf.IsChecked = s.ShowHomeQuickShelf;

            // Startup & Background
            ChkAutoStart.IsChecked = s.AutoStartWithWindows || StartupManager.IsStartupEnabled();
            ChkStartMinimized.IsChecked = s.StartMinimized;
            UpdateAutoStartDependencies();

            // Stealth Suite
            ChkPanicHideApp.IsChecked = s.PanicHideApp;
            ChkPanicMinimize.IsChecked = s.PanicMinimizeAllWindows;
            ChkPanicMute.IsChecked = s.PanicMuteAudio;
            ChkPanicRestore.IsChecked = s.PanicRestoreToggle;

            // Wi-Fi auto wallpaper
            if (ChkWifiAuto != null) ChkWifiAuto.IsChecked = s.WifiAutoWallpaperEnabled;
            if (WifiSsidBox != null) WifiSsidBox.Text = s.WifiTriggerSsid;
            if (WifiWallpaperBox != null) WifiWallpaperBox.Text = s.WifiTriggerWallpaperPath;
            if (ChkWifiRestore != null) ChkWifiRestore.IsChecked = s.WifiRestoreOnDisconnect;

            // Slideshow Engine
            ChkSlideshow.IsChecked = s.SlideshowEnabled;

            foreach (ComboBoxItem item in SlideshowIntervalCombo.Items)
            {
                if (item.Tag is int val && val == s.SlideshowIntervalMinutes)
                {
                    SlideshowIntervalCombo.SelectedItem = item;
                    break;
                }
            }
            if (SlideshowIntervalCombo.SelectedItem == null)
            {
                // No preset matched → it's a custom interval; select "Custom…" and fill the box.
                foreach (ComboBoxItem item in SlideshowIntervalCombo.Items)
                {
                    if (item.Tag is int t && t == -1) { SlideshowIntervalCombo.SelectedItem = item; break; }
                }
                if (CustomIntervalBox != null) CustomIntervalBox.Text = s.SlideshowIntervalMinutes.ToString();
            }
            UpdateCustomIntervalVisibility();

            // Add each favorite collection as a slideshow source option.
            // (Remove any previously-added collection items first to avoid duplicates.)
            for (int i = SlideshowSourceCombo.Items.Count - 1; i >= 0; i--)
            {
                if (SlideshowSourceCombo.Items[i] is ComboBoxItem ci && (ci.Tag as string)?.StartsWith("collection:") == true)
                    SlideshowSourceCombo.Items.RemoveAt(i);
            }
            foreach (var col in FavoritesManager.Instance.GetCollections())
            {
                SlideshowSourceCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"Collection: {col}",
                    Tag = "collection:" + col
                });
            }

            foreach (ComboBoxItem item in SlideshowSourceCombo.Items)
            {
                if (item.Tag as string == s.SlideshowSource)
                {
                    SlideshowSourceCombo.SelectedItem = item;
                    break;
                }
            }

            foreach (ComboBoxItem item in MonitorTargetCombo.Items)
            {
                if (item.Tag is int val && val == s.TargetMonitor)
                {
                    MonitorTargetCombo.SelectedItem = item;
                    break;
                }
            }
            if (MonitorTargetCombo.SelectedItem == null)
                MonitorTargetCombo.SelectedIndex = 0;

            // Discretion & Blacklist
            ChkDiscretion.IsChecked = s.DiscretionBlur;
            BlacklistBox.Text = s.TagBlacklist;

            // Sources & Rating Mode
            if (ChkCustomFilterProfile != null)
                ChkCustomFilterProfile.IsChecked = s.CustomFilterProfileEnabled;
            if (ComboItemCustom != null)
                ComboItemCustom.Visibility = s.CustomFilterProfileEnabled ? Visibility.Visible : Visibility.Collapsed;
            if (RatingModeCombo != null)
                RatingModeCombo.SelectedIndex = Math.Clamp((int)s.RatingMode, 0, s.CustomFilterProfileEnabled ? 3 : 2);
            ChkAllowAllSources.IsChecked = s.AllowAllSources;
            RefreshCustomSourcesList();

            // Appearance & Theme
            if (s.Theme == "Light")
                ThemeLightRadio.IsChecked = true;
            else
                ThemeDarkRadio.IsChecked = true;

            // Taskbar Appearance (merged TranslucentTB engine)
            if (ChkTaskbarEnabled != null) ChkTaskbarEnabled.IsChecked = s.TaskbarTransparencyEnabled;
            if (ChkTaskbarRestoreOnPanic != null)
                ChkTaskbarRestoreOnPanic.IsChecked = s.TaskbarRestoreOnPanic;

            LoadStateAppearance(s.DesktopAppearance, ChkStateDesktop, StateDesktopCombo, StateDesktopColor, StateDesktopSwatch, ChkStateDesktopNoTint);
            LoadStateAppearance(s.VisibleWindowAppearance, ChkStateVisible, StateVisibleCombo, StateVisibleColor, StateVisibleSwatch, ChkStateVisibleNoTint);
            LoadStateAppearance(s.MaximizedWindowAppearance, ChkStateMaximized, StateMaximizedCombo, StateMaximizedColor, StateMaximizedSwatch, ChkStateMaximizedNoTint);
            LoadStateAppearance(s.StartOpenedAppearance, ChkStateStart, StateStartCombo, StateStartColor, StateStartSwatch, ChkStateStartNoTint);

            // Interface: grid density + Mica
            switch (s.CardDensity)
            {
                case "compact": if (DensityCompactRadio != null) DensityCompactRadio.IsChecked = true; break;
                case "large": if (DensityLargeRadio != null) DensityLargeRadio.IsChecked = true; break;
                default: if (DensityComfortableRadio != null) DensityComfortableRadio.IsChecked = true; break;
            }
            if (ChkWindowMica != null)
            {
                ChkWindowMica.IsChecked = s.WindowMicaEnabled;
                if (!MicaHelper.IsMicaSupported)
                {
                    ChkWindowMica.IsEnabled = false;
                    if (MicaHintText != null)
                        MicaHintText.Text = "Requires Windows 11 (build 22621+). Not available on this system.";
                }
            }
        }

        private void ChkAutoStart_CheckedChanged(object sender, RoutedEventArgs e)
        {
            UpdateAutoStartDependencies();
        }

        private void UpdateAutoStartDependencies()
        {
            if (ChkStartMinimized != null)
            {
                bool isAuto = ChkAutoStart?.IsChecked == true;
                ChkStartMinimized.IsEnabled = isAuto;
                ChkStartMinimized.Opacity = isAuto ? 1.0 : 0.5;
            }
        }

        private void RefreshCustomSourcesList()
        {
            if (CustomSourcesItemsControl == null) return;
            CustomSourcesItemsControl.ItemsSource = null;
            CustomSourcesItemsControl.ItemsSource = Settings.Instance.CustomSources;
        }

        private void AddCustomSource_Click(object sender, RoutedEventArgs e)
        {
            string name = NewSourceNameBox?.Text?.Trim() ?? "";
            string url = NewSourceUrlBox?.Text?.Trim() ?? "";
            bool isSfw = NewSourceIsSfwChk?.IsChecked == true;

            if (string.IsNullOrWhiteSpace(name))
            {
                System.Windows.MessageBox.Show("Please enter a name for the custom source.", "WallSafe", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(url) || (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                System.Windows.MessageBox.Show("Please enter a valid HTTP/HTTPS URL.", "WallSafe", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newSource = new CustomSourceConfig
            {
                Name = name,
                BaseUrl = url.TrimEnd('/'),
                IsSfw = isSfw,
                Enabled = true
            };

            Settings.Instance.CustomSources.Add(newSource);
            RefreshCustomSourcesList();

            if (NewSourceNameBox != null) NewSourceNameBox.Text = "";
            if (NewSourceUrlBox != null) NewSourceUrlBox.Text = "";
            if (NewSourceIsSfwChk != null) NewSourceIsSfwChk.IsChecked = false;
        }

        private void NewSourceNameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (NewSourceNamePlaceholder != null && NewSourceNameBox != null)
            {
                NewSourceNamePlaceholder.Visibility = string.IsNullOrEmpty(NewSourceNameBox.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void NewSourceUrlBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (NewSourceUrlPlaceholder != null && NewSourceUrlBox != null)
            {
                NewSourceUrlPlaceholder.Visibility = string.IsNullOrEmpty(NewSourceUrlBox.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void RemoveCustomSource_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement btn && btn.Tag is string id)
            {
                Settings.Instance.CustomSources.RemoveAll(cs => cs.Id == id);
                RefreshCustomSourcesList();
            }
        }

        private void Theme_Checked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            if (ThemeLightRadio.IsChecked == true)
                ThemeManager.ApplyTheme("Light");
            else
                ThemeManager.ApplyTheme("Dark");
        }

        private void SafePathBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSafeImagePreview();
        }

        private void UpdateSafeImagePreview()
        {
            if (SafeImagePreview == null) return;

            string path = SafePathBox.Text;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(path, UriKind.Absolute);
                    bmp.DecodePixelWidth = 260;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    SafeImagePreview.Source = bmp;
                    return;
                }
                catch { }
            }
            SafeImagePreview.Source = null;
        }

        private void UpdateHotkeyDisplay()
        {
            var parts = new List<string>();
            var mods = (ModifierKeys)_recordedModifiers;
            if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (mods.HasFlag(ModifierKeys.Win)) parts.Add("Win");

            var key = (WinFormsKeys)_recordedKey;
            parts.Add(key.ToString());

            HotkeyDisplayPrompt.Text = string.Join(" + ", parts);
        }

        private void HotkeyRecordBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isRecordingHotkey)
            {
                StartRecordingHotkey();
            }
            e.Handled = true;
        }

        private void StartRecordingHotkey()
        {
            _isRecordingHotkey = true;
            HotkeyRecordBox.Focus();
            HotkeyDisplayPrompt.Text = "Press shortcut (up to 3 keys)…";
            HotkeyBadgeText.Text = "Listening…";
            HotkeyRecordBadge.Background = (System.Windows.Media.Brush)FindResource("AmberBrush");
            if (TryFindResource("AccentBrush") is System.Windows.Media.Brush accent)
                HotkeyRecordBox.BorderBrush = accent;
            HotkeyHintText.Text = "Press modifier(s) + key (e.g. Ctrl + Shift + W, Alt + Q, F12). Esc to cancel.";
        }

        private void StopRecordingHotkey(bool canceled = false)
        {
            _isRecordingHotkey = false;
            HotkeyBadgeText.Text = "Click to Record";
            HotkeyRecordBadge.Background = (System.Windows.Media.Brush)FindResource("AccentBrush");
            if (TryFindResource("BorderBrush") is System.Windows.Media.Brush border)
                HotkeyRecordBox.BorderBrush = border;

            UpdateHotkeyDisplay();

            if (canceled)
            {
                HotkeyHintText.Text = "Recording canceled. Current shortcut preserved.";
            }
            else
            {
                HotkeyHintText.Text = "✓ Shortcut recorded! Click 'Save' or 'Save & Apply' to commit.";
                SaveShortcutBtn.IsEnabled = true;
            }
        }

        private void HotkeyRecordBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!_isRecordingHotkey) return;
            e.Handled = true;

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;

            if (key == Key.Escape)
            {
                StopRecordingHotkey(canceled: true);
                return;
            }

            var mods = ModifierKeys.None;
            int keyCount = 0;

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) || key == Key.LeftCtrl || key == Key.RightCtrl)
            {
                mods |= ModifierKeys.Control;
                keyCount++;
            }
            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt) || key == Key.LeftAlt || key == Key.RightAlt)
            {
                mods |= ModifierKeys.Alt;
                keyCount++;
            }
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) || key == Key.LeftShift || key == Key.RightShift)
            {
                mods |= ModifierKeys.Shift;
                keyCount++;
            }
            if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin) || key == Key.LWin || key == Key.RWin)
            {
                mods |= ModifierKeys.Win;
                keyCount++;
            }

            bool isModifierOnly = key == Key.LeftCtrl || key == Key.RightCtrl ||
                                  key == Key.LeftAlt || key == Key.RightAlt ||
                                  key == Key.LeftShift || key == Key.RightShift ||
                                  key == Key.LWin || key == Key.RWin;

            if (isModifierOnly)
            {
                var parts = new List<string>();
                if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
                if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
                if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
                if (mods.HasFlag(ModifierKeys.Win)) parts.Add("Win");
                parts.Add("[Key]");
                HotkeyDisplayPrompt.Text = string.Join(" + ", parts);
                return;
            }

            // Normal key
            int vk = KeyInterop.VirtualKeyFromKey(key);
            var winKey = (WinFormsKeys)vk;

            int totalKeys = keyCount + 1;
            if (totalKeys > 3)
            {
                HotkeyHintText.Text = "⚠ Maximum 3 keys allowed! Try e.g. Ctrl + Shift + W or Alt + Q.";
                return;
            }

            _recordedModifiers = (int)mods;
            _recordedKey = (int)winKey;

            StopRecordingHotkey(canceled: false);
        }

        private void HotkeyRecordBox_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
        }

        private void HotkeyRecordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isRecordingHotkey)
            {
                StopRecordingHotkey(canceled: true);
            }
        }

        private void SaveShortcutBtn_Click(object sender, RoutedEventArgs e)
        {
            var s = Settings.Instance;
            s.HotkeyModifiers = _recordedModifiers;
            s.HotkeyKey = _recordedKey;
            s.Save();

            HotkeyManager.Instance.ReRegister();
            if (Owner is MainWindow mw)
            {
                mw.UpdatePanicButtonLabel();
            }

            SaveShortcutBtn.IsEnabled = false;
            HotkeyHintText.Text = $"✓ Saved & Active! Global shortcut is now {s.GetHotkeyDisplayName()}";
        }

        private void BrowseSafe_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Images (*.jpg;*.jpeg;*.png;*.bmp;*.webp)|*.jpg;*.jpeg;*.png;*.bmp;*.webp|All files (*.*)|*.*",
                Title = "Select Safe (Panic) Wallpaper"
            };

            if (dlg.ShowDialog() == true)
            {
                SafePathBox.Text = dlg.FileName;
            }
        }

        private void ResetWin11Safe_Click(object sender, RoutedEventArgs e)
        {
            string win11Path = Settings.DetectDefaultWin11Wallpaper();
            SafePathBox.Text = win11Path;
        }

        private void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            var confirm = System.Windows.MessageBox.Show(
                "Clear the local wallpaper thumbnail cache?\n\nThis frees disk space. Thumbnails will be re-downloaded as you browse. Your favorites and downloads are not affected.",
                "Clear Cache",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);
            if (confirm != MessageBoxResult.Yes) return;

            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WallSafe", "cache");

            if (Directory.Exists(dir))
            {
                foreach (var f in Directory.GetFiles(dir))
                {
                    try { File.Delete(f); } catch { }
                }
            }

            // Also clear the thumbnail cache managed by ImageCacheService.
            try { ImageCacheService.Instance.ClearCache(); } catch { }

            RefreshCacheInfo();
        }

        private void RefreshCacheInfo()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WallSafe", "cache");

            if (!Directory.Exists(dir))
            {
                CacheInfo.Text = "Cache: 0 MB";
                return;
            }

            long size = 0;
            int count = 0;
            foreach (var f in Directory.GetFiles(dir))
            {
                try
                {
                    size += new FileInfo(f).Length;
                    count++;
                }
                catch { }
            }

            CacheInfo.Text = $"Cache: {count} images ({(double)size / (1024 * 1024):0.0} MB)";
        }

        private void RefreshDownloadFolderInfo()
        {
            DownloadsFolderInfo.Text = $"Folder: {DownloadsManager.Instance.DownloadFolder}";
        }

        private void ChangeDownloadFolder_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select Folder for Downloaded High-Res Wallpapers",
                UseDescriptionForTitle = true,
                SelectedPath = DownloadsManager.Instance.DownloadFolder
            };

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DownloadsManager.Instance.DownloadFolder = dlg.SelectedPath;
                RefreshDownloadFolderInfo();
            }
        }

        private void ChkCustomFilterProfile_Changed(object sender, RoutedEventArgs e)
        {
            if (ComboItemCustom != null)
            {
                bool enabled = ChkCustomFilterProfile?.IsChecked == true;
                ComboItemCustom.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
                if (!enabled && RatingModeCombo?.SelectedIndex == 3)
                {
                    RatingModeCombo.SelectedIndex = 0;
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ApplySettings();
            Close();
        }

        private bool _settingsApplied;

        /// <summary>Persist and apply all settings. Safe to call multiple times.</summary>
        private void ApplySettings()
        {
            var s = Settings.Instance;
            s.SafeWallpaperPath = SafePathBox.Text;
            s.HotkeyModifiers = _recordedModifiers;
            s.HotkeyKey = _recordedKey;

            // Home Dashboard Section Visibility
            s.ShowHomeHero = ChkShowHomeHero.IsChecked == true;
            s.ShowHomeQuickControls = ChkShowHomeQuickControls.IsChecked == true;
            s.ShowHomeTrending = ChkShowHomeTrending.IsChecked == true;
            s.ShowHomeFranchises = ChkShowHomeFranchises.IsChecked == true;
            s.ShowHomeQuickShelf = ChkShowHomeQuickShelf.IsChecked == true;

            // Startup & Background
            s.AutoStartWithWindows = ChkAutoStart.IsChecked == true;
            s.StartMinimized = ChkStartMinimized.IsChecked == true;
            StartupManager.SetStartup(s.AutoStartWithWindows);

            // Save next-gen settings
            s.PanicHideApp = ChkPanicHideApp.IsChecked == true;
            s.PanicMinimizeAllWindows = ChkPanicMinimize.IsChecked == true;
            s.PanicMuteAudio = ChkPanicMute.IsChecked == true;
            s.PanicRestoreToggle = ChkPanicRestore.IsChecked == true;

            // Wi-Fi auto wallpaper
            s.WifiAutoWallpaperEnabled = ChkWifiAuto?.IsChecked == true;
            s.WifiTriggerSsid = WifiSsidBox?.Text?.Trim() ?? "";
            s.WifiTriggerWallpaperPath = WifiWallpaperBox?.Text?.Trim() ?? "";
            s.WifiRestoreOnDisconnect = ChkWifiRestore?.IsChecked == true;

            s.SlideshowEnabled = ChkSlideshow.IsChecked == true;
            if (SlideshowIntervalCombo.SelectedItem is ComboBoxItem intervalItem && intervalItem.Tag is int intervalVal)
            {
                if (intervalVal == -1) // Custom
                {
                    if (int.TryParse(CustomIntervalBox?.Text, out int custom) && custom > 0)
                        s.SlideshowIntervalMinutes = Math.Clamp(custom, 1, 1440);
                }
                else
                {
                    s.SlideshowIntervalMinutes = intervalVal;
                }
            }
            if (SlideshowSourceCombo.SelectedItem is ComboBoxItem srcItem && srcItem.Tag is string srcVal)
            {
                s.SlideshowSource = srcVal;
            }
            if (MonitorTargetCombo.SelectedItem is ComboBoxItem monItem && monItem.Tag is int monVal)
            {
                s.TargetMonitor = monVal;
            }

            s.DiscretionBlur = ChkDiscretion.IsChecked == true;
            s.TagBlacklist = BlacklistBox.Text.Trim();

            if (RatingModeCombo?.SelectedItem is ComboBoxItem rmItem && int.TryParse(rmItem.Tag as string, out int rmVal))
            {
                s.RatingMode = (ContentRatingMode)Math.Clamp(rmVal, 0, 3);
            }
            if (ChkCustomFilterProfile != null)
            {
                s.CustomFilterProfileEnabled = ChkCustomFilterProfile.IsChecked == true;
                if (!s.CustomFilterProfileEnabled && s.RatingMode == ContentRatingMode.Custom)
                {
                    s.RatingMode = ContentRatingMode.SfwOnly;
                }
            }
            s.AllowAllSources = ChkAllowAllSources.IsChecked == true;

            // Taskbar Appearance (merged TranslucentTB engine)
            s.TaskbarTransparencyEnabled = ChkTaskbarEnabled?.IsChecked == true;
            SaveStateAppearance(s.DesktopAppearance ??= new(), ChkStateDesktop, StateDesktopCombo, StateDesktopColor, ChkStateDesktopNoTint);
            SaveStateAppearance(s.VisibleWindowAppearance ??= new(), ChkStateVisible, StateVisibleCombo, StateVisibleColor, ChkStateVisibleNoTint);
            SaveStateAppearance(s.MaximizedWindowAppearance ??= new(), ChkStateMaximized, StateMaximizedCombo, StateMaximizedColor, ChkStateMaximizedNoTint);
            SaveStateAppearance(s.StartOpenedAppearance ??= new(), ChkStateStart, StateStartCombo, StateStartColor, ChkStateStartNoTint);
            s.TaskbarRestoreOnPanic = ChkTaskbarRestoreOnPanic?.IsChecked == true;

            // Interface: grid density + Mica
            if (DensityCompactRadio?.IsChecked == true) s.CardDensity = "compact";
            else if (DensityLargeRadio?.IsChecked == true) s.CardDensity = "large";
            else s.CardDensity = "comfortable";
            s.WindowMicaEnabled = ChkWindowMica?.IsChecked == true;

            s.Save();

            // Apply taskbar appearance right away.
            TaskbarManager.Instance.ApplyFromSettings();

            // Restart the Wi-Fi auto-wallpaper watcher with the new settings.
            try { WifiWatcher.Instance.Start(); } catch { }

            WallpaperManager.Instance.SetSafeWallpaper(s.SafeWallpaperPath);
            if (s.SlideshowEnabled)
                WallpaperManager.Instance.StartSlideshow(s.SlideshowIntervalMinutes);
            else
                WallpaperManager.Instance.StopSlideshow();

            HotkeyManager.Instance.ReRegister();
            if (Owner is MainWindow mw)
            {
                mw.UpdatePanicButtonLabel();
                mw.UpdateSfwToggleVisuals();
                mw.ApplySfwModeToFilters();
                mw.ApplyHomeSectionsVisibility();
            }

            _settingsApplied = true;
        }

        // Automatically persist & apply settings whenever the window closes,
        // so there is no explicit "Save & Apply" step — just change and close.
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_settingsApplied)
            {
                try { ApplySettings(); } catch { }
            }
            base.OnClosing(e);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}

