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
      <div class="feed-end" data-e="end" hidden></div>
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
  let page = 1, loading = false, done = false, gen = 0, total = 0, initialized = false, emptyStreak = 0;
  const seen = new Set();
  const MAX_EMPTY_PAGES = 4;  // pages in a row with nothing new before we call it the end

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
    page = 1; done = false; total = 0; loading = false; emptyStreak = 0;
    seen.clear();
    grid.innerHTML = skeletons(12);
    q('end').hidden = true;
    el.scrollTop = 0;
    syncControls();
    loadMore(true);
  };

  const showEnd = (kind, msg) => {
    more.hidden = true;
    const end = q('end');
    end.hidden = false;
    end.innerHTML = kind === 'error'
      ? `${icon('wifi')}<span>${esc(msg)}</span><button class="btn sm" data-e="retry">${icon('refresh')} Try again</button>`
      : `${icon('check')}<span>No more wallpapers found${f.tags ? ` for “${esc(prettyTag(f.tags))}”` : ''}.</span>`;
  };

  // The sentinel can stay inside the observer margin after a load, in which case the
  // IntersectionObserver never fires again. Check its position directly instead.
  const nearBottom = () => !el.hidden && q('sentinel').getBoundingClientRect().top - el.getBoundingClientRect().bottom < 900;

  const loadMore = async (first = false) => {
    if (loading || done) return;
    loading = true;
    more.hidden = first;
    q('end').hidden = true;
    renderInfo();
    const my = gen;
    let failed = null;
    try {
      const res = await rpc('posts.fetch', {
        source: f.source, sort: f.sort, tags: f.tags, page, limit: PAGE,
        minWidth: f.minWidth, wallpaperOnly: f.wallpaperOnly, meta: true,
      });
      if (my !== gen) return;
      const fresh = track(res.items).filter(p => !seen.has(p.key));
      fresh.forEach(p => seen.add(p.key));
      if (first) grid.replaceChildren();
      renderCards(grid, fresh, { append: true });
      total += fresh.length;
      page++;
      if (res.raw === 0) done = true;                       // the board has no more pages
      else if (fresh.length === 0) done = ++emptyStreak >= MAX_EMPTY_PAGES; // filtered out / duplicates
      else emptyStreak = 0;
      if (done && total === 0) showEmpty();
      else if (done) showEnd('end');
    } catch (e) {
      if (my !== gen) return;
      failed = e;
      if (!total) { grid.innerHTML = ''; showEmpty(true); }
      else showEnd('error', 'Couldn’t load more wallpapers.');
      done = true;
    } finally {
      if (my === gen) {
        loading = false;
        if (done || failed) more.hidden = true;
        renderInfo();
        if (!done && !failed) requestAnimationFrame(() => { if (nearBottom()) loadMore(); });
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
  el.addEventListener('scroll', () => { if (initialized && !loading && !done && nearBottom()) loadMore(); }, { passive: true });

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
    if (a === 'retry') { done = false; loadMore(); }
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
      else if (!loading && !done) requestAnimationFrame(() => { if (nearBottom()) loadMore(); });
    },
    refresh(reason, visible) {
      if (!initialized) return;
      if (reason === 'rating') { loadProfile(); reload(); }
      if (reason === 'manual' && visible) reload();
    },
  };
}
