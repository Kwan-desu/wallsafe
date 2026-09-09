using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;

namespace WallSafe
{
    public static class AppSuspensionManager
    {
        private static volatile bool _isHibernating;

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public static bool IsHibernating => _isHibernating;

        public static void EnterBackgroundMode(Window window)
        {
            window.Hide();
            Hibernate();
        }

        public static void Hibernate()
        {
            _isHibernating = true;

            _ = Task.Run(async () =>
            {
                await Task.Delay(200);
                if (!_isHibernating) return;

                try
                {
                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                    GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                    GC.WaitForPendingFinalizers();
                    GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);

                    IntPtr hProcess = Process.GetCurrentProcess().Handle;
                    SetProcessWorkingSetSize(hProcess, (IntPtr)(-1), (IntPtr)(-1));
                }
                catch { }
            });
        }

        public static void RestoreForegroundMode(Window window)
        {
            _isHibernating = false;

            window.Show();
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }
            window.Activate();
            window.Focus();

            try
            {
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                SetForegroundWindow(helper.Handle);
            }
            catch { }
        }
    }
}
