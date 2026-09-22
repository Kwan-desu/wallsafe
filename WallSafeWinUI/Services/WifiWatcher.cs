using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WallSafeWinUI.Services
{
    /// <summary>
    /// Watches ONLY connected Wi-Fi (WLAN) networks (ignoring Ethernet, cellular, and virtual adapters).
    /// When the active Wi-Fi SSID matches any user-configured trigger network (e.g. school, work, or hotspot),
    /// automatically applies the safe wallpaper and pauses slideshow.
    /// Optionally restores previous wallpaper when disconnecting from the trigger Wi-Fi network.
    /// </summary>
    public sealed class WifiWatcher : IDisposable
    {
        private static WifiWatcher? _instance;
        public static WifiWatcher Instance => _instance ??= new WifiWatcher();

        private readonly object _sync = new();
        private System.Threading.Timer? _timer;
        private string? _lastMatchedSsid;
        private bool _triggerActive;
        private bool _userBypassedTrigger;
        private string? _wallpaperBeforeTrigger;
        private bool _disposed;
        private bool _eventHooked;

        public bool IsTriggerActive
        {
            get { lock (_sync) return _triggerActive; }
        }

        public bool UserBypassedTrigger
        {
            get { lock (_sync) return _userBypassedTrigger; }
            set { lock (_sync) _userBypassedTrigger = value; }
        }

        public bool ToggleBypassFromShortcut()
        {
            lock (_sync)
            {
                if (!_triggerActive) return false;
                _userBypassedTrigger = !_userBypassedTrigger;
                NotifyStatus(_lastMatchedSsid ?? GetConnectedWifiSsid(), _triggerActive, _lastMatchedSsid);
                return _userBypassedTrigger;
            }
        }

        public string? GetOriginalWallpaper()
        {
            lock (_sync)
            {
                if (!string.IsNullOrEmpty(_wallpaperBeforeTrigger) && File.Exists(_wallpaperBeforeTrigger) && !WallpaperManager.IsSafeWallpaper(_wallpaperBeforeTrigger))
                    return _wallpaperBeforeTrigger;

                var saved = Settings.Instance.WifiSavedOriginalWallpaper;
                if (!string.IsNullOrEmpty(saved) && File.Exists(saved) && !WallpaperManager.IsSafeWallpaper(saved))
                    return saved;

                var last = Settings.Instance.LastNormalWallpaperPath;
                if (!string.IsNullOrEmpty(last) && File.Exists(last) && !WallpaperManager.IsSafeWallpaper(last))
                    return last;

                return null;
            }
        }

        public event Action<string?, bool, string?>? StatusChanged;

        private WifiWatcher() { }

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
                        NotifyStatus(GetConnectedWifiSsid(), false, null);
                    }
                    return;
                }

                if (!_eventHooked)
                {
                    try
                    {
                        Windows.Networking.Connectivity.NetworkInformation.NetworkStatusChanged += OnNetworkStatusChanged;
                        _eventHooked = true;
                    }
                    catch { }
                }

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
                    NotifyStatus(GetConnectedWifiSsid(), false, null);
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
                    // Query ONLY real connected Wi-Fi (WLAN) SSIDs
                    var activeWifiSsids = GetConnectedWifiSsids();
                    var triggers = Settings.Instance.WifiTriggerSsids;

                    string? matchedTrigger = null;
                    string? matchedSsid = null;

                    if (triggers.Count > 0 && activeWifiSsids.Count > 0)
                    {
                        foreach (var ssid in activeWifiSsids)
                        {
                            foreach (var tr in triggers)
                            {
                                if (string.Equals(ssid, tr, StringComparison.OrdinalIgnoreCase) ||
                                    ssid.IndexOf(tr, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    tr.IndexOf(ssid, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    matchedSsid = ssid;
                                    matchedTrigger = tr;
                                    break;
                                }
                            }
                            if (matchedTrigger != null) break;
                        }
                    }

                    bool onTrigger = matchedTrigger != null;

                    if (onTrigger && !_triggerActive)
                    {
                        _triggerActive = true;
                        _lastMatchedSsid = matchedSsid;
                        _userBypassedTrigger = false;
                        ApplyTriggerWallpaper();
                        NotifyStatus(matchedSsid, true, matchedTrigger);
                    }
                    else if (onTrigger && _triggerActive)
                    {
                        NotifyStatus(matchedSsid ?? _lastMatchedSsid, true, matchedTrigger);
                    }
                    else if (!onTrigger && (_triggerActive || Settings.Instance.WifiTriggerWasActive))
                    {
                        _triggerActive = false;
                        _lastMatchedSsid = null;
                        _userBypassedTrigger = false;
                        RestoreWallpaperAfterTrigger();
                        NotifyStatus(GetConnectedWifiSsid(), false, null);
                    }
                    else
                    {
                        NotifyStatus(GetConnectedWifiSsid(), false, null);
                    }
                }
                catch { /* best-effort background watcher */ }
            }
        }

        private void NotifyStatus(string? currentSsid, bool isActive, string? matchedTrig)
        {
            try { StatusChanged?.Invoke(currentSsid, isActive, matchedTrig); } catch { }
        }

        private void ApplyTriggerWallpaper()
        {
            try
            {
                string curr = WallpaperManager.GetCurrentWallpaper();
                if (!string.IsNullOrEmpty(curr) && File.Exists(curr) && !WallpaperManager.IsSafeWallpaper(curr))
                {
                    _wallpaperBeforeTrigger = curr;
                    Settings.Instance.WifiSavedOriginalWallpaper = curr;
                }
                else if (!string.IsNullOrEmpty(Settings.Instance.WifiSavedOriginalWallpaper) && File.Exists(Settings.Instance.WifiSavedOriginalWallpaper))
                {
                    _wallpaperBeforeTrigger = Settings.Instance.WifiSavedOriginalWallpaper;
                }
                else if (!string.IsNullOrEmpty(Settings.Instance.LastNormalWallpaperPath) && File.Exists(Settings.Instance.LastNormalWallpaperPath))
                {
                    _wallpaperBeforeTrigger = Settings.Instance.LastNormalWallpaperPath;
                    Settings.Instance.WifiSavedOriginalWallpaper = Settings.Instance.LastNormalWallpaperPath;
                }
                Settings.Instance.WifiTriggerWasActive = true;
                Settings.Instance.Save();
            }
            catch { }

            _userBypassedTrigger = false;

            RunOnUi(() =>
            {
                WallpaperManager.Instance.ApplySafeWallpaper(isWifiTrigger: true);
            });
        }

        private void RestoreWallpaperAfterTrigger()
        {
            string? prev = GetOriginalWallpaper();

            Settings.Instance.WifiTriggerWasActive = false;
            Settings.Instance.WifiSavedOriginalWallpaper = null;
            Settings.Instance.Save();

            _userBypassedTrigger = false;
            _wallpaperBeforeTrigger = null;

            bool shouldRestore = Settings.Instance.WifiRestoreOnDisconnect || !string.IsNullOrEmpty(prev);
            if (shouldRestore && !string.IsNullOrEmpty(prev) && File.Exists(prev))
            {
                RunOnUi(() =>
                {
                    WallpaperManager.Instance.ApplyWallpaperFromPath(prev, Settings.Instance.TargetMonitor);
                });
            }
        }

        private static void RunOnUi(Action action) => UiDispatch.Post(action);

        /// <summary>
        /// Returns all currently connected Wi-Fi (WLAN) SSIDs. Strictly ignores Ethernet, cellular, and virtual adapters.
        /// </summary>
        public static HashSet<string> GetConnectedWifiSsids()
        {
            var ssids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Windows Native WLAN API (wlanapi.dll) - queries physical Wi-Fi adapters directly
            try
            {
                var nativeSsids = QueryNativeWlanConnectedSsids();
                foreach (var s in nativeSsids)
                {
                    if (!string.IsNullOrWhiteSpace(s))
                        ssids.Add(s.Trim());
                }
            }
            catch { }

            // 2. WinRT NetworkInformation - filtered STRICTLY to WLAN profiles that are currently connected
            try
            {
                var profiles = Windows.Networking.Connectivity.NetworkInformation.GetConnectionProfiles();
                if (profiles != null)
                {
                    foreach (var p in profiles)
                    {
                        if (p.IsWlanConnectionProfile &&
                            p.GetNetworkConnectivityLevel() != Windows.Networking.Connectivity.NetworkConnectivityLevel.None)
                        {
                            if (p.WlanConnectionProfileDetails != null)
                            {
                                string ssid = p.WlanConnectionProfileDetails.GetConnectedSsid();
                                if (!string.IsNullOrWhiteSpace(ssid))
                                    ssids.Add(ssid.Trim());
                            }

                            if (!string.IsNullOrWhiteSpace(p.ProfileName))
                                ssids.Add(p.ProfileName.Trim());
                        }
                    }
                }
            }
            catch { }

            // 3. Fallback: netsh wlan show interfaces (only if connected)
            try
            {
                string output = RunNetsh("wlan show interfaces");
                bool isConnected = false;
                foreach (var rawLine in output.Split('\n'))
                {
                    var line = rawLine.Trim();
                    if (Regex.IsMatch(line, @"^State\s*:\s*connected", RegexOptions.IgnoreCase))
                        isConnected = true;

                    if (isConnected)
                    {
                        var m = Regex.Match(line, @"^SSID\s*:\s*(.+)$");
                        if (m.Success)
                        {
                            string val = m.Groups[1].Value.Trim();
                            if (!string.IsNullOrWhiteSpace(val))
                                ssids.Add(val);
                        }
                    }
                }
            }
            catch { }

            return ssids;
        }

        /// <summary>
        /// Return the primary connected Wi-Fi SSID, or null if Wi-Fi is disconnected.
        /// </summary>
        public static string? GetConnectedWifiSsid()
        {
            return GetConnectedWifiSsids().FirstOrDefault();
        }

        public static string? GetCurrentSsid() => GetConnectedWifiSsid();
        public static string? GetPrimaryConnectedNetworkName() => GetConnectedWifiSsid();

        #region Native WLAN API Interop

        [DllImport("wlanapi.dll")]
        private static extern int WlanOpenHandle(uint dwClientVersion, IntPtr pReserved, out uint pdwNegotiatedVersion, out IntPtr phClientHandle);

        [DllImport("wlanapi.dll")]
        private static extern int WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

        [DllImport("wlanapi.dll")]
        private static extern int WlanEnumInterfaces(IntPtr hClientHandle, IntPtr pReserved, out IntPtr ppInterfaceList);

        [DllImport("wlanapi.dll")]
        private static extern void WlanFreeMemory(IntPtr pMemory);

        [DllImport("wlanapi.dll")]
        private static extern int WlanQueryInterface(IntPtr hClientHandle, ref Guid pInterfaceGuid, int OpCode, IntPtr pReserved, out uint pdwDataSize, out IntPtr ppData, out int pWlanOpcodeValueType);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WLAN_INTERFACE_INFO
        {
            public Guid InterfaceGuid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strInterfaceDescription;
            public int isState; // 1 = connected
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DOT11_SSID
        {
            public uint uSSIDLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] ucSSID;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WLAN_CONNECTION_ATTRIBUTES
        {
            public int isState;
            public int wlanConnectionMode;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strProfileName;
            public WLAN_ASSOCIATION_ATTRIBUTES wlanAssociationAttributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WLAN_ASSOCIATION_ATTRIBUTES
        {
            public DOT11_SSID dot11Ssid;
            public int dot11BssType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public byte[] dot11Bssid;
            public int dot11PhyType;
            public uint uDot11PhyIndex;
            public uint wlanSignalQuality;
            public uint ulRxRate;
            public uint ulTxRate;
        }

        private static List<string> QueryNativeWlanConnectedSsids()
        {
            var list = new List<string>();
            IntPtr clientHandle = IntPtr.Zero;
            IntPtr pIfList = IntPtr.Zero;

            try
            {
                if (WlanOpenHandle(2, IntPtr.Zero, out _, out clientHandle) != 0) return list;
                if (WlanEnumInterfaces(clientHandle, IntPtr.Zero, out pIfList) != 0) return list;

                uint count = (uint)Marshal.ReadInt32(pIfList);
                IntPtr pInfo = new IntPtr(pIfList.ToInt64() + 8);
                int infoSize = Marshal.SizeOf(typeof(WLAN_INTERFACE_INFO));

                for (int i = 0; i < count; i++)
                {
                    var info = (WLAN_INTERFACE_INFO)Marshal.PtrToStructure(new IntPtr(pInfo.ToInt64() + (i * infoSize)), typeof(WLAN_INTERFACE_INFO))!;
                    if (info.isState == 1) // wlan_interface_state_connected
                    {
                        IntPtr pConn = IntPtr.Zero;
                        try
                        {
                            // 7 = wlan_intf_opcode_current_connection
                            if (WlanQueryInterface(clientHandle, ref info.InterfaceGuid, 7, IntPtr.Zero, out _, out pConn, out _) == 0 && pConn != IntPtr.Zero)
                            {
                                var conn = (WLAN_CONNECTION_ATTRIBUTES)Marshal.PtrToStructure(pConn, typeof(WLAN_CONNECTION_ATTRIBUTES))!;
                                if (conn.wlanAssociationAttributes.dot11Ssid.uSSIDLength > 0)
                                {
                                    string ssid = Encoding.UTF8.GetString(conn.wlanAssociationAttributes.dot11Ssid.ucSSID, 0, (int)conn.wlanAssociationAttributes.dot11Ssid.uSSIDLength);
                                    if (!string.IsNullOrEmpty(ssid) && !list.Contains(ssid))
                                        list.Add(ssid);
                                }
                            }
                        }
                        finally
                        {
                            if (pConn != IntPtr.Zero) WlanFreeMemory(pConn);
                        }
                    }
                }
            }
            finally
            {
                if (pIfList != IntPtr.Zero) WlanFreeMemory(pIfList);
                if (clientHandle != IntPtr.Zero) WlanCloseHandle(clientHandle, IntPtr.Zero);
            }

            return list;
        }

        #endregion

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
