using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace WallSafe
{
    public class ApiClient : IDisposable
    {
        private readonly HttpClient _http = new()
        {
            Timeout = TimeSpan.FromSeconds(20),
            DefaultRequestHeaders = {
                { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36" },
                { "Accept", "application/json, text/javascript, */*; q=0.01" }
            }
        };

        private static readonly Dictionary<string, string> BuiltinBaseUrls = new()
        {
            ["yande"] = "https://yande.re",
            ["konasfw"] = "https://konachan.net",
            ["konansfw"] = "https://konachan.com"
        };

        public static string ResolveBaseUrl(string source)
        {
            if (BuiltinBaseUrls.TryGetValue(source, out var url))
                return url;

            var custom = Settings.Instance.CustomSources.Find(c => c.Id.Equals(source, StringComparison.OrdinalIgnoreCase));
            if (custom != null && !string.IsNullOrWhiteSpace(custom.BaseUrl))
                return custom.BaseUrl.TrimEnd('/');

            return BuiltinBaseUrls["konasfw"];
        }

        public static string FormatTagQuery(string tags)
        {
            if (string.IsNullOrWhiteSpace(tags)) return string.Empty;

            var parts = tags.Split(new[] { ' ', '+' }, StringSplitOptions.RemoveEmptyEntries);
            var encodedParts = new List<string>();
            foreach (var rawPart in parts)
            {
                string unescaped = Uri.UnescapeDataString(rawPart).Trim();
                if (string.IsNullOrWhiteSpace(unescaped)) continue;

                if (unescaped.Contains(':'))
                {
                    var sub = unescaped.Split(new[] { ':' }, 2);
                    string directive = Uri.EscapeDataString(sub[0]);
                    string val = Uri.EscapeDataString(sub[1]);
                    encodedParts.Add($"{directive}:{val}");
                }
                else
                {
                    encodedParts.Add(Uri.EscapeDataString(unescaped));
                }
            }
            return string.Join("+", encodedParts);
        }

        public async Task<List<PostItem>> FetchPostsAsync(
            string source, string tags, int page, int limit, CancellationToken ct)
        {
            // If "all" is requested, query all valid sources concurrently and merge
            if (source.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                var targetSources = new List<string>();
                if (Settings.Instance.SfwOnlyMode)
                {
                    targetSources.Add("konasfw");
                    foreach (var cs in Settings.Instance.CustomSources)
                    {
                        if (cs.Enabled && cs.IsSfw && !string.IsNullOrWhiteSpace(cs.BaseUrl))
                            targetSources.Add(cs.Id);
                    }
                }
                else
                {
                    targetSources.Add("yande");
                    targetSources.Add("konasfw");
                    foreach (var cs in Settings.Instance.CustomSources)
                    {
                        if (cs.Enabled && !string.IsNullOrWhiteSpace(cs.BaseUrl))
                            targetSources.Add(cs.Id);
                    }
                    targetSources.Add("konansfw");
                }

                int perSourceLimit = Math.Max(limit, 16);
                var tasks = targetSources.Select(src => FetchSingleSourcePostsAsync(src, tags, page, perSourceLimit, ct));
                var results = await Task.WhenAll(tasks);

                var merged = new List<PostItem>();
                var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var list in results)
                {
                    foreach (var post in list)
                    {
                        string key = !string.IsNullOrEmpty(post.PreviewUrl) ? post.PreviewUrl : post.BestImageUrl;
                        if (!string.IsNullOrEmpty(key) && seenUrls.Add(key))
                        {
                            merged.Add(post);
                        }
                    }
                }

                // If sorting by score, preserve high score order
                return merged.OrderByDescending(p => p.Score).Take(limit).ToList();
            }

            return await FetchSingleSourcePostsAsync(source, tags, page, limit, ct);
        }

        private async Task<List<PostItem>> FetchSingleSourcePostsAsync(
            string source, string tags, int page, int limit, CancellationToken ct)
        {
            string baseUrl = ResolveBaseUrl(source);
            string formattedTags = FormatTagQuery(tags);
            var url = string.IsNullOrEmpty(formattedTags)
                ? $"{baseUrl}/post.json?page={page}&limit={limit}"
                : $"{baseUrl}/post.json?tags={formattedTags}&page={page}&limit={limit}";

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Referrer = new Uri(baseUrl + "/");
                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!resp.IsSuccessStatusCode) return new List<PostItem>();

                var json = await resp.Content.ReadAsStringAsync(ct);
                var arr = JArray.Parse(json);

                var result = new List<PostItem>();
                foreach (var token in arr)
                {
                    if (token is not JObject obj) continue;

                    int id = obj["id"]?.Value<int>() ?? 0;
                    string previewUrl = obj["preview_url"]?.Value<string>() ?? "";
                    string fileUrl = obj["file_url"]?.Value<string>() ?? "";
                    string sampleUrl = obj["sample_url"]?.Value<string>() ?? fileUrl;
                    string rating = obj["rating"]?.Value<string>() ?? "s";
                    int score = obj["score"]?.Value<int>() ?? 0;
                    int width = obj["width"]?.Value<int>() ?? 0;
                    int height = obj["height"]?.Value<int>() ?? 0;
                    string tagStr = obj["tags"]?.Value<string>() ?? "";
                    string author = obj["author"]?.Value<string>() ?? "";

                    // Fix protocol-relative URLs
                    if (!string.IsNullOrEmpty(previewUrl) && previewUrl.StartsWith("//"))
                        previewUrl = "https:" + previewUrl;
                    if (!string.IsNullOrEmpty(sampleUrl) && sampleUrl.StartsWith("//"))
                        sampleUrl = "https:" + sampleUrl;
                    if (!string.IsNullOrEmpty(fileUrl) && fileUrl.StartsWith("//"))
                        fileUrl = "https:" + fileUrl;

                    var post = new PostItem
                    {
                        Id = id,
                        Source = source,
                        PreviewUrl = previewUrl,
                        SampleUrl = sampleUrl,
                        FileUrl = fileUrl,
                        Width = width,
                        Height = height,
                        Rating = rating.ToLowerInvariant(),
                        Score = score,
                        Tags = tagStr,
                        Author = author,
                        SourceUrl = $"{baseUrl}/post/show/{id}",
                        IsFavorite = FavoritesManager.Instance.IsFavorite(id, source)
                    };

                    if (DownloadsManager.Instance.IsDownloaded(id, source, out var localPath))
                    {
                        post.IsDownloaded = true;
                        post.LocalPath = localPath;
                    }

                    result.Add(post);
                }
                return result;
            }
            catch
            {
                return new List<PostItem>();
            }
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, List<TagSuggestion>> _suggestionCache = new(StringComparer.OrdinalIgnoreCase);

        public static readonly List<(string Tag, string Title, int Type)> BuiltinPopularTags = new()
        {
            ("wuthering_waves", "Wuthering Waves", 3),
            ("genshin_impact", "Genshin Impact", 3),
            ("honkai:_star_rail", "Honkai: Star Rail", 3),
            ("zenless_zone_zero", "Zenless Zone Zero", 3),
            ("blue_archive", "Blue Archive", 3),
            ("lycoris_recoil", "Lycoris Recoil", 3),
            ("sousou_no_frieren", "Frieren", 3),
            ("hatsune_miku", "Hatsune Miku", 4),
            ("nishikigi_chisato", "Nishikigi Chisato", 4),
            ("inoue_takina", "Inoue Takina", 4),
            ("arashi_chisato", "Arashi Chisato", 4),
            ("shirasagi_chisato", "Shirasagi Chisato", 4),
            ("chisa", "Chisa", 4),
            ("frieren", "Frieren", 4),
            ("fern", "Fern", 4),
            ("himmel", "Himmel", 4),
            ("raiden_shogun", "Raiden Shogun", 4),
            ("furina", "Furina", 4),
            ("hu_tao", "Hu Tao", 4),
            ("nahida", "Nahida", 4),
            ("firefly_(honkai:_star_rail)", "Firefly", 4),
            ("acheron_(honkai:_star_rail)", "Acheron", 4),
            ("kafka_(honkai:_star_rail)", "Kafka", 4),
            ("ellen_joe", "Ellen Joe", 4),
            ("zhu_yuan", "Zhu Yuan", 4),
            ("jane_doe_(zenless_zone_zero)", "Jane Doe", 4),
            ("miyabi_(zenless_zone_zero)", "Hoshimi Miyabi", 4),
            ("rover_(wuthering_waves)", "Rover", 4),
            ("yinlin_(wuthering_waves)", "Yinlin", 4),
            ("changli_(wuthering_waves)", "Changli", 4),
            ("jinhsi_(wuthering_waves)", "Jinhsi", 4),
            ("shorekeeper_(wuthering_waves)", "Shorekeeper", 4),
            ("camellya_(wuthering_waves)", "Camellya", 4),
            ("scenery", "Scenery", 0),
            ("cyberpunk", "Cyberpunk", 0),
            ("night", "Night Sky", 0),
            ("clouds", "Clouds / Sky", 0),
            ("landscape", "Landscape", 0)
        };

        public static readonly Dictionary<string, (string Tag, string Title)> TagAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["wuwa"] = ("wuthering_waves", "Wuthering Waves"),
            ["wuthering"] = ("wuthering_waves", "Wuthering Waves"),
            ["genshin"] = ("genshin_impact", "Genshin Impact"),
            ["gi"] = ("genshin_impact", "Genshin Impact"),
            ["hsr"] = ("honkai:_star_rail", "Honkai: Star Rail"),
            ["star rail"] = ("honkai:_star_rail", "Honkai: Star Rail"),
            ["starrail"] = ("honkai:_star_rail", "Honkai: Star Rail"),
            ["hi3"] = ("honkai_impact_3rd", "Honkai Impact 3rd"),
            ["honkai"] = ("honkai_impact_3rd", "Honkai Impact 3rd"),
            ["zzz"] = ("zenless_zone_zero", "Zenless Zone Zero"),
            ["zenless"] = ("zenless_zone_zero", "Zenless Zone Zero"),
            ["fgo"] = ("fate/grand_order", "Fate/Grand Order"),
            ["fate"] = ("fate/grand_order", "Fate/Grand Order"),
            ["ba"] = ("blue_archive", "Blue Archive"),
            ["blue archive"] = ("blue_archive", "Blue Archive"),
            ["al"] = ("azur_lane", "Azur Lane"),
            ["azur lane"] = ("azur_lane", "Azur Lane"),
            ["ak"] = ("arknights", "Arknights"),
            ["arknights"] = ("arknights", "Arknights"),
            ["miku"] = ("hatsune_miku", "Hatsune Miku"),
            ["hatsune miku"] = ("hatsune_miku", "Hatsune Miku"),
            ["csm"] = ("chainsaw_man", "Chainsaw Man"),
            ["chainsaw man"] = ("chainsaw_man", "Chainsaw Man"),
            ["aot"] = ("shingeki_no_kyojin", "Attack on Titan"),
            ["jjk"] = ("jujutsu_kaisen", "Jujutsu Kaisen"),
            ["jujutsu"] = ("jujutsu_kaisen", "Jujutsu Kaisen"),
            ["rezero"] = ("re:zero_kara_hajimeru_isekai_seikatsu", "Re:Zero"),
            ["re zero"] = ("re:zero_kara_hajimeru_isekai_seikatsu", "Re:Zero"),
            ["sao"] = ("sword_art_online", "Sword Art Online"),
            ["bocchi"] = ("bocchi_the_rock!", "Bocchi the Rock!"),
            ["holo"] = ("hololive", "Hololive"),
            ["hololive"] = ("hololive", "Hololive"),
            ["dandadan"] = ("dandadan", "Dandadan"),
            ["frieren"] = ("sousou_no_frieren", "Frieren"),
            ["lycoris"] = ("lycoris_recoil", "Lycoris Recoil"),
            ["nier"] = ("nier_(series)", "NieR"),
            ["eva"] = ("neon_genesis_evangelion", "Evangelion"),
            ["evangelion"] = ("neon_genesis_evangelion", "Evangelion"),
            ["madoka"] = ("mahou_shoujo_madoka_magica", "Madoka Magica"),
            ["touhou"] = ("touhou", "Touhou"),
            ["2hu"] = ("touhou", "Touhou"),
            ["nikke"] = ("goddess_of_victory:_nikke", "Nikke")
        };

        public static string ResolveTag(string term)
        {
            term = term.Trim();
            if (TagAliases.TryGetValue(term, out var alias))
                return alias.Tag;
            return term.Replace(' ', '_');
        }

        public List<TagSuggestion> GetInstantLocalSuggestions(string name)
        {
            var rawQuery = name.Trim();
            if (string.IsNullOrEmpty(rawQuery))
                return new List<TagSuggestion>();

            // Check if already in suggestion cache
            if (_suggestionCache.TryGetValue(rawQuery, out var cached))
                return new List<TagSuggestion>(cached);

            var result = new List<TagSuggestion>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var normalized = rawQuery.Replace(' ', '_');

            // 1. Check Aliases (e.g. "wuwa" -> "wuthering_waves")
            foreach (var kvp in TagAliases)
            {
                if (kvp.Key.Equals(rawQuery, StringComparison.OrdinalIgnoreCase) ||
                    kvp.Key.StartsWith(rawQuery, StringComparison.OrdinalIgnoreCase) ||
                    kvp.Value.Tag.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                {
                    if (seen.Add(kvp.Value.Tag))
                    {
                        result.Add(new TagSuggestion
                        {
                            Name = kvp.Value.Tag,
                            Title = kvp.Value.Title,
                            Type = 3,
                            Count = 0
                        });
                    }
                }
            }

            // 2. Check Builtin Popular Tags (instant characters, anime, etc.)
            foreach (var item in BuiltinPopularTags)
            {
                if (item.Tag.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(item.Title) && item.Title.Contains(rawQuery, StringComparison.OrdinalIgnoreCase)))
                {
                    if (seen.Add(item.Tag))
                    {
                        result.Add(new TagSuggestion
                        {
                            Name = item.Tag,
                            Title = item.Title,
                            Type = item.Type,
                            Count = 0
                        });
                    }
                }
            }

            return result;
        }

        public async Task<List<TagSuggestion>> FetchTagSuggestionsAsync(
            string source, string name, CancellationToken ct)
        {
            var rawQuery = name.Trim();
            if (string.IsNullOrEmpty(rawQuery))
                return new List<TagSuggestion>();

            var cacheKey = $"{source}:{rawQuery}";
            if (_suggestionCache.TryGetValue(cacheKey, out var cachedResults))
                return cachedResults;

            var result = GetInstantLocalSuggestions(rawQuery);
            var seen = new HashSet<string>(result.ConvertAll(s => s.Name), StringComparer.OrdinalIgnoreCase);

            string baseUrl = ResolveBaseUrl(source);

            string apiQuery = TagAliases.TryGetValue(rawQuery, out var directAlias)
                ? directAlias.Tag
                : rawQuery.Replace(' ', '_');

            var url = $"{baseUrl}/tag.json?name={Uri.EscapeDataString(apiQuery)}&order=count&limit=14";

            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2.5));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

                var json = await _http.GetStringAsync(url, linkedCts.Token);
                var arr = JArray.Parse(json);
                foreach (var t in arr)
                {
                    var n = t["name"]?.Value<string>();
                    var count = t["count"]?.Value<int>() ?? 0;
                    var type = t["type"]?.Value<int>() ?? 0;

                    if (!string.IsNullOrEmpty(n) && seen.Add(n))
                    {
                        result.Add(new TagSuggestion
                        {
                            Name = n,
                            Count = count,
                            Type = type
                        });
                    }
                }
            }
            catch { }

            _suggestionCache[cacheKey] = result;
            _suggestionCache[rawQuery] = result;
            return result;
        }

        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime ExpireAt, List<(string Tag, int Count)> Tags)> _sourceSeriesCache = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _sourcePreviewCache = new();

        public async Task<List<(string Tag, int Count)>> GetPopularSeriesFromSourceAsync(string sourceKey, int limit = 25, CancellationToken ct = default)
        {
            string targetSource = sourceKey.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? (Settings.Instance.SfwOnlyMode ? "konasfw" : "yande")
                : sourceKey;

            string cacheKey = targetSource;
            if (_sourceSeriesCache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow < cached.ExpireAt)
            {
                return cached.Tags;
            }

            var results = new List<(string Tag, int Count)>();
            var ignoredTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "original", "all_male", "comic", "translation_request", "spoiler", "sound_warning", "bad_id"
            };

            try
            {
                string baseUrl = ResolveBaseUrl(targetSource).TrimEnd('/');
                string url = $"{baseUrl}/tag.json?order=count&type=3&limit={limit * 2}";

                var resp = await _http.GetStringAsync(url, ct);
                var array = JArray.Parse(resp);

                foreach (var item in array)
                {
                    string name = item["name"]?.ToString() ?? "";
                    int count = item["count"]?.ToObject<int>() ?? (item["post_count"]?.ToObject<int>() ?? 0);

                    if (string.IsNullOrWhiteSpace(name) || ignoredTags.Contains(name) || count <= 0)
                        continue;

                    results.Add((name, count));
                    if (results.Count >= limit) break;
                }

                if (results.Count > 0)
                {
                    _sourceSeriesCache[cacheKey] = (DateTime.UtcNow.AddMinutes(30), results);
                }
            }
            catch { }

            return results;
        }

        public async Task<string?> GetSourceCategoryPreviewUrlAsync(string sourceKey, string tag, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;

            string targetSource = sourceKey.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? (Settings.Instance.SfwOnlyMode ? "konasfw" : "yande")
                : sourceKey;

            string cacheKey = $"{targetSource}:{tag.Trim().ToLowerInvariant()}";
            if (_sourcePreviewCache.TryGetValue(cacheKey, out var cachedUrl))
            {
                return cachedUrl;
            }

            try
            {
                string encoded = Uri.EscapeDataString(tag.Trim());
                string baseUrl = ResolveBaseUrl(targetSource).TrimEnd('/');
                string url = $"{baseUrl}/post.json?limit=1&tags={encoded}+order:score";
                var resp = await _http.GetStringAsync(url, ct);
                var array = JArray.Parse(resp);
                if (array.Count > 0)
                {
                    var p = array[0];
                    string? preview = p["sample_url"]?.ToString() ?? p["preview_url"]?.ToString() ?? p["file_url"]?.ToString();
                    if (!string.IsNullOrEmpty(preview))
                    {
                        _sourcePreviewCache[cacheKey] = preview;
                        return preview;
                    }
                }
            }
            catch { }

            // Fallback to generic preview
            var generic = await GetCategoryPreviewUrlAsync(tag, ct);
            if (!string.IsNullOrEmpty(generic))
            {
                _sourcePreviewCache[cacheKey] = generic;
            }
            return generic;
        }

        public async Task<string?> GetCategoryPreviewUrlAsync(string tag, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;

            try
            {
                string encoded = Uri.EscapeDataString(tag.Trim());
                string url = $"https://konachan.net/post.json?limit=1&tags={encoded}+order:score";
                var resp = await _http.GetStringAsync(url, ct);
                var array = JArray.Parse(resp);
                if (array.Count > 0)
                {
                    var p = array[0];
                    return p["sample_url"]?.ToString() ?? p["preview_url"]?.ToString();
                }
            }
            catch { }

            try
            {
                // Fallback to yande.re
                string encoded = Uri.EscapeDataString(tag.Trim());
                string url = $"https://yande.re/post.json?limit=1&tags={encoded}+order:score";
                var resp = await _http.GetStringAsync(url, ct);
                var array = JArray.Parse(resp);
                if (array.Count > 0)
                {
                    var p = array[0];
                    return p["sample_url"]?.ToString() ?? p["preview_url"]?.ToString();
                }
            }
            catch { }

            return null;
        }

        public void Dispose() => _http.Dispose();
    }
}
