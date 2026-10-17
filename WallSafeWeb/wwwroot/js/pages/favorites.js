// Favorites: local library organised into collections.
import { rpc } from '../bridge.js';
import { h, esc, prompt, confirm, menu, toast } from '../ui.js';
import { icon } from '../icons.js';
import { track, isVeiled } from '../state.js';
import { renderCards, installCardHandlers } from '../cards.js';
import { navigate } from '../app.js';

export function create() {
  const el = h(`<section class="page favorites">
      <div class="page-head">
        <div><h1>Favorites</h1><p data-f="sub">Your personal wallpaper library.</p></div>
        <div class="head-actions">
          <button class="btn" data-f="new">${icon('folderPlus')} New collection</button>
          <button class="btn primary" data-f="slideshow">${icon('play')} Play as slideshow</button>
        </div>
      </div>
      <div class="col-shelf" data-f="cols"></div>
      <div class="section-head" style="margin-top:6px"><h2 data-f="gridTitle">All favorites</h2><span class="sub" data-f="gridCount"></span></div>
      <div class="grid" data-f="grid"></div>
    </section>`);

  const q = n => el.querySelector(`[data-f="${n}"]`);
  const grid = q('grid');
  installCardHandlers(el);

  q('cols').addEventListener('wheel', e => {
    const c = q('cols');
    if (c.scrollWidth <= c.clientWidth || Math.abs(e.deltaX) > Math.abs(e.deltaY)) return;
    e.preventDefault();
    c.scrollBy({ left: e.deltaY, behavior: 'smooth' });
  }, { passive: false });

  let active = '';        // '' = all, '__unsorted', or collection name
  let dirty = true;

  const renderCols = async () => {
    const [c, all] = await Promise.all([rpc('favorites.collections'), rpc('favorites.list', { collection: '' })]);
    track(all);
    if (active && active !== '__unsorted' && !c.items.some(i => i.name === active)) active = '';
    q('sub').textContent = c.all ? `${c.all} saved wallpaper${c.all === 1 ? '' : 's'} · ${c.items.length} collection${c.items.length === 1 ? '' : 's'}` : 'Your personal wallpaper library.';
    const unsorted = all.filter(p => !p.collection);
    const img = p => p ? `<img alt="" loading="lazy" src="${esc(p.thumb || p.local)}" class="${isVeiled(p) ? 'veiled' : ''}">` : '';
    const mosaic = list => `<div class="ct-cover mosaic n${Math.min(list.length, 3)}">${list.slice(0, 3).map(img).join('')}</div>`;
    const tile = (col, name, count, cover, ic, named) => `
      <button class="col-tile ${active === col ? 'active' : ''}" data-col="${esc(col)}" ${named ? 'data-named="1"' : ''} data-drop="${col === '' ? 'favorite' : col === '__unsorted' ? 'unsorted' : 'collection'}" title="${esc(name)}">
        <div class="ct-drop">${icon(col === '' ? 'heart' : 'folder')}<span>Drop to ${col === '' ? 'favorite' : col === '__unsorted' ? 'unsort' : 'add'}</span></div>
        ${cover}
        <div class="ct-info"><span class="ct-icon">${icon(ic)}</span><div><div class="ct-name">${esc(name)}</div><div class="ct-count">${count} wallpaper${count === 1 ? '' : 's'}</div></div></div>
      </button>`;
    q('cols').innerHTML =
      tile('', 'All favorites', c.all, mosaic(all), 'heart') +
      tile('__unsorted', 'Unsorted', c.unsorted, mosaic(unsorted), 'layers') +
      c.items.map(i => {
        const items = all.filter(p => p.collection === i.name);
        const cover = items.length ? mosaic(items) : `<div class="ct-cover">${i.cover ? `<img alt="" loading="lazy" src="${esc(i.cover)}">` : ''}</div>`;
        return tile(i.name, i.name, i.count, cover, 'folder', true);
      }).join('') +
      `<button class="col-tile add" data-f="new" data-drop="new"><div class="ct-add">${icon('folderPlus')}<span>New collection</span></div></button>`;
    q('cols').querySelectorAll('img').forEach(im => { im.onerror = () => im.remove(); });
  };

  const renderGrid = async () => {
    const posts = track(await rpc('favorites.list', { collection: active }));
    q('gridTitle').textContent = active === '' ? 'All favorites' : active === '__unsorted' ? 'Unsorted' : active;
    q('gridCount').textContent = posts.length ? `${posts.length} wallpaper${posts.length === 1 ? '' : 's'}` : '';
    if (!posts.length) {
      grid.innerHTML = `<div class="empty" style="grid-column:1/-1">
          <div class="e-icon">${icon(active ? 'folder' : 'heart')}</div>
          <h3>${active ? 'This collection is empty' : 'No favorites yet'}</h3>
          <p>${active ? 'Drag wallpapers onto this collection above, or use “Move to collection” from a card’s menu.' : 'Tap the heart on wallpapers you love — they’ll be saved here, offline.'}</p>
          ${active ? '' : `<button class="btn primary" data-go="explore">${icon('compass')} Explore wallpapers</button>`}</div>`;
      return;
    }
    renderCards(grid, posts, {
      menuExtra: p => p.collection ? ['-', { label: `Remove from “${p.collection}”`, icon: 'x', onClick: () => rpc('collections.assign', { key: p.key, collection: '' }) }] : [],
    });
  };

  const load = async () => { dirty = false; await renderCols(); await renderGrid(); };

  const newCollection = async () => {
    const name = await prompt('New collection', { placeholder: 'e.g. Cozy rooms', okLabel: 'Create', iconName: 'folderPlus' });
    if (!name) return;
    const ok = await rpc('collections.create', { name });
    if (!ok) { toast('A collection with that name already exists', { type: 'warn' }); return; }
    active = name;
    load();
  };

  el.addEventListener('click', async e => {
    if (e.target.closest('[data-f="new"]')) return newCollection();
    if (e.target.closest('[data-go]')) return navigate('#/explore');
    if (e.target.closest('[data-f="slideshow"]')) {
      const source = active === '' || active === '__unsorted' ? 'favorites' : 'collection:' + active;
      await rpc('slideshow.config', { source });
      await rpc('slideshow.set', { enabled: true });
      toast('Slideshow started', { type: 'ok', sub: active && active !== '__unsorted' ? `From “${active}”` : 'From all favorites', iconName: 'play' });
      return;
    }
    const col = e.target.closest('[data-col]');
    if (col) { active = col.dataset.col; load(); }
  });

  q('cols').addEventListener('contextmenu', e => {
    const col = e.target.closest('[data-named]');
    if (!col) return;
    e.preventDefault();
    const name = col.dataset.col;
    menu([
      { label: 'Rename…', icon: 'pencil', onClick: async () => {
        const nn = await prompt('Rename collection', { value: name, okLabel: 'Rename' });
        if (nn && nn !== name) {
          if (await rpc('collections.rename', { name, newName: nn })) { if (active === name) active = nn; load(); }
          else toast('That name is already used', { type: 'warn' });
        }
      } },
      { label: 'Delete collection', icon: 'trash', danger: true, onClick: async () => {
        if (await confirm(`Delete “${name}”?`, 'Wallpapers inside stay in your favorites and move to Unsorted.', { okLabel: 'Delete', danger: true, iconName: 'trash' })) {
          await rpc('collections.delete', { name });
          if (active === name) active = '';
          load();
        }
      } },
    ], { x: e.clientX, y: e.clientY });
  });

  return {
    el,
    show() { if (dirty) load(); },
    refresh(reason, visible) {
      if (reason !== 'favorites' && reason !== 'manual') return;
      if (visible) load(); else dirty = true;
    },
  };
}
