// Wallpaper cards + grid behaviour (delegated actions, HQ upgrade, preview navigation).
import { h, esc } from './ui.js';
import { icon } from './icons.js';
import { emit } from './bridge.js';
import { state, isVeiled, toggleFavorite, applyWallpaper, downloadPost, postMenu, refreshCards } from './state.js';

const HQ_KEY = 'ws.hqThumbs';
export const hqThumbs = () => localStorage.getItem(HQ_KEY) !== '0';
export const setHqThumbs = v => localStorage.setItem(HQ_KEY, v ? '1' : '0');

const RATING = { q: ['q', '16+'], e: ['e', '18+'] };

// Upgrade to the sample image once a card is actually on screen.
const hqObserver = new IntersectionObserver(entries => {
  for (const en of entries) {
    if (!en.isIntersecting) continue;
    hqObserver.unobserve(en.target);
    const card = en.target;
    const post = state.posts.get(card.dataset.key);
    if (!post?.sample || post.sample === post.thumb) continue;
    const img = new Image();
    img.draggable = false;
    img.decoding = 'async';
    img.className = 'hq';
    img.alt = '';
    img.onload = () => {
      card.querySelector('img:not(.hq)')?.after(img);
      card.classList.add('loaded');
    };
    img.src = post.sample;
  }
}, { rootMargin: '200px' });

export function card(post, i = 0, { menuExtra } = {}) {
  const r = RATING[post.rating];
  const el = h(`<article class="card ${isVeiled(post) ? 'veiled' : ''}" data-key="${esc(post.key)}" style="--i:${i % 12}" tabindex="0" draggable="true">
      
      <img alt="" loading="lazy" decoding="async" draggable="false" />
      <div class="veil"><span data-icon="eyeOff"></span><span>Sensitive · hover to reveal</span></div>
      <div class="shade"></div>
      <div class="top">
        ${r ? `<span class="badge ${r[0]}">${r[1]}</span>` : ''}
        <span class="fav-dot" ${post.isFavorite ? '' : 'hidden'}>${icon('heart')}</span>
      </div>
      <div class="bottom">
        <div class="meta"><b>${post.width ? `${post.width} × ${post.height}` : 'Local file'}</b><span>${esc(post.sourceName || '')}${post.aspect ? ' · ' + esc(post.aspect) : ''}</span></div>
        <div class="actions">
          <button class="act" data-act="fav" title="Favorite">${icon('heart')}</button>
          <button class="act" data-act="download" title="Download"><span data-icon="download"></span></button>
          <button class="act" data-act="more" title="More">${icon('more')}</button>
          <button class="act primary" data-act="apply" title="Set as wallpaper">${icon('monitor')}</button>
        </div>
      </div>
      <div class="progress"></div>
    </article>`);
  const img = el.querySelector('img');
  img.onload = () => el.classList.add('loaded');
  img.onerror = () => { if (post.local && img.src !== post.local) img.src = post.local; else el.classList.add('loaded'); };
  img.src = post.thumb || post.sample;
  if (menuExtra) el._menuExtra = menuExtra;
  if (hqThumbs()) hqObserver.observe(el);
  queueMicrotask(() => refreshCards(post.key));
  return el;
}

/** Render a list of posts into a container (replace or append). */
export function renderCards(container, posts, { append = false, menuExtra } = {}) {
  if (!append) container.replaceChildren();
  const start = container.children.length;
  const frag = document.createDocumentFragment();
  posts.forEach((p, i) => frag.appendChild(card(p, start + i, { menuExtra: menuExtra?.(p) })));
  container.appendChild(frag);
}

/** One delegated handler for every card grid / shelf on the page. */
export function installCardHandlers(root) {
  root.addEventListener('click', e => {
    const c = e.target.closest('.card');
    if (!c || !root.contains(c)) return;
    const post = state.posts.get(c.dataset.key);
    if (!post) return;
    const act = e.target.closest('[data-act]')?.dataset.act;
    e.stopPropagation();
    switch (act) {
      case 'fav': return toggleFavorite(post);
      case 'apply': return applyWallpaper(post);
      case 'download': return downloadPost(post);
      case 'more': return postMenu(post, e.target.closest('[data-act]'), c._menuExtra || []);
      default: return openPreviewFrom(c);
    }
  });
  root.addEventListener('contextmenu', e => {
    const c = e.target.closest('.card');
    if (!c) return;
    e.preventDefault();
    const post = state.posts.get(c.dataset.key);
    if (post) postMenu(post, { x: e.clientX, y: e.clientY }, c._menuExtra || []);
  });
  root.addEventListener('keydown', e => {
    const c = e.target.closest?.('.card');
    if (c && (e.key === 'Enter' || e.key === ' ')) { e.preventDefault(); openPreviewFrom(c); }
  });
}

function openPreviewFrom(c) {
  const container = c.parentElement;
  const keys = [...container.querySelectorAll(':scope > .card')].map(x => x.dataset.key);
  const list = keys.map(k => state.posts.get(k)).filter(Boolean);
  emit('ui:preview', { post: state.posts.get(c.dataset.key), list });
}

/** Re-evaluate discretion veils after the setting changes. */
export function refreshVeils(root = document) {
  root.querySelectorAll('.card[data-key]').forEach(c => {
    const p = state.posts.get(c.dataset.key);
    c.classList.toggle('veiled', isVeiled(p));
  });
}

export function skeletons(n = 12) {
  return Array.from({ length: n }, () => '<div class="skeleton-card"></div>').join('');
}
