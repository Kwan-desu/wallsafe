using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Win32;

namespace WallSafe
{
    #region COM Interop Definitions

    [ComImport]
    [Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorID, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorID);
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetMonitorDevicePathAt(uint monitorIndex);
        uint GetMonitorDevicePathCount();
        RECT GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorID);
        void SetBackgroundColor(uint color);
        uint GetBackgroundColor();
        void SetPosition(DesktopWallpaperPosition position);
        DesktopWallpaperPosition GetPosition();
        void SetSlideshow(IntPtr items);
        IntPtr GetSlideshow();
        void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string? monitorID, DesktopSlideshowDirection direction);
        DesktopSlideshowDirection GetStatus();
        bool Enable();
    }

    public enum DesktopWallpaperPosition { Center = 0, Tile = 1, Stretch = 2, Fit = 3, Fill = 4, Span = 5 }
    public enum DesktopSlideshowDirection { Forward = 0, Backward = 1 }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [ComImport]
    [Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD")]
    public class DesktopWallpaperCoClass { }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291433E")]
    internal class MMDeviceEnumeratorCoClass { }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        int Activate(ref Guid id, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr pNotify);
        int UnregisterControlChangeNotify(IntPtr pNotify);
        int GetChannelCount(out uint pnChannelCount);
        int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        int GetMasterVolumeLevel(out float pfLevelDB);
        int GetMasterVolumeLevelScalar(out float pfLevel);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
    }

    #endregion

    public class WallpaperManager
    {
        public static readonly WallpaperManager Instance = new();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        private const int SPI_SETDESKWALLPAPER = 20;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDCHANGE = 0x02;

        private readonly HttpClient _http = new()
        {
            Timeout = TimeSpan.FromSeconds(30),
            DefaultRequestHeaders = { { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36" } }
        };
        private string? _previousWallpaper;
        private string? _safeWallpaper;
        private bool _isPanicActive = false;
        private readonly string _cacheDir;
        private readonly DispatcherTimer _slideshowTimer;
        private readonly List<PostItem> _slideshowQueue = new();
        private readonly object _slideshowLock = new();
        private string? _lastSlideshowItemKey;

        public event Action<string>? WallpaperApplied;
        public event Action? SafeWallpaperTriggered;
        public event Action? SafeWallpaperRestored;

        public bool IsPanicActive => _isPanicActive;

        private WallpaperManager()
        {
            _cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WallSafe", "cache");
            Directory.CreateDirectory(_cacheDir);

            _safeWallpaper = Settings.Instance.SafeWallpaperPath;
            if (string.IsNullOrEmpty(_safeWallpaper) || !File.Exists(_safeWallpaper))
            {
                _safeWallpaper = Settings.DetectDefaultWin11Wallpaper();
                if (string.IsNullOrEmpty(_safeWallpaper))
                {
                    _safeWallpaper = GetCurrentWallpaper();
                }
            }

            _previousWallpaper = GetCurrentWallpaper();

            _slideshowTimer = new DispatcherTimer();
            _slideshowTimer.Tick += (_, _) => OnSlideshowTick();

            if (Settings.Instance.SlideshowEnabled)
            {
                StartSlideshow(Settings.Instance.SlideshowIntervalMinutes);
            }
        }

        public static string GetCurrentWallpaper()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", false);
                return key?.GetValue("Wallpaper") as string ?? "";
            }
            catch
            {
                return "";
            }
        }

        public uint GetMonitorCount()
        {
            try
            {
                var desktop = (IDesktopWallpaper)new DesktopWallpaperCoClass();
                return desktop.GetMonitorDevicePathCount();
            }
            catch
            {
                return (uint)System.Windows.Forms.Screen.AllScreens.Length;
            }
        }

        public List<DisplayMonitorInfo> GetConnectedMonitors()
        {
            var list = new List<DisplayMonitorInfo>();
            try
            {
                var desktop = (IDesktopWallpaper)new DesktopWallpaperCoClass();
                uint count = desktop.GetMonitorDevicePathCount();
                for (uint i = 0; i < count; i++)
                {
                    string path = desktop.GetMonitorDevicePathAt(i);
                    var rect = desktop.GetMonitorRECT(path);
                    int w = rect.Right - rect.Left;
                    int h = rect.Bottom - rect.Top;
                    list.Add(new DisplayMonitorInfo
                    {
                        Index = (int)i,
                        DeviceId = path,
                        DeviceName = $"Display {i + 1}",
                        Width = w > 0 ? w : 1920,
                        Height = h > 0 ? h : 1080,
                        IsPrimary = (i == 0)
                    });
                }
            }
            catch { }

            if (list.Count == 0)
            {
                var screens = System.Windows.Forms.Screen.AllScreens;
                for (int i = 0; i < screens.Length; i++)
                {
                    var s = screens[i];
                    list.Add(new DisplayMonitorInfo
                    {
                        Index = i,
                        DeviceId = s.DeviceName,
                        DeviceName = $"Display {i + 1}",
                        Width = s.Bounds.Width,
                        Height = s.Bounds.Height,
                        IsPrimary = s.Primary
                    });
                }
            }
            return list;
        }

        public async Task<bool> SetLockScreenAsync(string imageUrlOrPath)
        {
            try
            {
                string localFile;
                if (File.Exists(imageUrlOrPath))
                {
                    localFile = imageUrlOrPath;
                }
                else
                {
                    var uri = new Uri(imageUrlOrPath);
                    var ext = Path.GetExtension(uri.AbsolutePath);
                    if (string.IsNullOrEmpty(ext)) ext = ".jpg";
                    localFile = Path.Combine(_cacheDir, $"lockscreen_cache_{Guid.NewGuid():N}{ext}");
                    var data = await _http.GetByteArrayAsync(imageUrlOrPath);
                    await File.WriteAllBytesAsync(localFile, data);
                }

                string lockScreenTarget = Path.Combine(_cacheDir, "lockscreen.jpg");
                File.Copy(localFile, lockScreenTarget, overwrite: true);

                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\PersonalizationCSP");
                if (key != null)
                {
                    key.SetValue("LockScreenImagePath", lockScreenTarget);
                    key.SetValue("LockScreenImageUrl", lockScreenTarget);
                    key.SetValue("LockScreenImageStatus", 1);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task ApplyWallpaperAsync(PostItem post, int monitorIndex = -1)
        {
            string targetPath;

            if (!string.IsNullOrEmpty(post.LocalPath) && File.Exists(post.LocalPath))
            {
                targetPath = post.LocalPath;
            }
            else
            {
                // Prioritize FullDownloadUrl (full resolution master image, e.g. 4K/6K/8K uncompressed)
                string fullUrl = !string.IsNullOrEmpty(post.FullDownloadUrl) ? post.FullDownloadUrl : post.BestImageUrl;
                string ext = ".jpg";
                try
                {
                    var uri = new Uri(fullUrl);
                    ext = Path.GetExtension(uri.AbsolutePath);
                    if (string.IsNullOrEmpty(ext)) ext = ".jpg";
                }
                catch { }

                targetPath = Path.Combine(_cacheDir, $"{post.Source}_{post.Id}_full{ext}");

                // If not cached yet, download the original full resolution image
                if (!File.Exists(targetPath) || new FileInfo(targetPath).Length == 0)
                {
                    bool downloaded = false;
                    try
                    {
                        using var req = new HttpRequestMessage(HttpMethod.Get, fullUrl);
                        var refUri = ApiClient.GetReferrerForUrl(fullUrl);
                        if (refUri != null) req.Headers.Referrer = refUri;

                        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                        if (resp.IsSuccessStatusCode)
                        {
                            await using var fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
                            await resp.Content.CopyToAsync(fs);
                            downloaded = true;
                        }
                    }
                    catch { }

                    // Fallback to BestImageUrl / SampleUrl if full image failed to download
                    if (!downloaded && fullUrl != post.BestImageUrl)
                    {
                        try
                        {
                            using var fallbackReq = new HttpRequestMessage(HttpMethod.Get, post.BestImageUrl);
                            var fallbackRef = ApiClient.GetReferrerForUrl(post.BestImageUrl);
                            if (fallbackRef != null) fallbackReq.Headers.Referrer = fallbackRef;

                            using var fallbackResp = await _http.SendAsync(fallbackReq, HttpCompletionOption.ResponseHeadersRead);
                            fallbackResp.EnsureSuccessStatusCode();
                            await using var fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
                            await fallbackResp.Content.CopyToAsync(fs);
                        }
                        catch
                        {
                            // Final fallback: local cached thumbnail
                            string cachedThumb = await ImageCacheService.Instance.GetCachedImagePathAsync(post.BestImageUrl);
                            if (File.Exists(cachedThumb) && new FileInfo(cachedThumb).Length > 0)
                            {
                                targetPath = cachedThumb;
                            }
                        }
                    }
                }
            }

            _previousWallpaper = GetCurrentWallpaper();
            _isPanicActive = false;
            SetWallpaper(targetPath, monitorIndex);

            // Record in persistent history
            try
            {
                HistoryManager.Instance.RecordApplied(post, targetPath);
            }
            catch { }

            WallpaperApplied?.Invoke(targetPath);
        }

        public void ApplyWallpaperFromPath(string path, int monitorIndex = -1)
        {
            if (File.Exists(path))
            {
                _previousWallpaper = GetCurrentWallpaper();
                _isPanicActive = false;
                SetWallpaper(path, monitorIndex);
                WallpaperApplied?.Invoke(path);
            }
        }

        /// <summary>
        /// Bimodal Panic: Toggles between applying safe wallpaper and restoring previous anime wallpaper.
        /// </summary>
        public void ApplySafeWallpaper()
        {
            if (_isPanicActive && Settings.Instance.PanicRestoreToggle && !string.IsNullOrEmpty(_previousWallpaper) && File.Exists(_previousWallpaper))
            {
                // Restore previous wallpaper
                SetWallpaper(_previousWallpaper);
                _isPanicActive = false;
                SafeWallpaperRestored?.Invoke();
                WallpaperApplied?.Invoke(_previousWallpaper);
                return;
            }

            // Save pre-panic wallpaper
            _previousWallpaper = GetCurrentWallpaper();

            var target = Settings.Instance.SafeWallpaperPath;
            if (string.IsNullOrEmpty(target) || !File.Exists(target))
            {
                target = Settings.DetectDefaultWin11Wallpaper();
            }

            if (!string.IsNullOrEmpty(target) && File.Exists(target))
            {
                SetWallpaper(target);
                _isPanicActive = true;
                SafeWallpaperTriggered?.Invoke();
            }
        }

        public void SetSafeWallpaper(string path)
        {
            _safeWallpaper = path;
            Settings.Instance.SafeWallpaperPath = path;
            Settings.Instance.Save();
        }

        public void StartSlideshow(int intervalMinutes, bool advanceImmediately = false)
        {
            _slideshowTimer.Stop();
            int safeMinutes = Math.Max(1, intervalMinutes);
            _slideshowTimer.Interval = TimeSpan.FromMinutes(safeMinutes);
            _slideshowTimer.Start();

            if (advanceImmediately)
            {
                NextSlideshowWallpaper();
            }
        }

        public void StopSlideshow()
        {
            _slideshowTimer.Stop();
        }

        private void OnSlideshowTick()
        {
            if (!Settings.Instance.SlideshowEnabled || _isPanicActive) return;

            // Check if user is playing a 3D game or running fullscreen video
            if (IsForegroundFullscreen()) return;

            NextSlideshowWallpaper();
        }

        /// <summary>Immediately advance to the next wallpaper in the shuffle cycle queue.
        /// Usable from the tray menu or UI even when the timer-based slideshow is off.</summary>
        public void NextSlideshowWallpaper()
        {
            if (_slideshowTimer.IsEnabled)
            {
                _slideshowTimer.Stop();
                _slideshowTimer.Start();
            }
            _ = NextSlideshowWallpaperAsync();
        }

        public void ResetSlideshowQueue()
        {
            lock (_slideshowLock)
            {
                _slideshowQueue.Clear();
            }
        }

        private static string GetItemKey(PostItem item)
        {
            if (!string.IsNullOrEmpty(item.Source) && item.Id > 0)
                return $"{item.Source}_{item.Id}";
            if (!string.IsNullOrEmpty(item.LocalPath))
                return item.LocalPath;
            if (!string.IsNullOrEmpty(item.FullDownloadUrl))
                return item.FullDownloadUrl;
            if (!string.IsNullOrEmpty(item.BestImageUrl))
                return item.BestImageUrl;
            return item.FileUrl ?? item.SampleUrl ?? item.PreviewUrl ?? Guid.NewGuid().ToString("N");
        }

        private List<PostItem> GetSlideshowPool()
        {
            List<PostItem> rawPool;
            string src = Settings.Instance.SlideshowSource ?? "favorites";
            if (src == "downloads")
            {
                rawPool = DownloadsManager.Instance.GetAllDownloads();
            }
            else if (src.StartsWith("collection:", StringComparison.OrdinalIgnoreCase))
            {
                string collection = src.Substring("collection:".Length);
                rawPool = FavoritesManager.Instance.GetFavoritesByCollection(collection);
            }
            else
            {
                rawPool = FavoritesManager.Instance.GetAllFavorites();
            }

            return rawPool.Where(i =>
                (!string.IsNullOrEmpty(i.LocalPath) && File.Exists(i.LocalPath)) ||
                !string.IsNullOrEmpty(i.FullDownloadUrl) ||
                !string.IsNullOrEmpty(i.BestImageUrl) ||
                !string.IsNullOrEmpty(i.FileUrl) ||
                !string.IsNullOrEmpty(i.SampleUrl)
            ).ToList();
        }

        public async Task NextSlideshowWallpaperAsync()
        {
            if (_isPanicActive) return;

            PostItem? nextItem = null;

            lock (_slideshowLock)
            {
                var pool = GetSlideshowPool();
                if (pool.Count == 0) return;

                // Prune queue: remove items that are no longer in the active pool (e.g., deleted or unfavorited)
                var poolKeys = new HashSet<string>(pool.Select(GetItemKey));
                _slideshowQueue.RemoveAll(i => !poolKeys.Contains(GetItemKey(i)));

                // If queue is empty, refill with all pool items and perform Fisher-Yates shuffle
                // This guarantees every wallpaper is shown at least once per cycle before any repetitions
                if (_slideshowQueue.Count == 0)
                {
                    var cycle = new List<PostItem>(pool);
                    for (int i = cycle.Count - 1; i > 0; i--)
                    {
                        int j = Random.Shared.Next(i + 1);
                        (cycle[i], cycle[j]) = (cycle[j], cycle[i]);
                    }

                    // Anti-repeat boundary check: ensure first wallpaper in new cycle isn't identical to the last one played
                    if (cycle.Count > 1 && !string.IsNullOrEmpty(_lastSlideshowItemKey) && GetItemKey(cycle[0]) == _lastSlideshowItemKey)
                    {
                        (cycle[0], cycle[^1]) = (cycle[^1], cycle[0]);
                    }

                    _slideshowQueue.AddRange(cycle);
                }

                if (_slideshowQueue.Count > 0)
                {
                    nextItem = _slideshowQueue[0];
                    _slideshowQueue.RemoveAt(0);
                    _lastSlideshowItemKey = GetItemKey(nextItem);
                }
            }

            if (nextItem != null)
            {
                try
                {
                    await ApplyWallpaperAsync(nextItem, Settings.Instance.TargetMonitor);
                }
                catch { }
            }
        }

        public static bool IsForegroundFullscreen()
        {
            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return false;

            var sb = new StringBuilder(256);
            GetClassName(hWnd, sb, sb.Capacity);
            string className = sb.ToString();
            if (className is "Progman" or "WorkerW" or "Shell_TrayWnd") return false;

            if (GetWindowRect(hWnd, out var rect))
            {
                var screen = System.Windows.Forms.Screen.FromHandle(hWnd);
                return rect.Left <= screen.Bounds.Left &&
                       rect.Top <= screen.Bounds.Top &&
                       rect.Right >= screen.Bounds.Right &&
                       rect.Bottom >= screen.Bounds.Bottom;
            }
            return false;
        }

        public static void MinimizeAllWindows()
        {
            try
            {
                // Win + D key sequence
                keybd_event(0x5B /* VK_LWIN */, 0, 0, 0);
                keybd_event(0x44 /* 'D' */, 0, 0, 0);
                keybd_event(0x44, 0, 2 /* KEYEVENTF_KEYUP */, 0);
                keybd_event(0x5B, 0, 2, 0);
            }
            catch { }
        }

        public static void MuteMasterAudio()
        {
            try
            {
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorCoClass();
                enumerator.GetDefaultAudioEndpoint(0 /* eRender */, 1 /* eMultimedia */, out var device);
                var iid = typeof(IAudioEndpointVolume).GUID;
                device.Activate(ref iid, 1 /* CLSCTX_ALL */, IntPtr.Zero, out var epvObj);
                var epv = (IAudioEndpointVolume)epvObj;
                var nullGuid = Guid.Empty;
                epv.SetMute(true, ref nullGuid);
            }
            catch { }
        }

        private static void SetWallpaper(string path, int monitorIndex = -1)
        {
            try
            {
                // Use IDesktopWallpaper on Windows 8/10/11
                var desktop = (IDesktopWallpaper)new DesktopWallpaperCoClass();
                desktop.SetPosition(DesktopWallpaperPosition.Fill);

                if (monitorIndex >= 0)
                {
                    uint count = desktop.GetMonitorDevicePathCount();
                    if (monitorIndex < count)
                    {
                        string monitorId = desktop.GetMonitorDevicePathAt((uint)monitorIndex);
                        desktop.SetWallpaper(monitorId, path);
                        return;
                    }
                }

                // Apply to all monitors
                desktop.SetWallpaper(null, path);
                return;
            }
            catch
            {
                // Fallback to legacy SystemParametersInfo
            }

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
                if (key != null)
                {
                    key.SetValue("WallpaperStyle", "10"); // Fill
                    key.SetValue("TileWallpaper", "0");
                }
            }
            catch { }

            SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path,
                SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
        }
    }

}
