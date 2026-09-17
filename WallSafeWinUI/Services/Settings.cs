using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace WallSafeWinUI.Services
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
                    RatingMode = ContentRatingMode.Explicit;
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

        public FilterProfile SfwFilterProfile { get; set; } = new() { RatingTag = "rating:s", Source = "all" };
        public FilterProfile QuestionableFilterProfile { get; set; } = new() { RatingTag = "rating:q", Source = "all" };
        public FilterProfile ExplicitFilterProfile { get; set; } = new() { RatingTag = "rating:e", Source = "all" };

        public FilterProfile GetFilterProfileForMode(ContentRatingMode mode) => mode switch
        {
            ContentRatingMode.SfwOnly => SfwFilterProfile ??= new() { RatingTag = "rating:s", Source = "all" },
            ContentRatingMode.Questionable => QuestionableFilterProfile ??= new() { RatingTag = "rating:q", Source = "all" },
            ContentRatingMode.Explicit => ExplicitFilterProfile ??= new() { RatingTag = "rating:e", Source = "all" },
            ContentRatingMode.Custom => SavedFilterProfile ??= new() { RatingTag = "rating:s", Source = "all" },
            _ => SavedFilterProfile ??= new()
        };

        public bool AllowAllSources { get; set; } = true;
        public List<CustomSourceConfig> CustomSources { get; set; } = new();
        public List<SeriesCategoryItem> CustomCategories { get; set; } = new();

        // Win32 fsModifiers flags (Control|Shift) and VK 'W' by default.
        public int HotkeyModifiers { get; set; } = (int)(ModifierKeys.Control | ModifierKeys.Shift);
        public int HotkeyKey { get; set; } = 0x57; // 'W'

        public bool AutoStartWithWindows { get; set; } = false;
        public bool StartMinimized { get; set; } = false;
        public int MinWidthFilter { get; set; } = 0;
        public string AspectRatioFilter { get; set; } = "all";
        public string Theme { get; set; } = "Dark"; // "Dark", "Light", "System"

        // ── Personalization ──
        public string UserName { get; set; } = "";
        public bool ShowGreeting { get; set; } = true;
        public bool FirstRunCompleted { get; set; } = false;

        public string CardDensity { get; set; } = "comfortable";

        public bool WindowMicaEnabled { get; set; } = true;
        // Backdrop material: "Mica", "MicaAlt", "Acrylic", "None"
        public string BackdropMaterial { get; set; } = "Acrylic";

        // ── Wi-Fi-triggered auto wallpaper ──
        public bool WifiAutoWallpaperEnabled { get; set; } = false;
        public string WifiTriggerSsid { get; set; } = "";

        [JsonIgnore]
        public List<string> WifiTriggerSsids
        {
            get
            {
                var result = new List<string>();
                if (string.IsNullOrWhiteSpace(WifiTriggerSsid)) return result;
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var raw in WifiTriggerSsid.Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var name = raw.Trim();
                    if (name.Length > 0 && seen.Add(name)) result.Add(name);
                }
                return result;
            }
        }
        public string WifiTriggerWallpaperPath { get; set; } = "";
        public bool WifiRestoreOnDisconnect { get; set; } = false;

        // ── Location-triggered auto wallpaper ──
        public bool LocationAutoWallpaperEnabled { get; set; } = false;
        public double LocationLatitude { get; set; } = 0;
        public double LocationLongitude { get; set; } = 0;
        public int LocationRadiusMeters { get; set; } = 150;
        public bool LocationRestoreOnLeave { get; set; } = false;

        // Home Dashboard Section Visibility
        public bool ShowHomeStats { get; set; } = true;
        public bool ShowHomeHero { get; set; } = true;
        public bool ShowHomeQuickControls { get; set; } = true;
        public bool ShowHomeTrending { get; set; } = true;
        public bool ShowHomeFranchises { get; set; } = true;
        public bool ShowHomeQuickShelf { get; set; } = true;

        // Panic Suite
        public int PanicTriggerCount { get; set; } = 0;
        public bool PanicHideApp { get; set; } = true;
        public bool PanicMinimizeAllWindows { get; set; } = true;
        public bool PanicMuteAudio { get; set; } = false;
        public bool PanicRestoreToggle { get; set; } = true;
        public bool PanicRevertTaskbar { get; set; } = true;

        // Slideshow
        public bool SlideshowEnabled { get; set; } = false;
        public int SlideshowIntervalMinutes { get; set; } = 15;
        // Precise interval in seconds; when > 0 this overrides SlideshowIntervalMinutes,
        // enabling sub-minute intervals (e.g. 30 = 0.5 min).
        public int SlideshowIntervalSeconds { get; set; } = 0;
        public string SlideshowSource { get; set; } = "favorites";
        public int TargetMonitor { get; set; } = -1;

        [JsonIgnore]
        public double SlideshowIntervalEffectiveSeconds
        {
            get
            {
                if (SlideshowIntervalSeconds > 0) return SlideshowIntervalSeconds;
                return Math.Max(1, SlideshowIntervalMinutes) * 60.0;
            }
        }

        // Content Discretion
        public bool DiscretionBlur { get; set; } = false;
        public string TagBlacklist { get; set; } = "";

        // ── Taskbar transparency (TranslucentTB-style) ──
        public bool TaskbarEffectEnabled { get; set; } = false;
        // "Normal", "Opaque", "Clear", "Blur", "Acrylic"
        public string TaskbarState { get; set; } = "Acrylic";
        // #AARRGGBB — a mostly-transparent dark tint so the effect is visible by default.
        public string TaskbarTint { get; set; } = "#40000000";

        [JsonIgnore]
        public bool HotkeyCtrl
        {
            get => ((ModifierKeys)HotkeyModifiers).HasFlag(ModifierKeys.Control);
            set { if (value) HotkeyModifiers |= (int)ModifierKeys.Control; else HotkeyModifiers &= ~(int)ModifierKeys.Control; }
        }

        [JsonIgnore]
        public bool HotkeyShift
        {
            get => ((ModifierKeys)HotkeyModifiers).HasFlag(ModifierKeys.Shift);
            set { if (value) HotkeyModifiers |= (int)ModifierKeys.Shift; else HotkeyModifiers &= ~(int)ModifierKeys.Shift; }
        }

        [JsonIgnore]
        public bool HotkeyAlt
        {
            get => ((ModifierKeys)HotkeyModifiers).HasFlag(ModifierKeys.Alt);
            set { if (value) HotkeyModifiers |= (int)ModifierKeys.Alt; else HotkeyModifiers &= ~(int)ModifierKeys.Alt; }
        }

        [JsonIgnore]
        public bool HotkeyWin
        {
            get => ((ModifierKeys)HotkeyModifiers).HasFlag(ModifierKeys.Win);
            set { if (value) HotkeyModifiers |= (int)ModifierKeys.Win; else HotkeyModifiers &= ~(int)ModifierKeys.Win; }
        }

        private static string ConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WallSafe", "settings.json");

        public static string DetectDefaultWin11Wallpaper()
        {
            try
            {
                string[] win11Candidates =
                {
                    @"C:\Windows\Web\4K\Wallpaper\Windows\img0_3840x2160.jpg",
                    @"C:\Windows\Web\Wallpaper\Windows\img0.jpg",
                    @"C:\Windows\Web\Wallpaper\Windows\img19.jpg",
                    @"C:\Windows\Web\Wallpaper\Spotlight\img0.jpg",
                    @"C:\Windows\Web\Wallpaper\Theme1\img1.jpg",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Web\Wallpaper\Windows\img0.jpg")
                };
                foreach (var candidate in win11Candidates)
                    if (File.Exists(candidate)) return candidate;
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
                            if (!string.IsNullOrEmpty(detectedWin11)) s.SafeWallpaperPath = detectedWin11;
                        }
                        return s;
                    }
                }
            }
            catch { }

            var newSettings = new Settings();
            var detected = DetectDefaultWin11Wallpaper();
            if (!string.IsNullOrEmpty(detected)) newSettings.SafeWallpaperPath = detected;
            return newSettings;
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
            parts.Add(KeyNames.GetName(HotkeyKey));
            return string.Join(" + ", parts);
        }
    }
}
