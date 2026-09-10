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

            var ctx = new ContextMenuStrip
            {
                Renderer = new TrayMenuRenderer(),
                ShowImageMargin = false,
                Font = new System.Drawing.Font("Segoe UI Semibold", 9.25f),
                Padding = new Padding(4)
            };

            // Header (non-interactive brand row)
            var header = new ToolStripMenuItem("WallSafe")
            {
                Enabled = false,
                Font = new System.Drawing.Font("Segoe UI", 10.5f, System.Drawing.FontStyle.Bold)
            };
            ctx.Items.Add(header);
            ctx.Items.Add(new ToolStripSeparator());

            ctx.Items.Add("  Open WallSafe", null, (_, _) => ShowMainWindow());
            ctx.Items.Add("  Safe Wallpaper  ·  Panic Toggle", null, (_, _) => WallpaperManager.Instance.ApplySafeWallpaper());
            ctx.Items.Add("  Next Wallpaper", null, (_, _) =>
            {
                try { WallpaperManager.Instance.NextSlideshowWallpaper(); } catch { }
            });
            ctx.Items.Add(new ToolStripSeparator());

            var slideshowItem = new ToolStripMenuItem("  Auto Slideshow")
            {
                Checked = Settings.Instance.SlideshowEnabled,
                CheckOnClick = false
            };
            slideshowItem.Click += (_, _) =>
            {
                Settings.Instance.SlideshowEnabled = !Settings.Instance.SlideshowEnabled;
                Settings.Instance.Save();
                slideshowItem.Checked = Settings.Instance.SlideshowEnabled;
                if (Settings.Instance.SlideshowEnabled)
                    WallpaperManager.Instance.StartSlideshow(Settings.Instance.SlideshowIntervalMinutes, advanceImmediately: true);
                else
                    WallpaperManager.Instance.StopSlideshow();
            };
            ctx.Items.Add(slideshowItem);

            var taskbarItem = new ToolStripMenuItem("  Taskbar Transparency")
            {
                Checked = Settings.Instance.TaskbarTransparencyEnabled,
                CheckOnClick = false
            };
            taskbarItem.Click += (_, _) =>
            {
                Settings.Instance.TaskbarTransparencyEnabled = !Settings.Instance.TaskbarTransparencyEnabled;
                Settings.Instance.Save();
                taskbarItem.Checked = Settings.Instance.TaskbarTransparencyEnabled;
                TaskbarManager.Instance.ApplyFromSettings();
            };
            ctx.Items.Add(taskbarItem);

            ctx.Items.Add(new ToolStripSeparator());
            ctx.Items.Add("  Exit WallSafe", null, (_, _) => Shutdown());

            // Keep the checkmarks in sync each time the menu opens (state may change elsewhere).
            ctx.Opening += (_, _) =>
            {
                slideshowItem.Checked = Settings.Instance.SlideshowEnabled;
                taskbarItem.Checked = Settings.Instance.TaskbarTransparencyEnabled;
            };

            // Clip the menu window to a rounded-rectangle region so the square window
            // corners don't show a dark artifact behind our rounded background.
            void ApplyRoundedRegion()
            {
                try
                {
                    int r = 12;
                    var rect = new System.Drawing.Rectangle(0, 0, ctx.Width, ctx.Height);
                    using var path = new System.Drawing.Drawing2D.GraphicsPath();
                    int d = r * 2;
                    if (rect.Width > d && rect.Height > d)
                    {
                        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                        path.CloseFigure();
                        ctx.Region = new System.Drawing.Region(path);
                    }
                }
                catch { }
            }
            ctx.Opened += (_, _) => ApplyRoundedRegion();
            ctx.SizeChanged += (_, _) => ApplyRoundedRegion();

            _trayIcon.ContextMenuStrip = ctx;

            // Register global panic hotkey
            HotkeyManager.Instance.ReRegister();

            // Apply the merged taskbar-transparency appearance (TranslucentTB engine) if enabled.
            try { TaskbarManager.Instance.ApplyFromSettings(); } catch { }

            // Start the Wi-Fi-triggered auto-wallpaper watcher if enabled.
            try { WifiWatcher.Instance.Start(); } catch { }

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
            // Restore the taskbar to its default Windows appearance and stop the engine.
            try { TaskbarManager.Instance.Dispose(); } catch { }
            try { WifiWatcher.Instance.Dispose(); } catch { }
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
            // 1. Prefer a loose WallSafe.ico next to the executable (highest fidelity, multi-res).
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

            // 2. Fall back to the icon embedded in the running executable itself
            //    (the <ApplicationIcon> baked in at build time). This works even for
            //    single-file self-contained publishes where no loose .ico exists.
            try
            {
                string? exeFile = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exeFile) && File.Exists(exeFile))
                {
                    var extracted = System.Drawing.Icon.ExtractAssociatedIcon(exeFile);
                    if (extracted != null)
                    {
                        // Clone so the icon is independent of any transient handle.
                        return (System.Drawing.Icon)extracted.Clone();
                    }
                }
            }
            catch { }

            // 3. Last-resort GDI+ generated icon — copy the icon so the HICON handle can be freed immediately
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
