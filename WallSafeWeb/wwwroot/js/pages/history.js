// History: timeline of wallpapers applied to the desktop.
import { rpc, emit } from '../bridge.js';
import { h, esc, prettyTag, toast } from '../ui.js';
import { icon } from '../icons.js';
import { state, track, isVeiled, applyWallpaper, postMenu } from '../state.js';
import { navigate } from '../app.js';

const dayKey = d => { const x = new Date(d); x.setHours(0, 0, 0, 0); return +x; };

function dayLabel(ts) {
  const today = dayKey(Date.now());
  if (ts === today) return 'Today';
  if (ts === today - 864e5) return 'Yesterday';
  return new Date(ts).toLocaleDateString(undefined, { weekday: 'long', month: 'long', day: 'numeric' });
}

function titleOf(p) {
  const tags = (p.tags || '').split(' ').filter(t => t && !/^(highres|absurdres|\d+girls?|solo|tagme)$/.test(t));
  return tags.length ? tags.slice(0, 3).map(prettyTag).join(' · ') : (p.localPath ? p.localPath.split(/[\\/]/).pop() : 'Wallpaper');
}

export function create() {
  const el = h(`<section class="page history">
      <div class="page-head">
        <div><h1>History</h1><p data-x="sub">Every wallpaper you've applied, newest first.</p></div>
        <div class="head-actions"><button class="btn danger" data-x="clear">${icon('trash')} Clear history</button></div>
      </div>
      <div data-x="list"></div>
    </section>`);

  const q = n => el.querySelector(`[data-x="${n}"]`);
  let dirty = true;
  let posts = [];

  const load = async () => {
    dirty = false;
    posts = track(await rpc('history.list'));
    q('clear').hidden = !posts.length;
    q('sub').textContent = posts.length ? `${posts.length} wallpaper${posts.length === 1 ? '' : 's'} applied` : "Every wallpaper you've applied, newest first.";
    if (!posts.length) {
      q('list').innerHTML = `<div class="empty"><div class="e-icon">${icon('clock')}</div><h3>No history yet</h3>
        <p>Wallpapers you set from WallSafe show up here so you can bring them back in one click.</p>
        <button class="btn primary" data-go>${icon('compass')} Explore</button></div>`;
      return;
    }
    const groups = new Map();
    for (const p of posts) {
      const k = p.appliedAt ? dayKey(p.appliedAt) : 0;
      if (!groups.has(k)) groups.set(k, []);
      groups.get(k).push(p);
    }
    let i = 0;
    q('list').innerHTML = [...groups].map(([k, items]) => `
      <div class="timeline-day"><h3>${k ? esc(dayLabel(k)) : 'Earlier'}</h3>
        ${items.map(p => `
          <div class="hist-row" data-key="${esc(p.key)}" style="--i:${i++ % 14}">
            <div class="h-thumb ${isVeiled(p) ? 'veiled' : ''}" data-h="preview"><img alt="" loading="lazy" src="${esc(p.local || p.thumb)}"></div>
            <div class="h-info">
              <div class="h-title">${esc(titleOf(p))}</div>
              <div class="h-sub">${p.appliedAt ? new Date(p.appliedAt).toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' }) + ' · ' : ''}${p.width ? `${p.width}×${p.height} · ` : ''}${esc(p.sourceName || '')}</div>
            </div>
            <div class="h-actions">
              <button class="btn sm primary" data-h="apply">${icon('undo')} Re-apply</button>
              <button class="btn sm icon" data-h="more">${icon('more')}</button>
            </div>
          </div>`).join('')}
      </div>`).join('');
    q('list').querySelectorAll('.h-thumb img').forEach(img => {
      img.onerror = () => { const p = state.posts.get(img.closest('[data-key]').dataset.key); if (p && img.src !== p.thumb) img.src = p.thumb; };
    });
  };

  el.addEventListener('click', async e => {
    if (e.target.closest('[data-go]')) return navigate('#/explore');
    if (e.target.closest('[data-x="clear"]')) {
      await rpc('history.clear');
      toast('History cleared', { iconName: 'trash', timeout: 6000, actionLabel: 'Undo', action: () => rpc('history.undoClear') });
      return;
    }
    const row = e.target.closest('.hist-row');
    if (!row) return;
    const p = state.posts.get(row.dataset.key);
    const act = e.target.closest('[data-h]')?.dataset.h;
    if (act === 'apply') applyWallpaper(p);
    else if (act === 'more') postMenu(p, e.target.closest('[data-h]'));
    else if (act === 'preview') emit('ui:preview', { post: p, list: posts });
  });

  return {
    el,
    show() { if (dirty) load(); },
    refresh(reason, visible) {
      if (!['history', 'manual', 'discretion', 'applied'].includes(reason)) return;
      if (visible) load(); else dirty = true;
    },
  };
}
