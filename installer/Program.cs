using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WallSafeInstaller
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new InstallerForm());
        }
    }

    public class InstallerForm : Form
    {
        private ProgressBar _progressBar;
        private Label _statusLabel;
        private Button _actionButton;
        private CheckBox _desktopShortcutCheck;
        private CheckBox _launchAppCheck;
        private bool _completed = false;

        private readonly string _installDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "WallSafe");

        public InstallerForm()
        {
            Text = "WallSafe v3.0.0 Setup";
            Width = 480;
            Height = 320;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(249, 249, 251);
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WallSafe.ico");
                if (File.Exists(iconPath))
                    Icon = new Icon(iconPath);
            }
            catch { }

            BuildUi();
        }

        private void BuildUi()
        {
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 85,
                BackColor = Color.FromArgb(243, 243, 247),
                Padding = new Padding(24, 18, 24, 0)
            };

            var titleLabel = new Label
            {
                Text = "WallSafe v3.0.0",
                Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 35),
                AutoSize = true,
                Location = new Point(24, 16)
            };

            var subLabel = new Label
            {
                Text = "Modern anime desktop wallpaper manager with instant panic hotkey",
                Font = new Font("Segoe UI", 9.0f),
                ForeColor = Color.FromArgb(110, 110, 120),
                AutoSize = true,
                Location = new Point(24, 44)
            };

            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(subLabel);
            Controls.Add(headerPanel);

            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 18, 24, 18)
            };

            var pathLabel = new Label
            {
                Text = $"Install location: {_installDir}",
                ForeColor = Color.FromArgb(100, 100, 110),
                AutoSize = true,
                Location = new Point(24, 100)
            };
            Controls.Add(pathLabel);

            _desktopShortcutCheck = new CheckBox
            {
                Text = "Create Desktop shortcut",
                Checked = true,
                AutoSize = true,
                Location = new Point(24, 126),
                ForeColor = Color.FromArgb(40, 40, 45)
            };
            Controls.Add(_desktopShortcutCheck);

            _launchAppCheck = new CheckBox
            {
                Text = "Launch WallSafe after installation",
                Checked = true,
                AutoSize = true,
                Location = new Point(24, 152),
                ForeColor = Color.FromArgb(40, 40, 45)
            };
            Controls.Add(_launchAppCheck);

            _progressBar = new ProgressBar
            {
                Location = new Point(24, 188),
                Size = new Size(416, 16),
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 0,
                Visible = false
            };
            Controls.Add(_progressBar);

            _statusLabel = new Label
            {
                Text = "Click Install to begin setup.",
                Location = new Point(24, 212),
                Size = new Size(416, 20),
                ForeColor = Color.FromArgb(90, 90, 100)
            };
            Controls.Add(_statusLabel);

            _actionButton = new Button
            {
                Text = "Install",
                Size = new Size(110, 34),
                Location = new Point(330, 236),
                BackColor = Color.FromArgb(79, 70, 229),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _actionButton.FlatAppearance.BorderSize = 0;
            _actionButton.Click += ActionButton_Click;
            Controls.Add(_actionButton);
        }

        private async void ActionButton_Click(object? sender, EventArgs e)
        {
            if (_completed)
            {
                if (_launchAppCheck.Checked)
                {
                    string exePath = Path.Combine(_installDir, "WallSafeWinUI.exe");
                    if (File.Exists(exePath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = exePath,
                            WorkingDirectory = _installDir,
                            UseShellExecute = true
                        });
                    }
                }
                Close();
                return;
            }

            _actionButton.Enabled = false;
            _desktopShortcutCheck.Enabled = false;
            _launchAppCheck.Enabled = false;
            _progressBar.Visible = true;
            _progressBar.MarqueeAnimationSpeed = 30;

            await Task.Run(() => PerformInstall());
        }

        private void PerformInstall()
        {
            try
            {
                SetStatus("Terminating existing WallSafe processes if running...");
                try
                {
                    foreach (var proc in Process.GetProcessesByName("WallSafeWinUI"))
                    {
                        proc.Kill();
                        proc.WaitForExit(3000);
                    }
                }
                catch { }

                SetStatus("Extracting files to installation directory...");
                Directory.CreateDirectory(_installDir);

                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
                {
                    if (stream == null)
                        throw new InvalidOperationException("Embedded installation payload not found.");

                    using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                    foreach (var entry in archive.Entries)
                    {
                        string destinationPath = Path.GetFullPath(Path.Combine(_installDir, entry.FullName));
                        if (!destinationPath.StartsWith(Path.GetFullPath(_installDir), StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            Directory.CreateDirectory(destinationPath);
                        }
                        else
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                            entry.ExtractToFile(destinationPath, overwrite: true);
                        }
                    }
                }

                string exePath = Path.Combine(_installDir, "WallSafeWinUI.exe");

                SetStatus("Creating shortcuts...");
                // Start Menu shortcut
                string startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "WallSafe");
                Directory.CreateDirectory(startMenuDir);
                string startMenuLnk = Path.Combine(startMenuDir, "WallSafe.lnk");
                CreateShortcut(startMenuLnk, exePath, _installDir, "WallSafe Anime Wallpaper Manager");

                // Desktop shortcut
                if (_desktopShortcutCheck.Checked)
                {
                    string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    string desktopLnk = Path.Combine(desktopDir, "WallSafe.lnk");
                    CreateShortcut(desktopLnk, exePath, _installDir, "WallSafe Anime Wallpaper Manager");
                }

                SetStatus("Writing uninstaller and registry keys...");
                CreateUninstaller();
                RegisterUninstallEntry();

                Invoke(new Action(() =>
                {
                    _progressBar.Style = ProgressBarStyle.Continuous;
                    _progressBar.Value = 100;
                    _statusLabel.Text = "Installation completed successfully!";
                    _statusLabel.ForeColor = Color.FromArgb(16, 149, 106);
                    _actionButton.Text = "Finish";
                    _actionButton.Enabled = true;
                    _completed = true;
                }));
            }
            catch (Exception ex)
            {
                Invoke(new Action(() =>
                {
                    _progressBar.Visible = false;
                    _statusLabel.Text = "Error: " + ex.Message;
                    _statusLabel.ForeColor = Color.Red;
                    _actionButton.Text = "Close";
                    _actionButton.Enabled = true;
                    _completed = true;
                }));
            }
        }

        private void SetStatus(string text)
        {
            Invoke(new Action(() => _statusLabel.Text = text));
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string description)
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellType)!;
                    var shortcut = shell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = targetPath;
                    shortcut.WorkingDirectory = workingDir;
                    shortcut.Description = description;
                    shortcut.IconLocation = targetPath + ",0";
                    shortcut.Save();
                }
            }
            catch { }
        }

        private void CreateUninstaller()
        {
            try
            {
                string uninstScript = Path.Combine(_installDir, "Uninstall.cmd");
                string content = $@"@echo off
taskkill /F /IM WallSafeWinUI.exe >nul 2>&1
timeout /t 1 /nobreak >nul
del /q ""{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)}\WallSafe.lnk"" >nul 2>&1
rmdir /s /q ""{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "WallSafe")}"" >nul 2>&1
reg delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\WallSafe"" /f >nul 2>&1
echo WallSafe shortcuts and registry keys removed.
echo To completely remove application data and binaries:
echo rmdir /s /q ""{_installDir}""
start /b cmd /c ""timeout /t 1 >nul & rmdir /s /q \""{_installDir}\""""
";
                File.WriteAllText(uninstScript, content);
            }
            catch { }
        }

        private void RegisterUninstallEntry()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\WallSafe");
                if (key != null)
                {
                    key.SetValue("DisplayName", "WallSafe (WinUI 3)");
                    key.SetValue("DisplayVersion", "3.0.0");
                    key.SetValue("Publisher", "WallSafe Team");
                    key.SetValue("DisplayIcon", Path.Combine(_installDir, "WallSafeWinUI.exe") + ",0");
                    key.SetValue("InstallLocation", _installDir);
                    key.SetValue("UninstallString", $"\"{Path.Combine(_installDir, "Uninstall.cmd")}\"");
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }
            catch { }
        }
    }
}
