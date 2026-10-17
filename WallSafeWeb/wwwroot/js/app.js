// App shell: boot, appearance, router, title bar controls.
import { rpc, on, emit, isHost } from './bridge.js';
import { hydrateIcons, setIcon } from './icons.js';
import { $, $$, h, esc, toast, popover, modal, closePopover } from './ui.js';
import { state, S, setSetting } from './state.js';
import { attachSuggest } from './search.js';
import { refreshVeils } from './cards.js';
import './preview.js';
import { installDnd, refreshCollections } from './dnd.js';

import * as home from './pages/home.js';
import * as explore from './pages/explore.js';
import * as favorites from './pages/favorites.js';
import * as downloads from './pages/downloads.js';
import * as history from './pages/history.js';
import * as settings from './pages/settings.js';

const PAGES = { home, explore, favorites, downloads, history, settings };
const instances = {};
let currentRoute = null;

// ── Appearance ──────────────────────────────────────────────────
export function applyAppearance() {
  const s = S();
  const root = document.documentElement;
  const theme = s.Theme === 'Light' ? 'light' : s.Theme === 'System' ? (state.systemDark ? 'dark' : 'light') : 'dark';
  root.dataset.theme = theme;
  root.classList.toggle('backdrop', isHost && !!s.WindowMicaEnabled && s.BackdropMaterial !== 'None');
  root.style.setProperty('--accent', s.AccentColor || '#8b5cf6');
  root.style.setProperty('--blur', Math.max(4, (s.BlurStrength || 8) * 2) + 'px');
  for (const d of ['compact', 'comfortable', 'large']) root.classList.toggle('density-' + d, (s.CardDensity || 'comfortable') === d);
}

export function setAmbient(url) {
  const amb = $('#ambient');
  if (!url || localStorage.getItem('ws.ambient') === '0') { amb.classList.remove('on'); return; }
  amb.querySelector('.ambient-img').style.backgroundImage = `url("${url}")`;
  amb.classList.add('on');
}

// ── Router ──────────────────────────────────────────────────────
function parseHash() {
  const raw = location.hash.replace(/^#\/?/, '') || 'home';
  const [name, qs] = raw.split('?');
  return { name: PAGES[name] ? name : 'home', params: Object.fromEntries(new URLSearchParams(qs || '')) };
}

function route() {
  const { name, params } = parseHash();
  closePopover();
  const content = $('#content');
  if (!instances[name]) {
    instances[name] = PAGES[name].create();
    instances[name].el.classList.add('page');
    instances[name].el.hidden = true;
    content.appendChild(instances[name].el);
    hydrateIcons(instances[name].el);
  }
  for (const [k, inst] of Object.entries(instances)) {
    const on = k === name;
    if (on && inst.el.hidden) {
      inst.el.hidden = false;
      inst.el.classList.remove('enter');
      void inst.el.offsetWidth;
      inst.el.classList.add('enter');
    } else if (!on) inst.el.hidden = true;
  }
  currentRoute = name;
  instances[name].show?.(params);
  if (name !== 'home') setAmbient(null);
  else instances.home.resumeAmbient?.();

  $$('#sidebar a').forEach(a => a.classList.toggle('active', a.dataset.route === name));
  moveNavIndicator();
}

function moveNavIndicator() {
  const a = $('#sidebar a.active');
  const ind = $('#navIndicator');
  if (!a) { ind.style.height = '0'; return; }
  ind.style.height = a.offsetHeight + 'px';
  ind.style.transform = `translateY(${a.offsetTop}px)`;
}

export function navigate(hash) {
  if (location.hash === hash) route(); else location.hash = hash;
}

/** Tell every instantiated page something changed (they decide what to reload). */
export function broadcast(reason, data) {
  for (const [name, inst] of Object.entries(instances)) inst.refresh?.(reason, name === currentRoute, data);
}

// ── Title bar ───────────────────────────────────────────────────
function syncRating() {
  const seg = $('#ratingSeg');
  const mode = S().RatingMode ?? 0;
  seg.dataset.mode = mode;
  const btns = $$('button', seg);
  btns.forEach(b => b.setAttribute('aria-checked', String(+b.dataset.mode === mode)));
  const b = btns[mode];
  const thumb = $('.seg-thumb', seg);
  thumb.style.width = b.offsetWidth + 'px';
  thumb.style.transform = `translateX(${b.offsetLeft - 3}px)`;
}

async function setRating(mode) {
  if (mode === S().RatingMode) return;
  if (mode === 2 && !localStorage.getItem('ws.ack18')) {
    const ok = await modal({
      title: 'Show explicit content?', iconName: 'alert',
      body: '<p>18+ mode shows explicit artwork from the configured boards. Discretion blur still hides sensitive thumbnails until you hover them.</p>',
      actions: [{ label: 'Cancel' }, { label: "I'm 18 or older", primary: true, value: () => true }],
    });
    if (!ok) return;
    localStorage.setItem('ws.ack18', '1');
  }
  const r = await rpc('rating.set', { mode });
  S().RatingMode = r.mode;
  S().FilterProfile = r.profile;
  syncRating();
  broadcast('rating');
}

function syncSlideshow() {
  const pill = $('#slideshowPill');
  const on = state.slideshow.enabled;
  pill.classList.toggle('on', on);
  setIcon(pill.querySelector('[data-icon]'), on ? 'pause' : 'play');
  pill.querySelector('.pill-label').textContent = on ? 'Slideshow on' : 'Slideshow';
}

export const INTERVALS = [[30, '30s'], [60, '1m'], [300, '5m'], [900, '15m'], [1800, '30m'], [3600, '1h']];

async function openSlideshowPanel(anchor) {
  state.slideshow = await rpc('slideshow.state');
  const ss = state.slideshow;
  const el = h(`<div class="pop-panel">
      <div class="pop-row" style="margin-top:0">
        <div><h4>Slideshow</h4><div class="muted">Rotate your wallpaper automatically</div></div>
        <label class="switch"><input type="checkbox" data-s="toggle" ${ss.enabled ? 'checked' : ''}><span class="track"></span></label>
      </div>
      <div class="menu-label" style="padding:14px 0 6px">Change every</div>
      <div class="chips" data-s="intervals">${INTERVALS.map(([sec, l]) => `<button class="chip ${ss.seconds === sec ? 'active' : ''}" data-sec="${sec}">${l}</button>`).join('')}</div>
      <div class="menu-label" style="padding:14px 0 6px">Source</div>
      <select class="select" data-s="source">${ss.sources.map(o => `<option value="${esc(o.id)}" ${o.id === ss.source ? 'selected' : ''}>${esc(o.name)} (${o.count})</option>`).join('')}</select>
      <div class="pop-row"><span class="muted">Pauses during full-screen apps &amp; panic mode</span>
        <button class="btn sm" data-s="next"><span data-icon="skip"></span>Next</button></div>
    </div>`);
  el.querySelector('[data-s="toggle"]').addEventListener('change', async e => {
    state.slideshow = await rpc('slideshow.set', { enabled: e.target.checked });
    syncSlideshow();
    toast(e.target.checked ? 'Slideshow started' : 'Slideshow stopped', { iconName: e.target.checked ? 'play' : 'pause', timeout: 1600 });
  });
  el.querySelector('[data-s="intervals"]').addEventListener('click', async e => {
    const c = e.target.closest('[data-sec]');
    if (!c) return;
    el.querySelectorAll('[data-sec]').forEach(x => x.classList.toggle('active', x === c));
    state.slideshow = await rpc('slideshow.config', { seconds: +c.dataset.sec });
  });
  el.querySelector('[data-s="source"]').addEventListener('change', async e => {
    state.slideshow = await rpc('slideshow.config', { source: e.target.value });
  });
  el.querySelector('[data-s="next"]').addEventListener('click', async () => {
    try { await rpc('slideshow.next'); } catch (e) { toast('Nothing to rotate yet', { type: 'warn', sub: 'Add favorites or downloads first' }); }
  });
  popover(el, anchor, { align: 'end' });
}

function syncBlur() {
  const pill = $('#blurPill');
  const on = !!S().DiscretionBlur;
  pill.classList.toggle('on', on);
  setIcon(pill.querySelector('[data-icon]'), on ? 'eyeOff' : 'eye');
  pill.title = on ? 'Discretion blur: on' : 'Discretion blur: off';
}

function syncPanic() {
  const pill = $('#panicPill');
  pill.classList.toggle('on', state.panicActive);
  setIcon(pill.querySelector('[data-icon]'), state.panicActive ? 'shieldCheck' : 'shield');
  pill.querySelector('.pill-label').textContent = state.panicActive ? 'Safe mode' : 'Panic';
  pill.title = state.panicActive ? 'Safe wallpaper active — click to restore' : `Panic — switch to safe wallpaper (${S().HotkeyDisplay || ''})`;
}

function wireTitlebar() {
  $('#ratingSeg').addEventListener('click', e => {
    const b = e.target.closest('[data-mode]');
    if (b) setRating(+b.dataset.mode);
  });
  $('#slideshowPill').addEventListener('click', e => openSlideshowPanel(e.currentTarget));
  $('#blurPill').addEventListener('click', async () => {
    await setSetting('DiscretionBlur', !S().DiscretionBlur);
    syncBlur();
    refreshVeils();
    broadcast('discretion');
    toast(S().DiscretionBlur ? 'Discretion blur on' : 'Discretion blur off', { iconName: S().DiscretionBlur ? 'eyeOff' : 'eye', timeout: 1600 });
  });
  $('#panicPill').addEventListener('click', async () => {
    const r = await rpc('panic.trigger');
    state.panicActive = r.active;
    syncPanic();
  });
  $('#winMin').addEventListener('click', () => rpc('window.minimize'));
  $('#winMax').addEventListener('click', () => rpc('window.toggleMaximize'));
  $('#winClose').addEventListener('click', () => rpc('window.close'));
  $('#titlebar').addEventListener('dblclick', e => {
    getSelection()?.removeAllRanges();
    // In the host, app-region:drag areas are native caption, so Windows handles double-click itself.
    if (!isHost && e.target.closest('.drag')) rpc('window.toggleMaximize');
  });

  const input = $('#globalSearch');
  attachSuggest(input, $('#globalSuggest'), tag => {
    input.blur();
    navigate('#/explore?tags=' + encodeURIComponent(tag));
  });
}

function syncMaximized(max) {
  const b = $('#winMax');
  b.querySelector('.glyph').innerHTML = max ? '&#xE923;' : '&#xE922;';
  b.querySelector('svg').innerHTML = max
    ? '<rect x="0.5" y="2.5" width="7" height="7" rx="1"/><path d="M2.5 2.5V1.5a1 1 0 0 1 1-1h5a1 1 0 0 1 1 1v5a1 1 0 0 1-1 1H7.5"/>'
    : '<rect x="0.5" y="0.5" width="9" height="9" rx="1.5"/>';
  b.setAttribute('aria-label', max ? 'Restore' : 'Maximize');
}

// ── First run ───────────────────────────────────────────────────
async function firstRun() {
  if (S().FirstRunCompleted) return;
  await setSetting('FirstRunCompleted', true);
  const name = await modal({
    title: 'Welcome to WallSafe', iconName: 'sparkles',
    body: `<p>Discover gorgeous wallpapers and stay safe with the instant panic hotkey <b>${esc(S().HotkeyDisplay)}</b>. What should we call you?</p>
           <input class="input" maxlength="40" placeholder="Your name (optional)" />`,
    actions: [{ label: 'Skip' }, { label: 'Get started', primary: true, value: m => m.querySelector('input').value.trim() }],
  });
  if (typeof name === 'string' && name) {
    await setSetting('UserName', name);
    broadcast('settings');
  }
}

// ── Global keys ─────────────────────────────────────────────────
function wireKeys() {
  document.addEventListener('keydown', e => {
    if (e.ctrlKey && e.key.toLowerCase() === 'k') { e.preventDefault(); $('#globalSearch').focus(); }
    const routes = ['home', 'explore', 'favorites', 'downloads', 'history', 'settings'];
    if (e.ctrlKey && /^[1-6]$/.test(e.key)) { e.preventDefault(); navigate('#/' + routes[+e.key - 1]); }
    if (e.key === 'F5') { e.preventDefault(); broadcast('manual'); }
  });
  // Swallow browser defaults that make no sense in an app.
  document.addEventListener('contextmenu', e => { if (!e.target.closest('input,textarea')) e.preventDefault(); });
}

// ── Host events ─────────────────────────────────────────────────
function wireHostEvents() {
  on('panicChanged', d => { state.panicActive = !!d?.active; syncPanic(); broadcast('panic'); });
  on('windowState', d => syncMaximized(d.maximized));
  on('systemThemeChanged', d => { state.systemDark = d.dark; applyAppearance(); });
  on('slideshowChanged', async () => { state.slideshow = await rpc('slideshow.state'); syncSlideshow(); });
  on('favoritesChanged', () => { broadcast('favorites'); refreshCollections(); });
  on('downloadsChanged', () => broadcast('downloads'));
  on('historyChanged', () => broadcast('history'));
  on('wallpaperApplied', () => broadcast('applied'));
  on('windowShown', () => broadcast('shown'));
  on('ui:setting', ({ key }) => {
    if (['Theme', 'BackdropMaterial', 'WindowMicaEnabled', 'AccentColor', 'CardDensity', 'BlurStrength'].includes(key)) applyAppearance();
    if (key === 'DiscretionBlur' || key === 'BlurStrength' || key === 'TagBlacklist') { syncBlur(); refreshVeils(); }
    if (key === 'HotkeyDisplay') syncPanic();
  });
  window.addEventListener('resize', () => { syncRating(); moveNavIndicator(); });
}

// ── Boot ────────────────────────────────────────────────────────
async function boot() {
  hydrateIcons();
  // Native Windows caption glyphs when the system icon font exists (Win10 MDL2 / Win11 Fluent).
  if (isHost) document.documentElement.classList.add('fluent-glyphs');
  const init = await rpc('app.init');
  state.version = init.version;
  state.settings = init.settings;
  state.panicActive = init.panicActive;
  state.systemDark = init.systemDark;
  state.slideshow = init.slideshow;
  applyAppearance();

  wireTitlebar();
  wireKeys();
  installDnd();
  wireHostEvents();
  syncMaximized(init.maximized);
  syncBlur();
  syncPanic();
  syncSlideshow();
  requestAnimationFrame(syncRating);

  window.addEventListener('hashchange', route);
  route();
  setTimeout(firstRun, 600);
  if (isHost) setTimeout(checkUpdatesQuietly, 5000);
}

async function checkUpdatesQuietly() {
  try {
    const u = await rpc('update.check');
    if (u.available) toast(`WallSafe ${u.latest} is available`, { iconName: 'sparkles', timeout: 10000, actionLabel: 'View', action: () => navigate('#/settings?section=about') });
  } catch { /* offline */ }
}

boot().catch(err => {
  console.error(err);
  document.getElementById('content').innerHTML = `<div class="page"><div class="empty"><h3>WallSafe failed to start</h3><p>${esc(err.message)}</p></div></div>`;
});
