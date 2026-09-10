using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace WallSafe
{
    /// <summary>
    /// Watches the currently-connected Wi-Fi SSID and, when it matches the user-configured
    /// trigger network (e.g. a school/work network), automatically applies a chosen wallpaper
    /// (or the safe/default wallpaper). Optionally restores the previous wallpaper on disconnect.
    ///
    /// SSID detection uses `netsh wlan show interfaces`, which needs no extra dependencies and
    /// works on all supported Windows versions.
    /// </summary>
    public sealed class WifiWatcher : IDisposable
    {
        private static WifiWatcher? _instance;
        public static WifiWatcher Instance => _instance ??= new WifiWatcher();

        private readonly object _sync = new();
        private System.Threading.Timer? _timer;
        private string? _lastSsid;
        private bool _triggerActive;         // currently on the trigger network
        private string? _wallpaperBeforeTrigger; // to restore on disconnect
        private bool _disposed;

        private WifiWatcher() { }

        /// <summary>Start (or restart) polling based on current settings.</summary>
        public void Start()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _timer?.Dispose();
                if (!Settings.Instance.WifiAutoWallpaperEnabled)
                {
                    _timer = null;
                    return;
                }
                // Poll every 8 seconds — cheap and responsive enough for network changes.
                _timer = new System.Threading.Timer(_ => Poll(), null,
                    TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8));
            }
        }

        public void Stop()
        {
            lock (_sync)
            {
                _timer?.Dispose();
                _timer = null;
            }
        }

        private void Poll()
        {
            try
            {
                string? ssid = GetCurrentSsid();
                string trigger = Settings.Instance.WifiTriggerSsid?.Trim() ?? "";
                if (string.IsNullOrEmpty(trigger)) return;

                bool onTrigger = !string.IsNullOrEmpty(ssid) &&
                                 string.Equals(ssid, trigger, StringComparison.OrdinalIgnoreCase);

                // Rising edge: just connected to the trigger network.
                if (onTrigger && !_triggerActive)
                {
                    _triggerActive = true;
                    ApplyTriggerWallpaper();
                }
                // Falling edge: left the trigger network.
                else if (!onTrigger && _triggerActive)
                {
                    _triggerActive = false;
                    if (Settings.Instance.WifiRestoreOnDisconnect && !string.IsNullOrEmpty(_wallpaperBeforeTrigger))
                    {
                        var prev = _wallpaperBeforeTrigger;
                        RunOnUi(() =>
                        {
                            if (File.Exists(prev!))
                                WallpaperManager.Instance.ApplyWallpaperFromPath(prev!, Settings.Instance.TargetMonitor);
                        });
                    }
                }

                _lastSsid = ssid;
            }
            catch { /* best-effort background watcher */ }
        }

        private void ApplyTriggerWallpaper()
        {
            // Remember the current wallpaper so we can restore it on disconnect.
            try { _wallpaperBeforeTrigger = WallpaperManager.GetCurrentWallpaper(); } catch { }

            string configured = Settings.Instance.WifiTriggerWallpaperPath?.Trim() ?? "";
            string target = !string.IsNullOrEmpty(configured) && File.Exists(configured)
                ? configured
                : Settings.Instance.SafeWallpaperPath;

            RunOnUi(() =>
            {
                if (!string.IsNullOrEmpty(target) && File.Exists(target))
                    WallpaperManager.Instance.ApplyWallpaperFromPath(target, Settings.Instance.TargetMonitor);
                else
                    WallpaperManager.Instance.ApplySafeWallpaper();
            });
        }

        private static void RunOnUi(Action action)
        {
            var app = System.Windows.Application.Current;
            if (app != null) app.Dispatcher.Invoke(action);
            else action();
        }

        /// <summary>Return the SSID of the currently-connected Wi-Fi network, or null if none.</summary>
        public static string? GetCurrentSsid()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "wlan show interfaces",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc == null) return null;
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(4000);

                // Lines look like: "    SSID                   : MyNetwork"
                // Guard against matching "BSSID". Match SSID that is not preceded by 'B'.
                foreach (var rawLine in output.Split('\n'))
                {
                    var line = rawLine.Trim();
                    var m = Regex.Match(line, @"^SSID\s*:\s*(.+)$");
                    if (m.Success)
                    {
                        string val = m.Groups[1].Value.Trim();
                        return string.IsNullOrEmpty(val) ? null : val;
                    }
                }
            }
            catch { }
            return null;
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
                _timer?.Dispose();
                _timer = null;
            }
        }
    }
}
