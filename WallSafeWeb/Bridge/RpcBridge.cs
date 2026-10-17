using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WallSafeWeb.Services;

namespace WallSafeWeb.Bridge
{
    /// <summary>
    /// JSON-RPC style bridge between the web UI and the native services.
    /// UI → host:  { id, method, params }      host → UI: { id, ok, result | error }
    /// host → UI events: { event, data }
    /// </summary>
    public sealed class RpcBridge
    {
        private readonly MainForm _form;
        private readonly CoreWebView2 _core;
        private readonly ApiClient _api = new();
        private List<PostItem>? _historyUndo;

        // Settings the UI may write directly (side effects handled in ApplySideEffects).
        private static readonly HashSet<string> Writable = new(StringComparer.Ordinal)
        {
            "UserName","ShowGreeting","FirstRunCompleted","Theme","BackdropMaterial","WindowMicaEnabled","CardDensity",
            "PanicHideApp","PanicMinimizeAllWindows","PanicMuteAudio","PanicRestoreToggle",
            "StartMinimized","AutoStartWithWindows",
            "WifiAutoWallpaperEnabled","WifiTriggerSsid","WifiRestoreOnDisconnect","SafeWallpaperOnShutdown",
            "LocationAutoWallpaperEnabled","LocationLatitude","LocationLongitude","LocationRadiusMeters","LocationRestoreOnLeave",
            "DiscretionBlur","BlurStrength","TagBlacklist",
            "ShowHomeStats","ShowHomeHero","ShowHomeQuickControls","ShowHomeTrending","ShowHomeFranchises","ShowHomeQuickShelf",
            "TargetMonitor","AccentColor"
        };

        public RpcBridge(MainForm form, CoreWebView2 core)
        {
            _form = form;
            _core = core;

            var wm = WallpaperManager.Instance;
            wm.WallpaperApplied += path => Emit("wallpaperApplied", new { path });
            wm.SafeWallpaperTriggered += () => Emit("panicChanged", new { active = true });
            wm.SafeWallpaperRestored += () => Emit("panicChanged", new { active = false });
            FavoritesManager.Instance.FavoritesChanged += () => Emit("favoritesChanged", null);
            DownloadsManager.Instance.DownloadsChanged += () => Emit("downloadsChanged", null);
            HistoryManager.Instance.HistoryChanged += () => Emit("historyChanged", null);
            WifiWatcher.Instance.StatusChanged += (net, active, matched) => Emit("wifiStatus", WifiStatus(net, active, matched));
        }

        public void Emit(string name, object? data)
        {
            string json = JsonConvert.SerializeObject(new { @event = name, data });
            UiDispatch.Post(() => { try { _core.PostWebMessageAsJson(json); } catch { } });
        }

        public async void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            JObject msg;
            try { msg = JObject.Parse(e.WebMessageAsJson); }
            catch { return; }

            var id = msg["id"];
            string method = msg["method"]?.ToString() ?? "";
            var p = msg["params"] as JObject ?? new JObject();
            object reply;
            try
            {
                var result = await Dispatch(method, p);
                reply = new { id, ok = true, result };
            }
            catch (Exception ex)
            {
                App.LogCrash("Rpc:" + method, ex);
                reply = new { id, ok = false, error = ex.Message };
            }
            try { _core.PostWebMessageAsJson(JsonConvert.SerializeObject(reply)); } catch { }
        }

        private static string S(JObject p, string k, string def = "") => p[k]?.Type == JTokenType.Null ? def : p[k]?.ToString() ?? def;
        private static int I(JObject p, string k, int def = 0) => p[k] != null && p[k]!.Type != JTokenType.Null ? p[k]!.Value<int>() : def;
        private static bool B(JObject p, string k, bool def = false) => p[k] != null && p[k]!.Type != JTokenType.Null ? p[k]!.Value<bool>() : def;

        private static PostItem Post(JObject p) =>
            PostRegistry.Find(S(p, "key")) ?? throw new InvalidOperationException("Wallpaper not found. Try refreshing.");

        private async Task<object?> Dispatch(string method, JObject p)
        {
            var s = Settings.Instance;
            switch (method)
            {
                // ── app / window ──────────────────────────────
                case "app.init": return InitState();
                case "window.minimize": _form.WindowState = FormWindowState.Minimized; return null;
                case "window.toggleMaximize": _form.ToggleMaximize(); return new { maximized = _form.WindowState == FormWindowState.Maximized };
                case "window.close": _form.EnterBackground(); return null;
                case "app.exit": App.ExitApp(); return null;
                case "shell.open": MainForm.OpenExternal(S(p, "url")); return null;

                // ── settings ──────────────────────────────────
                case "settings.get": return SettingsSnapshot();
                case "settings.set": return SetSetting(S(p, "key"), p["value"]);
                case "rating.set":
                {
                    var mode = (ContentRatingMode)Math.Clamp(I(p, "mode"), 0, 2);
                    if (s.RatingMode != mode)
                    {
                        s.RatingMode = mode;
                        s.Save();
                        AppEvents.RaiseRatingModeChanged(mode);
                    }
                    return new { mode = (int)s.RatingMode, profile = s.GetFilterProfileForMode(mode) };
                }
                case "hotkey.set":
                {
                    s.HotkeyModifiers = I(p, "mods") & 0xF;
                    s.HotkeyKey = I(p, "vk");
                    s.Save();
                    App.ReRegisterHotkey();
                    return new { display = s.GetHotkeyDisplayName() };
                }

                // ── browsing ──────────────────────────────────
                case "posts.fetch":
                {
                    string source = S(p, "source", "all");
                    string sort = S(p, "sort", "order:score");
                    string tags = (S(p, "tags") + " " + sort).Trim();
                    int page = Math.Max(1, I(p, "page", 1));
                    int limit = Math.Clamp(I(p, "limit", 30), 1, 100);
                    int minWidth = I(p, "minWidth");
                    bool wallpaperOnly = B(p, "wallpaperOnly");

                    if (B(p, "remember", true))
                    {
                        var profile = s.GetFilterProfileForMode(s.RatingMode);
                        profile.Source = source;
                        profile.SortTag = sort;
                        s.Save();
                    }

                    var list = await _api.FetchPostsAsync(source, tags, page, limit, CancellationToken.None);
                    if (minWidth > 0) list = list.Where(x => x.Width >= minWidth).ToList();
                    if (wallpaperOnly) list = list.Where(x => x.Width <= 0 || ((double)x.Width / Math.Max(1, x.Height) >= 1.5 && x.Width >= 1280)).ToList();
                    return PostRegistry.Dtos(list);
                }
                case "tags.suggest":
                {
                    string q = S(p, "query");
                    var local = _api.GetInstantLocalSuggestions(q);
                    if (!B(p, "remote", true)) return Suggestions(local);
                    var remote = await _api.FetchTagSuggestionsAsync(S(p, "source", "konasfw") is "all" ? "konasfw" : S(p, "source", "konasfw"), q, CancellationToken.None);
                    return Suggestions(remote.Count > 0 ? remote : local);
                }
                case "tags.resolve": return ApiClient.ResolveTag(S(p, "query"));
                case "home.franchises":
                {
                    string key = s.RatingMode == ContentRatingMode.SfwOnly ? "konasfw" : "all";
                    var raw = await _api.GetPopularSeriesFromSourceAsync(key, I(p, "limit", 16));
                    var ti = CultureInfo.CurrentCulture.TextInfo;
                    return raw.Select(r => new
                    {
                        tag = r.Tag,
                        name = ti.ToTitleCase(r.Tag.Replace('_', ' ')),
                        count = r.Count,
                        type = IsGameTag(r.Tag) ? "Gaming" : "Anime"
                    }).ToList();
                }
                case "franchise.preview":
                {
                    string key = s.RatingMode == ContentRatingMode.SfwOnly ? "konasfw" : "all";
                    var url = await _api.GetSourceCategoryPreviewUrlAsync(key, S(p, "tag"));
                    if (!string.IsNullOrEmpty(url) && url.StartsWith("//")) url = "https:" + url;
                    return string.IsNullOrEmpty(url) ? null : ImageServer.Remote(url);
                }
                case "home.stats":
                {
                    var st = HomeStats.Compute();
                    return new
                    {
                        favorites = st.Favorites, downloads = st.Downloads, applied = st.Applied,
                        collections = st.Collections, panic = st.PanicTriggers,
                        topTag = st.Favorites > 0 ? st.TopTagDisplay : "—", topSource = st.TopSourceDisplay
                    };
                }

                // ── post actions ──────────────────────────────
                case "post.favorite":
                {
                    var post = Post(p);
                    bool now = FavoritesManager.Instance.ToggleFavorite(post);
                    return new { isFavorite = now };
                }
                case "post.apply":
                {
                    var post = Post(p);
                    int monitor = p["monitor"] != null ? I(p, "monitor", -1) : s.TargetMonitor;
                    await WallpaperManager.Instance.ApplyWallpaperAsync(post, monitor);
                    return null;
                }
                case "post.lockscreen":
                {
                    var post = Post(p);
                    string src = !string.IsNullOrEmpty(post.LocalPath) && File.Exists(post.LocalPath) ? post.LocalPath : post.FullDownloadUrl;
                    return await WallpaperManager.Instance.SetLockScreenAsync(src);
                }
                case "post.download":
                {
                    var post = Post(p);
                    string key = PostRegistry.KeyOf(post);
                    double last = -1;
                    var progress = new Progress<double>(v =>
                    {
                        if (v - last < 0.03 && v < 1) return;
                        last = v;
                        Emit("downloadProgress", new { key, progress = v });
                    });
                    string path = await DownloadsManager.Instance.DownloadPostAsync(post, progress);
                    return PostRegistry.Dto(post);
                }
                case "post.reveal":
                {
                    var post = Post(p);
                    if (!string.IsNullOrEmpty(post.LocalPath) && File.Exists(post.LocalPath))
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{post.LocalPath}\"");
                    return null;
                }
                case "post.get": return PostRegistry.Dto(Post(p));

                // ── favorites / collections ───────────────────
                case "favorites.list":
                {
                    string c = S(p, "collection");
                    var list = c == "__unsorted" ? FavoritesManager.Instance.GetUncategorizedFavorites()
                             : FavoritesManager.Instance.GetFavoritesByCollection(c);
                    return PostRegistry.Dtos(list);
                }
                case "favorites.collections":
                {
                    var fm = FavoritesManager.Instance;
                    return new
                    {
                        all = fm.GetAllFavorites().Count,
                        unsorted = fm.GetUncategorizedFavorites().Count,
                        items = fm.GetCollections().Select(n => new
                        {
                            name = n,
                            count = fm.GetFavoritesByCollection(n).Count,
                            cover = ImageServer.Remote(fm.GetCollectionCover(n))
                        }).ToList()
                    };
                }
                case "collections.create": return FavoritesManager.Instance.CreateCollection(S(p, "name"));
                case "collections.rename": return FavoritesManager.Instance.RenameCollection(S(p, "name"), S(p, "newName"));
                case "collections.delete": FavoritesManager.Instance.DeleteCollection(S(p, "name")); return null;
                case "collections.assign":
                    FavoritesManager.Instance.AssignToCollection(Post(p), S(p, "collection"));
                    return null;

                // ── downloads / history ───────────────────────
                case "downloads.list": return PostRegistry.Dtos(DownloadsManager.Instance.GetAllDownloads());
                case "downloads.delete": DownloadsManager.Instance.DeleteDownload(Post(p)); return null;
                case "downloads.openFolder":
                    System.Diagnostics.Process.Start("explorer.exe", $"\"{DownloadsManager.Instance.DownloadFolder}\"");
                    return null;
                case "history.list": return PostRegistry.Dtos(HistoryManager.Instance.GetAllHistory());
                case "history.clear":
                    _historyUndo = HistoryManager.Instance.GetAllHistory();
                    HistoryManager.Instance.ClearHistory();
                    return null;
                case "history.undoClear":
                    if (_historyUndo != null) { HistoryManager.Instance.RestoreHistory(_historyUndo); _historyUndo = null; }
                    return null;

                // ── panic / slideshow ─────────────────────────
                case "panic.trigger": App.TriggerPanic(); return new { active = WallpaperManager.Instance.IsPanicActive };
                case "slideshow.set": App.SetSlideshow(B(p, "enabled")); return SlideshowState();
                case "slideshow.next": await WallpaperManager.Instance.NextSlideshowWallpaperAsync(); return null;
                case "slideshow.config":
                {
                    if (p["source"] != null)
                    {
                        s.SlideshowSource = S(p, "source", "favorites");
                        WallpaperManager.Instance.ResetSlideshowQueue();
                    }
                    if (p["seconds"] != null)
                    {
                        int sec = Math.Max(10, I(p, "seconds", 900));
                        s.SlideshowIntervalSeconds = sec;
                        s.SlideshowIntervalMinutes = Math.Max(1, (int)Math.Round(sec / 60.0));
                    }
                    s.Save();
                    if (s.SlideshowEnabled) WallpaperManager.Instance.StartSlideshowFromSettings(p["source"] != null);
                    return SlideshowState();
                }
                case "slideshow.state": return SlideshowState();

                // ── system helpers ────────────────────────────
                case "dialog.pickSafeWallpaper":
                {
                    using var dlg = new OpenFileDialog { Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp", Title = "Choose a safe wallpaper" };
                    if (dlg.ShowDialog(_form) != DialogResult.OK) return null;
                    WallpaperManager.Instance.SetSafeWallpaper(dlg.FileName);
                    return dlg.FileName;
                }
                case "safe.reset":
                {
                    var def = Settings.DetectDefaultWin11Wallpaper();
                    WallpaperManager.Instance.SetSafeWallpaper(def);
                    return def;
                }
                case "dialog.pickDownloadFolder":
                {
                    using var dlg = new FolderBrowserDialog { Description = "Choose where WallSafe saves downloads", UseDescriptionForTitle = true,
                        SelectedPath = DownloadsManager.Instance.DownloadFolder };
                    if (dlg.ShowDialog(_form) != DialogResult.OK) return null;
                    DownloadsManager.Instance.DownloadFolder = dlg.SelectedPath;
                    return dlg.SelectedPath;
                }
                case "wifi.status":
                    return WifiStatus(WifiWatcher.GetPrimaryConnectedNetworkName(), WifiWatcher.Instance.IsTriggerActive, null);
                case "location.current":
                {
                    var pos = await LocationWatcher.GetCurrentAsync();
                    if (pos is not { } c) return null;
                    s.LocationLatitude = c.Lat; s.LocationLongitude = c.Lon; s.Save();
                    return new { lat = c.Lat, lon = c.Lon };
                }
                case "cache.size": return ImageCacheService.Instance.GetCacheSizeInBytes();
                case "cache.clear": ImageCacheService.Instance.ClearCache(); return 0;
                case "monitors.list":
                    return WallpaperManager.Instance.GetConnectedMonitors()
                        .Select(m => new { index = m.Index, name = m.DisplayName, width = m.Width, height = m.Height, primary = m.IsPrimary }).ToList();
                case "sources.add":
                {
                    string url = S(p, "baseUrl").Trim().TrimEnd('/');
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != "https")
                        throw new ArgumentException("Enter a valid https:// board URL.");
                    var cs = new CustomSourceConfig
                    {
                        Id = "custom_" + Guid.NewGuid().ToString("N")[..8],
                        Name = string.IsNullOrWhiteSpace(S(p, "name")) ? u.Host : S(p, "name").Trim(),
                        BaseUrl = url,
                        IsSfw = B(p, "isSfw", true),
                        Enabled = true
                    };
                    s.CustomSources.Add(cs);
                    s.Save();
                    return Sources();
                }
                case "sources.remove":
                    s.CustomSources.RemoveAll(c => c.Id == S(p, "id"));
                    s.Save();
                    return Sources();
                case "sources.toggle":
                {
                    var cs = s.CustomSources.FirstOrDefault(c => c.Id == S(p, "id"));
                    if (cs != null) { cs.Enabled = B(p, "enabled", true); s.Save(); }
                    return Sources();
                }

                // ── updates ───────────────────────────────────
                case "update.check":
                {
                    var info = await UpdateService.CheckForUpdateAsync();
                    return new
                    {
                        available = info.IsUpdateAvailable, current = info.CurrentVersion.ToString(),
                        latest = info.LatestVersionString, title = info.ReleaseTitle, notes = info.ReleaseNotes,
                        canInstall = !string.IsNullOrEmpty(info.DownloadUrl), url = info.ReleaseUrl, downloadUrl = info.DownloadUrl
                    };
                }
                case "update.install":
                {
                    var progress = new Progress<double>(v => Emit("updateProgress", new { progress = v }));
                    await UpdateService.DownloadAndInstallUpdateAsync(S(p, "downloadUrl"), progress);
                    return null;
                }
            }
            throw new NotSupportedException($"Unknown method '{method}'.");
        }

        // ── helpers ───────────────────────────────────────────────
        private object InitState() => new
        {
            version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "4.0.0",
            settings = SettingsSnapshot(),
            panicActive = WallpaperManager.Instance.IsPanicActive,
            maximized = _form.WindowState == FormWindowState.Maximized,
            systemDark = MainForm.IsDarkTheme(),
            slideshow = SlideshowState(),
        };

        private JObject SettingsSnapshot()
        {
            var s = Settings.Instance;
            var o = JObject.FromObject(s);
            o.Remove("SavedFilterProfile");
            o["RatingMode"] = (int)s.RatingMode;
            o["HotkeyDisplay"] = s.GetHotkeyDisplayName();
            o["DownloadFolder"] = DownloadsManager.Instance.DownloadFolder;
            o["AutoStartWithWindows"] = StartupManager.IsStartupEnabled();
            o["FilterProfile"] = JObject.FromObject(s.GetFilterProfileForMode(s.RatingMode));
            o["CustomSources"] = JArray.FromObject(Sources());
            o["AccentColor"] = AccentColor;
            o["SafeWallpaperPreview"] = string.IsNullOrEmpty(s.SafeWallpaperPath) ? "" : ImageServer.Local(s.SafeWallpaperPath);
            return o;
        }

        // Accent colour lives in its own tiny file so the WinUI build's settings.json stays untouched.
        private static string AccentPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WallSafe", "web-accent.txt");

        private static string AccentColor
        {
            get { try { return File.Exists(AccentPath) ? File.ReadAllText(AccentPath).Trim() : "#8b5cf6"; } catch { return "#8b5cf6"; } }
            set { try { File.WriteAllText(AccentPath, value); } catch { } }
        }

        private object? SetSetting(string key, JToken? value)
        {
            if (!Writable.Contains(key)) throw new ArgumentException($"Setting '{key}' is not writable.");
            var s = Settings.Instance;

            if (key == "AccentColor")
            {
                string c = value?.ToString() ?? "";
                if (System.Text.RegularExpressions.Regex.IsMatch(c, "^#[0-9a-fA-F]{6}$")) AccentColor = c;
                return AccentColor;
            }

            var prop = typeof(Settings).GetProperty(key, BindingFlags.Public | BindingFlags.Instance)
                       ?? throw new ArgumentException($"Unknown setting '{key}'.");
            object? typed = value?.ToObject(prop.PropertyType);
            prop.SetValue(s, typed);

            switch (key)
            {
                case "AutoStartWithWindows": StartupManager.SetStartup(s.AutoStartWithWindows); break;
                case "BlurStrength": s.BlurStrength = Math.Clamp(s.BlurStrength, 2, 30); break;
                case "LocationRadiusMeters": s.LocationRadiusMeters = Math.Max(20, s.LocationRadiusMeters); break;
            }
            s.Save();

            switch (key)
            {
                case "Theme":
                case "BackdropMaterial":
                case "WindowMicaEnabled":
                    UiDispatch.Post(_form.ApplyChrome); break;
                case "WifiAutoWallpaperEnabled":
                case "WifiRestoreOnDisconnect":
                    WifiWatcher.Instance.Start(immediate: true); break;
                case "WifiTriggerSsid":
                    if (s.WifiAutoWallpaperEnabled) WifiWatcher.Instance.TriggerImmediatePoll(); break;
                case "LocationAutoWallpaperEnabled":
                    if (s.LocationAutoWallpaperEnabled) _ = LocationWatcher.Instance.StartAsync(); else LocationWatcher.Instance.Stop();
                    break;
                case "DiscretionBlur":
                    AppEvents.RaiseDiscretionBlurChanged(s.DiscretionBlur); break;
            }
            return prop.GetValue(s);
        }

        private static object SlideshowState()
        {
            var s = Settings.Instance;
            var fm = FavoritesManager.Instance;
            var sources = new List<object>
            {
                new { id = "favorites", name = "All favorites", count = fm.GetAllFavorites().Count },
                new { id = "downloads", name = "Downloads", count = DownloadsManager.Instance.GetAllDownloads().Count },
            };
            foreach (var c in fm.GetCollections())
                sources.Add(new { id = "collection:" + c, name = c, count = fm.GetFavoritesByCollection(c).Count });
            return new
            {
                enabled = s.SlideshowEnabled,
                seconds = (int)s.SlideshowIntervalEffectiveSeconds,
                source = s.SlideshowSource ?? "favorites",
                sources
            };
        }

        private static List<object> Sources() => Settings.Instance.CustomSources
            .Select(c => (object)new { id = c.Id, name = c.Name, baseUrl = c.BaseUrl, isSfw = c.IsSfw, enabled = c.Enabled }).ToList();

        private static object WifiStatus(string? net, bool active, string? matched) => new
        {
            network = net,
            enabled = Settings.Instance.WifiAutoWallpaperEnabled,
            triggerActive = active,
            bypassed = WifiWatcher.Instance.UserBypassedTrigger,
            matched
        };

        private static List<object> Suggestions(IEnumerable<TagSuggestion> list) => list.Take(12)
            .Select(t => (object)new { name = t.Name, title = t.Title, count = t.Count, category = t.CategoryName, color = t.CategoryColor })
            .ToList();

        private static bool IsGameTag(string tag)
        {
            string t = tag.ToLowerInvariant();
            return new[] { "genshin", "honkai", "zenless", "wuthering", "nikke", "azur_lane", "arknights", "blue_archive", "fate", "touhou", "kantai", "idolmaster", "pokemon" }
                .Any(t.Contains);
        }
    }
}
