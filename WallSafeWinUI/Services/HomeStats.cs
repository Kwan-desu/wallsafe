using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WallSafeWinUI.Services
{
    /// <summary>Aggregated stats for the Home dashboard header.</summary>
    public class HomeStats
    {
        public int Favorites { get; set; }
        public int Downloads { get; set; }
        public int Applied { get; set; }
        public int Collections { get; set; }
        public int PanicTriggers { get; set; }

        public string TopTagDisplay { get; set; } = "—";
        public string TopSourceDisplay { get; set; } = "—";
        public string FavoriteCoverUrl { get; set; } = "";

        public static HomeStats Compute()
        {
            var s = new HomeStats();
            try
            {
                var favs = FavoritesManager.Instance.GetAllFavorites();
                var dls = DownloadsManager.Instance.GetAllDownloads();
                var history = HistoryManager.Instance.GetAllHistory();

                s.Favorites = favs.Count;
                s.Downloads = dls.Count;
                s.Applied = history.Count;
                s.Collections = FavoritesManager.Instance.GetCollections().Count;
                s.PanicTriggers = Settings.Instance.PanicTriggerCount;
                s.FavoriteCoverUrl = favs.FirstOrDefault()?.PreviewUrl ?? "";

                // Most-favourited franchise/character: tally meaningful tags across favorites.
                var ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "highres","absurdres","original","solo","long_hair","short_hair","looking_at_viewer",
                    "breasts","blush","smile","open_mouth","sitting","standing","1girl","2girls","multiple_girls"
                };
                var tagCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in favs)
                {
                    if (string.IsNullOrWhiteSpace(f.Tags)) continue;
                    foreach (var t in f.Tags.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (ignore.Contains(t) || t.Length < 3) continue;
                        tagCounts[t] = tagCounts.TryGetValue(t, out var c) ? c + 1 : 1;
                    }
                }
                var top = tagCounts.OrderByDescending(kv => kv.Value).FirstOrDefault();
                if (!string.IsNullOrEmpty(top.Key) && top.Value > 1)
                    s.TopTagDisplay = Prettify(top.Key);

                // Top source among favorites.
                var srcCounts = favs.GroupBy(f => f.Source)
                    .Select(g => (Src: g.Key, Count: g.Count()))
                    .OrderByDescending(x => x.Count).FirstOrDefault();
                if (!string.IsNullOrEmpty(srcCounts.Src))
                    s.TopSourceDisplay = SourceName(srcCounts.Src);
            }
            catch { }
            return s;
        }

        private static string Prettify(string tag)
        {
            var cleaned = tag.Replace('_', ' ').Trim();
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleaned);
        }

        private static string SourceName(string src) => src switch
        {
            "konasfw" => "Konachan",
            "konansfw" => "Konachan.com",
            "yande" => "yande.re",
            _ => src
        };
    }
}
