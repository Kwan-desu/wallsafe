using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace WallSafeWinUI.Services
{
    /// <summary>
    /// Protects the user when shutting down, restarting, or putting their laptop to sleep.
    /// If configured, switches to the safe wallpaper immediately during shutdown/sleep so that
    /// upon powering back on in public (school, office, library), the screen boots up displaying
    /// the safe wallpaper with zero exposure risk.
    /// Automatically restores the normal wallpaper after startup if confirmed not on a trigger Wi-Fi network.
    /// </summary>
    public sealed class ShutdownProtectionService : IDisposable
    {
        private static ShutdownProtectionService? _instance;
        public static ShutdownProtectionService Instance => _instance ??= new ShutdownProtectionService();

        private bool _initialized;
        private CancellationTokenSource? _startupEvalCts;

        private delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData);

        [DllImport("comctl32.dll", SetLastError = true)]
        private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass, IntPtr dwRefData);

        [DllImport("comctl32.dll", SetLastError = true)]
        private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr RegisterSuspendResumeNotification(IntPtr hRecipient, uint Flags);

        private const uint WM_QUERYENDSESSION = 0x0011;
        private const uint WM_ENDSESSION = 0x0016;
        private const uint WM_POWERBROADCAST = 0x0218;
        private const uint PBT_APMSUSPEND = 0x0004;
        private const uint PBT_APMRESUMESUSPEND = 0x0007;
        private const uint PBT_APMRESUMEAUTOMATIC = 0x0012;
        private const uint DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;

        private static SubclassProc? _subclassDelegate;
        private static IntPtr _powerNotifyHandle = IntPtr.Zero;

        private ShutdownProtectionService() { }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                AppDomain.CurrentDomain.ProcessExit += (_, _) => ApplySafeWallpaperOnShutdownSync();
            }
            catch { }

            // If safe wallpaper was applied on shutdown, evaluate restoration after startup
            if (Settings.Instance.ShutdownSafeApplied)
            {
                TriggerStartupEvaluation();
            }
        }

        public static void HookWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            try
            {
                _subclassDelegate = SubclassWindowProc;
                SetWindowSubclass(hWnd, _subclassDelegate, new UIntPtr(991), IntPtr.Zero);

                // Register for power state notifications (sleep / suspend / resume)
                _powerNotifyHandle = RegisterSuspendResumeNotification(hWnd, DEVICE_NOTIFY_WINDOW_HANDLE);
            }
            catch { }
        }

        private static IntPtr SubclassWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
        {
            switch (uMsg)
            {
                case WM_QUERYENDSESSION:
                case WM_ENDSESSION:
                    ApplySafeWallpaperOnShutdownSync();
                    break;

                case WM_POWERBROADCAST:
                    long wp = wParam.ToInt64();
                    if (wp == PBT_APMSUSPEND)
                    {
                        ApplySafeWallpaperOnShutdownSync();
                    }
                    else if (wp is PBT_APMRESUMESUSPEND or PBT_APMRESUMEAUTOMATIC)
                    {
                        Instance.TriggerResumeEvaluation();
                    }
                    break;
            }
            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        public void TriggerResumeEvaluation()
        {
            if (Settings.Instance.ShutdownSafeApplied)
            {
                TriggerStartupEvaluation();
            }
        }

        public void TriggerStartupEvaluation()
        {
            _startupEvalCts?.Cancel();
            _startupEvalCts = new CancellationTokenSource();
            var token = _startupEvalCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    // Allow up to 6 seconds for Wi-Fi adapter to associate with network
                    for (int i = 0; i < 6; i++)
                    {
                        if (token.IsCancellationRequested) return;
                        await Task.Delay(1000, token);

                        // If Wi-Fi watcher has matched a trigger network, keep the safe wallpaper
                        if (WifiWatcher.Instance.IsTriggerActive)
                        {
                            Settings.Instance.ShutdownSafeApplied = false;
                            Settings.Instance.Save();
                            return;
                        }
                    }

                    if (token.IsCancellationRequested) return;

                    // If Wi-Fi trigger is not active after grace period, restore previous normal wallpaper
                    if (!WifiWatcher.Instance.IsTriggerActive)
                    {
                        string? restore = Settings.Instance.ShutdownSavedOriginalWallpaper;
                        if (string.IsNullOrEmpty(restore) || !File.Exists(restore) || WallpaperManager.IsSafeWallpaper(restore))
                        {
                            restore = Settings.Instance.LastNormalWallpaperPath;
                        }

                        if (!string.IsNullOrEmpty(restore) && File.Exists(restore) && !WallpaperManager.IsSafeWallpaper(restore))
                        {
                            UiDispatch.Post(() =>
                            {
                                WallpaperManager.SetWallpaper(restore);
                                WallpaperManager.Instance.NotifyWallpaperApplied(restore);
                            });
                        }

                        Settings.Instance.ShutdownSafeApplied = false;
                        Settings.Instance.ShutdownSavedOriginalWallpaper = null;
                        Settings.Instance.Save();
                    }
                }
                catch { }
            }, token);
        }

        /// <summary>
        /// Synchronously changes the desktop wallpaper to safe wallpaper before Windows powers off or sleeps.
        /// Direct Win32 call takes ~10ms so Windows writes the safe wallpaper to the shell state.
        /// </summary>
        public static void ApplySafeWallpaperOnShutdownSync()
        {
            try
            {
                if (!Settings.Instance.SafeWallpaperOnShutdown) return;

                string current = WallpaperManager.GetCurrentWallpaper();
                if (!string.IsNullOrEmpty(current) && File.Exists(current) && !WallpaperManager.IsSafeWallpaper(current))
                {
                    Settings.Instance.ShutdownSavedOriginalWallpaper = current;
                    Settings.Instance.LastNormalWallpaperPath = current;
                }

                string safeTarget = WallpaperManager.Instance.GetEffectiveSafeWallpaperPath();
                if (!string.IsNullOrEmpty(safeTarget) && File.Exists(safeTarget))
                {
                    Settings.Instance.ShutdownSafeApplied = true;
                    Settings.Instance.Save();

                    WallpaperManager.SetWallpaper(safeTarget);
                }
            }
            catch { }
        }

        public void Dispose()
        {
            _startupEvalCts?.Cancel();
            _startupEvalCts?.Dispose();
        }
    }
}
