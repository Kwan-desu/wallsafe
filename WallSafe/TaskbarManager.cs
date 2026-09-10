using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace WallSafe
{
    /// <summary>
    /// Native C# reimplementation of TranslucentTB's core taskbar-transparency engine.
    ///
    /// Rather than shipping TranslucentTB's C++/WinRT binaries, this class reproduces its
    /// fundamental mechanism directly in managed code: it calls the undocumented
    /// user32!SetWindowCompositionAttribute with a WCA_ACCENT_POLICY payload against the
    /// taskbar window(s) (Shell_TrayWnd + Shell_SecondaryTrayWnd). This is the exact same
    /// API TranslucentTB uses to tint / blur / clear the taskbar.
    ///
    /// A lightweight background refresh loop re-applies the desired appearance on an
    /// interval so the effect survives Explorer refreshes, DPI changes and display
    /// reconfiguration — mirroring TranslucentTB's attribute worker.
    /// </summary>
    public sealed class TaskbarManager : IDisposable
    {
        // ─────────────────────────── Accent state (TranslucentTB ACCENT_STATE) ───────────────────────────
        public enum AccentState
        {
            Normal = 0,   // Restore the taskbar's default Windows appearance (fake value → ACCENT_DISABLED)
            Opaque = 1,   // Tinted, fully opaque (ACCENT_ENABLE_GRADIENT)
            Clear = 2,    // Tinted, transparent (ACCENT_ENABLE_TRANSPARENTGRADIENT)
            Blur = 3,     // Tinted + blur behind (ACCENT_ENABLE_BLURBEHIND)
            Acrylic = 4   // Fluent acrylic blur (ACCENT_ENABLE_ACRYLICBLURBEHIND)
        }

        // Maps our high-level state to the raw ACCENT_STATE integer sent to Windows.
        private static int ToNativeAccent(AccentState state) => state switch
        {
            AccentState.Normal => 0,   // ACCENT_DISABLED — lets the taskbar fall back to default
            AccentState.Opaque => 1,   // ACCENT_ENABLE_GRADIENT
            AccentState.Clear => 2,    // ACCENT_ENABLE_TRANSPARENTGRADIENT
            AccentState.Blur => 3,     // ACCENT_ENABLE_BLURBEHIND
            AccentState.Acrylic => 4,  // ACCENT_ENABLE_ACRYLICBLURBEHIND
            _ => 0
        };

        // ─────────────────────────── P/Invoke ───────────────────────────
        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public int AccentState;
            public int AccentFlags;
            public uint GradientColor; // 0xAABBGGRR
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        private const int WCA_ACCENT_POLICY = 19;

        // Flags telling the DWM which edges to draw + that GradientColor is valid.
        // 0x2 = use gradient color; 0xF0 = draw all four borders (matches TranslucentTB).
        private const int ACCENT_FLAGS = 0x2 | 0xF0;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string lpClassName, string? lpWindowName);

        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder className, int maxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hwnd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

        // ─────────────────────────── State ───────────────────────────
        private const string PrimaryTaskbarClass = "Shell_TrayWnd";
        private const string SecondaryTaskbarClass = "Shell_SecondaryTrayWnd";

        private readonly object _sync = new();
        private System.Threading.Timer? _refreshTimer;
        private AccentState _currentState = AccentState.Normal;
        private uint _currentColor; // 0xAABBGGRR
        private bool _enabled;
        private bool _dynamicEnabled;
        private bool _disposed;

        private static TaskbarManager? _instance;
        public static TaskbarManager Instance => _instance ??= new TaskbarManager();

        public bool IsEnabled { get { lock (_sync) return _enabled; } }
        public AccentState CurrentState { get { lock (_sync) return _currentState; } }

        /// <summary>
        /// Enable and apply a taskbar appearance. colorArgb is 0xAARRGGBB (WPF/System.Drawing order).
        /// </summary>
        public void Apply(AccentState state, uint colorArgb)
        {
            lock (_sync)
            {
                _enabled = true;
                _currentState = state;
                _currentColor = ArgbToAbgr(colorArgb);
            }

            ApplyToAllTaskbars();
            StartRefreshLoop();
        }

        /// <summary>Convenience overload taking a System.Drawing.Color.</summary>
        public void Apply(AccentState state, System.Drawing.Color color)
            => Apply(state, (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B));

        /// <summary>
        /// Restore the taskbar to its default Windows appearance and stop the refresh loop.
        /// Safe to call multiple times / when never enabled.
        /// </summary>
        public void Restore()
        {
            lock (_sync)
            {
                _enabled = false;
                _dynamicEnabled = false;
                _currentState = AccentState.Normal;
            }

            StopRefreshLoop();

            // Push a Normal (ACCENT_DISABLED) policy so the taskbar visibly reverts now.
            foreach (var hwnd in FindTaskbars())
            {
                SetAccent(hwnd, AccentState.Normal, 0);
            }
        }

        /// <summary>Re-apply from the persisted Settings. Called on startup and after settings changes.</summary>
        public void ApplyFromSettings()
        {
            var s = Settings.Instance;
            if (!s.TaskbarTransparencyEnabled)
            {
                lock (_sync) { _dynamicEnabled = false; }
                Restore();
                return;
            }

            lock (_sync)
            {
                _dynamicEnabled = true;
                _enabled = true;
            }
            EvaluateAndApply();     // apply the correct state immediately
            StartRefreshLoop();     // and keep it in sync as the desktop state changes
        }

        // ─────────── Dynamic state evaluation (TranslucentTB-style) ───────────

        /// <summary>Pick the highest-priority enabled state for the current desktop condition
        /// and apply its appearance. Priority: Maximized > Visible window > Start opened > Desktop.</summary>
        private void EvaluateAndApply()
        {
            bool dyn;
            lock (_sync) dyn = _dynamicEnabled;
            if (!dyn) return;

            var s = Settings.Instance;
            TaskbarStateAppearance? chosen = null;

            try
            {
                if (s.MaximizedWindowAppearance is { Enabled: true } max && IsAnyWindowMaximized())
                    chosen = max;
                else if (s.StartOpenedAppearance is { Enabled: true } start && IsStartMenuOpen())
                    chosen = start;
                else if (s.VisibleWindowAppearance is { Enabled: true } vis && IsAnyWindowVisibleOnDesktop())
                    chosen = vis;
            }
            catch { }

            // Fall back to the Desktop appearance (default state).
            chosen ??= (s.DesktopAppearance is { Enabled: true } ? s.DesktopAppearance : null);

            if (chosen == null)
            {
                // Nothing enabled → normal taskbar.
                lock (_sync) { _currentState = AccentState.Normal; _currentColor = 0; }
                foreach (var hwnd in FindTaskbars()) SetAccent(hwnd, AccentState.Normal, 0);
                return;
            }

            var state = (AccentState)Math.Clamp(chosen.AccentState, 0, 4);
            uint abgr = chosen.NoTint ? 0x00000000u : ArgbToAbgr(unchecked((uint)chosen.ColorArgb));
            lock (_sync) { _currentState = state; _currentColor = abgr; }
            foreach (var hwnd in FindTaskbars()) SetAccent(hwnd, state, abgr);
        }

        private static bool IsAnyWindowMaximized()
        {
            bool found = false;
            EnumWindows((hwnd, _) =>
            {
                if (IsWindowVisible(hwnd) && !IsIconic(hwnd) && IsZoomed(hwnd) && HasTitleBarArea(hwnd))
                {
                    found = true;
                    return false; // stop
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        private static bool IsAnyWindowVisibleOnDesktop()
        {
            bool found = false;
            EnumWindows((hwnd, _) =>
            {
                if (IsWindowVisible(hwnd) && !IsIconic(hwnd) && HasTitleBarArea(hwnd))
                {
                    found = true;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        /// <summary>Heuristic: a top-level app window (has a caption, isn't a tool/shell window).</summary>
        private static bool HasTitleBarArea(IntPtr hwnd)
        {
            const int GWL_STYLE = -16;
            const int GWL_EXSTYLE = -20;
            const long WS_CAPTION = 0x00C00000;
            const long WS_EX_TOOLWINDOW = 0x00000080;

            long style = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
            long ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            if ((ex & WS_EX_TOOLWINDOW) != 0) return false;
            if ((style & WS_CAPTION) != WS_CAPTION) return false;

            var sb = new System.Text.StringBuilder(64);
            GetClassName(hwnd, sb, sb.Capacity);
            string cls = sb.ToString();
            // Exclude the desktop/shell windows.
            if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false;
            return true;
        }

        /// <summary>Detect whether the Start menu / launcher is currently open.</summary>
        private static bool IsStartMenuOpen()
        {
            // The Start UI host window is visible (non-cloaked) while Start is open.
            IntPtr start = FindWindow("Windows.UI.Core.CoreWindow", "Start");
            if (start == IntPtr.Zero)
                start = FindWindow("Windows.UI.Core.CoreWindow", "Search"); // shares host on some builds
            if (start != IntPtr.Zero && IsWindowVisible(start))
                return true;
            return false;
        }

        // ─────────────────────────── Internals ───────────────────────────
        private void ApplyToAllTaskbars()
        {
            AccentState state;
            uint colorAbgr;
            lock (_sync)
            {
                state = _currentState;
                colorAbgr = _currentColor;
            }

            foreach (var hwnd in FindTaskbars())
            {
                SetAccent(hwnd, state, colorAbgr);
            }
        }

        private static void SetAccent(IntPtr hwnd, AccentState state, uint colorAbgr)
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return;

            var policy = new AccentPolicy
            {
                AccentState = ToNativeAccent(state),
                AccentFlags = state == AccentState.Normal ? 0 : ACCENT_FLAGS,
                GradientColor = colorAbgr,
                AnimationId = 0
            };

            int size = Marshal.SizeOf(policy);
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(policy, ptr, false);
                var data = new WindowCompositionAttributeData
                {
                    Attribute = WCA_ACCENT_POLICY,
                    Data = ptr,
                    SizeOfData = size
                };
                SetWindowCompositionAttribute(hwnd, ref data);
            }
            catch
            {
                // Best-effort: an occasional failure (e.g. during an Explorer restart) is non-fatal;
                // the refresh loop will re-apply shortly.
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>Enumerate the primary taskbar and every secondary (per-monitor) taskbar.</summary>
        private static IEnumerable<IntPtr> FindTaskbars()
        {
            var result = new List<IntPtr>();

            IntPtr primary = FindWindow(PrimaryTaskbarClass, null);
            if (primary != IntPtr.Zero) result.Add(primary);

            // Secondary taskbars on multi-monitor setups.
            EnumWindows((hwnd, _) =>
            {
                var sb = new System.Text.StringBuilder(256);
                if (GetClassName(hwnd, sb, sb.Capacity) > 0 &&
                    sb.ToString() == SecondaryTaskbarClass)
                {
                    result.Add(hwnd);
                }
                return true;
            }, IntPtr.Zero);

            return result;
        }

        private void StartRefreshLoop()
        {
            lock (_sync)
            {
                if (_disposed) return;
                if (_refreshTimer == null)
                {
                    // Poll frequently so dynamic state changes (maximize, Start open, etc.)
                    // apply promptly; also survives Explorer refreshes / display changes.
                    _refreshTimer = new System.Threading.Timer(_ =>
                    {
                        try
                        {
                            bool enabled, dyn;
                            lock (_sync) { enabled = _enabled; dyn = _dynamicEnabled; }
                            if (!enabled) return;
                            if (dyn) EvaluateAndApply();
                            else ApplyToAllTaskbars();
                        }
                        catch { }
                    }, null, TimeSpan.FromMilliseconds(700), TimeSpan.FromMilliseconds(700));
                }
            }
        }

        private void StopRefreshLoop()
        {
            lock (_sync)
            {
                _refreshTimer?.Dispose();
                _refreshTimer = null;
            }
        }

        // Convert 0xAARRGGBB (managed color order) → 0xAABBGGRR (Win32 COLORREF-with-alpha order).
        private static uint ArgbToAbgr(uint argb)
        {
            uint a = (argb >> 24) & 0xFF;
            uint r = (argb >> 16) & 0xFF;
            uint g = (argb >> 8) & 0xFF;
            uint b = argb & 0xFF;
            return (a << 24) | (b << 16) | (g << 8) | r;
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
            }
            try { Restore(); } catch { }
            StopRefreshLoop();
        }
    }
}
