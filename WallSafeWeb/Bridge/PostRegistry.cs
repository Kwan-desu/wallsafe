using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WallSafeWeb.Services;

namespace WallSafeWeb.Bridge
{
    /// <summary>
    /// JSON shape of a wallpaper sent to the UI. Posts are registered by key so the UI
    /// only has to send the key back for actions (favorite/apply/download…).
    /// </summary>
    public static class PostRegistry
    {
        private static readonly ConcurrentDictionary<string, PostItem> _posts = new(StringComparer.OrdinalIgnoreCase);

        public static string KeyOf(PostItem p) =>
            p.Id > 0 ? $"{p.Source}:{p.Id}" : "local:" + StableHash(p.LocalPath ?? p.BestImageUrl);

        private static string StableHash(string s) =>
            Convert.ToHexString(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(s ?? "")))[..12];

        public static PostItem? Find(string? key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_posts.TryGetValue(key, out var p)) return p;

            // Fall back to persisted collections (e.g. after a UI reload).
            var all = FavoritesManager.Instance.GetAllFavorites()
                .Concat(DownloadsManager.Instance.GetAllDownloads())
                .Concat(HistoryManager.Instance.GetAllHistory());
            foreach (var item in all)
                if (KeyOf(item).Equals(key, StringComparison.OrdinalIgnoreCase)) { _posts[key] = item; return item; }
            return null;
        }

        public static object Dto(PostItem p)
        {
            string key = KeyOf(p);
            _posts[key] = p;

            // Keep live flags in sync with the managers.
            if (p.Id > 0)
            {
                p.IsFavorite = FavoritesManager.Instance.IsFavorite(p.Id, p.Source);
                if (DownloadsManager.Instance.IsDownloaded(p.Id, p.Source, out var lp)) { p.IsDownloaded = true; p.LocalPath = lp; }
            }

            bool hasLocal = !string.IsNullOrEmpty(p.LocalPath) && File.Exists(p.LocalPath);
            string thumb = !string.IsNullOrEmpty(p.PreviewUrl) ? ImageServer.Remote(p.PreviewUrl)
                         : hasLocal ? ImageServer.Local(p.LocalPath) : ImageServer.Remote(p.BestImageUrl);
            string sample = !string.IsNullOrEmpty(p.SampleUrl) ? ImageServer.Remote(p.SampleUrl)
                          : hasLocal ? ImageServer.Local(p.LocalPath) : ImageServer.Remote(p.BestImageUrl);

            return new
            {
                key,
                id = p.Id,
                source = p.Source,
                sourceName = SourceName(p.Source),
                thumb,
                sample,
                local = hasLocal ? ImageServer.Local(p.LocalPath) : null,
                localPath = hasLocal ? p.LocalPath : null,
                width = p.Width,
                height = p.Height,
                aspect = p.AspectRatioText,
                rating = p.Rating,
                score = p.Score,
                tags = p.Tags,
                author = p.Author,
                postUrl = p.SourceUrl,
                isFavorite = p.IsFavorite,
                isDownloaded = p.IsDownloaded,
                collection = p.Collection,
                appliedAt = p.AppliedAt,
                sensitive = IsSensitive(p)
            };
        }

        public static List<object> Dtos(IEnumerable<PostItem> items) => items.Select(Dto).ToList();

        public static bool IsSensitive(PostItem p)
        {
            if (!string.Equals(p.Rating, "s", StringComparison.OrdinalIgnoreCase)) return true;
            var bl = Settings.Instance.TagBlacklist;
            if (string.IsNullOrWhiteSpace(bl) || string.IsNullOrWhiteSpace(p.Tags)) return false;
            var tags = new HashSet<string>(p.Tags.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
            foreach (var t in bl.Split(new[] { ' ', ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                if (tags.Contains(t.Trim())) return true;
            return false;
        }

        public static string SourceName(string src) => src switch
        {
            "konasfw" => "Konachan",
            "konansfw" => "Konachan.com",
            "yande" => "yande.re",
            _ => Settings.Instance.CustomSources.FirstOrDefault(c => c.Id == src)?.Name ?? src
        };
    }
}
