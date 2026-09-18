using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WallSafeWinUI.Services
{
    /// <summary>
    /// Content-addressable disk cache for booru thumbnails / samples. Returns a
    /// local file path that WinUI's BitmapImage can load directly. Unlike the WPF
    /// build there is no in-process BitmapSource cache — WinUI decodes from the URI
    /// and manages its own image cache, so we only need the disk layer here.
    /// </summary>
    public class ImageCacheService
    {
        public static readonly ImageCacheService Instance = new();

        private readonly string _cacheFolder;
        private readonly HttpClient _http;
        private readonly ConcurrentDictionary<string, Task<string>> _inFlightDownloads = new();
        private readonly ConcurrentDictionary<string, Task<string>> _inFlightBlur = new();
        private readonly SemaphoreSlim _downloadThrottle = new(16, 16);
        private long _totalCacheSizeBytes = -1;

        private ImageCacheService()
        {
            _http = new HttpClient(new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                EnableMultipleHttp2Connections = true
            })
            {
                Timeout = TimeSpan.FromSeconds(25)
            };
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");

            _cacheFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WallSafe", "Cache", "Thumbnails");
            Directory.CreateDirectory(_cacheFolder);

            Task.Run(() =>
            {
                try
                {
                    var di = new DirectoryInfo(_cacheFolder);
                    long total = di.Exists ? di.GetFiles().Sum(fi => fi.Length) : 0;
                    Interlocked.Exchange(ref _totalCacheSizeBytes, total);
                }
                catch { Interlocked.Exchange(ref _totalCacheSizeBytes, 0); }
            });
        }

        public void PreloadThumbnails(IEnumerable<string> urls)
        {
            var validUrls = urls.Where(u => !string.IsNullOrWhiteSpace(u)).Take(36).ToList();
            if (validUrls.Count == 0) return;
            Task.Run(async () =>
            {
                var tasks = validUrls.Select(url => GetCachedImagePathAsync(url));
                try { await Task.WhenAll(tasks); } catch { }
            });
        }

        public Task<string> GetCachedImagePathAsync(string imageUrl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) return Task.FromResult(string.Empty);

            string fileName = GetHashedFileName(imageUrl);
            string cachedPath = Path.Combine(_cacheFolder, fileName);

            if (File.Exists(cachedPath) && new FileInfo(cachedPath).Length > 0)
                return Task.FromResult(cachedPath);

            return _inFlightDownloads.GetOrAdd(imageUrl, async (url) =>
            {
                try
                {
                    if (File.Exists(cachedPath) && new FileInfo(cachedPath).Length > 0)
                        return cachedPath;

                    string tempFile = Path.Combine(_cacheFolder, $"dl_{Guid.NewGuid():N}.tmp");
                    await _downloadThrottle.WaitAsync(cancellationToken);
                    try
                    {
                        for (int attempt = 1; attempt <= 2; attempt++)
                        {
                            try
                            {
                                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                                if (url.Contains("yande.re", StringComparison.OrdinalIgnoreCase))
                                    request.Headers.Referrer = new Uri("https://yande.re/");
                                else if (url.Contains("konachan", StringComparison.OrdinalIgnoreCase))
                                    request.Headers.Referrer = new Uri("https://konachan.net/");

                                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                                if (response.IsSuccessStatusCode)
                                {
                                    await using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 16384, true))
                                    {
                                        await response.Content.CopyToAsync(fileStream, cancellationToken);
                                    }
                                    if (File.Exists(tempFile) && new FileInfo(tempFile).Length > 0)
                                    {
                                        long len = new FileInfo(tempFile).Length;
                                        File.Move(tempFile, cachedPath, overwrite: true);
                                        Interlocked.Add(ref _totalCacheSizeBytes, len);
                                        return cachedPath;
                                    }
                                }
                            }
                            catch when (attempt < 2)
                            {
                                await Task.Delay(200, cancellationToken);
                            }
                            finally
                            {
                                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
                            }
                        }
                    }
                    finally { _downloadThrottle.Release(); }

                    if (File.Exists(cachedPath) && new FileInfo(cachedPath).Length > 0)
                        return cachedPath;
                    return url;
                }
                catch { return url; }
                finally { _inFlightDownloads.TryRemove(imageUrl, out _); }
            });
        }

        public Task<string> GetCachedBlurredImagePathAsync(string imageUrl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) return Task.FromResult(string.Empty);

            string baseHashedName = GetHashedFileName(imageUrl);
            string blurFileName = Path.GetFileNameWithoutExtension(baseHashedName) + "_blur.jpg";
            string blurredPath = Path.Combine(_cacheFolder, blurFileName);

            if (File.Exists(blurredPath) && new FileInfo(blurredPath).Length > 0)
                return Task.FromResult(blurredPath);

            return _inFlightBlur.GetOrAdd(imageUrl, async (url) =>
            {
                try
                {
                    if (File.Exists(blurredPath) && new FileInfo(blurredPath).Length > 0)
                        return blurredPath;

                    string sourcePath = await GetCachedImagePathAsync(url, cancellationToken);
                    if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                        return string.Empty;

                    using var fileStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    var memStream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                    await fileStream.CopyToAsync(memStream.AsStreamForWrite());
                    memStream.Seek(0);

                    var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(memStream);
                    uint origW = decoder.PixelWidth;
                    uint origH = decoder.PixelHeight;
                    if (origW == 0 || origH == 0) return string.Empty;

                    uint targetW = 160;
                    uint targetH = Math.Max(10, (origH * targetW) / origW);

                    var transform = new Windows.Graphics.Imaging.BitmapTransform
                    {
                        ScaledWidth = targetW,
                        ScaledHeight = targetH,
                        InterpolationMode = Windows.Graphics.Imaging.BitmapInterpolationMode.Fant
                    };

                    var pixelData = await decoder.GetPixelDataAsync(
                        Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                        Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                        transform,
                        Windows.Graphics.Imaging.ExifOrientationMode.RespectExifOrientation,
                        Windows.Graphics.Imaging.ColorManagementMode.ColorManageToSRgb);

                    byte[] pixels = pixelData.DetachPixelData();
                    FastBlur.ProcessBgra(pixels, (int)targetW, (int)targetH, radius: 8);

                    var outMem = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                    var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.JpegEncoderId, outMem);
                    encoder.SetPixelData(
                        Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                        Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                        targetW,
                        targetH,
                        96,
                        96,
                        pixels);
                    await encoder.FlushAsync();

                    string tempFile = Path.Combine(_cacheFolder, $"blur_{Guid.NewGuid():N}.tmp");
                    using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        outMem.Seek(0);
                        await outMem.AsStreamForRead().CopyToAsync(fs);
                    }

                    if (File.Exists(tempFile) && new FileInfo(tempFile).Length > 0)
                    {
                        File.Move(tempFile, blurredPath, overwrite: true);
                        return blurredPath;
                    }

                    return string.Empty;
                }
                catch { return string.Empty; }
                finally { _inFlightBlur.TryRemove(imageUrl, out _); }
            });
        }

        public void ClearCache()
        {
            try
            {
                if (Directory.Exists(_cacheFolder))
                    foreach (var file in Directory.GetFiles(_cacheFolder))
                        try { File.Delete(file); } catch { }
                Interlocked.Exchange(ref _totalCacheSizeBytes, 0);
            }
            catch { }
        }

        public long GetCacheSizeInBytes()
        {
            long current = Interlocked.Read(ref _totalCacheSizeBytes);
            if (current >= 0) return current;
            try
            {
                if (!Directory.Exists(_cacheFolder)) return 0;
                long size = new DirectoryInfo(_cacheFolder).GetFiles().Sum(fi => fi.Length);
                Interlocked.Exchange(ref _totalCacheSizeBytes, size);
                return size;
            }
            catch { return 0; }
        }

        private static string GetHashedFileName(string url)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(url);
            byte[] hash = MD5.HashData(utf8);
            string hashStr = Convert.ToHexString(hash).ToLowerInvariant();
            string ext = ".jpg";
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                string e = Path.GetExtension(uri.AbsolutePath);
                if (!string.IsNullOrWhiteSpace(e) && e.Length <= 5) ext = e;
            }
            return $"{hashStr}{ext}";
        }
    }
}
