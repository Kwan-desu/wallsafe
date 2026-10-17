// Tag autocomplete shared by the title-bar search and the Explore toolbar.
import { rpc } from './bridge.js';
import { esc, fmtCount, debounce } from './ui.js';
import { S } from './state.js';

const RECENT_KEY = 'ws.recentSearches';
export const recentSearches = () => { try { return JSON.parse(localStorage.getItem(RECENT_KEY)) || []; } catch { return []; } };
export function pushRecent(tag) {
  if (!tag) return;
  const r = [tag, ...recentSearches().filter(t => t !== tag)].slice(0, 6);
  localStorage.setItem(RECENT_KEY, JSON.stringify(r));
}

/**
 * Wire an <input> to a suggestion list.
 * onSubmit(tag) is called with the resolved booru tag ('' clears the search).
 */
export function attachSuggest(input, box, onSubmit) {
  let items = [];
  let active = -1;
  let reqId = 0;

  const render = (list, heading) => {
    items = list;
    active = -1;
    if (!list.length) { box.hidden = true; return; }
    box.innerHTML = (heading ? `<div class="s-head">${esc(heading)}</div>` : '') + list.map((s, i) => `
      <div class="s-item" role="option" data-i="${i}">
        <span class="s-dot" style="background:${esc(s.color || '#94a3b8')}"></span>
        <span class="s-text">${esc(s.name)}${s.title ? `<small>${esc(s.title)}</small>` : ''}</span>
        <span class="s-cat">${esc(s.category || '')}</span>
        ${s.count ? `<span class="s-count">${fmtCount(s.count)}</span>` : ''}
      </div>`).join('');
    box.hidden = false;
  };

  const highlight = () => box.querySelectorAll('.s-item').forEach((el, i) => el.classList.toggle('active', i === active));

  const showRecent = () => {
    const r = recentSearches();
    render(r.map(t => ({ name: t, category: 'Recent', color: 'var(--accent)' })), r.length ? 'Recent searches' : '');
  };

  const fetchRemote = debounce(async (q, id) => {
    try {
      const src = S().FilterProfile?.Source || 'konasfw';
      const res = await rpc('tags.suggest', { query: q, source: src, remote: true });
      if (id === reqId && input.value.trim() === q) render(res);
    } catch { /* offline: keep local results */ }
  }, 220);

  input.addEventListener('input', async () => {
    const q = input.value.trim();
    const id = ++reqId;
    if (!q) { showRecent(); return; }
    try {
      const local = await rpc('tags.suggest', { query: q, remote: false });
      if (id === reqId) render(local);
    } catch { }
    fetchRemote(q, id);
  });

  input.addEventListener('focus', () => { if (!input.value.trim()) showRecent(); });
  input.addEventListener('blur', () => setTimeout(() => { box.hidden = true; }, 150));

  input.addEventListener('keydown', async e => {
    if (e.key === 'ArrowDown' && items.length) { e.preventDefault(); active = (active + 1) % items.length; highlight(); }
    else if (e.key === 'ArrowUp' && items.length) { e.preventDefault(); active = (active - 1 + items.length) % items.length; highlight(); }
    else if (e.key === 'Escape') { box.hidden = true; input.blur(); }
    else if (e.key === 'Enter') {
      e.preventDefault();
      let tag;
      if (active >= 0 && items[active]) tag = items[active].name;
      else {
        const q = input.value.trim();
        tag = q ? await rpc('tags.resolve', { query: q }).catch(() => q.replace(/ /g, '_')) : '';
      }
      submit(tag);
    }
  });

  box.addEventListener('pointerdown', e => {
    const it = e.target.closest('.s-item');
    if (!it) return;
    e.preventDefault();
    submit(items[+it.dataset.i]?.name || '');
  });

  function submit(tag) {
    reqId++;
    box.hidden = true;
    input.value = tag;
    pushRecent(tag);
    onSubmit(tag);
  }
}
