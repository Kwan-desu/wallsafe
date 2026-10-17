// Explore: infinite booru feed with search, source, sort and resolution filters.
import { rpc } from '../bridge.js';
import { h, esc, prettyTag, toast } from '../ui.js';
import { icon } from '../icons.js';
import { S, track } from '../state.js';
import { renderCards, installCardHandlers, skeletons } from '../cards.js';
import { attachSuggest } from '../search.js';

const SORTS = [['order:score', 'Top rated', 'star'], ['', 'Newest', 'clock'], ['order:random', 'Random', 'sparkles']];
const RES = [[0, 'Any size'], [1920, '1080p+'], [2560, '1440p+'], [3840, '4K+']];
const PAGE = 30;

export function create() {
  const el = h(`<section class="page explore">
      <div class="page-head" style="margin-bottom:6px">
        <div><h1>Explore</h1><p>Stream wallpapers from every connected board.</p></div>
      </div>
      <div class="toolbar">
        <div class="search-wrap">
          <span data-icon="search"></span>
          <input class="input" data-e="search" type="search" placeholder="Search tags — e.g. scenery, genshin, miku" autocomplete="off" spellcheck="false" />
          <button class="btn ghost sm icon clear" data-e="clear" title="Clear" hidden>${icon('x')}</button>
          <div class="suggest" data-e="suggest" hidden></div>
        </div>
        <div class="chips" data-e="sorts">${SORTS.map(([v, l, ic]) => `<button class="chip" data-sort="${v}">${icon(ic)}${l}</button>`).join('')}</div>
        <span class="grow"></span>
        <select class="select" data-e="source" title="Source"></select>
        <select class="select" data-e="res" title="Minimum resolution">${RES.map(([v, l]) => `<option value="${v}">${l}</option>`).join('')}</select>
        <button class="chip" data-e="wp" title="Landscape images that fit a desktop">${icon('monitor')}Desktop fit</button>
        <button class="btn icon" data-e="refresh" title="Refresh (F5)">${icon('refresh')}</button>
      </div>
      <div class="result-info" data-e="info"></div>
      <div class="grid" data-e="grid"></div>
      <div class="load-more" data-e="more"><div class="spinner"></div></div>
      <div class="sentinel" data-e="sentinel"></div>
    </section>`);

  const q = n => el.querySelector(`[data-e="${n}"]`);
  const grid = q('grid'), more = q('more'), info = q('info');
  installCardHandlers(el);

  const f = {
    tags: '', sort: 'order:score', source: 'all',
    minWidth: +(localStorage.getItem('ws.minWidth') || 0),
    wallpaperOnly: localStorage.getItem('ws.wpOnly') === '1',
  };
  let page = 1, loading = false, done = false, gen = 0, total = 0, initialized = false;

  const fillSources = () => {
    const mode = S().RatingMode ?? 0;
    const opts = [['all', 'All sources'], ['konasfw', 'Konachan (SFW)']];
    if (mode > 0) opts.push(['yande', 'yande.re'], ['konansfw', 'Konachan.com']);
    for (const c of S().CustomSources || []) if (c.enabled && (mode > 0 || c.isSfw)) opts.push([c.id, c.name]);
    q('source').innerHTML = opts.map(([v, l]) => `<option value="${esc(v)}">${esc(l)}</option>`).join('');
    if (!opts.some(o => o[0] === f.source)) f.source = 'all';
    q('source').value = f.source;
  };

  const syncControls = () => {
    q('sorts').querySelectorAll('[data-sort]').forEach(b => b.classList.toggle('active', b.dataset.sort === f.sort));
    q('res').value = String(f.minWidth);
    q('wp').classList.toggle('active', f.wallpaperOnly);
    q('search').value = f.tags;
    q('clear').hidden = !f.tags;
  };

  const renderInfo = () => {
    const parts = [];
    if (f.tags) parts.push(`<span class="tag-pill">${icon('tag')} ${esc(prettyTag(f.tags))}<button data-e="untag" title="Clear search">${icon('x')}</button></span>`);
    parts.push(`<span>${total ? `${total} wallpapers loaded` : loading ? 'Loading…' : ''}</span>`);
    info.innerHTML = parts.join('');
  };

  const reload = () => {
    gen++;
    page = 1; done = false; total = 0; loading = false;
    grid.innerHTML = skeletons(12);
    el.scrollTop = 0;
    syncControls();
    loadMore(true);
  };

  const loadMore = async (first = false) => {
    if (loading || done) return;
    loading = true;
    more.hidden = first;
    renderInfo();
    const my = gen;
    try {
      const posts = track(await rpc('posts.fetch', {
        source: f.source, sort: f.sort, tags: f.tags, page, limit: PAGE,
        minWidth: f.minWidth, wallpaperOnly: f.wallpaperOnly,
      }));
      if (my !== gen) return;
      if (first) grid.replaceChildren();
      renderCards(grid, posts, { append: true });
      total += posts.length;
      page++;
      // Filters can empty a page while more exist; stop only on a truly empty response streak.
      if (posts.length === 0) done = page > 3 || (!f.minWidth && !f.wallpaperOnly);
      if (first && posts.length === 0 && done) showEmpty();
    } catch (e) {
      if (my !== gen) return;
      if (first) grid.innerHTML = '';
      done = true;
      toast('Could not load wallpapers', { type: 'err', sub: e.message });
      if (!total) showEmpty(true);
    } finally {
      if (my === gen) {
        loading = false;
        more.hidden = done;
        renderInfo();
        // Keep filling if the page isn't scrollable yet.
        requestAnimationFrame(() => { if (!done && !el.hidden && el.scrollHeight <= el.clientHeight + 400) loadMore(); });
      }
    }
  };

  const showEmpty = (error = false) => {
    grid.innerHTML = `<div class="empty" style="grid-column:1/-1">
        <div class="e-icon">${icon(error ? 'wifi' : 'search')}</div>
        <h3>${error ? 'Boards unreachable' : 'Nothing matched'}</h3>
        <p>${error ? 'Check your internet connection, then refresh.' : 'Try a different tag, source or a lower minimum resolution.'}</p>
        <button class="btn" data-e="reset">${icon('refresh')} Reset filters</button></div>`;
  };

  new IntersectionObserver(es => { if (es[0].isIntersecting && !el.hidden && initialized) loadMore(); }, { root: el, rootMargin: '900px' })
    .observe(q('sentinel'));

  // ── controls ──
  attachSuggest(q('search'), q('suggest'), tag => { f.tags = tag; reload(); });
  q('search').addEventListener('input', () => { q('clear').hidden = !q('search').value; });
  q('clear').addEventListener('click', () => { f.tags = ''; reload(); });
  q('sorts').addEventListener('click', e => { const b = e.target.closest('[data-sort]'); if (b && b.dataset.sort !== f.sort) { f.sort = b.dataset.sort; reload(); } });
  q('source').addEventListener('change', e => { f.source = e.target.value; reload(); });
  q('res').addEventListener('change', e => { f.minWidth = +e.target.value; localStorage.setItem('ws.minWidth', f.minWidth); reload(); });
  q('wp').addEventListener('click', () => { f.wallpaperOnly = !f.wallpaperOnly; localStorage.setItem('ws.wpOnly', f.wallpaperOnly ? '1' : '0'); reload(); });
  q('refresh').addEventListener('click', reload);
  el.addEventListener('click', e => {
    const a = e.target.closest('[data-e]')?.dataset.e;
    if (a === 'untag') { f.tags = ''; reload(); }
    if (a === 'reset') { f.tags = ''; f.minWidth = 0; f.wallpaperOnly = false; f.sort = 'order:score'; f.source = 'all'; localStorage.setItem('ws.minWidth', 0); localStorage.setItem('ws.wpOnly', '0'); fillSources(); reload(); }
  });

  const loadProfile = () => {
    const prof = S().FilterProfile || {};
    f.source = prof.Source || 'all';
    f.sort = prof.SortTag ?? 'order:score';
    if (!SORTS.some(s => s[0] === f.sort)) f.sort = 'order:score';
    fillSources();
  };

  return {
    el,
    show(params) {
      let fresh = false;
      if (!initialized) { initialized = true; loadProfile(); fresh = true; }
      if (params.tags !== undefined) {
        const changed = params.tags !== f.tags;
        f.tags = params.tags;
        history.replaceState(null, '', '#/explore');
        if (changed || fresh) reload();
      } else if (fresh) reload();
    },
    refresh(reason, visible) {
      if (!initialized) return;
      if (reason === 'rating') { loadProfile(); reload(); }
      if (reason === 'manual' && visible) reload();
    },
  };
}
