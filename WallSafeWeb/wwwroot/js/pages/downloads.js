// Downloads: original-resolution files saved to disk.
import { rpc } from '../bridge.js';
import { h, esc, confirm, toast } from '../ui.js';
import { icon } from '../icons.js';
import { S, track } from '../state.js';
import { renderCards, installCardHandlers } from '../cards.js';
import { navigate } from '../app.js';

export function create() {
  const el = h(`<section class="page downloads">
      <div class="page-head">
        <div><h1>Downloads</h1><p data-d="sub"></p></div>
        <div class="head-actions">
          <button class="btn" data-d="folder">${icon('folder')} Open folder</button>
          <button class="btn primary" data-d="slideshow">${icon('play')} Play as slideshow</button>
        </div>
      </div>
      <div class="grid" data-d="grid"></div>
    </section>`);

  const q = n => el.querySelector(`[data-d="${n}"]`);
  const grid = q('grid');
  installCardHandlers(el);
  let dirty = true;

  const load = async () => {
    dirty = false;
    const posts = track(await rpc('downloads.list'));
    q('sub').innerHTML = `${posts.length} file${posts.length === 1 ? '' : 's'} · <span style="font-family:ui-monospace,Consolas,monospace;font-size:12px">${esc(S().DownloadFolder || '')}</span>`;
    if (!posts.length) {
      grid.innerHTML = `<div class="empty" style="grid-column:1/-1">
          <div class="e-icon">${icon('download')}</div><h3>No downloads yet</h3>
          <p>Download originals to keep them offline at full resolution — perfect for the slideshow.</p>
          <button class="btn primary" data-go>${icon('compass')} Find wallpapers</button></div>`;
      return;
    }
    renderCards(grid, posts, {
      menuExtra: p => ['-', { label: 'Delete file', icon: 'trash', danger: true, onClick: () => remove(p) }],
    });
  };

  const remove = async p => {
    if (!(await confirm('Delete this download?', 'The file will be removed from your downloads folder.', { okLabel: 'Delete', danger: true, iconName: 'trash' }))) return;
    await rpc('downloads.delete', { key: p.key });
    p.isDownloaded = false;
    toast('Download deleted', { iconName: 'trash', timeout: 1800 });
  };

  el.addEventListener('click', async e => {
    if (e.target.closest('[data-d="folder"]')) rpc('downloads.openFolder');
    if (e.target.closest('[data-go]')) navigate('#/explore');
    if (e.target.closest('[data-d="slideshow"]')) {
      await rpc('slideshow.config', { source: 'downloads' });
      await rpc('slideshow.set', { enabled: true });
      toast('Slideshow started', { type: 'ok', sub: 'From your downloads', iconName: 'play' });
    }
  });

  return {
    el,
    show() { if (dirty) load(); },
    refresh(reason, visible) {
      if (reason !== 'downloads' && reason !== 'manual' && reason !== 'settings') return;
      if (visible) load(); else dirty = true;
    },
  };
}
