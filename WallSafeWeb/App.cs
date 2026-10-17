using System;
using System.Threading;
using System.Windows.Forms;
using WallSafeWeb.Services;

namespace WallSafeWeb
{
    /// <summary>
    /// Process entry + app-wide lifecycle (single instance, tray/hotkey host, background services).
    /// Mirrors WallSafeWinUI.App so the shared services behave identically.
    /// </summary>
    public static class App
    {
        private const string MutexName = "WallSafe_Web_SingleInstance_Mutex_2026";
        private const string WakeupEventName = "WallSafe_Web_ShowWindow_Event_2026";

        private static Mutex? _mutex;
        private static EventWaitHandle? _wakeup;

        public static MainForm? Window { get; private set; }

        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception ex) LogCrash("AppDomain", ex);
            };
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                LogCrash("UnobservedTask", e.Exception);
                e.SetObserved();
            };

            _mutex = new Mutex(true, MutexName, out bool isNew);
            if (!isNew)
            {
                try { using var ev = EventWaitHandle.OpenExisting(WakeupEventName); ev.Set(); } catch { }
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => LogCrash("UiThread", e.Exception);

            // WebView2 runtime is required (preinstalled on Windows 11; most Windows 10 PCs have it via Edge).
            try { Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString(); }
            catch
            {
                if (MessageBox.Show("WallSafe needs the Microsoft Edge WebView2 Runtime, which isn't installed on this PC.\n\nOpen the download page now?",
                        "WallSafe", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://go.microsoft.com/fwlink/p/?LinkId=2124703") { UseShellExecute = true }); } catch { }
                return;
            }

            // Upgrading from the WinUI build: the Run key may still point at the old WallSafeWinUI.exe.
            try { if (StartupManager.IsStartupEnabled()) StartupManager.SetStartup(true); } catch { }

            bool autostart = Array.Exists(args, a =>
                a.Equals("--autostart", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
            bool startHidden = autostart && Settings.Instance.StartMinimized;

            Window = new MainForm(startHidden);

            // SynchronizationContext exists once the first control is created.
            UiDispatch.Context = SynchronizationContext.Current;
            UiDispatch.UiThreadId = Environment.CurrentManagedThreadId;

            StartWakeupListener();
            try { SetupNativeHost(); } catch (Exception ex) { LogCrash("NativeHost", ex); }
            try { WifiWatcher.Instance.Start(); } catch (Exception ex) { LogCrash("WifiWatcher", ex); }
            try { ShutdownProtectionService.Instance.Initialize(); } catch (Exception ex) { LogCrash("ShutdownProtection", ex); }
            try { if (Settings.Instance.LocationAutoWallpaperEnabled) _ = LocationWatcher.Instance.StartAsync(); }
            catch (Exception ex) { LogCrash("LocationWatcher", ex); }

            Application.Run(new HiddenContext(Window, startHidden));
        }

        /// <summary>Keeps the message loop alive while the window is hidden in the tray.</summary>
        private sealed class HiddenContext : ApplicationContext
        {
            public HiddenContext(MainForm form, bool startHidden)
            {
                if (startHidden) form.EnterBackground();
                else form.ShowAndActivate();
            }
        }

        private static void StartWakeupListener()
        {
            try
            {
                _wakeup = new EventWaitHandle(false, EventResetMode.AutoReset, WakeupEventName);
                var t = new Thread(() =>
                {
                    while (_wakeup != null)
                    {
                        try { if (_wakeup.WaitOne()) UiDispatch.Post(ShowWindow); }
                        catch { break; }
                    }
                })
                { IsBackground = true, Name = "WallSafeWakeup" };
                t.Start();
            }
            catch (Exception ex) { LogCrash("WakeupListener", ex); }
        }

        private static void SetupNativeHost()
        {
            var host = NativeHost.Instance;
            host.OnOpen = () => UiDispatch.Post(ShowWindow);
            host.OnLeftClick = () => UiDispatch.Post(ToggleWindow);
            host.OnPanic = () => UiDispatch.Post(TriggerPanic);
            host.OnNext = () => { try { WallpaperManager.Instance.NextSlideshowWallpaper(); } catch { } };
            host.OnToggleSlideshow = () => UiDispatch.Post(() => SetSlideshow(!Settings.Instance.SlideshowEnabled));
            host.OnHotkey = () => UiDispatch.Post(TriggerPanic);
            host.OnExit = ExitApp;
            host.Start();
        }

        /// <summary>Full panic sequence (hotkey / tray / in-app button).</summary>
        public static void TriggerPanic()
        {
            var s = Settings.Instance;
            bool goingSafe = !WallpaperManager.Instance.IsPanicActive;
            if (goingSafe && s.PanicHideApp) Window?.EnterBackground();
            if (goingSafe && s.PanicMinimizeAllWindows) WallpaperManager.MinimizeAllWindows();
            if (goingSafe && s.PanicMuteAudio) WallpaperManager.MuteMasterAudio();
            WallpaperManager.Instance.ApplySafeWallpaper();
        }

        public static void SetSlideshow(bool enabled)
        {
            Settings.Instance.SlideshowEnabled = enabled;
            Settings.Instance.Save();
            if (enabled) WallpaperManager.Instance.StartSlideshowFromSettings(advanceImmediately: true);
            else WallpaperManager.Instance.StopSlideshow();
            Window?.Bridge?.Emit("slideshowChanged", new { enabled });
        }

        public static void ReRegisterHotkey() => NativeHost.Instance.RegisterConfiguredHotkey();

        public static void ShowWindow() => Window?.ShowAndActivate();

        public static void ToggleWindow()
        {
            if (Window == null) return;
            if (Window.Visible) Window.EnterBackground();
            else Window.ShowAndActivate();
        }

        public static void ExitApp()
        {
            var pid = Environment.ProcessId;
            // Watchdog: guarantee termination even if a cleanup step hangs.
            new Thread(() =>
            {
                Thread.Sleep(1500);
                try { System.Diagnostics.Process.GetProcessById(pid).Kill(); } catch { }
            })
            { IsBackground = true }.Start();

            try { NativeHost.Instance.Dispose(); } catch { }
            try { WifiWatcher.Instance.Dispose(); } catch { }
            try { LocationWatcher.Instance.Dispose(); } catch { }
            try { _mutex?.ReleaseMutex(); _mutex?.Dispose(); _mutex = null; } catch { }
            var w = _wakeup; _wakeup = null; try { w?.Set(); } catch { }

            UiDispatch.Post(() =>
            {
                try { if (Window != null) { Window.ForceClose = true; Window.Close(); } } catch { }
                Application.Exit();
            });
        }

        internal static void LogCrash(string source, Exception? ex)
        {
            try
            {
                string dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WallSafe");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "crash-web.log"),
                    $"[{DateTime.Now:u}] {source}: {ex}\r\n\r\n");
            }
            catch { }
        }
    }
}
