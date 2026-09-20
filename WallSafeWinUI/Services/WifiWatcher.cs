using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WallSafeWinUI.Services
{
    /// <summary>
    /// Watches currently-connected Wi-Fi SSIDs and Network Connection Profiles.
    /// When any connected network matches the user-configured trigger networks (e.g. school, work,
    /// or specific SSIDs/profiles), automatically applies the safe wallpaper and pauses slideshow.
    /// Optionally restores the previous wallpaper on disconnect.
    /// </summary>
    public sealed class WifiWatcher : IDisposable
    {
        private static WifiWatcher? _instance;
        public static WifiWatcher Instance => _instance ??= new WifiWatcher();

        private readonly object _sync = new();
        private System.Threading.Timer? _timer;
        private string? _lastMatchedNetwork;
        private bool _triggerActive;             // currently on the trigger network
        private string? _wallpaperBeforeTrigger; // to restore on disconnect
        private bool _disposed;
        private bool _eventHooked;

        public bool IsTriggerActive
        {
            get { lock (_sync) return _triggerActive; }
        }

        /// <summary>
        /// Fired whenever network status or trigger state updates: (currentNetwork, isTriggerActive, matchedTrigger).
        /// </summary>
        public event Action<string?, bool, string?>? StatusChanged;

        private WifiWatcher() { }

        /// <summary>Start (or restart) polling based on current settings.</summary>
        public void Start(bool immediate = false)
        {
            lock (_sync)
            {
                if (_disposed) return;
                _timer?.Dispose();

                if (!Settings.Instance.WifiAutoWallpaperEnabled)
                {
                    _timer = null;
                    if (_triggerActive)
                    {
                        _triggerActive = false;
                        RestoreWallpaperAfterTrigger();
                        NotifyStatus(GetPrimaryConnectedNetworkName(), false, null);
                    }
                    return;
                }

                // Hook Windows network change notifications once for instant reaction
                if (!_eventHooked)
                {
                    try
                    {
                        Windows.Networking.Connectivity.NetworkInformation.NetworkStatusChanged += OnNetworkStatusChanged;
                        _eventHooked = true;
                    }
                    catch { }
                }

                // Reset trigger edge so immediate poll re-evaluates
                _triggerActive = false;

                _timer = new System.Threading.Timer(_ => Poll(), null,
                    immediate ? TimeSpan.Zero : TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(5));
            }
        }

        public void TriggerImmediatePoll()
        {
            Task.Run(() => Poll());
        }

        private void OnNetworkStatusChanged(object? sender)
        {
            TriggerImmediatePoll();
        }

        public void Stop()
        {
            lock (_sync)
            {
                _timer?.Dispose();
                _timer = null;
                if (_triggerActive)
                {
                    _triggerActive = false;
                    RestoreWallpaperAfterTrigger();
                    NotifyStatus(GetPrimaryConnectedNetworkName(), false, null);
                }
            }
        }

        public void Poll()
        {
            lock (_sync)
            {
                if (_disposed || !Settings.Instance.WifiAutoWallpaperEnabled) return;

                try
                {
                    var activeNetworks = GetAllCurrentNetworkNames();
                    var triggers = Settings.Instance.WifiTriggerSsids;

                    string? matchedTrigger = null;
                    string? matchedNetwork = null;

                    if (triggers.Count > 0 && activeNetworks.Count > 0)
                    {
                        foreach (var net in activeNetworks)
                        {
                            foreach (var tr in triggers)
                            {
                                if (string.Equals(net, tr, StringComparison.OrdinalIgnoreCase) ||
                                    net.IndexOf(tr, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    tr.IndexOf(net, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    matchedNetwork = net;
                                    matchedTrigger = tr;
                                    break;
                                }
                            }
                            if (matchedTrigger != null) break;
                        }
                    }

                    bool onTrigger = matchedTrigger != null;

                    // Rising edge: just connected to the trigger network
                    if (onTrigger && !_triggerActive)
                    {
                        _triggerActive = true;
                        _lastMatchedNetwork = matchedNetwork;
                        ApplyTriggerWallpaper();
                        NotifyStatus(matchedNetwork, true, matchedTrigger);
                    }
                    else if (onTrigger && _triggerActive)
                    {
                        NotifyStatus(matchedNetwork ?? _lastMatchedNetwork, true, matchedTrigger);
                    }
                    // Falling edge: disconnected from the trigger network
                    else if (!onTrigger && _triggerActive)
                    {
                        _triggerActive = false;
                        _lastMatchedNetwork = null;
                        RestoreWallpaperAfterTrigger();
                        NotifyStatus(GetPrimaryConnectedNetworkName(), false, null);
                    }
                    else
                    {
                        NotifyStatus(GetPrimaryConnectedNetworkName(), false, null);
                    }
                }
                catch { /* best-effort background watcher */ }
            }
        }

        private void NotifyStatus(string? currentNet, bool isActive, string? matchedTrig)
        {
            try { StatusChanged?.Invoke(currentNet, isActive, matchedTrig); } catch { }
        }

        private void ApplyTriggerWallpaper()
        {
            try
            {
                string curr = WallpaperManager.GetCurrentWallpaper();
                var safeTarget = Settings.Instance.SafeWallpaperPath;
                if (string.IsNullOrEmpty(safeTarget) || !File.Exists(safeTarget))
                    safeTarget = Settings.DetectDefaultWin11Wallpaper();

                // Only record previous wallpaper if it's not already the safe wallpaper
                if (!string.Equals(curr, safeTarget, StringComparison.OrdinalIgnoreCase))
                    _wallpaperBeforeTrigger = curr;
            }
            catch { }

            string configured = Settings.Instance.WifiTriggerWallpaperPath?.Trim() ?? "";
            string target = !string.IsNullOrEmpty(configured) && File.Exists(configured)
                ? configured
                : Settings.Instance.SafeWallpaperPath;

            RunOnUi(() =>
            {
                if (!string.IsNullOrEmpty(target) && File.Exists(target))
                    WallpaperManager.Instance.ApplyWallpaperFromPath(target, Settings.Instance.TargetMonitor);
                else
                    WallpaperManager.Instance.ApplySafeWallpaper(isWifiTrigger: true);
            });
        }

        private void RestoreWallpaperAfterTrigger()
        {
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

        private static void RunOnUi(Action action) => UiDispatch.Post(action);

        /// <summary>
        /// Returns all currently active Wi-Fi SSIDs, Network Names, and Connection Profiles.
        /// Uses Windows Network List Manager COM API, WinRT NetworkInformation, and netsh fallback.
        /// </summary>
        public static HashSet<string> GetAllCurrentNetworkNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Windows Network List Manager (NLM) COM API - works on all Windows versions,
            // non-admin, returns actual active network names (e.g. Kwanmk_5G, Student@APU, etc.)
            try
            {
                var nlmType = Type.GetTypeFromCLSID(new Guid("DCB00C01-570F-4A9B-8D69-199FDBA5723B"));
                if (nlmType != null)
                {
                    dynamic nlm = Activator.CreateInstance(nlmType)!;
                    var networks = nlm.GetNetworks(1); // NLM_ENUM_NETWORK_CONNECTED = 1
                    foreach (dynamic net in networks)
                    {
                        string name = net.GetName();
                        if (!string.IsNullOrWhiteSpace(name)) names.Add(name.Trim());
                        string desc = net.GetDescription();
                        if (!string.IsNullOrWhiteSpace(desc)) names.Add(desc.Trim());
                    }
                }
            }
            catch { }

            // 2. WinRT NetworkInformation - queries all connected profiles and Wi-Fi SSIDs
            try
            {
                var profiles = Windows.Networking.Connectivity.NetworkInformation.GetConnectionProfiles();
                if (profiles != null)
                {
                    foreach (var p in profiles)
                    {
                        var level = p.GetNetworkConnectivityLevel();
                        if (level != Windows.Networking.Connectivity.NetworkConnectivityLevel.None)
                        {
                            if (!string.IsNullOrWhiteSpace(p.ProfileName))
                                names.Add(p.ProfileName.Trim());

                            var netNames = p.GetNetworkNames();
                            if (netNames != null)
                            {
                                foreach (var n in netNames)
                                    if (!string.IsNullOrWhiteSpace(n)) names.Add(n.Trim());
                            }

                            if (p.WlanConnectionProfileDetails != null)
                            {
                                string ssid = p.WlanConnectionProfileDetails.GetConnectedSsid();
                                if (!string.IsNullOrWhiteSpace(ssid)) names.Add(ssid.Trim());
                            }

                            // If connected to internet, also include "Internet" as a recognized keyword
                            if (level == Windows.Networking.Connectivity.NetworkConnectivityLevel.InternetAccess)
                            {
                                names.Add("Internet");
                            }
                        }
                    }
                }
            }
            catch { }

            // 3. Fallback: netsh wlan show interfaces
            try
            {
                string output = RunNetsh("wlan show interfaces");
                foreach (var rawLine in output.Split('\n'))
                {
                    var line = rawLine.Trim();
                    var m = Regex.Match(line, @"^SSID\s*:\s*(.+)$");
                    if (m.Success)
                    {
                        string val = m.Groups[1].Value.Trim();
                        if (!string.IsNullOrWhiteSpace(val)) names.Add(val);
                    }
                }
            }
            catch { }

            return names;
        }

        /// <summary>
        /// Return the primary connected network name for UI display.
        /// </summary>
        public static string? GetPrimaryConnectedNetworkName()
        {
            var all = GetAllCurrentNetworkNames();
            // Prioritize user-facing network names over virtual adapters
            var prioritized = all.Where(n => !n.StartsWith("vEthernet", StringComparison.OrdinalIgnoreCase) &&
                                             !string.Equals(n, "Tailscale", StringComparison.OrdinalIgnoreCase) &&
                                             !string.Equals(n, "Internet", StringComparison.OrdinalIgnoreCase)).ToList();

            return prioritized.FirstOrDefault() ?? all.FirstOrDefault();
        }

        /// <summary>Run a netsh command and return its stdout (empty string on failure).</summary>
        private static string RunNetsh(string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc == null) return "";
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(4000);
                return output;
            }
            catch { return ""; }
        }

        /// <summary>Return the SSID of the currently-connected Wi-Fi network, or null if none.</summary>
        public static string? GetCurrentSsid()
        {
            return GetPrimaryConnectedNetworkName();
        }

        /// <summary>
        /// Return all Wi-Fi network profiles saved on this machine.
        /// </summary>
        public static List<string> GetSavedProfiles()
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string output = RunNetsh("wlan show profiles");

            foreach (var rawLine in output.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;

                var m = Regex.Match(line, @"[Pp]rofile\s*:\s*(.+)$");
                if (m.Success)
                {
                    string name = m.Groups[1].Value.Trim();
                    if (name.Length > 0 && seen.Add(name))
                        result.Add(name);
                }
            }
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
                try
                {
                    if (_eventHooked)
                        Windows.Networking.Connectivity.NetworkInformation.NetworkStatusChanged -= OnNetworkStatusChanged;
                }
                catch { }
                _timer?.Dispose();
                _timer = null;
            }
        }
    }
}
