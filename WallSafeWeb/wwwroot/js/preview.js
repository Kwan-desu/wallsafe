// Full-screen wallpaper viewer: progressive load, zoom/pan, keyboard navigation, actions.
import { h, esc, fmtCount, prettyTag, menu } from './ui.js';
import { icon, setIcon } from './icons.js';
import { rpc, on, emit } from './bridge.js';
import { state, isVeiled, toggleFavorite, applyWallpaper, downloadPost, setLockScreen, assignCollection } from './state.js';

let current = null; // { el, list, index, cleanup }

export function openPreview(post, list) {
  if (!post) return;
  list = list?.length ? list : [post];
  let index = Math.max(0, list.findIndex(p => p.key === post.key));
  if (current) close();

  const el = h(`<div class="viewer ${localStorage.getItem('ws.viewerPanel') === '0' ? 'no-panel' : ''}" role="dialog" aria-modal="true">
      <div class="viewer-bg"></div>
      <div class="viewer-stage">
        <div class="viewer-top">
          <button class="btn glass icon" data-v="close" title="Close (Esc)">${icon('x')}</button>
          <span class="grow"></span>
          <button class="btn glass icon" data-v="panel" title="Toggle details (I)">${icon('panel')}</button>
        </div>
        <button class="viewer-nav prev" data-v="prev" title="Previous (←)">${icon('chevronLeft')}</button>
        <img alt="" draggable="false" />
        <button class="viewer-nav next" data-v="next" title="Next (→)">${icon('chevronRight')}</button>
        <div class="viewer-zoom">
          <button class="btn ghost sm icon" data-v="zout" title="Zoom out">${icon('zoomOut')}</button>
          <span data-v="zlabel">Fit</span>
          <button class="btn ghost sm icon" data-v="zin" title="Zoom in">${icon('zoomIn')}</button>
          <button class="btn ghost sm" data-v="zfit">Fit</button>
        </div>
      </div>
      <aside class="viewer-panel"></aside>
    </div>`);
  document.getElementById('layer').appendChild(el);

  const stage = el.querySelector('.viewer-stage');
  const img = stage.querySelector('img');
  const panel = el.querySelector('.viewer-panel');
  const zlabel = el.querySelector('[data-v="zlabel"]');
  let zoom = 1, tx = 0, ty = 0, loadToken = 0;

  const applyTransform = () => {
    img.style.transform = `translate(${tx}px, ${ty}px) scale(${zoom})`;
    zlabel.textContent = zoom === 1 ? 'Fit' : Math.round(zoom * 100) + '%';
  };
  const resetZoom = () => { zoom = 1; tx = ty = 0; applyTransform(); };
  const setZoom = (z, cx, cy) => {
    const nz = Math.min(8, Math.max(1, z));
    if (cx !== undefined) {
      const r = stage.getBoundingClientRect();
      const ox = cx - r.left - r.width / 2 - tx, oy = cy - r.top - r.height / 2 - ty;
      tx -= ox * (nz / zoom - 1); ty -= oy * (nz / zoom - 1);
    }
    zoom = nz;
    if (zoom === 1) tx = ty = 0;
    applyTransform();
  };

  const show = () => {
    const p = list[index];
    const token = ++loadToken;
    resetZoom();
    el.querySelector('.viewer-bg').style.backgroundImage = `url("${p.thumb}")`;
    img.classList.toggle('veiled', isVeiled(p));
    img.classList.add('low');
    img.style.animation = 'none'; void img.offsetWidth; img.style.animation = '';
    img.src = p.thumb || p.sample;
    const hi = new Image();
    hi.onload = () => { if (token === loadToken) { img.src = hi.src; img.classList.remove('low'); } };
    hi.src = p.local || p.sample || p.thumb;
    el.querySelector('.prev').style.visibility = index > 0 ? '' : 'hidden';
    el.querySelector('.next').style.visibility = index < list.length - 1 ? '' : 'hidden';
    renderPanel(p);
  };

  const renderPanel = p => {
    const tags = (p.tags || '').split(' ').filter(Boolean).slice(0, 40);
    panel.innerHTML = `
      <h3>${p.width ? `${p.width} × ${p.height}` : 'Wallpaper'}</h3>
      <div class="vp-sub">${esc(p.sourceName || '')}${p.aspect ? ' · ' + esc(p.aspect) : ''}${p.rating && p.rating !== 's' ? ` · <span class="badge ${p.rating}">${p.rating === 'q' ? '16+' : '18+'}</span>` : ''}</div>
      <div class="vp-actions">
        <button class="btn primary wide" data-a="apply">${icon('monitor')} Set as wallpaper</button>
        <button class="btn" data-a="fav">${icon('heart')} <span>${p.isFavorite ? 'Favorited' : 'Favorite'}</span></button>
        <button class="btn" data-a="download"><span data-icon="${p.isDownloaded ? 'check' : 'download'}">${icon(p.isDownloaded ? 'check' : 'download')}</span> <span>${p.isDownloaded ? 'Downloaded' : 'Download'}</span></button>
        <button class="btn" data-a="lock">${icon('lock')} Lock screen</button>
        <button class="btn" data-a="more">${icon('more')} More</button>
      </div>
      <div class="vp-facts">
        <div class="vp-fact"><small>Score</small><b>${fmtCount(p.score || 0)}</b></div>
        <div class="vp-fact"><small>Artist / uploader</small><b>${esc(p.author || '—')}</b></div>
        <div class="vp-fact"><small>Source</small><b>${esc(p.sourceName || '—')}</b></div>
        <div class="vp-fact"><small>Post</small><b>#${p.id || '—'}</b></div>
      </div>
      ${p.collection ? `<div class="vp-label">Collection</div><div class="chips"><span class="chip sm">${icon('folder')} ${esc(p.collection)}</span></div>` : ''}
      ${tags.length ? `<div class="vp-label">Tags</div><div class="vp-tags">${tags.map(t => `<button class="chip" data-tag="${esc(t)}">${esc(prettyTag(t))}</button>`).join('')}</div>` : ''}
    `;
    const fav = panel.querySelector('[data-a="fav"]');
    if (p.isFavorite) fav.querySelector('svg').style.fill = '#fb7185';
  };

  panel.addEventListener('click', async e => {
    const p = list[index];
    const tag = e.target.closest('[data-tag]')?.dataset.tag;
    if (tag) { close(); location.hash = '#/explore?tags=' + encodeURIComponent(tag); return; }
    const a = e.target.closest('[data-a]')?.dataset.a;
    if (a === 'apply') applyWallpaper(p);
    if (a === 'fav') { await toggleFavorite(p); renderPanel(p); }
    if (a === 'download') { await downloadPost(p); renderPanel(p); }
    if (a === 'lock') setLockScreen(p);
    if (a === 'more') {
      const monitors = await rpc('monitors.list').catch(() => []);
      const anchor = e.target.closest('[data-a]');
      menu([
        ...(monitors.length > 1 ? [{ header: 'Apply to display' }, ...monitors.map(m => ({ label: m.name, icon: 'monitor', onClick: () => applyWallpaper(p, m.index) })), '-'] : []),
        { label: 'Move to collection…', icon: 'folder', onClick: async () => menu(await assignCollection(p), anchor) },
        p.isDownloaded ? { label: 'Show in folder', icon: 'folder', onClick: () => rpc('post.reveal', { key: p.key }) } : null,
        p.postUrl ? { label: 'Open source page', icon: 'external', onClick: () => rpc('shell.open', { url: p.postUrl }) } : null,
      ], anchor, { align: 'end' });
    }
  });

  // Zoom & pan
  stage.addEventListener('wheel', e => {
    e.preventDefault();
    setZoom(zoom * (e.deltaY < 0 ? 1.18 : 1 / 1.18), e.clientX, e.clientY);
  }, { passive: false });
  img.addEventListener('dblclick', e => zoom > 1 ? resetZoom() : setZoom(2.5, e.clientX, e.clientY));
  let drag = null;
  stage.addEventListener('pointerdown', e => {
    if (e.target !== img || zoom === 1) return;
    drag = { x: e.clientX - tx, y: e.clientY - ty };
    stage.classList.add('dragging');
    stage.setPointerCapture(e.pointerId);
  });
  stage.addEventListener('pointermove', e => { if (drag) { tx = e.clientX - drag.x; ty = e.clientY - drag.y; applyTransform(); } });
  stage.addEventListener('pointerup', () => { drag = null; stage.classList.remove('dragging'); });
  stage.addEventListener('click', e => { if (e.target === stage) close(); });

  el.addEventListener('click', e => {
    const v = e.target.closest('[data-v]')?.dataset.v;
    if (v === 'close') close();
    if (v === 'prev' && index > 0) { index--; show(); }
    if (v === 'next' && index < list.length - 1) { index++; show(); }
    if (v === 'zin') setZoom(zoom * 1.4);
    if (v === 'zout') setZoom(zoom / 1.4);
    if (v === 'zfit') resetZoom();
    if (v === 'panel') {
      el.classList.toggle('no-panel');
      localStorage.setItem('ws.viewerPanel', el.classList.contains('no-panel') ? '0' : '1');
    }
  });

  const onKey = e => {
    if (document.querySelector('.modal')) return;
    if (e.key === 'Escape') { if (document.querySelector('.popover')) return; close(); }
    else if (e.key === 'ArrowLeft' && index > 0) { index--; show(); }
    else if (e.key === 'ArrowRight' && index < list.length - 1) { index++; show(); }
    else if (e.key.toLowerCase() === 'i') el.querySelector('[data-v="panel"]').click();
    else if (e.key.toLowerCase() === 'f') { toggleFavorite(list[index]).then(() => renderPanel(list[index])); }
    else return;
    e.preventDefault();
  };
  document.addEventListener('keydown', onKey);
  const offFav = on('favoritesChanged', () => renderPanel(list[index]));

  current = { el, cleanup: () => { document.removeEventListener('keydown', onKey); offFav(); } };
  show();
}

export function close() {
  if (!current) return;
  const c = current;
  current = null;
  c.cleanup();
  c.el.style.animation = 'fade .18s reverse forwards';
  setTimeout(() => c.el.remove(), 170);
}

on('ui:preview', ({ post, list }) => openPreview(post, list));
on('panicChanged', d => { if (d?.active) close(); });
