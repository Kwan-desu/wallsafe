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
      pending.delete(m.id);
      m.ok ? resolve(m.result) : reject(new Error(m.error || 'Request failed'));
    } else if (m.event) {
      emit(m.event, m.data);
    }
  });
}

export async function rpc(method, params = {}) {
  if (!wv) {
    mock ??= (await import('./mock.js')).createMock(emit);
    return mock(method, params);
  }
  return new Promise((resolve, reject) => {
    const id = ++seq;
    pending.set(id, { resolve, reject });
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
