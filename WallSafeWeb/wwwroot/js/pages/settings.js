// Settings: every option of the WinUI build, grouped into sections.
import { rpc, on, emit, isHost } from '../bridge.js';
import { h, esc, fmtBytes, toast, confirm, debounce } from '../ui.js';
import { icon } from '../icons.js';
import { state, S, setSetting } from '../state.js';
import { hqThumbs, setHqThumbs } from '../cards.js';
import { INTERVALS, applyAppearance, broadcast } from '../app.js';

const ACCENTS = ['#8b5cf6', '#6366f1', '#3b82f6', '#06b6d4', '#10b981', '#f59e0b', '#f43f5e', '#ec4899'];
const SECTIONS = [
  ['profile', 'user', 'Personalization'], ['appearance', 'palette', 'Appearance'], ['panic', 'shield', 'Panic & hotkey'],
  ['safe', 'shieldCheck', 'Safe wallpaper'], ['slideshow', 'play', 'Slideshow'], ['discretion', 'eyeOff', 'Discretion'],
  ['triggers', 'wifi', 'Auto-safe triggers'], ['home', 'home', 'Home dashboard'], ['system', 'power', 'Startup & storage'],
  ['sources', 'globe', 'Image sources'], ['about', 'info', 'About & updates'],
];

const sw = (key, checked) => `<label class="switch"><input type="checkbox" data-set="${key}" ${checked ? 'checked' : ''}><span class="track"></span></label>`;
const row = (title, desc, ctrl, cls = '') => `<div class="s-row ${cls}"><div class="s-text"><div class="s-title">${title}</div>${desc ? `<div class="s-desc">${desc}</div>` : ''}</div><div class="s-ctrl">${ctrl}</div></div>`;
const group = (id, ic, title, desc, body) => `<section class="s-group" id="set-${id}"><h2><span class="gi">${icon(ic)}</span>${title}</h2>${desc ? `<p>${desc}</p>` : ''}<div class="s-card">${body}</div></section>`;

export function create() {
  const el = h(`<section class="page settings">
      <div class="page-head"><div><h1>Settings</h1><p>Tune WallSafe to the way you work.</p></div></div>
      <div class="settings-layout">
        <nav class="settings-nav">${SECTIONS.map(([id, ic, t]) => `<a href="#" data-sec="${id}">${icon(ic)}${t}</a>`).join('')}</nav>
        <div class="settings-body"></div>
      </div>
    </section>`);
  const body = el.querySelector('.settings-body');
  let recording = null;

  const render = async () => {
    const s = S();
    const [monitors, cache, wifi] = await Promise.all([
      rpc('monitors.list').catch(() => []), rpc('cache.size').catch(() => 0), rpc('wifi.status').catch(() => null),
    ]);
    const ss = state.slideshow;
    const mat = s.WindowMicaEnabled ? (s.BackdropMaterial || 'Mica') : 'None';

    body.innerHTML = [
      group('profile', 'user', 'Personalization', 'How WallSafe greets you.',
        row('Your name', 'Shown in the Home greeting.', `<input class="input" data-set="UserName" maxlength="40" placeholder="Optional" value="${esc(s.UserName || '')}" style="width:220px">`) +
        row('Show greeting', 'Time-of-day greeting on the Home hero.', sw('ShowGreeting', s.ShowGreeting !== false))),

      group('appearance', 'palette', 'Appearance', 'Colors, materials and layout density.',
        row('Theme', '', `<select class="select" data-set="Theme">${['Dark', 'Light', 'System'].map(t => `<option ${s.Theme === t ? 'selected' : ''} value="${t}">${t === 'System' ? 'Use system setting' : t}</option>`).join('')}</select>`) +
        row('Window material', isHost ? 'Windows 11 Mica / Acrylic shows your desktop through the app.' : 'Available in the desktop app.',
          `<select class="select" data-k="material" ${isHost ? '' : 'disabled'}>${[['Mica', 'Mica'], ['MicaAlt', 'Mica Alt'], ['Acrylic', 'Acrylic'], ['None', 'Solid']].map(([v, l]) => `<option value="${v}" ${mat === v ? 'selected' : ''}>${l}</option>`).join('')}</select>`) +
        row('Accent color', '', `<div class="swatches">${ACCENTS.map(c => `<button class="swatch ${(s.AccentColor || ACCENTS[0]).toLowerCase() === c ? 'on' : ''}" data-accent="${c}" style="background:${c}" aria-label="Accent ${c}"></button>`).join('')}</div>`) +
        row('Card size', 'How large wallpaper cards appear in grids.', `<div class="chips" data-k="density">${[['compact', 'Compact'], ['comfortable', 'Comfortable'], ['large', 'Large']].map(([v, l]) => `<button class="chip ${(s.CardDensity || 'comfortable') === v ? 'active' : ''}" data-v="${v}">${l}</button>`).join('')}</div>`) +
        row('Sharp thumbnails', 'Load higher-quality previews for cards on screen (uses more bandwidth).', `<label class="switch"><input type="checkbox" data-k="hq" ${hqThumbs() ? 'checked' : ''}><span class="track"></span></label>`) +
        row('Ambient glow', 'Tint the background with colors from the featured wallpaper.', `<label class="switch"><input type="checkbox" data-k="ambient" ${localStorage.getItem('ws.ambient') !== '0' ? 'checked' : ''}><span class="track"></span></label>`)),

      group('panic', 'shield', 'Panic & hotkey', 'One keystroke hides everything — works in any app, even full-screen games.',
        row('Global panic hotkey', 'Press it again to restore your wallpaper.',
          `<div class="hotkey-display" data-k="hkdisplay">${(s.HotkeyDisplay || '').split(' + ').map(k => `<kbd>${esc(k)}</kbd>`).join('')}</div><button class="btn" data-k="record">${icon('keyboard')} Change</button>`) +
        row('Hide WallSafe', 'Send the window to the tray instantly.', sw('PanicHideApp', s.PanicHideApp)) +
        row('Minimize all windows', 'Show the desktop (like Win + D).', sw('PanicMinimizeAllWindows', s.PanicMinimizeAllWindows)) +
        row('Mute system audio', '', sw('PanicMuteAudio', s.PanicMuteAudio)) +
        row('Second press restores', 'Toggle back to your previous wallpaper.', sw('PanicRestoreToggle', s.PanicRestoreToggle)) +
        row('Try it', `Triggered ${s.PanicTriggerCount || 0} time${s.PanicTriggerCount === 1 ? '' : 's'} so far.`, `<button class="btn danger" data-k="testpanic">${icon('shield')} Test panic</button>`)),

      group('safe', 'shieldCheck', 'Safe wallpaper', 'What your desktop switches to in panic mode.',
        `<div class="s-row"><div class="safe-preview" style="background-image:url('${esc(s.SafeWallpaperPreview || '')}')"></div>
           <div class="s-text"><div class="s-title">Current safe wallpaper</div><div class="s-desc mono">${esc(s.SafeWallpaperPath || 'Windows default')}</div></div>
           <div class="s-ctrl"><button class="btn" data-k="picksafe">${icon('image')} Choose…</button><button class="btn ghost" data-k="resetsafe">Reset</button></div></div>` +
        row('Protect on shutdown & sleep', 'Switch to the safe wallpaper before the PC sleeps or shuts down, so it never boots up showing anime art in public.', sw('SafeWallpaperOnShutdown', s.SafeWallpaperOnShutdown))),

      group('slideshow', 'play', 'Slideshow', 'Automatically rotate wallpapers. Pauses during full-screen apps.',
        row('Enable slideshow', '', `<label class="switch"><input type="checkbox" data-k="ssOn" ${ss.enabled ? 'checked' : ''}><span class="track"></span></label>`) +
        row('Change every', '', `<div class="chips" data-k="ssInt">${INTERVALS.map(([sec, l]) => `<button class="chip ${ss.seconds === sec ? 'active' : ''}" data-v="${sec}">${l}</button>`).join('')}</div>`) +
        row('Rotate from', '', `<select class="select" data-k="ssSrc">${ss.sources.map(o => `<option value="${esc(o.id)}" ${o.id === ss.source ? 'selected' : ''}>${esc(o.name)} (${o.count})</option>`).join('')}</select>`) +
        row('Display', 'Which monitor receives new wallpapers.', `<select class="select" data-set="TargetMonitor" data-num>${[{ index: -1, name: 'All displays' }, ...monitors].map(m => `<option value="${m.index}" ${s.TargetMonitor === m.index ? 'selected' : ''}>${esc(m.name)}</option>`).join('')}</select>`)),

      group('discretion', 'eyeOff', 'Discretion', 'Blur sensitive thumbnails until you hover them.',
        row('Discretion blur', 'Applies to 16+ / 18+ art and blacklisted tags.', sw('DiscretionBlur', s.DiscretionBlur)) +
        row('Blur strength', `<span data-k="blurLabel">${s.BlurStrength}</span>`, `<input type="range" class="range" min="3" max="18" value="${s.BlurStrength}" data-set="BlurStrength" data-num>`) +
        `<div class="s-row col"><div class="s-text"><div class="s-title">Always blur these tags</div><div class="s-desc">Space or comma separated, e.g. <span class="mono">gore spoilers</span></div></div>
           <textarea class="textarea" data-set="TagBlacklist" rows="2" placeholder="tag_one tag_two">${esc(s.TagBlacklist || '')}</textarea></div>`),

      group('triggers', 'wifi', 'Auto-safe triggers', 'Switch to the safe wallpaper automatically at school, work or anywhere public.',
        row(`${icon('wifi')} Wi-Fi trigger`, 'Go safe while connected to specific networks.', sw('WifiAutoWallpaperEnabled', s.WifiAutoWallpaperEnabled)) +
        `<div class="s-row col sub ${s.WifiAutoWallpaperEnabled ? '' : 'disabled'}" data-k="wifiBox">
           <div data-k="wifiStatus">${wifiStatusHtml(wifi)}</div>
           <textarea class="textarea" data-set="WifiTriggerSsid" rows="3" placeholder="One network name per line">${esc(s.WifiTriggerSsid || '')}</textarea>
           <div style="display:flex;gap:8px;align-items:center;justify-content:space-between;flex-wrap:wrap">
             <button class="btn sm" data-k="addwifi">${icon('plus')} Add current network</button>
             <label style="display:flex;gap:10px;align-items:center;font-size:13px">Restore when disconnected ${sw('WifiRestoreOnDisconnect', s.WifiRestoreOnDisconnect)}</label>
           </div></div>` +
        row(`${icon('mapPin')} Location trigger`, 'Go safe within a radius of a place (uses Windows location).', sw('LocationAutoWallpaperEnabled', s.LocationAutoWallpaperEnabled)) +
        `<div class="s-row col sub ${s.LocationAutoWallpaperEnabled ? '' : 'disabled'}" data-k="locBox">
           <div style="display:grid;grid-template-columns:1fr 1fr 120px;gap:8px">
             <input class="input" type="number" step="0.000001" data-set="LocationLatitude" data-num placeholder="Latitude" value="${s.LocationLatitude || ''}">
             <input class="input" type="number" step="0.000001" data-set="LocationLongitude" data-num placeholder="Longitude" value="${s.LocationLongitude || ''}">
             <input class="input" type="number" min="20" step="10" data-set="LocationRadiusMeters" data-num placeholder="Radius m" value="${s.LocationRadiusMeters || 150}">
           </div>
           <div style="display:flex;gap:8px;align-items:center;justify-content:space-between;flex-wrap:wrap">
             <button class="btn sm" data-k="useloc">${icon('mapPin')} Use my current location</button>
             <label style="display:flex;gap:10px;align-items:center;font-size:13px">Restore when leaving ${sw('LocationRestoreOnLeave', s.LocationRestoreOnLeave)}</label>
           </div></div>`),

      group('home', 'home', 'Home dashboard', 'Choose which sections appear on Home.',
        row('Featured hero', '', sw('ShowHomeHero', s.ShowHomeHero !== false)) +
        row('Statistics', '', sw('ShowHomeStats', s.ShowHomeStats !== false)) +
        row('Trending wallpapers', '', sw('ShowHomeTrending', s.ShowHomeTrending !== false)) +
        row('Popular universes', '', sw('ShowHomeFranchises', s.ShowHomeFranchises !== false)) +
        row('Your favorites', '', sw('ShowHomeQuickShelf', s.ShowHomeQuickShelf !== false))),

      group('system', 'power', 'Startup & storage', '',
        row('Start with Windows', '', sw('AutoStartWithWindows', s.AutoStartWithWindows)) +
        row('Start in the tray', 'When launched at sign-in, stay hidden so only the hotkey & tray are active.', sw('StartMinimized', s.StartMinimized)) +
        row('Download folder', `<span class="mono" data-k="dlpath">${esc(s.DownloadFolder || '')}</span>`, `<button class="btn" data-k="pickdl">${icon('folder')} Change…</button>`) +
        row('Image cache', `<span data-k="cacheSize">${fmtBytes(cache)}</span> of thumbnails stored locally for instant loading.`, `<button class="btn" data-k="clearcache">${icon('trash')} Clear cache</button>`)),

      group('sources', 'globe', 'Image sources', 'Built-in: Konachan (SFW), Konachan.com and yande.re. Add any Moebooru-compatible board.',
        `<div class="s-row col">${(s.CustomSources || []).map(c => `
            <div class="source-row"><div class="s-text"><div class="s-name">${esc(c.name)} ${c.isSfw ? '<span class="badge" style="background:var(--ok)">SFW</span>' : '<span class="badge e">NSFW</span>'}</div><div class="s-url">${esc(c.baseUrl)}</div></div>
              ${sw('', c.enabled).replace('data-set=""', `data-src-toggle="${esc(c.id)}"`)}
              <button class="btn sm icon ghost" data-src-del="${esc(c.id)}" title="Remove">${icon('trash')}</button></div>`).join('') || '<div class="s-desc">No custom boards yet.</div>'}
           <div style="display:grid;grid-template-columns:160px 1fr auto auto;gap:8px;align-items:center;margin-top:6px">
             <input class="input" data-k="srcName" placeholder="Name">
             <input class="input" data-k="srcUrl" placeholder="https://board.example">
             <label style="display:flex;gap:8px;align-items:center;font-size:13px">SFW <label class="switch"><input type="checkbox" data-k="srcSfw" checked><span class="track"></span></label></label>
             <button class="btn" data-k="srcAdd">${icon('plus')} Add</button>
           </div></div>`),

      group('about', 'info', 'About & updates', '',
        `<div class="about-hero"><div class="logo"></div><div><h3>WallSafe</h3><div class="s-desc">Version ${esc(state.version)} · WebView2 edition</div></div></div>` +
        `<div class="s-row col" data-k="updBox"><div style="display:flex;gap:10px;align-items:center;justify-content:space-between">
            <div class="s-text"><div class="s-title" data-k="updTitle">Check for updates</div><div class="s-desc" data-k="updDesc">Updates are downloaded from GitHub releases.</div></div>
            <div class="s-ctrl"><button class="btn" data-k="updCheck">${icon('refresh')} Check now</button><button class="btn primary" data-k="updInstall" hidden>${icon('download')} Install</button></div></div>
            <div class="progress-bar" data-k="updBar" hidden><div></div></div></div>` +
        row('Project page', '', `<button class="btn ghost" data-k="gh">${icon('external')} GitHub</button>`) +
        row('Quit WallSafe', 'Closing the window keeps WallSafe in the tray. This fully exits — the panic hotkey stops working.', `<button class="btn danger" data-k="exit">${icon('power')} Exit</button>`)),
    ].join('');
  };

  // ── generic setting bindings ──
  const saveText = debounce((key, value) => setSetting(key, value), 400);
  body.addEventListener('change', async e => {
    const t = e.target;
    const key = t.dataset.set;
    if (key) {
      let v = t.type === 'checkbox' ? t.checked : t.value;
      if (t.dataset.num !== undefined) v = Number(v);
      if (t.tagName === 'TEXTAREA' || t.type === 'text' || t.type === 'number') return; // handled by input
      await setSetting(key, v);
      afterSetting(key, v);
      return;
    }
    const k = t.dataset.k;
    if (k === 'material') {
      const none = t.value === 'None';
      await setSetting('WindowMicaEnabled', !none);
      if (!none) await setSetting('BackdropMaterial', t.value);
      applyAppearance();
    }
    if (k === 'hq') { setHqThumbs(t.checked); toast('Applies to newly loaded wallpapers', { timeout: 1800 }); }
    if (k === 'ambient') { localStorage.setItem('ws.ambient', t.checked ? '1' : '0'); toast(t.checked ? 'Ambient glow on' : 'Ambient glow off', { timeout: 1400 }); }
    if (k === 'ssOn') { state.slideshow = await rpc('slideshow.set', { enabled: t.checked }); }
    if (k === 'ssSrc') { state.slideshow = await rpc('slideshow.config', { source: t.value }); }
    const tg = t.dataset.srcToggle;
    if (tg) { S().CustomSources = await rpc('sources.toggle', { id: tg, enabled: t.checked }); broadcast('rating'); }
  });

  body.addEventListener('input', e => {
    const t = e.target;
    const key = t.dataset.set;
    if (!key) return;
    if (t.type === 'range') { body.querySelector('[data-k="blurLabel"]').textContent = t.value; document.documentElement.style.setProperty('--blur', t.value * 2 + 'px'); }
    if (t.tagName === 'TEXTAREA' || t.type === 'text' || t.type === 'number' || t.type === 'range') {
      let v = t.value;
      if (t.dataset.num !== undefined) { v = Number(v); if (Number.isNaN(v)) return; }
      saveText(key, v);
      if (key === 'UserName') setTimeout(() => broadcast('settings'), 450);
    }
  });

  const afterSetting = (key, v) => {
    if (key.startsWith('ShowHome') || key === 'ShowGreeting') broadcast('settings');
    if (key === 'WifiAutoWallpaperEnabled') body.querySelector('[data-k="wifiBox"]').classList.toggle('disabled', !v);
    if (key === 'LocationAutoWallpaperEnabled') body.querySelector('[data-k="locBox"]').classList.toggle('disabled', !v);
    if (key === 'DiscretionBlur') broadcast('discretion');
    if (key === 'AutoStartWithWindows') toast(v ? 'WallSafe will start with Windows' : 'Removed from startup', { type: 'ok', timeout: 1800 });
  };

  body.addEventListener('click', async e => {
    const acc = e.target.closest('[data-accent]');
    if (acc) {
      await setSetting('AccentColor', acc.dataset.accent);
      body.querySelectorAll('[data-accent]').forEach(x => x.classList.toggle('on', x === acc));
      return;
    }
    const chip = e.target.closest('[data-k="density"] [data-v], [data-k="ssInt"] [data-v]');
    if (chip) {
      chip.parentElement.querySelectorAll('[data-v]').forEach(x => x.classList.toggle('active', x === chip));
      if (chip.closest('[data-k="density"]')) await setSetting('CardDensity', chip.dataset.v);
      else state.slideshow = await rpc('slideshow.config', { seconds: +chip.dataset.v });
      return;
    }
    const del = e.target.closest('[data-src-del]');
    if (del) {
      if (!(await confirm('Remove this board?', 'You can add it again later.', { okLabel: 'Remove', danger: true }))) return;
      S().CustomSources = await rpc('sources.remove', { id: del.dataset.srcDel });
      broadcast('rating');
      return render();
    }

    const k = e.target.closest('[data-k]')?.dataset.k;
    switch (k) {
      case 'record': return startRecording(e.target.closest('[data-k]'));
      case 'testpanic': return rpc('panic.trigger');
      case 'picksafe': {
        const p = await rpc('dialog.pickSafeWallpaper');
        if (p) { Object.assign(S(), await rpc('settings.get')); render(); toast('Safe wallpaper updated', { type: 'ok' }); }
        return;
      }
      case 'resetsafe': await rpc('safe.reset'); Object.assign(S(), await rpc('settings.get')); render(); return;
      case 'pickdl': {
        const p = await rpc('dialog.pickDownloadFolder');
        if (p) { S().DownloadFolder = p; body.querySelector('[data-k="dlpath"]').textContent = p; broadcast('settings'); }
        return;
      }
      case 'clearcache': {
        await rpc('cache.clear');
        body.querySelector('[data-k="cacheSize"]').textContent = fmtBytes(0);
        toast('Cache cleared', { type: 'ok' });
        return;
      }
      case 'addwifi': {
        const st = await rpc('wifi.status');
        if (!st?.network) { toast('No network detected', { type: 'warn' }); return; }
        const ta = body.querySelector('[data-set="WifiTriggerSsid"]');
        const lines = ta.value.split(/\r?\n/).map(x => x.trim()).filter(Boolean);
        if (!lines.some(l => l.toLowerCase() === st.network.toLowerCase())) lines.push(st.network);
        ta.value = lines.join('\n');
        await setSetting('WifiTriggerSsid', ta.value);
        toast(`Added “${st.network}”`, { type: 'ok' });
        return;
      }
      case 'useloc': {
        const t = toast('Getting your location…', { iconName: 'mapPin', timeout: 0 });
        const pos = await rpc('location.current').catch(() => null);
        t.close();
        if (!pos) { toast('Location unavailable', { type: 'warn', sub: 'Enable location access in Windows Settings › Privacy' }); return; }
        S().LocationLatitude = pos.lat; S().LocationLongitude = pos.lon;
        body.querySelector('[data-set="LocationLatitude"]').value = pos.lat.toFixed(6);
        body.querySelector('[data-set="LocationLongitude"]').value = pos.lon.toFixed(6);
        toast('Location captured', { type: 'ok' });
        return;
      }
      case 'srcAdd': {
        try {
          S().CustomSources = await rpc('sources.add', {
            name: body.querySelector('[data-k="srcName"]').value,
            baseUrl: body.querySelector('[data-k="srcUrl"]').value,
            isSfw: body.querySelector('[data-k="srcSfw"]').checked,
          });
          broadcast('rating');
          render();
          toast('Board added', { type: 'ok' });
        } catch (err) { toast(err.message, { type: 'err' }); }
        return;
      }
      case 'updCheck': return checkUpdate();
      case 'updInstall': return installUpdate();
      case 'gh': return rpc('shell.open', { url: 'https://github.com/Kwan-desu/wallsafe' });
      case 'exit':
        if (await confirm('Exit WallSafe?', 'The panic hotkey, slideshow and auto-safe triggers will stop until you open it again.', { okLabel: 'Exit', danger: true, iconName: 'power' }))
          rpc('app.exit');
        return;
    }
  });

  // ── hotkey recorder ──
  const MOD_VK = new Set([16, 17, 18, 91, 92, 93]);
  const startRecording = btn => {
    if (recording) return stopRecording();
    const disp = body.querySelector('[data-k="hkdisplay"]');
    disp.classList.add('recording');
    disp.innerHTML = '<kbd>Press keys…</kbd>';
    btn.innerHTML = `${icon('x')} Cancel`;
    const onKey = async e => {
      e.preventDefault(); e.stopPropagation();
      if (e.key === 'Escape') return stopRecording();
      const parts = [e.ctrlKey && 'Ctrl', e.altKey && 'Alt', e.shiftKey && 'Shift', e.metaKey && 'Win'].filter(Boolean);
      if (MOD_VK.has(e.keyCode)) { disp.innerHTML = [...parts, '…'].map(k => `<kbd>${k}</kbd>`).join(''); return; }
      const isFn = e.keyCode >= 0x70 && e.keyCode <= 0x7B;
      if (!parts.length && !isFn) { toast('Use at least one modifier (Ctrl, Alt, Shift or Win)', { type: 'warn', timeout: 2200 }); return; }
      const mods = (e.altKey ? 1 : 0) | (e.ctrlKey ? 2 : 0) | (e.shiftKey ? 4 : 0) | (e.metaKey ? 8 : 0);
      const r = await rpc('hotkey.set', { mods, vk: e.keyCode });
      S().HotkeyDisplay = r.display;
      S().HotkeyModifiers = mods; S().HotkeyKey = e.keyCode;
      emit('ui:setting', { key: 'HotkeyDisplay' });
      toast('Panic hotkey updated', { type: 'ok', sub: r.display });
      stopRecording();
    };
    document.addEventListener('keydown', onKey, true);
    recording = { onKey, btn };
  };
  const stopRecording = () => {
    if (!recording) return;
    document.removeEventListener('keydown', recording.onKey, true);
    recording.btn.innerHTML = `${icon('keyboard')} Change`;
    const disp = body.querySelector('[data-k="hkdisplay"]');
    disp.classList.remove('recording');
    disp.innerHTML = (S().HotkeyDisplay || '').split(' + ').map(k => `<kbd>${esc(k)}</kbd>`).join('');
    recording = null;
  };

  // ── updates ──
  let upd = null;
  const checkUpdate = async () => {
    const title = body.querySelector('[data-k="updTitle"]'), desc = body.querySelector('[data-k="updDesc"]');
    const btn = body.querySelector('[data-k="updCheck"]');
    btn.disabled = true;
    title.textContent = 'Checking…';
    try {
      upd = await rpc('update.check');
      if (upd.available) {
        title.textContent = `Update available — v${upd.latest}`;
        desc.textContent = upd.title || 'A new version of WallSafe is ready.';
        body.querySelector('[data-k="updInstall"]').hidden = !upd.canInstall;
      } else {
        title.textContent = "You're up to date";
        desc.textContent = `v${upd.current} is the latest release.`;
      }
    } catch (e) {
      title.textContent = 'Update check failed';
      desc.textContent = e.message;
    } finally { btn.disabled = false; }
  };
  const installUpdate = async () => {
    if (!upd?.downloadUrl) return;
    body.querySelector('[data-k="updBar"]').hidden = false;
    body.querySelector('[data-k="updInstall"]').disabled = true;
    try { await rpc('update.install', { downloadUrl: upd.downloadUrl }); }
    catch (e) { toast('Update failed', { type: 'err', sub: e.message }); body.querySelector('[data-k="updInstall"]').disabled = false; }
  };
  on('updateProgress', d => { const b = body.querySelector('[data-k="updBar"] > div'); if (b) b.style.width = (d.progress * 100).toFixed(0) + '%'; });
  on('wifiStatus', st => { const w = body.querySelector('[data-k="wifiStatus"]'); if (w) w.innerHTML = wifiStatusHtml(st); });

  // ── section nav ──
  const nav = el.querySelector('.settings-nav');
  nav.addEventListener('click', e => {
    const a = e.target.closest('[data-sec]');
    if (!a) return;
    e.preventDefault();
    body.querySelector('#set-' + a.dataset.sec)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  });
  el.addEventListener('scroll', () => {
    let cur = SECTIONS[0][0];
    for (const [id] of SECTIONS) {
      const g = body.querySelector('#set-' + id);
      if (g && g.getBoundingClientRect().top - el.getBoundingClientRect().top < 140) cur = id;
    }
    nav.querySelectorAll('[data-sec]').forEach(a => a.classList.toggle('active', a.dataset.sec === cur));
  }, { passive: true });

  return {
    el,
    async show(params) {
      stopRecording();
      Object.assign(S(), await rpc('settings.get'));
      state.slideshow = await rpc('slideshow.state');
      await render();
      el.dispatchEvent(new Event('scroll'));
      if (params.section) setTimeout(() => body.querySelector('#set-' + params.section)?.scrollIntoView({ block: 'start' }), 50);
    },
    refresh() { /* settings re-render on show */ },
  };
}

function wifiStatusHtml(st) {
  if (!st) return '';
  if (!st.enabled) return `<div class="status-box">${icon('wifi')}<div><b>Connected to ${esc(st.network || 'no network')}</b>Turn on the trigger to start monitoring.</div></div>`;
  if (st.triggerActive && st.bypassed) return `<div class="status-box warn">${icon('alert')}<div><b>Trigger bypassed</b>On “${esc(st.matched || st.network)}”, but you revealed your normal wallpaper with the hotkey.</div></div>`;
  if (st.triggerActive) return `<div class="status-box ok">${icon('shieldCheck')}<div><b>Safe wallpaper active</b>Connected to trigger network “${esc(st.matched || st.network)}”.</div></div>`;
  return `<div class="status-box">${icon('wifi')}<div><b>Monitoring</b>${st.network ? `Connected to “${esc(st.network)}” — not a trigger network.` : 'No network connection detected.'}</div></div>`;
}
