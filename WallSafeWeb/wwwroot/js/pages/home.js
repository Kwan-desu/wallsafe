// Home: hero carousel, stats, trending / franchise / favorites shelves.
import { rpc, emit } from '../bridge.js';
import { h, esc, fmtCount, wireShelf } from '../ui.js';
import { icon } from '../icons.js';
import { state, S, track, isVeiled, applyWallpaper } from '../state.js';
import { renderCards, installCardHandlers } from '../cards.js';
import { navigate, setAmbient } from '../app.js';

export function create() {
  const el = h(`<section class="page home">
      <div class="hero" data-h="hero">
        <div class="hero-slides"></div>
        <div class="hero-body">
          <div class="hero-eyebrow"><span data-icon="flame"></span><span data-h="eyebrow">Trending now</span></div>
          <h1 data-h="greeting">Welcome back</h1>
          <p data-h="sub">Fresh high-resolution art, ready for your desktop.</p>
          <div class="hero-actions">
            <button class="btn primary lg" data-h="apply"><span data-icon="monitor"></span>Set as wallpaper</button>
            <button class="btn glass lg" data-h="preview"><span data-icon="maximize"></span>Preview</button>
            <button class="btn glass lg icon" data-h="next" title="Next"><span data-icon="chevronRight"></span></button>
          </div>
        </div>
        <div class="hero-dots"></div>
      </div>

      <div class="stats" data-h="stats">
        <div class="stat c1"><div class="s-icon">${icon('heart')}</div><div class="s-val" data-s="favorites">–</div><div class="s-label">Favorites</div></div>
        <div class="stat c2"><div class="s-icon">${icon('download')}</div><div class="s-val" data-s="downloads">–</div><div class="s-label">Downloads</div></div>
        <div class="stat c3"><div class="s-icon">${icon('monitor')}</div><div class="s-val" data-s="applied">–</div><div class="s-label">Wallpapers applied</div></div>
        <div class="stat c4"><div class="s-icon">${icon('star')}</div><div class="s-val" data-s="topTag">–</div><div class="s-label">Top pick</div></div>
        <div class="stat c5"><div class="s-icon">${icon('shield')}</div><div class="s-val" data-s="panic">–</div><div class="s-label">Panic triggers</div></div>
      </div>

      <div class="section" data-h="trendingSec">
        <div class="section-head">
          <h2>Trending wallpapers</h2><span class="sub" data-h="trendSub"></span><span class="grow"></span>
          <button class="btn sm ghost" data-go="#/explore">See all ${icon('chevronRight')}</button>
          <div class="shelf-nav"><button class="btn sm icon" data-prev>${icon('chevronLeft')}</button><button class="btn sm icon" data-next>${icon('chevronRight')}</button></div>
        </div>
        <div class="shelf" data-h="trending"></div>
      </div>

      <div class="section" data-h="franchiseSec">
        <div class="section-head">
          <h2>Popular universes</h2><span class="sub" data-h="franSub">Live ranking</span><span class="grow"></span>
          <div class="shelf-nav"><button class="btn sm icon" data-prev>${icon('chevronLeft')}</button><button class="btn sm icon" data-next>${icon('chevronRight')}</button></div>
        </div>
        <div class="shelf" data-h="franchises"></div>
      </div>

      <div class="section" data-h="favSec">
        <div class="section-head">
          <h2>Your favorites</h2><span class="sub" data-h="favSub"></span><span class="grow"></span>
          <button class="btn sm ghost" data-go="#/favorites">Open library ${icon('chevronRight')}</button>
          <div class="shelf-nav"><button class="btn sm icon" data-prev>${icon('chevronLeft')}</button><button class="btn sm icon" data-next>${icon('chevronRight')}</button></div>
        </div>
        <div class="shelf" data-h="favs"></div>
      </div>
    </section>`);

  const q = n => el.querySelector(`[data-h="${n}"]`);
  installCardHandlers(el);
  for (const sec of el.querySelectorAll('.section')) wireShelf(sec.querySelector('.shelf'), sec.querySelector('[data-prev]'), sec.querySelector('[data-next]'));
  el.addEventListener('click', e => { const g = e.target.closest('[data-go]'); if (g) navigate(g.dataset.go); });

  // ── hero carousel ──
  let heroPosts = [], heroIndex = 0, heroTimer = null;
  const slides = q('hero').querySelector('.hero-slides');
  const dots = q('hero').querySelector('.hero-dots');

  const showHero = i => {
    if (!heroPosts.length) return;
    heroIndex = (i + heroPosts.length) % heroPosts.length;
    slides.querySelectorAll('.hero-slide').forEach((s, k) => s.classList.toggle('on', k === heroIndex));
    dots.querySelectorAll('button').forEach((d, k) => d.classList.toggle('on', k === heroIndex));
    const p = heroPosts[heroIndex];
    q('hero').classList.toggle('veiled', isVeiled(p));
    q('eyebrow').textContent = `Trending · ${p.sourceName} · ${p.width}×${p.height}`;
    if (!el.hidden) setAmbient(isVeiled(p) ? null : p.thumb);
  };
  const restartTimer = () => { clearInterval(heroTimer); heroTimer = setInterval(() => { if (!el.hidden && !document.hidden) showHero(heroIndex + 1); }, 8000); };

  const renderHero = posts => {
    heroPosts = posts.filter(p => p.width >= p.height).slice(0, 6);
    if (!heroPosts.length) heroPosts = posts.slice(0, 6);
    slides.innerHTML = heroPosts.map(p => `<div class="hero-slide"><img alt="" decoding="async" src="${esc(p.sample || p.thumb)}"></div>`).join('');
    dots.innerHTML = heroPosts.map((_, i) => `<button aria-label="Slide ${i + 1}" data-dot="${i}"></button>`).join('');
    showHero(0);
    restartTimer();
  };
  dots.addEventListener('click', e => { const d = e.target.closest('[data-dot]'); if (d) { showHero(+d.dataset.dot); restartTimer(); } });
  q('next').addEventListener('click', () => { showHero(heroIndex + 1); restartTimer(); });
  q('apply').addEventListener('click', () => heroPosts[heroIndex] && applyWallpaper(heroPosts[heroIndex]));
  q('preview').addEventListener('click', () => heroPosts[heroIndex] && emit('ui:preview', { post: heroPosts[heroIndex], list: heroPosts }));

  // ── data ──
  let loadedMode = null;

  const greet = () => {
    const hr = new Date().getHours();
    const part = hr < 5 ? 'Good night' : hr < 12 ? 'Good morning' : hr < 18 ? 'Good afternoon' : 'Good evening';
    const name = (S().UserName || '').trim();
    q('greeting').textContent = S().ShowGreeting !== false && name ? `${part}, ${name}` : part;
    const mode = ['Safe-for-work', '16+', '18+'][S().RatingMode ?? 0];
    q('sub').textContent = `${mode} picks from the boards, refreshed live. Press ${S().HotkeyDisplay || 'your panic hotkey'} anytime to go safe.`;
  };

  const applyVisibility = () => {
    const s = S();
    q('hero').hidden = s.ShowHomeHero === false;
    q('stats').hidden = s.ShowHomeStats === false;
    q('trendingSec').hidden = s.ShowHomeTrending === false;
    q('franchiseSec').hidden = s.ShowHomeFranchises === false;
    q('favSec').hidden = s.ShowHomeQuickShelf === false;
  };

  const loadStats = async () => {
    try {
      const st = await rpc('home.stats');
      for (const k of ['favorites', 'downloads', 'applied', 'topTag', 'panic']) {
        const v = el.querySelector(`[data-s="${k}"]`);
        v.textContent = typeof st[k] === 'number' ? fmtCount(st[k]) : st[k];
        v.title = String(st[k]);
      }
    } catch { }
  };

  const loadTrending = async () => {
    const shelf = q('trending');
    shelf.innerHTML = Array.from({ length: 6 }, () => '<div class="skeleton-card"></div>').join('');
    const sfw = (S().RatingMode ?? 0) === 0;
    q('trendSub').textContent = sfw ? 'Konachan · top score' : 'All boards · top score';
    try {
      const posts = track(await rpc('posts.fetch', { source: sfw ? 'konasfw' : 'all', sort: 'order:score', page: 1, limit: 20, remember: false }));
      renderCards(shelf, posts);
      renderHero(posts);
      if (!posts.length) shelf.innerHTML = emptyShelf('wifi', 'Could not reach the image boards', 'Check your connection and press F5 to retry.');
    } catch (e) {
      shelf.innerHTML = emptyShelf('alert', 'Trending unavailable', e.message);
    }
  };

  const loadFranchises = async () => {
    const shelf = q('franchises');
    shelf.innerHTML = Array.from({ length: 5 }, () => '<div class="skeleton-card" style="aspect-ratio:16/9"></div>').join('');
    q('franSub').textContent = 'Live ranking · ' + ((S().RatingMode ?? 0) === 0 ? 'SFW sources' : 'all sources');
    try {
      const list = await rpc('home.franchises', { limit: 16 });
      shelf.innerHTML = list.map((f, i) => `
        <div class="franchise" data-tag="${esc(f.tag)}" style="--i:${i}" title="${esc(f.name)}">
          <img alt="" decoding="async">
          <span class="f-type badge ${f.type === 'Gaming' ? 'gaming' : 'anime'}">${esc(f.type)}</span>
          <div class="f-info"><div class="f-name">${esc(f.name)}</div>
            <div class="f-sub">${icon('image')} ${fmtCount(f.count)} artworks</div></div>
        </div>`).join('') || emptyShelf('globe', 'No universes found', 'The board did not return a ranking.');
      // Lazy, sequential previews so we don't hammer the board.
      const imgs = [...shelf.querySelectorAll('.franchise')];
      for (const card of imgs) {
        if (loadedMode !== (S().RatingMode ?? 0)) break;
        try {
          const url = await rpc('franchise.preview', { tag: card.dataset.tag });
          if (url) { const im = card.querySelector('img'); im.onload = () => im.classList.add('ok'); im.src = url; }
        } catch { }
      }
    } catch (e) {
      shelf.innerHTML = emptyShelf('alert', 'Rankings unavailable', e.message);
    }
  };
  q('franchises').addEventListener('click', e => {
    const f = e.target.closest('.franchise');
    if (f) navigate('#/explore?tags=' + encodeURIComponent(f.dataset.tag));
  });

  const loadFavorites = async () => {
    const shelf = q('favs');
    try {
      const posts = track(await rpc('favorites.list', { collection: '' })).slice(0, 24);
      q('favSub').textContent = posts.length ? `${posts.length}${posts.length === 24 ? '+' : ''} saved` : '';
      if (posts.length) renderCards(shelf, posts);
      else shelf.innerHTML = emptyShelf('heart', 'No favorites yet', 'Tap the heart on any wallpaper to keep it here.');
    } catch { }
  };

  const emptyShelf = (ic, title, text) => `<div class="empty" style="grid-column:1/-1;width:min(560px,80vw);padding:36px 20px">
      <div class="e-icon">${icon(ic)}</div><h3>${esc(title)}</h3><p>${esc(text)}</p></div>`;

  const loadAll = () => {
    loadedMode = S().RatingMode ?? 0;
    greet(); applyVisibility(); loadStats(); loadTrending(); loadFranchises(); loadFavorites();
  };

  return {
    el,
    show() {
      greet(); applyVisibility();
      if (loadedMode === null) loadAll();
      else loadStats();
    },
    resumeAmbient() { const p = heroPosts[heroIndex]; setAmbient(p && !isVeiled(p) ? p.thumb : null); },
    refresh(reason, visible) {
      if (reason === 'rating' || reason === 'manual' && visible) { loadAll(); return; }
      if (reason === 'favorites') { loadFavorites(); loadStats(); }
      if (reason === 'downloads' || reason === 'applied' || reason === 'panic' || reason === 'history') loadStats();
      if (reason === 'settings') { greet(); applyVisibility(); }
      if (reason === 'discretion') showHero(heroIndex);
    },
  };
}
