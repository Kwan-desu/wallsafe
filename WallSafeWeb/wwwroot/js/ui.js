// Small UI toolkit: DOM helpers, toasts, menus, popovers, modals, formatting.
import { icon, hydrateIcons } from './icons.js';

export const $ = (sel, root = document) => root.querySelector(sel);
export const $$ = (sel, root = document) => [...root.querySelectorAll(sel)];

const ESC = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };
export const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ESC[c]);

/** Build an element from an HTML string (first element child). */
export function h(html) {
  const t = document.createElement('template');
  t.innerHTML = html.trim();
  const el = t.content.firstElementChild;
  hydrateIcons(t.content);
  return el;
}

export const fmtCount = n => n >= 1e6 ? (n / 1e6).toFixed(1).replace(/\.0$/, '') + 'M'
  : n >= 1e3 ? (n / 1e3).toFixed(1).replace(/\.0$/, '') + 'k' : String(n ?? 0);

export const fmtBytes = b => {
  if (!b) return '0 B';
  const u = ['B', 'KB', 'MB', 'GB'];
  const i = Math.min(u.length - 1, Math.floor(Math.log(b) / Math.log(1024)));
  return (b / 1024 ** i).toFixed(i ? 1 : 0) + ' ' + u[i];
};

export const prettyTag = t => String(t || '').replace(/_/g, ' ').replace(/\b\w/g, c => c.toUpperCase());

export function timeAgo(date) {
  const d = new Date(date);
  const s = (Date.now() - d) / 1000;
  if (s < 60) return 'just now';
  if (s < 3600) return Math.floor(s / 60) + ' min ago';
  if (s < 86400) return Math.floor(s / 3600) + ' h ago';
  return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}

export const debounce = (fn, ms) => {
  let t;
  return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); };
};

// ── Toasts ──────────────────────────────────────────────────────
const TOAST_ICON = { info: 'info', ok: 'check', err: 'alert', warn: 'alert' };

export function toast(text, { type = 'info', sub = '', action, actionLabel, timeout = 3600, iconName } = {}) {
  const el = h(`<div class="toast ${type}" role="status">
      <div class="t-icon">${icon(iconName || TOAST_ICON[type] || 'info')}</div>
      <div class="t-text">${esc(text)}${sub ? `<small>${esc(sub)}</small>` : ''}</div>
      ${action ? `<button class="btn sm">${esc(actionLabel || 'Undo')}</button>` : ''}
    </div>`);
  const close = () => { el.classList.add('out'); setTimeout(() => el.remove(), 260); };
  el.querySelector('.btn')?.addEventListener('click', () => { action(); close(); });
  const host = $('#toasts');
  host.appendChild(el);
  while (host.children.length > 4) host.firstElementChild.remove();
  if (timeout) setTimeout(close, timeout);
  return { close, el };
}

// ── Popovers & menus ────────────────────────────────────────────
let openPop = null;

export function closePopover() {
  if (!openPop) return;
  const p = openPop;
  openPop = null;
  p.cleanup?.();
  p.el.remove();
}

/**
 * Show a floating panel next to an anchor element or at a point.
 * @returns the panel element
 */
export function popover(content, anchor, { align = 'start', className = '', onClose } = {}) {
  closePopover();
  const el = typeof content === 'string' ? h(`<div class="popover ${className}">${content}</div>`) : content;
  if (typeof content !== 'string') el.classList.add('popover', ...className.split(' ').filter(Boolean));
  $('#layer').appendChild(el);
  hydrateIcons(el);

  const r = anchor instanceof Element ? anchor.getBoundingClientRect()
    : { left: anchor.x, right: anchor.x, top: anchor.y, bottom: anchor.y, width: 0, height: 0 };
  const pw = el.offsetWidth, ph = el.offsetHeight;
  let x = align === 'end' ? r.right - pw : r.left;
  let y = r.bottom + 6;
  if (y + ph > innerHeight - 8) y = Math.max(8, r.top - ph - 6);
  x = Math.min(Math.max(8, x), innerWidth - pw - 8);
  el.style.left = x + 'px';
  el.style.top = y + 'px';
  el.style.transformOrigin = `${align === 'end' ? 'right' : 'left'} ${y < r.top ? 'bottom' : 'top'}`;

  const onDown = e => { if (!el.contains(e.target) && !(anchor instanceof Element && anchor.contains(e.target))) closePopover(); };
  const onKey = e => { if (e.key === 'Escape') closePopover(); };
  setTimeout(() => { document.addEventListener('pointerdown', onDown, true); });
  document.addEventListener('keydown', onKey);
  window.addEventListener('blur', closePopover, { once: true });
  openPop = {
    el, cleanup: () => {
      document.removeEventListener('pointerdown', onDown, true);
      document.removeEventListener('keydown', onKey);
      onClose?.();
    }
  };
  return el;
}

/**
 * items: [{ label, icon, onClick, danger, checked } | '-' | { header }]
 */
export function menu(items, anchor, opts) {
  const el = document.createElement('div');
  el.setAttribute('role', 'menu');
  for (const it of items) {
    if (!it) continue;
    if (it === '-') { el.appendChild(h('<div class="menu-sep"></div>')); continue; }
    if (it.header) { el.appendChild(h(`<div class="menu-label">${esc(it.header)}</div>`)); continue; }
    const b = h(`<button class="menu-item ${it.danger ? 'danger' : ''}" role="menuitem">
        ${it.icon ? icon(it.icon) : '<span style="width:16px"></span>'}<span>${esc(it.label)}</span>
        ${it.checked ? `<span class="check">${icon('check')}</span>` : ''}
      </button>`);
    b.addEventListener('click', () => { closePopover(); it.onClick?.(); });
    el.appendChild(b);
  }
  return popover(el, anchor, opts);
}

// ── Modals ──────────────────────────────────────────────────────
export function modal({ title, body = '', iconName, actions = [], onMount, dismissable = true }) {
  closePopover();
  return new Promise(resolve => {
    const dim = h('<div class="backdrop-dim"></div>');
    const m = h(`<div class="modal" role="dialog" aria-modal="true">
        ${iconName ? `<div class="m-icon">${icon(iconName)}</div>` : ''}
        <h3>${esc(title)}</h3>
        <div class="m-body">${body}</div>
        <div class="m-actions"></div>
      </div>`);
    const done = v => {
      document.removeEventListener('keydown', onKey, true);
      dim.remove(); m.remove();
      resolve(v);
    };
    const onKey = e => {
      if (e.key === 'Escape' && dismissable) { e.stopPropagation(); done(null); }
      if (e.key === 'Enter' && e.target.tagName !== 'TEXTAREA') {
        const primary = actions.find(a => a.primary);
        if (primary) { e.preventDefault(); done(primary.value?.(m) ?? true); }
      }
    };
    for (const a of actions) {
      const b = h(`<button class="btn ${a.primary ? 'primary' : ''} ${a.danger ? 'danger' : ''}">${esc(a.label)}</button>`);
      b.addEventListener('click', () => done(a.value ? a.value(m) : !!a.primary));
      m.querySelector('.m-actions').appendChild(b);
    }
    if (dismissable) dim.addEventListener('click', () => done(null));
    document.addEventListener('keydown', onKey, true);
    $('#layer').append(dim, m);
    hydrateIcons(m);
    onMount?.(m);
    (m.querySelector('input,textarea') || m.querySelector('.btn.primary'))?.focus();
  });
}

export async function prompt(title, { text = '', value = '', placeholder = '', okLabel = 'Save', iconName = 'pencil' } = {}) {
  const r = await modal({
    title, iconName,
    body: `${text ? `<p>${esc(text)}</p>` : ''}<input class="input" maxlength="60" placeholder="${esc(placeholder)}" value="${esc(value)}" />`,
    actions: [{ label: 'Cancel' }, { label: okLabel, primary: true, value: m => m.querySelector('input').value.trim() }],
    onMount: m => m.querySelector('input').select(),
  });
  return typeof r === 'string' ? r : null;
}

export async function confirm(title, text, { okLabel = 'Confirm', danger = false, iconName = 'alert' } = {}) {
  return (await modal({
    title, iconName, body: `<p>${esc(text)}</p>`,
    actions: [{ label: 'Cancel' }, { label: okLabel, primary: !danger, danger, value: () => true }],
  })) === true;
}

/** Horizontal wheel scrolling + arrow buttons for a shelf. */
export function wireShelf(shelf, prevBtn, nextBtn) {
  const update = () => {
    const max = shelf.scrollWidth - shelf.clientWidth - 2;
    if (prevBtn) prevBtn.disabled = shelf.scrollLeft <= 2;
    if (nextBtn) nextBtn.disabled = shelf.scrollLeft >= max;
    shelf.classList.toggle('at-end', shelf.scrollLeft >= max);
  };
  shelf.addEventListener('wheel', e => {
    if (Math.abs(e.deltaY) <= Math.abs(e.deltaX)) return;
    const max = shelf.scrollWidth - shelf.clientWidth;
    if ((e.deltaY < 0 && shelf.scrollLeft <= 0) || (e.deltaY > 0 && shelf.scrollLeft >= max)) return;
    e.preventDefault();
    shelf.scrollBy({ left: e.deltaY * 2.2, behavior: 'smooth' });
  }, { passive: false });
  shelf.addEventListener('scroll', update, { passive: true });
  new ResizeObserver(update).observe(shelf);
  const step = dir => shelf.scrollBy({ left: dir * shelf.clientWidth * 0.85, behavior: 'smooth' });
  prevBtn?.addEventListener('click', () => step(-1));
  nextBtn?.addEventListener('click', () => step(1));
  return update;
}
