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
                SlideshowIntervalCombo.SelectedIndex = 1; // 15 mins default

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
            if (RatingModeCombo != null)
                RatingModeCombo.SelectedIndex = Math.Clamp((int)s.RatingMode, 0, 2);
            if (ChkCustomFilterProfile != null)
                ChkCustomFilterProfile.IsChecked = s.CustomFilterProfileEnabled;
            ChkAllowAllSources.IsChecked = s.AllowAllSources;
            RefreshCustomSourcesList();

            // Appearance & Theme
            if (s.Theme == "Light")
                ThemeLightRadio.IsChecked = true;
            else
                ThemeDarkRadio.IsChecked = true;
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

        private void Save_Click(object sender, RoutedEventArgs e)
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

            s.SlideshowEnabled = ChkSlideshow.IsChecked == true;
            if (SlideshowIntervalCombo.SelectedItem is ComboBoxItem intervalItem && intervalItem.Tag is int intervalVal)
            {
                s.SlideshowIntervalMinutes = intervalVal;
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
                s.RatingMode = (ContentRatingMode)Math.Clamp(rmVal, 0, 2);
            }
            if (ChkCustomFilterProfile != null)
            {
                s.CustomFilterProfileEnabled = ChkCustomFilterProfile.IsChecked == true;
            }
            s.AllowAllSources = ChkAllowAllSources.IsChecked == true;

            s.Save();

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

            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}

