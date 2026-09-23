using System;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WallSafeWinUI.Services;

namespace WallSafeWinUI
{
    public partial class App : Application
    {
        public static new App Current => (App)Application.Current;
        public static MainWindow? RootWindow { get; private set; }

        private const string MutexName = "WallSafe_WinUI_SingleInstance_Mutex_2026";
        private const string WakeupEventName = "WallSafe_WinUI_ShowWindow_Event_2026";
        private static Mutex? _singleInstanceMutex;
        private static EventWaitHandle? _wakeupEvent;
        private DispatcherQueue? _dispatcherQueue;

        public App()
        {
            InitializeComponent();
            UnhandledException += OnUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception ex) LogCrash("AppDomain", ex);
            };
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                LogCrash("UnobservedTask", e.Exception);
                e.SetObserved();
            };
        }

        private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            LogCrash("XamlUnhandled", e.Exception);
            e.Handled = true; // keep the app alive rather than crashing to desktop
        }

        internal static void LogCrash(string source, Exception? ex)
        {
            try
            {
                string dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WallSafe");
                System.IO.Directory.CreateDirectory(dir);
                string path = System.IO.Path.Combine(dir, "crash.log");
                string entry = $"[{DateTime.Now:u}] {source}: {ex}\r\n\r\n";
                System.IO.File.AppendAllText(path, entry);
            }
            catch { }
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                LaunchCore(args);
            }
            catch (Exception ex)
            {
                LogCrash("OnLaunched", ex);
                // Last-resort: still try to show a window so the app isn't a silent no-op.
                try
                {
                    if (RootWindow == null) RootWindow = new MainWindow();
                    RootWindow.Activate();
                }
                catch (Exception ex2) { LogCrash("OnLaunchedRecovery", ex2); }
            }
        }

        private void LaunchCore(LaunchActivatedEventArgs args)
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            UiDispatch.Queue = _dispatcherQueue;

            // Single-instance guard: signal an existing instance and exit.
            _singleInstanceMutex = new Mutex(true, MutexName, out bool isNew);
            if (!isNew)
            {
                try
                {
                    using var ev = EventWaitHandle.OpenExisting(WakeupEventName);
                    ev.Set();
                }
                catch { }
                Exit();
                return;
            }

            try
            {
                _wakeupEvent = new EventWaitHandle(false, EventResetMode.AutoReset, WakeupEventName);
                var listener = new Thread(() =>
                {
                    while (_wakeupEvent != null)
                    {
                        try { if (_wakeupEvent.WaitOne()) UiDispatch.Post(ShowMainWindow); }
                        catch { break; }
                    }
                })
                { IsBackground = true };
                listener.Start();
            }
            catch (Exception ex) { LogCrash("WakeupListener", ex); }

            try { ThemeService.Initialize(); } catch (Exception ex) { LogCrash("ThemeInit", ex); }

            // Show the window FIRST so a failure in an optional background service can
            // never prevent the UI from appearing.
            bool startHidden = false;
            try { startHidden = Settings.Instance.StartMinimized && WasStartedByAutostart(args); }
            catch (Exception ex) { LogCrash("StartHiddenCheck", ex); }

            try
            {
                RootWindow = new MainWindow();
                if (startHidden) AppSuspensionManager.Hibernate();
                else RootWindow.Activate();
            }
            catch (Exception ex) { LogCrash("MainWindow", ex); throw; }

            // Optional background features — each isolated so one failure is non-fatal.
            try { SetupNativeHost(); } catch (Exception ex) { LogCrash("NativeHost", ex); }
            try { WifiWatcher.Instance.Start(); } catch (Exception ex) { LogCrash("WifiWatcher", ex); }
            try { ShutdownProtectionService.Instance.Initialize(); } catch (Exception ex) { LogCrash("ShutdownProtection", ex); }
            try { if (Settings.Instance.LocationAutoWallpaperEnabled) _ = LocationWatcher.Instance.StartAsync(); }
            catch (Exception ex) { LogCrash("LocationWatcher", ex); }
        }

        private static bool WasStartedByAutostart(LaunchActivatedEventArgs args)
        {
            var cmd = Environment.GetCommandLineArgs();
            foreach (var a in cmd)
            {
                if (a.Equals("--autostart", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("--minimized", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private void SetupNativeHost()
        {
            var host = NativeHost.Instance;

            host.OnOpen = () => UiDispatch.Post(ShowMainWindow);
            host.OnLeftClick = () => UiDispatch.Post(ToggleMainWindow);
            host.OnPanic = () => WallpaperManager.Instance.ApplySafeWallpaper();
            host.OnNext = () => { try { WallpaperManager.Instance.NextSlideshowWallpaper(); } catch { } };

            host.OnToggleSlideshow = () =>
            {
                Settings.Instance.SlideshowEnabled = !Settings.Instance.SlideshowEnabled;
                Settings.Instance.Save();
                if (Settings.Instance.SlideshowEnabled)
                    WallpaperManager.Instance.StartSlideshowFromSettings(advanceImmediately: true);
                else
                    WallpaperManager.Instance.StopSlideshow();
            };

            // The global panic hotkey — invoked on the native host's own thread.
            host.OnHotkey = () =>
            {
                UiDispatch.Post(() =>
                {
                    if (Settings.Instance.PanicHideApp) App.HideAllWindows();
                    if (Settings.Instance.PanicMinimizeAllWindows) WallpaperManager.MinimizeAllWindows();
                    if (Settings.Instance.PanicMuteAudio) WallpaperManager.MuteMasterAudio();
                    WallpaperManager.Instance.ApplySafeWallpaper();
                });
            };

            host.OnExit = ExitApp;

            host.Start();
        }

        /// <summary>Re-apply the configured global hotkey (called after the user changes it in Settings).</summary>
        public static void ReRegisterHotkey() => NativeHost.Instance.RegisterConfiguredHotkey();

        public void ShowMainWindow()
        {
            if (RootWindow == null) RootWindow = new MainWindow();
            AppSuspensionManager.RestoreForegroundMode(RootWindow);
        }

        public void ToggleMainWindow()
        {
            if (RootWindow == null) { ShowMainWindow(); return; }
            if (RootWindow.IsVisibleWindow)
                AppSuspensionManager.EnterBackgroundMode(RootWindow);
            else
                ShowMainWindow();
        }

        public static void HideAllWindows()
        {
            if (RootWindow != null) AppSuspensionManager.EnterBackgroundMode(RootWindow);
        }

        private void ExitApp()
        {
            LogCrash("ExitApp", new Exception("ExitApp invoked on thread " + Environment.CurrentManagedThreadId));

            // Force-kill FIRST via an independent watchdog thread so nothing below can
            // prevent termination. Kill() does NOT run finalizers and cannot deadlock.
            var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            new Thread(() =>
            {
                try { System.Diagnostics.Process.GetProcessById(pid).Kill(); }
                catch (Exception ex) { LogCrash("WatchdogKill", ex); }
            })
            { IsBackground = true }.Start();

            // Best-effort cleanup (may be skipped if the watchdog kills us first — that's fine).
            try { NativeHost.Instance.Dispose(); } catch { }
            try { WifiWatcher.Instance.Dispose(); } catch { }
            try { LocationWatcher.Instance.Dispose(); } catch { }
            try { _wakeupEvent?.Set(); } catch { }
            try
            {
                if (_singleInstanceMutex != null)
                {
                    _singleInstanceMutex.ReleaseMutex();
                    _singleInstanceMutex.Dispose();
                    _singleInstanceMutex = null;
                }
            }
            catch { }

            try { System.Diagnostics.Process.GetCurrentProcess().Kill(); } catch { }
            try { Environment.Exit(0); } catch { }
        }
    }

    // Minimal ICommand so the tray left-click can invoke an action.
    public sealed class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _action;
        public RelayCommand(Action action) => _action = action;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _action();
    }
}
