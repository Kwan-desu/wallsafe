using System;
using System.Drawing;
using System.IO;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using WallSafeWeb.Bridge;
using WallSafeWeb.Services;

namespace WallSafeWeb
{
    /// <summary>
    /// Frameless-looking host window: the native caption is removed via WM_NCCALCSIZE so the
    /// HTML title bar fills the top, while native resize borders, shadow, rounded corners,
    /// Snap and Mica/Acrylic (DWM system backdrop) are kept.
    /// </summary>
    public sealed class MainForm : Form
    {
        public const string AppHost = "app.wallsafe";

        private WebView2 _web;
        private int _recoveries;
        private readonly bool _startHidden;
        private bool _webReady;

        public RpcBridge? Bridge { get; private set; }
        public bool ForceClose { get; set; }

        public MainForm(bool startHidden)
        {
            _startHidden = startHidden;
            Text = "WallSafe";
            Icon = LoadIcon();
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1360, 860);
            MinimumSize = new Size(980, 640);
            BackColor = Color.Black; // black == transparent under DwmExtendFrameIntoClientArea
            ShowInTaskbar = true;

            _web = CreateWebView();

            ShutdownProtectionService.HookWindow(Handle);
            _ = InitWebViewAsync();
        }

        private WebView2 CreateWebView()
        {
            var web = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.Transparent };
            Controls.Add(web);
            return web;
        }

        /// <summary>
        /// The WebView2 browser process can exit (runtime update, GPU reset, OS memory pressure while
        /// suspended in the tray). The control is unusable afterwards, so pending RPCs never answer
        /// and the page sits on spinners. Rebuild the control and reload the UI.
        /// </summary>
        private void RecoverWebView()
        {
            if (IsDisposed || ++_recoveries > 5) return;
            _webReady = false;
            var old = _web;
            _web = CreateWebView();
            _web.BringToFront();
            try { Controls.Remove(old); old.Dispose(); } catch { }
            _ = InitWebViewAsync();
        }

        private static Icon? LoadIcon()
        {
            try
            {
                var p = Path.Combine(AppContext.BaseDirectory, "Assets", "WallSafe.ico");
                return File.Exists(p) ? new Icon(p) : null;
            }
            catch { return null; }
        }

        // ── WebView2 bootstrap ────────────────────────────────────
        private async Task InitWebViewAsync()
        {
            try
            {
                string dataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WallSafe", "WebView2");
                var env = await CoreWebView2Environment.CreateAsync(null, dataDir,
                    new CoreWebView2EnvironmentOptions("--enable-features=OverlayScrollbar --disable-features=msSmartScreenProtection"));
                await _web.EnsureCoreWebView2Async(env);

                var core = _web.CoreWebView2;
                bool dev = Environment.GetEnvironmentVariable("WALLSAFE_DEVTOOLS") == "1";
                core.Settings.AreDevToolsEnabled = dev;
                core.Settings.AreDefaultContextMenusEnabled = dev;
                core.Settings.AreBrowserAcceleratorKeysEnabled = dev;
                core.Settings.IsZoomControlEnabled = false;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.IsGeneralAutofillEnabled = false;
                core.Settings.IsPasswordAutosaveEnabled = false;
                core.Settings.IsNonClientRegionSupportEnabled = true; // CSS app-region: drag

                string root = Path.Combine(AppContext.BaseDirectory, "wwwroot");
                core.SetVirtualHostNameToFolderMapping(AppHost, root, CoreWebView2HostResourceAccessKind.Deny);

                var images = new ImageServer(env);
                core.AddWebResourceRequestedFilter("https://img.wallsafe/*", CoreWebView2WebResourceContext.All);
                core.WebResourceRequested += images.Handle;

                if (Bridge == null) Bridge = new RpcBridge(this, core); else Bridge.Attach(core);
                core.WebMessageReceived += Bridge.OnMessage;

                // Everything that isn't our own app opens in the user's browser.
                core.NewWindowRequested += (_, e) => { e.Handled = true; OpenExternal(e.Uri); };
                core.NavigationStarting += (_, e) =>
                {
                    if (!e.Uri.StartsWith($"https://{AppHost}/", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        OpenExternal(e.Uri);
                    }
                };
                core.ProcessFailed += (_, e) =>
                {
                    App.LogCrash("WebView2ProcessFailed", new Exception(e.ProcessFailedKind.ToString()));
                    if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.BrowserProcessExited)
                    {
                        BeginInvoke(new Action(RecoverWebView));
                        return;
                    }
                    try { core.Reload(); } catch { }
                };

                core.Navigate($"https://{AppHost}/index.html");
                _webReady = true;
                if (_startHidden) _ = SuspendWebAsync();
            }
            catch (WebView2RuntimeNotFoundException)
            {
                var r = MessageBox.Show(this,
                    "WallSafe needs the Microsoft Edge WebView2 Runtime, which is not installed.\n\nOpen the download page now?",
                    "WallSafe", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r == DialogResult.Yes) OpenExternal("https://developer.microsoft.com/microsoft-edge/webview2/");
                App.ExitApp();
            }
            catch (Exception ex)
            {
                App.LogCrash("InitWebView", ex);
                MessageBox.Show(this, "WallSafe could not start its interface:\n" + ex.Message, "WallSafe",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static void OpenExternal(string url)
        {
            try
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http"))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(u.AbsoluteUri) { UseShellExecute = true });
            }
            catch { }
        }

        // ── Visibility / background mode ──────────────────────────
        public void ShowAndActivate()
        {
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            try
            {
                var core = _web.CoreWebView2;
                if (core != null)
                {
                    core.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Normal;
                    if (core.IsSuspended) core.Resume();
                }
            }
            catch { }
            Activate();
            SetForegroundWindow(Handle);
            Bridge?.Emit("windowShown", null);
        }

        public void EnterBackground()
        {
            if (Visible) Hide();
            _ = SuspendWebAsync();
        }

        private async Task SuspendWebAsync()
        {
            try
            {
                await Task.Delay(300);
                if (Visible || !_webReady) return;
                var core = _web.CoreWebView2;
                if (core == null) return;
                core.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Low;
                if (!core.IsSuspended) await core.TrySuspendAsync();
            }
            catch { }
            try
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(2, GCCollectionMode.Aggressive, true, true);
                SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, -1, -1);
            }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // The close button hides to the tray; real exit is via the tray menu.
            if (!ForceClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                EnterBackground();
                return;
            }
            base.OnFormClosing(e);
        }

        public void ToggleMaximize() =>
            WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Bridge?.Emit("windowState", new { maximized = WindowState == FormWindowState.Maximized });
        }

        // ── DWM chrome ────────────────────────────────────────────
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyChrome();
            // Force a WM_NCCALCSIZE so the caption is removed immediately.
            SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }

        public void ApplyChrome()
        {
            try
            {
                int dark = IsDarkTheme() ? 1 : 0;
                DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, 4);
                int corner = DWMWCP_ROUND;
                DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, 4);

                var s = Settings.Instance;
                string mat = s.WindowMicaEnabled ? (s.BackdropMaterial ?? "Mica") : "None";
                int backdrop = mat switch { "Mica" => 2, "Acrylic" => 3, "MicaAlt" => 4, _ => 1 };
                DwmSetWindowAttribute(Handle, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, 4);

                var margins = backdrop > 1 ? new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 } : new MARGINS();
                DwmExtendFrameIntoClientArea(Handle, ref margins);

                // Black is see-through under the extended frame; otherwise match the page background
                // so the 4px top resize band is invisible.
                BackColor = backdrop > 1 ? Color.Black
                    : (dark == 1 ? Color.FromArgb(14, 15, 20) : Color.FromArgb(243, 244, 248));
            }
            catch { }
        }

        public static bool IsDarkTheme()
        {
            string t = Settings.Instance.Theme ?? "Dark";
            if (t == "Light") return false;
            if (t == "Dark") return true;
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
            }
            catch { return true; }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCCALCSIZE && m.WParam != IntPtr.Zero)
            {
                // Let Windows compute the standard client rect (keeps the invisible resize
                // borders on left/right/bottom), then reclaim the caption area at the top.
                var before = Marshal.PtrToStructure<NCCALCSIZE_PARAMS>(m.LParam);
                int originalTop = before.rgrc0.Top;
                base.WndProc(ref m);
                var after = Marshal.PtrToStructure<NCCALCSIZE_PARAMS>(m.LParam);
                after.rgrc0.Top = originalTop + (IsZoomed(Handle) ? MaximizedTopInset() : 0);
                Marshal.StructureToPtr(after, m.LParam, false);
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                // Thin top strip (outside the WebView) acts as the top resize edge.
                if ((int)m.Result == HTCLIENT && WindowState == FormWindowState.Normal)
                {
                    int y = (short)((long)m.LParam >> 16);
                    var pt = PointToClient(new Point(0, y));
                    if (pt.Y < TopResizeBand) m.Result = (IntPtr)HTTOP;
                }
                return;
            }

            if (m.Msg == WM_SETTINGCHANGE && Settings.Instance.Theme == "System")
            {
                ApplyChrome();
                Bridge?.Emit("systemThemeChanged", new { dark = IsDarkTheme() });
            }

            base.WndProc(ref m);
        }

        private const int TopResizeBand = 4;

        protected override CreateParams CreateParams
        {
            get
            {
                // Without WS_SYSMENU, DWM no longer paints the native min/max/close glyphs into the
                // extended (Mica) frame behind our HTML caption buttons. Min/max boxes stay so
                // taskbar minimize, Win+Arrow snapping and maximize animations keep working.
                const int WS_SYSMENU = 0x00080000;
                var cp = base.CreateParams;
                cp.Style &= ~WS_SYSMENU;
                return cp;
            }
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            // Leave a 4px band above the WebView (only when restored) so the top edge stays resizable.
            Padding = WindowState == FormWindowState.Normal ? new Padding(0, TopResizeBand, 0, 0) : Padding.Empty;
            base.OnLayout(levent);
        }

        private int MaximizedTopInset()
        {
            uint dpi = GetDpiForWindow(Handle);
            return GetSystemMetricsForDpi(SM_CYFRAME, dpi) + GetSystemMetricsForDpi(SM_CXPADDEDBORDER, dpi);
        }

        // ── Win32 ─────────────────────────────────────────────────
        private const int WM_NCCALCSIZE = 0x0083, WM_NCHITTEST = 0x0084, WM_SETTINGCHANGE = 0x001A;
        private const int HTCLIENT = 1, HTTOP = 12;
        private const int SM_CYFRAME = 33, SM_CXPADDEDBORDER = 92;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20, DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWA_SYSTEMBACKDROP_TYPE = 38;
        private const int DWMWCP_ROUND = 2;
        private const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10, SWP_FRAMECHANGED = 0x20;

        [StructLayout(LayoutKind.Sequential)] private struct RECTW { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct NCCALCSIZE_PARAMS { public RECTW rgrc0, rgrc1, rgrc2; public IntPtr lppos; }
        [StructLayout(LayoutKind.Sequential)] private struct MARGINS { public int Left, Right, Top, Bottom; }

        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS m);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr h);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr h);
        [DllImport("user32.dll")] private static extern int GetSystemMetricsForDpi(int index, uint dpi);
        [DllImport("kernel32.dll")] private static extern bool SetProcessWorkingSetSize(IntPtr p, nint min, nint max);
    }
}
