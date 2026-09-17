using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace WallSafeWinUI.Services
{
    public static class StartupManager
    {
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "WallSafe";

        public static string GetExecutablePath()
        {
            try
            {
                string? path = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(path))
                    return path;

                path = Process.GetCurrentProcess().MainModule?.FileName;
                return path ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
                if (key == null) return false;

                var val = key.GetValue(AppName) as string;
                return !string.IsNullOrEmpty(val);
            }
            catch
            {
                return false;
            }
        }

        public static bool SetStartup(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                if (key == null) return false;

                if (enable)
                {
                    string exePath = GetExecutablePath();
                    if (string.IsNullOrWhiteSpace(exePath)) return false;

                    string runCommand = $"\"{exePath}\" --autostart";
                    key.SetValue(AppName, runCommand);
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
