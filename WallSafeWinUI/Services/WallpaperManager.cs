using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace WallSafeWinUI.Services
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

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int SM_CMONITORS = 80;
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
        private System.Threading.Timer? _slideshowTimer;
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
                if (string.IsNullOrEmpty(_safeWallpaper)) _safeWallpaper = GetCurrentWallpaper();
            }

            _previousWallpaper = GetCurrentWallpaper();

            if (Settings.Instance.SlideshowEnabled)
                StartSlideshow(Settings.Instance.SlideshowIntervalMinutes);
        }

        public static string GetCurrentWallpaper()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", false);
                return key?.GetValue("Wallpaper") as string ?? "";
            }
            catch { return ""; }
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
                int n = GetSystemMetrics(SM_CMONITORS);
                return (uint)Math.Max(1, n);
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
                list.Add(new DisplayMonitorInfo
                {
                    Index = 0,
                    DeviceId = "PRIMARY",
                    DeviceName = "Display 1",
                    Width = GetSystemMetrics(0),  // SM_CXSCREEN
                    Height = GetSystemMetrics(1), // SM_CYSCREEN
                    IsPrimary = true
                });
            }
            return list;
        }

        public async Task<bool> SetLockScreenAsync(string imageUrlOrPath)
        {
            try
            {
                string localFile;
                if (File.Exists(imageUrlOrPath)) localFile = imageUrlOrPath;
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
            catch { return false; }
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

                if (!File.Exists(targetPath) || new FileInfo(targetPath).Length == 0)
                {
                    bool downloaded = false;
                    try
                    {
                        using var req = new HttpRequestMessage(HttpMethod.Get, fullUrl);
                        if (fullUrl.Contains("yande.re", StringComparison.OrdinalIgnoreCase))
                            req.Headers.Referrer = new Uri("https://yande.re/");
                        else if (fullUrl.Contains("konachan", StringComparison.OrdinalIgnoreCase))
                            req.Headers.Referrer = new Uri("https://konachan.net/");

                        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                        if (resp.IsSuccessStatusCode)
                        {
                            await using var fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
                            await resp.Content.CopyToAsync(fs);
                            downloaded = true;
                        }
                    }
                    catch { }

                    if (!downloaded && fullUrl != post.BestImageUrl)
                    {
                        try
                        {
                            using var fallbackReq = new HttpRequestMessage(HttpMethod.Get, post.BestImageUrl);
                            if (post.BestImageUrl.Contains("yande.re", StringComparison.OrdinalIgnoreCase))
                                fallbackReq.Headers.Referrer = new Uri("https://yande.re/");
                            else if (post.BestImageUrl.Contains("konachan", StringComparison.OrdinalIgnoreCase))
                                fallbackReq.Headers.Referrer = new Uri("https://konachan.net/");

                            using var fallbackResp = await _http.SendAsync(fallbackReq, HttpCompletionOption.ResponseHeadersRead);
                            fallbackResp.EnsureSuccessStatusCode();
                            await using var fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
                            await fallbackResp.Content.CopyToAsync(fs);
                        }
                        catch
                        {
                            string cachedThumb = await ImageCacheService.Instance.GetCachedImagePathAsync(post.BestImageUrl);
                            if (File.Exists(cachedThumb) && new FileInfo(cachedThumb).Length > 0)
                                targetPath = cachedThumb;
                        }
                    }
                }
            }

            _previousWallpaper = GetCurrentWallpaper();
            _isPanicActive = false;
            SetWallpaper(targetPath, monitorIndex);

            try { HistoryManager.Instance.RecordApplied(post, targetPath); } catch { }

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

        public void ApplySafeWallpaper()
        {
            if (_isPanicActive && Settings.Instance.PanicRestoreToggle && !string.IsNullOrEmpty(_previousWallpaper) && File.Exists(_previousWallpaper))
            {
                SetWallpaper(_previousWallpaper);
                _isPanicActive = false;
                SafeWallpaperRestored?.Invoke();
                WallpaperApplied?.Invoke(_previousWallpaper);
                return;
            }

            _previousWallpaper = GetCurrentWallpaper();

            var target = Settings.Instance.SafeWallpaperPath;
            if (string.IsNullOrEmpty(target) || !File.Exists(target))
                target = Settings.DetectDefaultWin11Wallpaper();

            // Guaranteed fallback: if nothing valid is configured or detected, generate a
            // neutral solid-color image so the panic ALWAYS visibly changes the wallpaper.
            if (string.IsNullOrEmpty(target) || !File.Exists(target))
                target = EnsureSolidFallbackImage();

            if (!string.IsNullOrEmpty(target) && File.Exists(target))
            {
                SetWallpaper(target);
                _isPanicActive = true;
                Settings.Instance.PanicTriggerCount++;
                Settings.Instance.Save();
                SafeWallpaperTriggered?.Invoke();
                WallpaperApplied?.Invoke(target);
            }
        }

        /// <summary>Create (once) a plain dark-gray BMP used as a last-resort safe wallpaper.</summary>
        private string EnsureSolidFallbackImage()
        {
            try
            {
                string path = Path.Combine(_cacheDir, "safe_fallback.bmp");
                if (File.Exists(path) && new FileInfo(path).Length > 0) return path;

                const int w = 1920, h = 1080;
                int rowSize = ((w * 3 + 3) / 4) * 4; // 24bpp, rows padded to 4 bytes
                int pixelDataSize = rowSize * h;
                int fileSize = 54 + pixelDataSize;

                using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
                using var bw = new BinaryWriter(fs);
                // BITMAPFILEHEADER
                bw.Write((byte)'B'); bw.Write((byte)'M');
                bw.Write(fileSize); bw.Write(0); bw.Write(54);
                // BITMAPINFOHEADER
                bw.Write(40); bw.Write(w); bw.Write(h);
                bw.Write((short)1); bw.Write((short)24);
                bw.Write(0); bw.Write(pixelDataSize);
                bw.Write(2835); bw.Write(2835); bw.Write(0); bw.Write(0);
                // Pixels: dark slate (BGR 0x1C2028 -> B=0x28,G=0x20,R=0x1C)
                var row = new byte[rowSize];
                for (int x = 0; x < w; x++)
                {
                    row[x * 3 + 0] = 0x28; row[x * 3 + 1] = 0x20; row[x * 3 + 2] = 0x1C;
                }
                for (int y = 0; y < h; y++) bw.Write(row);
                bw.Flush();
                return path;
            }
            catch { return ""; }
        }

        public void SetSafeWallpaper(string path)
        {
            _safeWallpaper = path;
            Settings.Instance.SafeWallpaperPath = path;
            Settings.Instance.Save();
        }

        public void StartSlideshow(int intervalMinutes, bool advanceImmediately = false)
        {
            // Kept for callers passing minutes, but the actual period now honours the
            // precise seconds setting (allowing sub-minute intervals like 0.5 min).
            StartSlideshowFromSettings(advanceImmediately);
        }

        /// <summary>Start the slideshow using the effective interval from Settings (supports sub-minute).</summary>
        public void StartSlideshowFromSettings(bool advanceImmediately = false)
        {
            _slideshowTimer?.Dispose();
            double seconds = Math.Max(5, Settings.Instance.SlideshowIntervalEffectiveSeconds); // floor at 5s for safety
            var period = TimeSpan.FromSeconds(seconds);
            _slideshowTimer = new System.Threading.Timer(_ => OnSlideshowTick(), null, period, period);
            if (advanceImmediately) NextSlideshowWallpaper();
        }

        public void StopSlideshow()
        {
            _slideshowTimer?.Dispose();
            _slideshowTimer = null;
        }

        private void OnSlideshowTick()
        {
            if (!Settings.Instance.SlideshowEnabled || _isPanicActive) return;
            if (IsForegroundFullscreen()) return;
            NextSlideshowWallpaper();
        }

        public void NextSlideshowWallpaper()
        {
            _ = NextSlideshowWallpaperAsync();
        }

        public void ResetSlideshowQueue()
        {
            lock (_slideshowLock) { _slideshowQueue.Clear(); }
        }

        private static string GetItemKey(PostItem item)
        {
            if (!string.IsNullOrEmpty(item.Source) && item.Id > 0) return $"{item.Source}_{item.Id}";
            if (!string.IsNullOrEmpty(item.LocalPath)) return item.LocalPath;
            if (!string.IsNullOrEmpty(item.FullDownloadUrl)) return item.FullDownloadUrl;
            if (!string.IsNullOrEmpty(item.BestImageUrl)) return item.BestImageUrl;
            return item.FileUrl ?? item.SampleUrl ?? item.PreviewUrl ?? Guid.NewGuid().ToString("N");
        }

        private List<PostItem> GetSlideshowPool()
        {
            List<PostItem> rawPool;
            string src = Settings.Instance.SlideshowSource ?? "favorites";
            if (src == "downloads") rawPool = DownloadsManager.Instance.GetAllDownloads();
            else if (src.StartsWith("collection:", StringComparison.OrdinalIgnoreCase))
            {
                string collection = src.Substring("collection:".Length);
                rawPool = FavoritesManager.Instance.GetFavoritesByCollection(collection);
            }
            else rawPool = FavoritesManager.Instance.GetAllFavorites();

            return rawPool.Where(i =>
                (!string.IsNullOrEmpty(i.LocalPath) && File.Exists(i.LocalPath)) ||
                !string.IsNullOrEmpty(i.FullDownloadUrl) ||
                !string.IsNullOrEmpty(i.BestImageUrl) ||
                !string.IsNullOrEmpty(i.FileUrl) ||
                !string.IsNullOrEmpty(i.SampleUrl)).ToList();
        }

        public async Task NextSlideshowWallpaperAsync()
        {
            if (_isPanicActive) return;
            PostItem? nextItem = null;

            lock (_slideshowLock)
            {
                var pool = GetSlideshowPool();
                if (pool.Count == 0) return;

                var poolKeys = new HashSet<string>(pool.Select(GetItemKey));
                _slideshowQueue.RemoveAll(i => !poolKeys.Contains(GetItemKey(i)));

                if (_slideshowQueue.Count == 0)
                {
                    var cycle = new List<PostItem>(pool);
                    for (int i = cycle.Count - 1; i > 0; i--)
                    {
                        int j = Random.Shared.Next(i + 1);
                        (cycle[i], cycle[j]) = (cycle[j], cycle[i]);
                    }
                    if (cycle.Count > 1 && !string.IsNullOrEmpty(_lastSlideshowItemKey) && GetItemKey(cycle[0]) == _lastSlideshowItemKey)
                        (cycle[0], cycle[^1]) = (cycle[^1], cycle[0]);
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
                try { await ApplyWallpaperAsync(nextItem, Settings.Instance.TargetMonitor); } catch { }
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
                IntPtr hMon = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
                var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(hMon, ref mi))
                {
                    return rect.Left <= mi.rcMonitor.Left &&
                           rect.Top <= mi.rcMonitor.Top &&
                           rect.Right >= mi.rcMonitor.Right &&
                           rect.Bottom >= mi.rcMonitor.Bottom;
                }
            }
            return false;
        }

        public static void MinimizeAllWindows()
        {
            try
            {
                keybd_event(0x5B, 0, 0, 0);
                keybd_event(0x44, 0, 0, 0);
                keybd_event(0x44, 0, 2, 0);
                keybd_event(0x5B, 0, 2, 0);
            }
            catch { }
        }

        public static void MuteMasterAudio()
        {
            try
            {
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorCoClass();
                enumerator.GetDefaultAudioEndpoint(0, 1, out var device);
                var iid = typeof(IAudioEndpointVolume).GUID;
                device.Activate(ref iid, 1, IntPtr.Zero, out var epvObj);
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

                desktop.SetWallpaper(null, path);
                return;
            }
            catch { }

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
                if (key != null)
                {
                    key.SetValue("WallpaperStyle", "10");
                    key.SetValue("TileWallpaper", "0");
                }
            }
            catch { }

            SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
        }
    }
}
