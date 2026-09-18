using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace WallSafeWinUI.Services
{
    public class UpdateInfo
    {
        public bool IsUpdateAvailable { get; set; }
        public Version CurrentVersion { get; set; } = new(3, 1, 0);
        public Version? LatestVersion { get; set; }
        public string LatestVersionString { get; set; } = "";
        public string ReleaseTitle { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public long DownloadSize { get; set; }
        public string ReleaseUrl { get; set; } = "";
    }

    public static class UpdateService
    {
        public static readonly Version CurrentVersion = new(3, 1, 0);
        private const string RepoApiUrl = "https://api.github.com/repos/Kwan-desu/wallsafe/releases/latest";

        public static async Task<UpdateInfo> CheckForUpdateAsync()
        {
            var info = new UpdateInfo { CurrentVersion = CurrentVersion };

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WallSafe-WinUI-App/3.1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");

            var response = await client.GetAsync(RepoApiUrl);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"GitHub API returned status {response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync();
            var release = JObject.Parse(json);

            string tagName = release["tag_name"]?.ToString() ?? "";
            info.ReleaseTitle = release["name"]?.ToString() ?? tagName;
            info.ReleaseNotes = release["body"]?.ToString() ?? "";
            info.ReleaseUrl = release["html_url"]?.ToString() ?? "https://github.com/Kwan-desu/wallsafe/releases";

            var match = Regex.Match(tagName, @"\d+(\.\d+)+");
            if (match.Success && Version.TryParse(match.Value, out var latestVer))
            {
                info.LatestVersion = latestVer;
                info.LatestVersionString = latestVer.ToString();
                info.IsUpdateAvailable = latestVer > CurrentVersion;
            }

            var assets = release["assets"] as JArray;
            if (assets != null)
            {
                var setupAsset = assets.FirstOrDefault(a => (a["name"]?.ToString() ?? "").EndsWith("-Setup.exe", StringComparison.OrdinalIgnoreCase))
                              ?? assets.FirstOrDefault(a => (a["name"]?.ToString() ?? "").EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

                if (setupAsset != null)
                {
                    info.DownloadUrl = setupAsset["browser_download_url"]?.ToString() ?? "";
                    info.DownloadSize = setupAsset["size"]?.Value<long>() ?? 0;
                }
            }

            return info;
        }

        public static async Task DownloadAndInstallUpdateAsync(string downloadUrl, IProgress<double>? progress = null)
        {
            if (string.IsNullOrEmpty(downloadUrl))
                throw new ArgumentException("No download URL provided.");

            string tempSetup = Path.Combine(Path.GetTempPath(), "WallSafe-Update-Setup.exe");

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WallSafe-WinUI-App/3.1.0");

            using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;

            using (var contentStream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = new FileStream(tempSetup, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                var buffer = new byte[8192];
                long totalRead = 0L;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                        progress?.Report((double)totalRead / totalBytes);
                }
            }

            // Launch the downloaded setup executable and close current application
            Process.Start(new ProcessStartInfo
            {
                FileName = tempSetup,
                UseShellExecute = true
            });

            // Exit current application so the installer can overwrite files
            Environment.Exit(0);
        }
    }
}
