using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace WallSafeWeb.Services
{
    public class UpdateInfo
    {
        public bool IsUpdateAvailable { get; set; }
        public Version CurrentVersion { get; set; } = UpdateService.CurrentVersion;
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
        public static readonly Version CurrentVersion =
            new((System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(4, 0, 0)).ToString(3));
        private const string RepoApiUrl = "https://api.github.com/repos/Kwan-desu/wallsafe/releases/latest";
        // Public releases need no auth. Optionally set WALLSAFE_GITHUB_TOKEN for rate limits.
        private static readonly string? ApiToken = System.Environment.GetEnvironmentVariable("WALLSAFE_GITHUB_TOKEN");

        public static async Task<UpdateInfo> CheckForUpdateAsync()
        {
            var info = new UpdateInfo { CurrentVersion = CurrentVersion };

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"WallSafe-Web-App/{CurrentVersion}");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");
            if (!string.IsNullOrEmpty(ApiToken))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", ApiToken);
            }

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
                    // Public repo: the browser URL serves the file directly. The API asset URL returns
                    // JSON metadata unless Accept: application/octet-stream is sent.
                    info.DownloadUrl = setupAsset["browser_download_url"]?.ToString() ?? setupAsset["url"]?.ToString() ?? "";
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

            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var client = new HttpClient(handler);
            var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            request.Headers.UserAgent.ParseAdd($"WallSafe-Web-App/{CurrentVersion}");
            if (downloadUrl.Contains("api.github.com"))
            {
                request.Headers.Accept.ParseAdd("application/octet-stream");
                if (!string.IsNullOrEmpty(ApiToken))
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", ApiToken);
            }

            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (response.StatusCode == System.Net.HttpStatusCode.MovedPermanently ||
                response.StatusCode == System.Net.HttpStatusCode.Found ||
                response.StatusCode == System.Net.HttpStatusCode.SeeOther ||
                response.StatusCode == System.Net.HttpStatusCode.TemporaryRedirect)
            {
                var redirectUrl = response.Headers.Location;
                if (redirectUrl != null)
                {
                    using var redirectClient = new HttpClient();
                    redirectClient.DefaultRequestHeaders.UserAgent.ParseAdd($"WallSafe-Web-App/{CurrentVersion}");
                    response = await redirectClient.GetAsync(redirectUrl, HttpCompletionOption.ResponseHeadersRead);
                }
            }
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

            // Never launch something that isn't a Windows executable (e.g. an HTML/JSON error page).
            using (var fs = File.OpenRead(tempSetup))
            {
                var mz = new byte[2];
                if (fs.Length < 1_000_000 || fs.Read(mz, 0, 2) != 2 || mz[0] != (byte)'M' || mz[1] != (byte)'Z')
                    throw new InvalidDataException("The downloaded update is not a valid installer.");
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
