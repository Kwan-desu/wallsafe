using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace WallSafeWinUI.Services
{
    /// <summary>
    /// A single native message-only window that owns BOTH the system-tray icon
    /// (Shell_NotifyIcon + a classic TrackPopupMenu) and the global panic hotkey
    /// (RegisterHotKey / WM_HOTKEY). Everything runs on one dedicated pump thread with
    /// a WndProc we fully control, so tray clicks come back as WM_COMMAND and hotkeys
    /// as WM_HOTKEY — no dependency on WinUI/H.NotifyIcon event routing (which was
    /// silently dropping the menu clicks and hotkey callbacks).
    /// </summary>
    public sealed class NativeHost : IDisposable
    {
        public static readonly NativeHost Instance = new();

        // ── Win32 ──
        private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASS
        {
            public uint style;
            public WndProc lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string? lpszMenuName;
            public string lpszClassName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
            public int uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
            public int dwInfoFlags;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern ushort RegisterClassW(ref WNDCLASS c);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CreateWindowExW(int ex, string cls, string name, int style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr p);
        [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr h);
        [DllImport("user32.dll")] private static extern IntPtr DefWindowProcW(IntPtr h, uint m, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] private static extern int GetMessageW(out MSG m, IntPtr h, uint a, uint b);
        [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG m);
        [DllImport("user32.dll")] private static extern IntPtr DispatchMessageW(ref MSG m);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandleW(string? n);

        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr h, int id);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool Shell_NotifyIconW(int msg, ref NOTIFYICONDATA d);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr LoadImageW(IntPtr inst, string name, uint type, int cx, int cy, uint load);
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr h);

        [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
        [DllImport("user32.dll")] private static extern bool DestroyMenu(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenuW(IntPtr menu, uint flags, uint id, string? item);
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] private static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr hwnd, IntPtr lptpm);
        [DllImport("user32.dll")] private static extern bool PostMessageW(IntPtr h, uint msg, IntPtr w, IntPtr l);

        // Message / flag constants
        private const int HWND_MESSAGE = -3;
        private const uint WM_HOTKEY = 0x0312;
        private const uint WM_COMMAND = 0x0111;
        private const uint WM_APP_TRAY = 0x0400 + 1; // WM_APP+1 tray callback
        private const uint WM_RBUTTONUP = 0x0205;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_CONTEXTMENU = 0x007B;
        private const uint WM_NULL = 0x0000;

        private const int NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2, NIM_SETVERSION = 4;
        private const int NIF_MESSAGE = 0x01, NIF_ICON = 0x02, NIF_TIP = 0x04;
        private const uint IMAGE_ICON = 1;
        private const uint LR_LOADFROMFILE = 0x10, LR_DEFAULTSIZE = 0x40;

        private const uint MF_STRING = 0x0000, MF_SEPARATOR = 0x0800, MF_CHECKED = 0x0008;
        private const uint TPM_RIGHTBUTTON = 0x0002, TPM_RETURNCMD = 0x0100, TPM_LEFTALIGN = 0x0000;

        private const int HOTKEY_ID = 0xB001;

        // Menu command IDs
        private const uint CMD_OPEN = 1;
        private const uint CMD_PANIC = 2;
        private const uint CMD_NEXT = 3;
        private const uint CMD_SLIDESHOW = 4;
        private const uint CMD_EXIT = 6;

        private IntPtr _hwnd;
        private IntPtr _hIcon;
        private Thread? _thread;
        private WndProc? _proc; // keep alive
        private readonly ManualResetEventSlim _ready = new(false);
        private uint _hotkeyRegistered;

        public Action? OnOpen, OnPanic, OnNext, OnToggleSlideshow, OnExit, OnHotkey, OnLeftClick;

        private NativeHost() { }

        public void Start()
        {
            if (_thread != null) return;
            _thread = new Thread(Pump) { IsBackground = true, Name = "WallSafeNativeHost" };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
            _ready.Wait(3000);
        }

        private void Pump()
        {
            _proc = HostWndProc;
            string cls = "WallSafeNativeHost_" + Guid.NewGuid().ToString("N");
            var wc = new WNDCLASS { lpfnWndProc = _proc, hInstance = GetModuleHandleW(null), lpszClassName = cls };
            RegisterClassW(ref wc);
            // Use a normal (not message-only) window so it can own the tray + foreground focus.
            _hwnd = CreateWindowExW(0, cls, "WallSafe", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, wc.hInstance, IntPtr.Zero);

            AddTrayIcon();
            RegisterConfiguredHotkey();

            _ready.Set();

            while (GetMessageW(out var m, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref m);
                DispatchMessageW(ref m);
            }
        }

        private IntPtr HostWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
                {
                    OnHotkey?.Invoke();
                    return IntPtr.Zero;
                }

                if (msg == WM_APP_TRAY)
                {
                    uint ev = (uint)(lParam.ToInt64() & 0xFFFF);
                    if (ev == WM_RBUTTONUP || ev == WM_CONTEXTMENU)
                        ShowContextMenu();
                    else if (ev == WM_LBUTTONUP)
                        OnLeftClick?.Invoke();
                    return IntPtr.Zero;
                }

                if (msg == WM_COMMAND)
                {
                    HandleCommand((uint)(wParam.ToInt64() & 0xFFFF));
                    return IntPtr.Zero;
                }
            }
            catch (Exception ex) { App.LogCrash("NativeHostWndProc", ex); }

            return DefWindowProcW(hWnd, msg, wParam, lParam);
        }

        private void HandleCommand(uint id)
        {
            switch (id)
            {
                case CMD_OPEN: OnOpen?.Invoke(); break;
                case CMD_PANIC: OnPanic?.Invoke(); break;
                case CMD_NEXT: OnNext?.Invoke(); break;
                case CMD_SLIDESHOW: OnToggleSlideshow?.Invoke(); break;
                case CMD_EXIT: OnExit?.Invoke(); break;
            }
        }

        private void ShowContextMenu()
        {
            IntPtr menu = CreatePopupMenu();
            if (menu == IntPtr.Zero) return;
            try
            {
                AppendMenuW(menu, MF_STRING, CMD_OPEN, "Open WallSafe");
                AppendMenuW(menu, MF_STRING, CMD_PANIC, "Safe wallpaper · panic toggle");
                AppendMenuW(menu, MF_STRING, CMD_NEXT, "Next wallpaper");
                AppendMenuW(menu, MF_SEPARATOR, 0, null);
                AppendMenuW(menu, MF_STRING | (Settings.Instance.SlideshowEnabled ? MF_CHECKED : 0), CMD_SLIDESHOW, "Auto slideshow");
                AppendMenuW(menu, MF_SEPARATOR, 0, null);
                AppendMenuW(menu, MF_STRING, CMD_EXIT, "Exit WallSafe");

                GetCursorPos(out var pt);
                SetForegroundWindow(_hwnd); // required so the menu dismisses correctly
                int cmd = TrackPopupMenuEx(menu, TPM_RIGHTBUTTON | TPM_RETURNCMD | TPM_LEFTALIGN, pt.X, pt.Y, _hwnd, IntPtr.Zero);
                PostMessageW(_hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);
                if (cmd != 0) HandleCommand((uint)cmd);
            }
            finally { DestroyMenu(menu); }
        }

        private void AddTrayIcon()
        {
            try
            {
                string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "WallSafe.ico");
                if (System.IO.File.Exists(iconPath))
                    _hIcon = LoadImageW(IntPtr.Zero, iconPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);

                var data = new NOTIFYICONDATA
                {
                    cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                    hWnd = _hwnd,
                    uID = 1,
                    uFlags = NIF_MESSAGE | NIF_TIP | (_hIcon != IntPtr.Zero ? NIF_ICON : 0),
                    uCallbackMessage = (int)WM_APP_TRAY,
                    hIcon = _hIcon,
                    szTip = "WallSafe — Desktop Anime Wallpaper Manager"
                };
                Shell_NotifyIconW(NIM_ADD, ref data);
                data.uTimeoutOrVersion = 4; // NOTIFYICON_VERSION_4
                Shell_NotifyIconW(NIM_SETVERSION, ref data);
            }
            catch (Exception ex) { App.LogCrash("AddTrayIcon", ex); }
        }

        private void RemoveTrayIcon()
        {
            try
            {
                var data = new NOTIFYICONDATA { cbSize = Marshal.SizeOf<NOTIFYICONDATA>(), hWnd = _hwnd, uID = 1 };
                Shell_NotifyIconW(NIM_DELETE, ref data);
            }
            catch { }
            if (_hIcon != IntPtr.Zero) { try { DestroyIcon(_hIcon); } catch { } _hIcon = IntPtr.Zero; }
        }

        public void RegisterConfiguredHotkey()
        {
            if (_hwnd == IntPtr.Zero) return;
            try
            {
                if (_hotkeyRegistered != 0) { UnregisterHotKey(_hwnd, HOTKEY_ID); _hotkeyRegistered = 0; }
                var mods = (uint)(ModifierKeys)Settings.Instance.HotkeyModifiers;
                uint vk = (uint)Settings.Instance.HotkeyKey;
                if (vk == 0) return;
                const uint MOD_NOREPEAT = 0x4000;
                bool ok = RegisterHotKey(_hwnd, HOTKEY_ID, mods | MOD_NOREPEAT, vk);
                if (!ok) ok = RegisterHotKey(_hwnd, HOTKEY_ID, mods, vk);
                _hotkeyRegistered = ok ? 1u : 0u;
            }
            catch (Exception ex) { App.LogCrash("RegisterHotkey", ex); }
        }

        public void Dispose()
        {
            try { if (_hotkeyRegistered != 0 && _hwnd != IntPtr.Zero) UnregisterHotKey(_hwnd, HOTKEY_ID); } catch { }
            RemoveTrayIcon();
            try { if (_hwnd != IntPtr.Zero) DestroyWindow(_hwnd); } catch { }
        }
    }
}
