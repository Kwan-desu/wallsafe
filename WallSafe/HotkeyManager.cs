using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WallSafe
{
    [Flags]
    public enum ModifierKeys
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Win = 8
    }

    public class HotkeyManager : IDisposable
    {
        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private static HotkeyManager? _instance;
        public static HotkeyManager Instance => _instance ??= new HotkeyManager();

        private readonly NativeWindow _window = new HotkeyWindow();
        private int _registeredId = 0;
        private Action? _action;
        private bool _panicTaskbarConcealed = false;

        public HotkeyManager()
        {
            _window.CreateHandle(new CreateParams());
            ((HotkeyWindow)_window).HotkeyPressed += OnHotkey;
        }

        public bool IsRegistered => _registeredId != 0;
        public event Action<bool>? RegistrationChanged;

        public bool Register(Keys key, ModifierKeys mods, Action callback)
        {
            Unregister();
            _action = callback;
            _registeredId = 1001;
            bool ok = RegisterHotKey(_window.Handle, _registeredId, (int)mods, (int)key);
            if (!ok)
            {
                _registeredId = 0;
            }
            RegistrationChanged?.Invoke(ok);
            return ok;
        }

        public bool ReRegister()
        {
            var s = Settings.Instance;
            var mods = (ModifierKeys)s.HotkeyModifiers;
            var key = (Keys)s.HotkeyKey;

            return Register(key, mods, () =>
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    // 1. Conceal all WallSafe UI immediately to prevent screen leakage
                    if (Settings.Instance.PanicHideApp)
                    {
                        foreach (System.Windows.Window w in System.Windows.Application.Current.Windows)
                        {
                            w.Hide();
                        }
                    }

                    // 2. Boss-Key Show Desktop
                    if (Settings.Instance.PanicMinimizeAllWindows)
                    {
                        WallpaperManager.MinimizeAllWindows();
                    }

                    // 3. Audio Mute
                    if (Settings.Instance.PanicMuteAudio)
                    {
                        WallpaperManager.MuteMasterAudio();
                    }

                    // 4. Taskbar concealment: revert the (merged TranslucentTB) taskbar effect
                    //    to Normal on conceal, and restore the user's appearance on the
                    //    second (restore) press — kept in sync with the bimodal wallpaper toggle.
                    if (Settings.Instance.TaskbarTransparencyEnabled && Settings.Instance.TaskbarRestoreOnPanic)
                    {
                        if (!_panicTaskbarConcealed)
                        {
                            TaskbarManager.Instance.Restore();
                            _panicTaskbarConcealed = true;
                        }
                        else
                        {
                            TaskbarManager.Instance.ApplyFromSettings();
                            _panicTaskbarConcealed = false;
                        }
                    }

                    // 5. Bimodal Safe Wallpaper Toggle
                    WallpaperManager.Instance.ApplySafeWallpaper();
                });
            });
        }

        public void Unregister()
        {
            if (_registeredId != 0)
            {
                UnregisterHotKey(_window.Handle, _registeredId);
                _registeredId = 0;
                RegistrationChanged?.Invoke(false);
            }
        }

        private void OnHotkey(int id)
        {
            if (id == _registeredId)
            {
                _action?.Invoke();
            }
        }

        public void Dispose()
        {
            Unregister();
            _window.DestroyHandle();
        }

        private class HotkeyWindow : NativeWindow
        {
            public event Action<int>? HotkeyPressed;
            private const int WM_HOTKEY = 0x0312;

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY)
                    HotkeyPressed?.Invoke(m.WParam.ToInt32());
                base.WndProc(ref m);
            }
        }
    }
}
