using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace WallSafeWinUI.Services
{
    /// <summary>
    /// Background/foreground lifecycle for the WinUI main window. Hiding the window
    /// aggressively compacts the working set (like the WPF build's AppSuspensionManager)
    /// so the app is cheap to keep resident in the tray.
    /// </summary>
    public static class AppSuspensionManager
    {
        private static volatile bool _isHibernating;

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr min, IntPtr max);

        public static bool IsHibernating => _isHibernating;

        public static void EnterBackgroundMode(MainWindow window)
        {
            window.HideWindow();
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
                    SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, (IntPtr)(-1), (IntPtr)(-1));
                }
                catch { }
            });
        }

        public static void RestoreForegroundMode(MainWindow window)
        {
            _isHibernating = false;
            window.ShowWindow();
        }
    }
}
