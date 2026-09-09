using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;

namespace WallSafe
{
    public partial class App : System.Windows.Application
    {
        private NotifyIcon? _trayIcon;
        private MainWindow? _mainWindow;
        private const string MutexName = "WallSafe_SingleInstance_App_Mutex_2026";
        private const string WakeupEventName = "WallSafe_ShowWindow_Event_2026";
        private static System.Threading.Mutex? _singleInstanceMutex;
        private static System.Threading.EventWaitHandle? _wakeupEvent;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Global Exception Handlers so crashes never silently disappear
            DispatcherUnhandledException += (s, args) =>
            {
                System.Windows.MessageBox.Show(
                    $"WallSafe encountered an unhandled error:\n\n{args.Exception.Message}\n\nStack:\n{args.Exception.StackTrace}",
                    "WallSafe Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"WallSafe encountered a fatal error:\n\n{ex.Message}\n\nStack:\n{ex.StackTrace}",
                        "WallSafe Fatal Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            };

            // Single Instance check
            _singleInstanceMutex = new System.Threading.Mutex(true, MutexName, out bool isNew);
            if (!isNew)
            {
                // Signal the already-running instance to reveal its window
                try
                {
                    using var ev = System.Threading.EventWaitHandle.OpenExisting(WakeupEventName);
                    ev.Set();
                }
                catch { }

                Shutdown();
                return;
            }

            // Listen for wakeup events from subsequent launches
            try
            {
                _wakeupEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, WakeupEventName);
                var listenerThread = new System.Threading.Thread(() =>
                {
                    while (_wakeupEvent != null)
                    {
                        try
                        {
                            if (_wakeupEvent.WaitOne())
                            {
                                Dispatcher.Invoke(() => ShowMainWindow());
                            }
                        }
                        catch { break; }
                    }
                })
                {
                    IsBackground = true
                };
                listenerThread.Start();
            }
            catch { }

            base.OnStartup(e);

            // Initialize user's configured theme (Dark or Light)
            ThemeManager.Initialize();

            // Setup Tray icon
            _trayIcon = new NotifyIcon
            {
                Icon = LoadAppIcon(),
                Visible = true,
                Text = "WallSafe - Desktop Anime Wallpaper Manager"
            };

            _trayIcon.Click += TrayIcon_Click;

            var ctx = new ContextMenuStrip();
            ctx.Items.Add("Open WallSafe", null, (_, _) => ShowMainWindow());
            ctx.Items.Add("Safe Wallpaper (Panic Toggle)", null, (_, _) => WallpaperManager.Instance.ApplySafeWallpaper());
            ctx.Items.Add(new ToolStripSeparator());

            var slideshowItem = new ToolStripMenuItem("Auto Slideshow");
            slideshowItem.Checked = Settings.Instance.SlideshowEnabled;
            slideshowItem.Click += (_, _) =>
            {
                Settings.Instance.SlideshowEnabled = !Settings.Instance.SlideshowEnabled;
                Settings.Instance.Save();
                slideshowItem.Checked = Settings.Instance.SlideshowEnabled;
                if (Settings.Instance.SlideshowEnabled)
                    WallpaperManager.Instance.StartSlideshow(Settings.Instance.SlideshowIntervalMinutes);
                else
                    WallpaperManager.Instance.StopSlideshow();
            };
            ctx.Items.Add(slideshowItem);

            ctx.Items.Add(new ToolStripSeparator());
            ctx.Items.Add("Exit WallSafe", null, (_, _) => Shutdown());
            _trayIcon.ContextMenuStrip = ctx;

            // Register global panic hotkey
            HotkeyManager.Instance.ReRegister();

            // Check if launched on Windows startup or requested minimized
            bool isAutoStart = false;
            bool forceMinimized = false;
            if (e.Args != null)
            {
                foreach (var arg in e.Args)
                {
                    if (arg.Equals("--autostart", StringComparison.OrdinalIgnoreCase) ||
                        arg.Equals("-autostart", StringComparison.OrdinalIgnoreCase))
                    {
                        isAutoStart = true;
                    }
                    else if (arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
                             arg.Equals("-minimized", StringComparison.OrdinalIgnoreCase) ||
                             arg.Equals("/minimized", StringComparison.OrdinalIgnoreCase))
                    {
                        forceMinimized = true;
                    }
                }
            }

            bool shouldStartHidden = forceMinimized || (isAutoStart && Settings.Instance.StartMinimized);

            if (shouldStartHidden)
            {
                _mainWindow = new MainWindow();
                AppSuspensionManager.EnterBackgroundMode(_mainWindow);
            }
            else
            {
                ShowMainWindow();
            }
        }

        private void TrayIcon_Click(object? sender, EventArgs e)
        {
            if (e is MouseEventArgs me && me.Button == MouseButtons.Left)
                ToggleMainWindow();
        }

        public void ShowMainWindow()
        {
            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow();
            }

            AppSuspensionManager.RestoreForegroundMode(_mainWindow);
        }

        public void ToggleMainWindow()
        {
            if (_mainWindow == null)
            {
                ShowMainWindow();
                return;
            }

            if (_mainWindow.Visibility == Visibility.Visible && _mainWindow.IsVisible)
            {
                AppSuspensionManager.EnterBackgroundMode(_mainWindow);
            }
            else
            {
                ShowMainWindow();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { HotkeyManager.Instance.Dispose(); } catch { }
            if (_trayIcon != null)
            {
                try { _trayIcon.Visible = false; } catch { }
                try { _trayIcon.Dispose(); } catch { }
                _trayIcon = null;
            }
            try { _wakeupEvent?.Dispose(); } catch { }
            if (_singleInstanceMutex != null)
            {
                try { _singleInstanceMutex.ReleaseMutex(); } catch { }
                try { _singleInstanceMutex.Dispose(); } catch { }
            }
            base.OnExit(e);
        }

        private static System.Drawing.Icon LoadAppIcon()
        {
            try
            {
                string exePath = AppDomain.CurrentDomain.BaseDirectory;
                string iconPath = Path.Combine(exePath, "WallSafe.ico");
                if (File.Exists(iconPath))
                {
                    return new System.Drawing.Icon(iconPath);
                }
            }
            catch { }

            // Fallback GDI+ generated icon — copy the icon so the HICON handle can be freed immediately
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(Color.FromArgb(255, 99, 102, 241));
            g.FillEllipse(brush, 2, 2, 28, 28);
            using var whiteBrush = new SolidBrush(Color.White);
            g.FillRectangle(whiteBrush, 8, 14, 16, 4);
            g.FillRectangle(whiteBrush, 14, 8, 4, 16);
            IntPtr hicon = bmp.GetHicon();
            try
            {
                return (System.Drawing.Icon)System.Drawing.Icon.FromHandle(hicon).Clone(); // Clone clears dependency on hicon
            }
            finally
            {
                // Free the GDI HICON handle — Icon.Clone() made an independent copy
                if (hicon != IntPtr.Zero)
                    DestroyIcon(hicon);
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}
