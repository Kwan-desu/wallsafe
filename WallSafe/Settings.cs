using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace WallSafe
{
    public class Settings
    {
        public static readonly Settings Instance = Load();

        public string SafeWallpaperPath { get; set; } = "";
        public string DownloadDirectory { get; set; } = "";
        public string DefaultSource { get; set; } = "konasfw";
        public string DefaultRating { get; set; } = "rating:s";
        public string DefaultSort { get; set; } = "order:score";
        public bool SfwOnlyMode { get; set; } = true; // SFW Only (default) vs Include NSFW
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
