// Shared app state + wallpaper actions used by every page.
import { rpc, on, emit } from './bridge.js';
import { toast, menu, prompt, $$ } from './ui.js';
import { setIcon } from './icons.js';

export const state = {
  settings: {},
  version: '',
  panicActive: false,
  slideshow: { enabled: false, seconds: 900, source: 'favorites', sources: [] },
  posts: new Map(),       // key -> post DTO (latest copy)
  busy: new Set(),        // keys with an in-flight apply/download
};

export const S = () => state.settings;

/** Register DTOs coming from the host so every view shares one object per key. */
export function track(list) {
  return (list || []).map(p => {
    const prev = state.posts.get(p.key);
    if (prev) { Object.assign(prev, p); return prev; }
    state.posts.set(p.key, p);
    return p;
  });
}

export async function setSetting(key, value) {
  state.settings[key] = value;
  try {
    const v = await rpc('settings.set', { key, value });
    state.settings[key] = v;
    emit('ui:setting', { key, value: v });
    return v;
  } catch (e) {
    toast('Could not save setting', { type: 'err', sub: e.message });
    throw e;
  }
}

export function isVeiled(post) {
  return !!(S().DiscretionBlur && post?.sensitive);
}

/** Refresh every rendered card for this key (favorite/download/busy state). */
export function refreshCards(key) {
  const p = state.posts.get(key);
  if (!p) return;
  for (const el of $$(`[data-key="${CSS.escape(key)}"]`)) {
    const fav = el.querySelector('[data-act="fav"]');
    if (fav) {
      fav.classList.toggle('fav-on', !!p.isFavorite);
      fav.title = p.isFavorite ? 'Remove from favorites' : 'Add to favorites';
    }
    const dot = el.querySelector('.fav-dot');
    if (dot) dot.hidden = !p.isFavorite;
    const dl = el.querySelector('[data-act="download"]');
    if (dl) { setIcon(dl.querySelector('[data-icon]'), p.isDownloaded ? 'check' : 'download'); dl.title = p.isDownloaded ? 'Downloaded' : 'Download original'; }
    el.classList.toggle('busy', state.busy.has(key));
  }
}

// ── actions ─────────────────────────────────────────────────────
export async function toggleFavorite(post) {
  const was = post.isFavorite;
  post.isFavorite = !was;
  refreshCards(post.key);
  try {
    const r = await rpc('post.favorite', { key: post.key });
    post.isFavorite = r.isFavorite;
    refreshCards(post.key);
    toast(r.isFavorite ? 'Added to favorites' : 'Removed from favorites', { iconName: 'heart', timeout: 1800 });
  } catch (e) {
    post.isFavorite = was;
    refreshCards(post.key);
    toast('Could not update favorites', { type: 'err', sub: e.message });
  }
}

export async function applyWallpaper(post, monitor) {
  if (state.busy.has(post.key)) return;
  state.busy.add(post.key);
  refreshCards(post.key);
  const t = toast('Applying wallpaper…', { iconName: 'monitor', timeout: 0 });
  try {
    await rpc('post.apply', monitor === undefined ? { key: post.key } : { key: post.key, monitor });
    t.close();
    toast('Wallpaper applied', { type: 'ok', sub: `${post.width}×${post.height} · ${post.sourceName}` });
  } catch (e) {
    t.close();
    toast('Could not apply wallpaper', { type: 'err', sub: e.message });
  } finally {
    state.busy.delete(post.key);
    refreshCards(post.key);
  }
}

export async function setLockScreen(post) {
  const t = toast('Setting lock screen…', { iconName: 'lock', timeout: 0 });
  try {
    const ok = await rpc('post.lockscreen', { key: post.key });
    t.close();
    toast(ok ? 'Lock screen updated' : 'Lock screen could not be changed', { type: ok ? 'ok' : 'warn', sub: ok ? 'Takes effect next time you lock' : 'Windows may block this without admin policy' });
  } catch (e) { t.close(); toast('Lock screen failed', { type: 'err', sub: e.message }); }
}

export async function downloadPost(post) {
  if (post.isDownloaded) { toast('Already downloaded', { iconName: 'check', action: () => rpc('post.reveal', { key: post.key }), actionLabel: 'Show' }); return; }
  if (state.busy.has(post.key)) return;
  state.busy.add(post.key);
  refreshCards(post.key);
  try {
    const dto = await rpc('post.download', { key: post.key });
    track([dto]);
    toast('Download complete', { type: 'ok', sub: `${post.width}×${post.height}`, action: () => rpc('post.reveal', { key: post.key }), actionLabel: 'Show' });
  } catch (e) {
    toast('Download failed', { type: 'err', sub: e.message });
  } finally {
    state.busy.delete(post.key);
    setProgress(post.key, null);
    refreshCards(post.key);
  }
}

function setProgress(key, v) {
  for (const el of $$(`[data-key="${CSS.escape(key)}"] .progress`)) {
    el.style.opacity = v == null ? '0' : '1';
    el.style.width = v == null ? '0' : (v * 100).toFixed(1) + '%';
  }
}
on('downloadProgress', d => setProgress(d.key, d.progress));

export async function assignCollection(post) {
  const cols = await rpc('favorites.collections');
  const items = [
    { header: 'Move to collection' },
    { label: 'Unsorted', icon: 'heart', checked: !post.collection, onClick: () => doAssign(post, '') },
    ...cols.items.map(c => ({ label: c.name, icon: 'folder', checked: post.collection === c.name, onClick: () => doAssign(post, c.name) })),
    '-',
    { label: 'New collection…', icon: 'folderPlus', onClick: async () => {
      const name = await prompt('New collection', { placeholder: 'e.g. Cozy rooms', okLabel: 'Create', iconName: 'folderPlus' });
      if (name) doAssign(post, name);
    } },
  ];
  return items;
}

async function doAssign(post, collection) {
  await rpc('collections.assign', { key: post.key, collection });
  post.collection = collection;
  post.isFavorite = true;
  refreshCards(post.key);
  toast(collection ? `Moved to “${collection}”` : 'Moved to Unsorted', { iconName: 'folder', timeout: 2000 });
}

export async function postMenu(post, anchor, extra = []) {
  const monitors = await rpc('monitors.list').catch(() => []);
  const items = [
    { label: 'Preview', icon: 'maximize', onClick: () => emit('ui:preview', { post }) },
    { label: 'Set as wallpaper', icon: 'monitor', onClick: () => applyWallpaper(post) },
    ...(monitors.length > 1 ? monitors.map(m => ({ label: `On ${m.name.split(' (')[0]}`, icon: 'monitor', onClick: () => applyWallpaper(post, m.index) })) : []),
    { label: 'Set as lock screen', icon: 'lock', onClick: () => setLockScreen(post) },
    '-',
    { label: post.isFavorite ? 'Remove from favorites' : 'Add to favorites', icon: 'heart', onClick: () => toggleFavorite(post) },
    { label: 'Move to collection…', icon: 'folder', onClick: async () => menu(await assignCollection(post), anchor) },
    { label: post.isDownloaded ? 'Show in folder' : 'Download original', icon: post.isDownloaded ? 'folder' : 'download',
      onClick: () => post.isDownloaded ? rpc('post.reveal', { key: post.key }) : downloadPost(post) },
    post.postUrl ? { label: 'Open source page', icon: 'external', onClick: () => rpc('shell.open', { url: post.postUrl }) } : null,
    ...extra,
  ];
  menu(items, anchor);
}
