// Host bridge: request/response RPC + pushed events over WebView2 web messages.
// When opened in a plain browser (design work), a mock backend is loaded instead.

const wv = window.chrome?.webview;
export const isHost = !!wv;

let seq = 0;
const pending = new Map();
const listeners = new Map();
let mock = null;

if (wv) {
  wv.addEventListener('message', e => {
    const m = e.data;
    if (!m) return;
    if (m.id !== undefined && m.id !== null && pending.has(m.id)) {
      const { resolve, reject } = pending.get(m.id);
      m.ok ? resolve(m.result) : reject(new Error(m.error || 'Request failed'));
    } else if (m.event) {
      emit(m.event, m.data);
    }
  });
}

// Calls that wait on the user (file pickers, prompts) or long downloads get no deadline.
const NO_TIMEOUT = /^(dialog\.|update\.install|post\.download|location\.current)/;
const TIMEOUT_MS = 45000;

export async function rpc(method, params = {}) {
  if (!wv) {
    mock ??= (await import('./mock.js')).createMock(emit);
    return mock(method, params);
  }
  return new Promise((resolve, reject) => {
    const id = ++seq;
    let timer = null;
    const done = fn => v => { clearTimeout(timer); pending.delete(id); fn(v); };
    pending.set(id, { resolve: done(resolve), reject: done(reject) });
    if (!NO_TIMEOUT.test(method)) {
      timer = setTimeout(() => {
        if (pending.has(id)) { pending.delete(id); reject(new Error('The request timed out')); }
      }, TIMEOUT_MS);
    }
    wv.postMessage({ id, method, params });
  });
}

export function on(event, fn) {
  if (!listeners.has(event)) listeners.set(event, new Set());
  listeners.get(event).add(fn);
  return () => listeners.get(event)?.delete(fn);
}

export function emit(event, data) {
  listeners.get(event)?.forEach(fn => { try { fn(data); } catch (err) { console.error(event, err); } });
}
