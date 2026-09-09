using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WallSafe
{
    public class DownloadsManager
    {
        public static readonly DownloadsManager Instance = new();

        private readonly HttpClient _http = new()
        {
            Timeout = TimeSpan.FromMinutes(3),
            DefaultRequestHeaders = { { "User-Agent", "WallSafe/2.0" } }
        };

        private readonly List<PostItem> _downloads = new();
        private readonly object _lock = new();

        public event Action? DownloadsChanged;

        private static string MetadataFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WallSafe", "downloads.json");

        public string DownloadFolder
        {
            get
            {
                var custom = Settings.Instance.DownloadDirectory;
                if (!string.IsNullOrWhiteSpace(custom) && Directory.Exists(custom))
                    return custom;

                var defaultPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "WallSafe");
                Directory.CreateDirectory(defaultPath);
                return defaultPath;
            }
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    Settings.Instance.DownloadDirectory = value;
                    Settings.Instance.Save();
                }
            }
        }

        private DownloadsManager()
        {
            Load();
        }

        private void Load()
        {
            try
            {
                if (File.Exists(MetadataFilePath))
                {
                    var json = File.ReadAllText(MetadataFilePath);
                    var items = JsonConvert.DeserializeObject<List<PostItem>>(json);
                    if (items != null)
                    {
                        lock (_lock)
                        {
                            _downloads.Clear();
                            foreach (var item in items)
                            {
                                if (!string.IsNullOrEmpty(item.LocalPath) && File.Exists(item.LocalPath))
                                {
                                    item.IsDownloaded = true;
                                    _downloads.Add(item);
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        public void Save()
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(MetadataFilePath)!);
                    File.WriteAllText(MetadataFilePath, JsonConvert.SerializeObject(_downloads, Formatting.Indented));
                }
                DownloadsChanged?.Invoke();
            }
            catch { }
        }

        public bool IsDownloaded(int id, string source, out string? localPath)
        {
            lock (_lock)
            {
                var match = _downloads.FirstOrDefault(d => d.Id == id && d.Source.Equals(source, StringComparison.OrdinalIgnoreCase));
                if (match != null && !string.IsNullOrEmpty(match.LocalPath) && File.Exists(match.LocalPath))
                {
                    localPath = match.LocalPath;
                    return true;
                }
            }
            localPath = null;
            return false;
        }

        public async Task<string> DownloadPostAsync(PostItem post, IProgress<double>? progress = null, CancellationToken ct = default)
        {
            string url = post.FullDownloadUrl;
            string ext = Path.GetExtension(new Uri(url).AbsolutePath);
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";

            string fileName = $"WallSafe_{post.Source}_{post.Id}_{post.Width}x{post.Height}{ext}";
            string destination = Path.Combine(DownloadFolder, fileName);

            if (!File.Exists(destination))
            {
                string tempFile = $"{destination}.download";
                try
                {
                    using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                    await using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 16384, true))
                    {
                        var buffer = new byte[16384];
                        long totalRead = 0;
                        int read;

                        while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                        {
                            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                            totalRead += read;
                            if (totalBytes > 0 && progress != null)
                            {
                                progress.Report((double)totalRead / totalBytes);
                            }
                        }
                        await fileStream.FlushAsync(ct);
                    }

                    if (File.Exists(destination))
                    {
                        try { File.Delete(destination); } catch { }
                    }
                    File.Move(tempFile, destination, true);
                }
                catch
                {
                    try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
                    throw;
                }
            }

            post.LocalPath = destination;
            post.IsDownloaded = true;

            lock (_lock)
            {
                _downloads.RemoveAll(d => d.Id == post.Id && d.Source.Equals(post.Source, StringComparison.OrdinalIgnoreCase));
                _downloads.Insert(0, post);
            }
            Save();

            return destination;
        }

        public void DeleteDownload(PostItem post)
        {
            lock (_lock)
            {
                var match = _downloads.FirstOrDefault(d => d.Id == post.Id && d.Source.Equals(post.Source, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    if (!string.IsNullOrEmpty(match.LocalPath) && File.Exists(match.LocalPath))
                    {
                        try { File.Delete(match.LocalPath); } catch { }
                    }
                    match.IsDownloaded = false;
                    _downloads.Remove(match);
                }
            }
            post.IsDownloaded = false;
            Save();
        }

        public List<PostItem> GetAllDownloads()
        {
            lock (_lock)
            {
                return _downloads.Where(d => !string.IsNullOrEmpty(d.LocalPath) && File.Exists(d.LocalPath)).ToList();
            }
        }
    }
}
