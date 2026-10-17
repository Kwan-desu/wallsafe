// Drag & drop: drag any wallpaper card onto a collection, the sidebar or the floating dock.
import { rpc } from './bridge.js';
import { h, esc, toast } from './ui.js';
import { icon } from './icons.js';
import { state, toggleFavorite, applyWallpaper, downloadPost } from './state.js';

const MIME = 'application/x-wallsafe-post';
let dragged = null;        // post being dragged
let dock = null;
let collections = [];      // cached names for the dock

async function refreshCollections() {
  try { collections = (await rpc('favorites.collections')).items.map(i => i.name); } catch { }
}

function buildDock() {
  const el = h(`<div class="drop-dock" role="toolbar" aria-label="Drop targets">
      <div class="dd-group">
        <div class="dd-target" data-drop="apply">${icon('monitor')}<span>Set wallpaper</span></div>
        <div class="dd-target" data-drop="favorite">${icon('heart')}<span>Favorite</span></div>
        <div class="dd-target" data-drop="download">${icon('download')}<span>Download</span></div>
      </div>
      <div class="dd-sep"></div>
      <div class="dd-group dd-cols">
        ${collections.map(n => `<div class="dd-target col" data-drop="collection" data-col="${esc(n)}">${icon('folder')}<span>${esc(n)}</span></div>`).join('')}
        <div class="dd-target col new" data-drop="new">${icon('folderPlus')}<span>New collection</span></div>
      </div>
    </div>`);
  document.body.appendChild(el);
  requestAnimationFrame(() => el.classList.add('on'));
  return el;
}

function dragImage(card, post) {
  const ghost = h(`<div class="drag-ghost"><img alt="" src="${esc(post.thumb || post.local || '')}"><span>${post.width ? `${post.width}×${post.height}` : 'Wallpaper'}</span></div>`);
  document.body.appendChild(ghost);
  setTimeout(() => ghost.remove(), 0);
  return ghost;
}

function endDrag() {
  document.body.classList.remove('dragging-post');
  document.querySelectorAll('.drop-over').forEach(x => x.classList.remove('drop-over'));
  document.querySelectorAll('.card.drag-src').forEach(x => x.classList.remove('drag-src'));
  if (dock) { const d = dock; d.classList.remove('on'); setTimeout(() => d.remove(), 250); dock = null; }
  dragged = null;
}

async function perform(target, post) {
  const kind = target.dataset.drop;
  const name = n => n ? `“${n}”` : '';
  switch (kind) {
    case 'apply': return applyWallpaper(post);
    case 'download':
      if (post.isDownloaded) return toast('Already downloaded', { iconName: 'check', timeout: 1600 });
      return downloadPost(post);
    case 'favorite':
      if (post.isFavorite) return toast('Already in favorites', { iconName: 'heart', timeout: 1600 });
      return toggleFavorite(post);
    case 'unsorted':
      if (!post.collection) return;
      await rpc('collections.assign', { key: post.key, collection: '' });
      post.collection = '';
      return toast('Moved to Unsorted', { iconName: 'layers', timeout: 1800 });
    case 'new': {
      const { prompt } = await import('./ui.js');
      const n = await prompt('New collection', { placeholder: 'e.g. Cozy rooms', okLabel: 'Create & add', iconName: 'folderPlus' });
      if (!n) return;
      await rpc('collections.create', { name: n });
      await rpc('collections.assign', { key: post.key, collection: n });
      post.collection = n; post.isFavorite = true;
      return toast(`Added to ${name(n)}`, { type: 'ok', iconName: 'folder' });
    }
    case 'collection': {
      const col = target.dataset.col;
      if (post.collection === col) return toast(`Already in ${name(col)}`, { iconName: 'folder', timeout: 1600 });
      await rpc('collections.assign', { key: post.key, collection: col });
      const wasFav = post.isFavorite;
      post.collection = col; post.isFavorite = true;
      return toast(`${wasFav ? 'Moved' : 'Saved'} to ${name(col)}`, { type: 'ok', iconName: 'folder', timeout: 2200 });
    }
  }
}

export function installDnd() {
  refreshCollections();

  document.addEventListener('dragstart', e => {
    const card = e.target.closest?.('.card[data-key]');
    if (!card) { if (e.target.tagName === 'IMG') e.preventDefault(); return; }
    const post = state.posts.get(card.dataset.key);
    if (!post) { e.preventDefault(); return; }
    dragged = post;
    e.dataTransfer.effectAllowed = 'copyMove';
    e.dataTransfer.setData(MIME, post.key);
    e.dataTransfer.setData('text/uri-list', post.postUrl || '');
    try { e.dataTransfer.setDragImage(dragImage(card, post), 60, 40); } catch { }
    card.classList.add('drag-src');
    document.body.classList.add('dragging-post');
    // Show the dock on pages without in-page collection targets.
    if (!document.querySelector('.favorites:not([hidden])')) dock = buildDock();
  });

  document.addEventListener('dragend', endDrag);

  document.addEventListener('dragover', e => {
    const t = e.target.closest?.('[data-drop]');
    if (!dragged) { e.preventDefault(); e.dataTransfer.dropEffect = 'none'; return; } // block external file drops navigating
    if (!t) return;
    e.preventDefault();
    e.dataTransfer.dropEffect = t.dataset.drop === 'collection' || t.dataset.drop === 'unsorted' ? 'move' : 'copy';
    document.querySelectorAll('.drop-over').forEach(x => x !== t && x.classList.remove('drop-over'));
    t.classList.add('drop-over');
  });

  document.addEventListener('dragleave', e => {
    const t = e.target.closest?.('[data-drop]');
    if (t && !t.contains(e.relatedTarget)) t.classList.remove('drop-over');
  });

  document.addEventListener('drop', async e => {
    e.preventDefault();
    const t = e.target.closest?.('[data-drop]');
    const post = dragged;
    endDrag();
    if (!t || !post) return;
    t.classList.add('drop-done');
    setTimeout(() => t.classList.remove('drop-done'), 500);
    try { await perform(t, post); } catch (err) { toast('Could not complete that', { type: 'err', sub: err.message }); }
    refreshCollections();
  });
}

export { refreshCollections };
