using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using Microsoft.Web.WebView2.Core;
using WallSafeWeb.Services;

namespace WallSafeWeb.Bridge
{
    /// <summary>
    /// Serves images to the page through the fake origin https://img.wallsafe:
    ///   /r?u=&lt;remote url&gt;  → downloaded once (with the right Referer) into the shared disk cache
    ///   /l?p=&lt;local path&gt;  → a local image file (downloads, applied wallpapers)
    /// This avoids booru hotlink blocks and gives the UI instant cached thumbnails.
    /// </summary>
    public sealed class ImageServer
    {
        private static readonly HashSet<string> ImageExt = new(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };

        private readonly CoreWebView2Environment _env;

        public ImageServer(CoreWebView2Environment env) => _env = env;

        public static string Remote(string? url) =>
            string.IsNullOrWhiteSpace(url) ? "" : "https://img.wallsafe/r?u=" + Uri.EscapeDataString(url);

        public static string Local(string? path) =>
            string.IsNullOrWhiteSpace(path) ? "" : "https://img.wallsafe/l?p=" + Uri.EscapeDataString(path);

        public async void Handle(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            var deferral = e.GetDeferral();
            try
            {
                var uri = new Uri(e.Request.Uri);
                var q = HttpUtility.ParseQueryString(uri.Query);
                string? file = null;

                if (uri.AbsolutePath == "/r")
                {
                    string url = q["u"] ?? "";
                    if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    {
                        string cached = await ImageCacheService.Instance.GetCachedImagePathAsync(url);
                        if (!string.IsNullOrEmpty(cached) && cached != url && File.Exists(cached)) file = cached;
                    }
                }
                else if (uri.AbsolutePath == "/l")
                {
                    string p = q["p"] ?? "";
                    if (Path.IsPathFullyQualified(p) && ImageExt.Contains(Path.GetExtension(p)) && File.Exists(p))
                        file = p;
                }

                if (file == null)
                {
                    e.Response = _env.CreateWebResourceResponse(null, 404, "Not Found", "Access-Control-Allow-Origin: *");
                    return;
                }

                var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, true);
                string headers =
                    $"Content-Type: {Mime(file)}\r\n" +
                    $"Content-Length: {stream.Length}\r\n" +
                    "Cache-Control: public, max-age=604800\r\n" +
                    "Access-Control-Allow-Origin: *";
                e.Response = _env.CreateWebResourceResponse(stream, 200, "OK", headers);
            }
            catch (Exception ex)
            {
                App.LogCrash("ImageServer", ex);
                try { e.Response = _env.CreateWebResourceResponse(null, 500, "Error", ""); } catch { }
            }
            finally
            {
                deferral.Complete();
            }
        }

        private static string Mime(string path) => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "image/jpeg"
        };
    }
}
