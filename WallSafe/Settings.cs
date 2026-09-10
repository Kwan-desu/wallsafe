using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace WallSafe
{
    public enum ContentRatingMode
    {
        SfwOnly = 0,
        Questionable = 1,
        Explicit = 2,
        Custom = 3
    }

    public class FilterProfile
    {
        public string RatingTag { get; set; } = "rating:s";
        public string SortTag { get; set; } = "order:score";
        public int ResolutionIndex { get; set; } = 0;
        public int AspectIndex { get; set; } = 0;
        public string Source { get; set; } = "konasfw";
    }

    /// <summary>A single dynamic taskbar appearance state (TranslucentTB-style).</summary>
    public class TaskbarStateAppearance
    {
        public bool Enabled { get; set; }
        // 0=Normal, 1=Opaque, 2=Clear, 3=Blur, 4=Acrylic
        public int AccentState { get; set; } = 4;
        // 0xAARRGGBB
        public int ColorArgb { get; set; } = unchecked((int)0x66000000);
        // When true, no color tint is applied — only the accent effect (transparent gradient color).
        public bool NoTint { get; set; } = false;
    }

    public class Settings
    {
        public static readonly Settings Instance = Load();

        public string SafeWallpaperPath { get; set; } = "";
        public string DownloadDirectory { get; set; } = "";
        public string DefaultSource { get; set; } = "konasfw";
        public string DefaultRating { get; set; } = "rating:s";
        public string DefaultSort { get; set; } = "order:score";

        public ContentRatingMode RatingMode { get; set; } = ContentRatingMode.SfwOnly;

        [JsonProperty("SfwOnlyMode")]
        private bool? SfwOnlyModeSerializer
        {
            get => RatingMode == ContentRatingMode.SfwOnly;
            set
            {
                if (value.HasValue && !value.Value && RatingMode == ContentRatingMode.SfwOnly)
                {
                    RatingMode = ContentRatingMode.Explicit;
                }
            }
        }

        [JsonIgnore]
        public bool SfwOnlyMode
        {
            get => RatingMode == ContentRatingMode.SfwOnly;
            set => RatingMode = value ? ContentRatingMode.SfwOnly : ContentRatingMode.Explicit;
        }

        public bool CustomFilterProfileEnabled { get; set; } = false;
        public FilterProfile SavedFilterProfile { get; set; } = new();

        public bool AllowAllSources { get; set; } = true;
        public List<CustomSourceConfig> CustomSources { get; set; } = new();
        public List<SeriesCategoryItem> CustomCategories { get; set; } = new();
        public int HotkeyModifiers { get; set; } = (int)(ModifierKeys.Control | ModifierKeys.Shift);
        public int HotkeyKey { get; set; } = (int)Keys.W;
        public bool AutoStartWithWindows { get; set; } = false;
        public bool StartMinimized { get; set; } = false;
        public int MinWidthFilter { get; set; } = 0; // 0 = Any, 1920 = 1080p+, 2560 = 2K+, 3840 = 4K+
        public string AspectRatioFilter { get; set; } = "all"; // all, landscape, ultrawide, portrait
        public string Theme { get; set; } = "Dark"; // "Dark" or "Light"

        // Grid card density: "compact" | "comfortable" | "large"
        public string CardDensity { get; set; } = "comfortable";

        // Windows 11 Mica window backdrop (opt-in; falls back to opaque on older Windows).
        public bool WindowMicaEnabled { get; set; } = false;

        // ── Wi-Fi-triggered auto wallpaper ──
        // When connected to WifiTriggerSsid, automatically apply a wallpaper.
        public bool WifiAutoWallpaperEnabled { get; set; } = false;
        public string WifiTriggerSsid { get; set; } = "";
        // Wallpaper to apply when the trigger SSID connects. Empty = use SafeWallpaperPath.
        public string WifiTriggerWallpaperPath { get; set; } = "";
        // Optionally restore the previous wallpaper when disconnecting from the trigger SSID.
        public bool WifiRestoreOnDisconnect { get; set; } = false;

        // Home Dashboard Section Visibility
        public bool ShowHomeHero { get; set; } = true;
        public bool ShowHomeQuickControls { get; set; } = true;
        public bool ShowHomeTrending { get; set; } = true;
        public bool ShowHomeFranchises { get; set; } = true;
        public bool ShowHomeQuickShelf { get; set; } = true;

        // Next-Gen Stealth & Panic Suite
        public bool PanicHideApp { get; set; } = true;
        public bool PanicMinimizeAllWindows { get; set; } = true;
        public bool PanicMuteAudio { get; set; } = false;
        public bool PanicRestoreToggle { get; set; } = true;

        // Automation & Slideshow Engine
        public bool SlideshowEnabled { get; set; } = false;
        public int SlideshowIntervalMinutes { get; set; } = 15;
        public string SlideshowSource { get; set; } = "favorites"; // "favorites", "downloads"
        public int TargetMonitor { get; set; } = -1; // -1 for all, 0, 1... for specific monitor

        // Content Discretion & Filtering
        public bool DiscretionBlur { get; set; } = false;
        public string TagBlacklist { get; set; } = "";

        // ── Taskbar Transparency Engine (merged from TranslucentTB) ──
        // Master enable for the built-in taskbar transparency feature.
        public bool TaskbarTransparencyEnabled { get; set; } = false;
        // 0=Normal, 1=Opaque, 2=Clear, 3=Blur, 4=Acrylic (matches TaskbarManager.AccentState)
        public int TaskbarAccentState { get; set; } = 4; // Acrylic by default
        // Tint color as 0xAARRGGBB. Default: subtle dark translucent tint.
        public int TaskbarColorArgb { get; set; } = unchecked((int)0x66000000);
        // When true, the taskbar reverts to Normal while the panic hotkey conceals WallSafe,
        // then restores the configured appearance on the second (restore) press.
        public bool TaskbarRestoreOnPanic { get; set; } = true;

        // ── Dynamic per-state taskbar appearances (TranslucentTB-style) ──
        // Each state has its own enabled flag, accent state and tint color. They are
        // evaluated by priority (Maximized > Visible window > Start opened > Desktop).
        public TaskbarStateAppearance DesktopAppearance { get; set; }
            = new() { Enabled = true, AccentState = 4, ColorArgb = unchecked((int)0x66000000) };
        public TaskbarStateAppearance VisibleWindowAppearance { get; set; }
            = new() { Enabled = false, AccentState = 2, ColorArgb = unchecked((int)0x99000000) };
        public TaskbarStateAppearance MaximizedWindowAppearance { get; set; }
            = new() { Enabled = false, AccentState = 1, ColorArgb = unchecked((int)0xFF1C1C1C) };
        public TaskbarStateAppearance StartOpenedAppearance { get; set; }
            = new() { Enabled = false, AccentState = 4, ColorArgb = unchecked((int)0x66000000) };

        // Migrates the old single-appearance settings into the new Desktop state (run once).
        [JsonProperty]
        private bool TaskbarStatesMigrated { get; set; } = false;

        [JsonIgnore]
        public bool HotkeyCtrl
        {
            get => ((ModifierKeys)HotkeyModifiers).HasFlag(ModifierKeys.Control);
            set
            {
                if (value) HotkeyModifiers |= (int)ModifierKeys.Control;
                else HotkeyModifiers &= ~(int)ModifierKeys.Control;
            }
        }

        [JsonIgnore]
        public bool HotkeyShift
        {
            get => ((ModifierKeys)HotkeyModifiers).HasFlag(ModifierKeys.Shift);
            set
            {
                if (value) HotkeyModifiers |= (int)ModifierKeys.Shift;
                else HotkeyModifiers &= ~(int)ModifierKeys.Shift;
            }
        }

        [JsonIgnore]
        public bool HotkeyAlt
        {
            get => ((ModifierKeys)HotkeyModifiers).HasFlag(ModifierKeys.Alt);
            set
            {
                if (value) HotkeyModifiers |= (int)ModifierKeys.Alt;
                else HotkeyModifiers &= ~(int)ModifierKeys.Alt;
            }
        }

        [JsonIgnore]
        public bool HotkeyWin
        {
            get => ((ModifierKeys)HotkeyModifiers).HasFlag(ModifierKeys.Win);
            set
            {
                if (value) HotkeyModifiers |= (int)ModifierKeys.Win;
                else HotkeyModifiers &= ~(int)ModifierKeys.Win;
            }
        }

        private static string ConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WallSafe", "settings.json");

        public static string DetectDefaultWin11Wallpaper()
        {
            try
            {
                string[] win11Candidates = new[]
                {
                    @"C:\Windows\Web\4K\Wallpaper\Windows\img0_3840x2160.jpg",
                    @"C:\Windows\Web\Wallpaper\Windows\img0.jpg",
                    @"C:\Windows\Web\Wallpaper\Windows\img19.jpg",
                    @"C:\Windows\Web\Wallpaper\Spotlight\img0.jpg",
                    @"C:\Windows\Web\Wallpaper\Theme1\img1.jpg",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Web\Wallpaper\Windows\img0.jpg")
                };

                foreach (var candidate in win11Candidates)
                {
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
            catch { }

            return "";
        }

        private static Settings Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var s = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(ConfigPath));
                    if (s != null)
                    {
                        if (string.IsNullOrEmpty(s.SafeWallpaperPath) || !File.Exists(s.SafeWallpaperPath))
                        {
                            var detectedWin11 = DetectDefaultWin11Wallpaper();
                            if (!string.IsNullOrEmpty(detectedWin11))
                                s.SafeWallpaperPath = detectedWin11;
                        }
                        s.MigrateTaskbarStates();
                        return s;
                    }
                }
            }
            catch { }

            var newSettings = new Settings();
            var detected = DetectDefaultWin11Wallpaper();
            if (!string.IsNullOrEmpty(detected))
            {
                newSettings.SafeWallpaperPath = detected;
            }
            return newSettings;
        }

        /// <summary>One-time migration: seed the Desktop dynamic state from the legacy
        /// single-appearance taskbar settings so existing configs keep working.</summary>
        public void MigrateTaskbarStates()
        {
            if (TaskbarStatesMigrated) return;
            DesktopAppearance ??= new TaskbarStateAppearance();
            DesktopAppearance.Enabled = true;
            DesktopAppearance.AccentState = TaskbarAccentState;
            DesktopAppearance.ColorArgb = TaskbarColorArgb;
            TaskbarStatesMigrated = true;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch { }
        }

        public string GetHotkeyDisplayName()
        {
            var parts = new List<string>();
            var mods = (ModifierKeys)HotkeyModifiers;
            if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (mods.HasFlag(ModifierKeys.Win)) parts.Add("Win");

            var key = (Keys)HotkeyKey;
            parts.Add(key.ToString());
            return string.Join(" + ", parts);
        }
    }
}
