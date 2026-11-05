// Browser-only mock backend so the UI can be designed/tested outside the WinForms host.
// Never loaded inside WallSafe itself.

export function createMock(emit) {
  const settings = {
    UserName: 'Kwan', ShowGreeting: true, FirstRunCompleted: true, Theme: 'Dark', BackdropMaterial: 'Mica',
    WindowMicaEnabled: false, CardDensity: 'comfortable', RatingMode: 0, HotkeyDisplay: 'Ctrl + Shift + W',
    HotkeyModifiers: 6, HotkeyKey: 0x57, PanicHideApp: true, PanicMinimizeAllWindows: true, PanicMuteAudio: false,
    PanicRestoreToggle: true, SlideshowEnabled: false, SlideshowSource: 'favorites', SlideshowIntervalSeconds: 900,
    StartMinimized: false, AutoStartWithWindows: false, DownloadFolder: 'C:\\Users\\You\\Pictures\\WallSafe',
    SafeWallpaperPath: 'C:\\Windows\\Web\\Wallpaper\\Windows\\img0.jpg', SafeWallpaperPreview: '',
    WifiAutoWallpaperEnabled: false, WifiTriggerSsid: '', WifiRestoreOnDisconnect: true, SafeWallpaperOnShutdown: true,
    LocationAutoWallpaperEnabled: false, LocationLatitude: 0, LocationLongitude: 0, LocationRadiusMeters: 150, LocationRestoreOnLeave: false,
    DiscretionBlur: true, BlurStrength: 8, TagBlacklist: '', ShowHomeStats: true, ShowHomeHero: true, ShowHomeTrending: true,
    ShowHomeFranchises: true, ShowHomeQuickShelf: true, TargetMonitor: -1, AccentColor: '#8b5cf6',
    FilterProfile: { Source: 'all', SortTag: 'order:score' }, CustomSources: [], PanicTriggerCount: 3,
  };
  const favs = new Map();
  const downloads = new Map();
  const history = [];
  let panic = false;
  const collections = ['Cozy', 'Night city'];

  const make = (n, extra = {}) => {
    const w = [1920, 2560, 3840, 1600][n % 4], h = Math.round(w / [1.78, 1.6, 1.78, 0.75][n % 4]);
    const key = `mock:${n}`;
    return {
      key, id: n, source: 'konasfw', sourceName: 'Konachan',
      thumb: `https://picsum.photos/seed/ws${n}/480/300`, sample: `https://picsum.photos/seed/ws${n}/1600/1000`,
      local: null, localPath: null, width: w, height: h, aspect: w / h > 1.6 ? '16:9' : 'Portrait',
      rating: ['s', 's', 's', 'q', 's', 'e'][n % 6], score: 500 - n, tags: 'scenery sky clouds original long_hair genshin_impact',
      author: 'artist_' + (n % 7), postUrl: 'https://konachan.net/post/show/' + n,
      isFavorite: favs.has(key), isDownloaded: downloads.has(key), collection: favs.get(key)?.collection || '',
      appliedAt: null, sensitive: n % 6 >= 3, ...extra,
    };
  };
  const delay = ms => new Promise(r => setTimeout(r, ms));

  return async function (method, p) {
    await delay(120 + Math.random() * 200);
    switch (method) {
      case 'app.init': return { version: '4.0.0-dev', settings, panicActive: panic, maximized: false, systemDark: true, slideshow: slideshow() };
      case 'settings.get': return settings;
      case 'settings.set': settings[p.key] = p.value; return p.value;
      case 'rating.set': settings.RatingMode = p.mode; return { mode: p.mode, profile: settings.FilterProfile };
      case 'hotkey.set': settings.HotkeyDisplay = 'Custom'; return { display: 'Custom' };
      case 'posts.fetch': {
        const base = (p.page - 1) * p.limit + (p.tags ? 400 : 0) + settings.RatingMode * 1000;
        const items = Array.from({ length: p.page > 4 ? 0 : p.limit }, (_, i) => make(base + i + 1));
        return p.meta ? { items, raw: items.length } : items;
      }
      case 'tags.suggest': return ['genshin_impact', 'hatsune_miku', 'scenery', 'sky', 'night'].filter(t => t.includes(p.query.toLowerCase()) || true)
        .map((t, i) => ({ name: t, title: null, count: 9000 - i * 1200, category: ['Series', 'Character', 'Tag'][i % 3], color: ['#A855F7', '#10B981', '#94A3B8'][i % 3] }));
      case 'tags.resolve': return p.query.replace(/ /g, '_');
      case 'home.franchises': return ['genshin_impact', 'hololive', 'touhou', 'blue_archive', 'vocaloid', 'original', 'fate/grand_order', 'arknights']
        .map((t, i) => ({ tag: t, name: t.replace(/_/g, ' ').replace(/\b\w/g, c => c.toUpperCase()), count: 48000 - i * 5000, type: i % 2 ? 'Anime' : 'Gaming' }));
      case 'franchise.preview': return `https://picsum.photos/seed/f${p.tag.length}${p.tag[0]}/800/450`;
      case 'home.stats': return { favorites: favs.size, downloads: downloads.size, applied: history.length, collections: collections.length, panic: settings.PanicTriggerCount, topTag: favs.size ? 'Genshin Impact' : '—', topSource: 'Konachan' };
      case 'post.favorite': {
        if (favs.has(p.key)) favs.delete(p.key); else favs.set(p.key, { collection: '' });
        setTimeout(() => emit('favoritesChanged'), 10);
        return { isFavorite: favs.has(p.key) };
      }
      case 'post.apply': {
        const n = +p.key.split(':')[1];
        history.unshift(make(n, { appliedAt: new Date().toISOString() }));
        setTimeout(() => { emit('wallpaperApplied', { path: 'x' }); emit('historyChanged'); }, 400);
        await delay(600); return null;
      }
      case 'post.lockscreen': await delay(400); return true;
      case 'post.download': {
        for (let i = 1; i <= 10; i++) { await delay(90); emit('downloadProgress', { key: p.key, progress: i / 10 }); }
        downloads.set(p.key, true); setTimeout(() => emit('downloadsChanged'), 10);
        return make(+p.key.split(':')[1]);
      }
      case 'post.get': return make(+p.key.split(':')[1]);
      case 'favorites.list': return [...favs.entries()]
        .filter(([, v]) => !p.collection || (p.collection === '__unsorted' ? !v.collection : v.collection === p.collection))
        .map(([k]) => make(+k.split(':')[1]));
      case 'favorites.collections': return { all: favs.size, unsorted: [...favs.values()].filter(v => !v.collection).length, items: collections.map(n => ({ name: n, count: [...favs.values()].filter(v => v.collection === n).length, cover: '' })) };
      case 'collections.create': if (!collections.includes(p.name)) collections.push(p.name); emit('favoritesChanged'); return true;
      case 'collections.rename': { const i = collections.indexOf(p.name); if (i >= 0) collections[i] = p.newName; emit('favoritesChanged'); return true; }
      case 'collections.delete': collections.splice(collections.indexOf(p.name), 1); emit('favoritesChanged'); return null;
      case 'collections.assign': favs.set(p.key, { collection: p.collection }); emit('favoritesChanged'); return null;
      case 'downloads.list': return [...downloads.keys()].map(k => make(+k.split(':')[1], { isDownloaded: true }));
      case 'downloads.delete': downloads.delete(p.key); emit('downloadsChanged'); return null;
      case 'history.list': return history;
      case 'history.clear': history.length = 0; emit('historyChanged'); return null;
      case 'history.undoClear': return null;
      case 'panic.trigger': panic = !panic; emit('panicChanged', { active: panic }); return { active: panic };
      case 'slideshow.set': settings.SlideshowEnabled = p.enabled; return slideshow();
      case 'slideshow.config': if (p.source) settings.SlideshowSource = p.source; if (p.seconds) settings.SlideshowIntervalSeconds = p.seconds; return slideshow();
      case 'slideshow.state': return slideshow();
      case 'wifi.status': return { network: 'Home-5G', enabled: settings.WifiAutoWallpaperEnabled, triggerActive: false, bypassed: false };
      case 'cache.size': return 184_000_000;
      case 'cache.clear': return 0;
      case 'monitors.list': return [{ index: 0, name: 'Display 1 (2560×1440) • Primary', primary: true }, { index: 1, name: 'Display 2 (1920×1080)', primary: false }];
      case 'update.check': await delay(800); return { available: false, current: '4.0.0', latest: '4.0.0' };
      default: return null;
    }
  };

  function slideshow() {
    return {
      enabled: settings.SlideshowEnabled, seconds: settings.SlideshowIntervalSeconds, source: settings.SlideshowSource,
      sources: [{ id: 'favorites', name: 'All favorites', count: favs.size }, { id: 'downloads', name: 'Downloads', count: downloads.size },
        ...collections.map(c => ({ id: 'collection:' + c, name: c, count: 0 }))],
    };
  }
}
