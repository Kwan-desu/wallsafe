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

namespace WallSafe
{
    public class ImageCacheService
    {
        public static readonly ImageCacheService Instance = new();

        private readonly string _cacheFolder;
        private readonly HttpClient _http;
        private readonly ConcurrentDictionary<string, Task<string>> _inFlightDownloads = new();
        private readonly ConcurrentDictionary<string, System.Windows.Media.Imaging.BitmapSource> _bitmapMemoryCache = new();
        private readonly SemaphoreSlim _downloadThrottle = new(16, 16);
        private long _totalCacheSizeBytes = -1;

        public System.Windows.Media.Imaging.BitmapSource? GetFromMemoryCache(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            if (_bitmapMemoryCache.TryGetValue(url, out var bmp)) return bmp;
            return null;
        }

        public void AddToMemoryCache(string url, System.Windows.Media.Imaging.BitmapSource bmp)
        {
            if (string.IsNullOrEmpty(url) || bmp == null) return;
            if (_bitmapMemoryCache.Count > 250)
            {
                var toRemove = _bitmapMemoryCache.Keys.Take(50).ToList();
                foreach (var k in toRemove) _bitmapMemoryCache.TryRemove(k, out _);
            }
            _bitmapMemoryCache[url] = bmp;
        }

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
                "WallSafe",
                "Cache",
                "Thumbnails"
            );

            Directory.CreateDirectory(_cacheFolder);

            // Compute initial cache size in background without blocking UI
            Task.Run(() =>
            {
                try
                {
                    if (Directory.Exists(_cacheFolder))
                    {
                        var di = new DirectoryInfo(_cacheFolder);
                        long total = di.GetFiles().Sum(fi => fi.Length);
                        Interlocked.Exchange(ref _totalCacheSizeBytes, total);
                    }
                    else
                    {
                        Interlocked.Exchange(ref _totalCacheSizeBytes, 0);
                    }
                }
                catch
                {
                    Interlocked.Exchange(ref _totalCacheSizeBytes, 0);
                }
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
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return Task.FromResult(string.Empty);
            }

            string fileName = GetHashedFileName(imageUrl);
            string cachedPath = Path.Combine(_cacheFolder, fileName);

            if (File.Exists(cachedPath) && new FileInfo(cachedPath).Length > 0)
            {
                return Task.FromResult(cachedPath);
            }

            return _inFlightDownloads.GetOrAdd(imageUrl, async (url) =>
            {
                try
                {
                    if (File.Exists(cachedPath) && new FileInfo(cachedPath).Length > 0)
                    {
                        return cachedPath;
                    }

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
                                {
                                    request.Headers.Referrer = new Uri("https://yande.re/");
                                }
                                else if (url.Contains("konachan", StringComparison.OrdinalIgnoreCase))
                                {
                                    request.Headers.Referrer = new Uri("https://konachan.net/");
                                }

                                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                                if (response.IsSuccessStatusCode)
                                {
                                    await using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 16384, true))
                                    {
                                        await response.Content.CopyToAsync(fileStream, cancellationToken);
                                    }

                                    if (File.Exists(tempFile) && new FileInfo(tempFile).Length > 0)
                                    {
                                        var fi = new FileInfo(tempFile);
                                        long len = fi.Length;
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
                    finally
                    {
                        _downloadThrottle.Release();
                    }

                    if (File.Exists(cachedPath) && new FileInfo(cachedPath).Length > 0)
                    {
                        return cachedPath;
                    }

                    return url;
                }
                catch
                {
                    return url;
                }
                finally
                {
                    _inFlightDownloads.TryRemove(imageUrl, out _);
                }
            });
        }

        public void ClearCache()
        {
            try
            {
                if (Directory.Exists(_cacheFolder))
                {
                    var files = Directory.GetFiles(_cacheFolder);
                    foreach (var file in files)
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
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
                var di = new DirectoryInfo(_cacheFolder);
                long size = di.GetFiles().Sum(fi => fi.Length);
                Interlocked.Exchange(ref _totalCacheSizeBytes, size);
                return size;
            }
            catch
            {
                return 0;
            }
        }

        private static string GetHashedFileName(string url)
        {
            Span<byte> utf8 = stackalloc byte[Encoding.UTF8.GetByteCount(url)];
            Encoding.UTF8.GetBytes(url, utf8);
            Span<byte> hash = stackalloc byte[16];
            MD5.HashData(utf8, hash);
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
