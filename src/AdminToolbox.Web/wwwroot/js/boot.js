// SPDX-License-Identifier: MIT
// Runs before first paint: theme, language and the small JS API used by Blazor (window.atb).
(() => {
  'use strict';
  const SUPPORTED = ['de', 'en', 'tr'];
  const root = document.documentElement;

  const store = {
    get(key) { try { return localStorage.getItem(key); } catch { return null; } },
    set(key, value) { try { localStorage.setItem(key, value); return true; } catch { return false; } },
    remove(key) { try { localStorage.removeItem(key); return true; } catch { return false; } },
  };

  const applyTheme = (theme) => {
    if (theme === 'light' || theme === 'dark') root.setAttribute('data-theme', theme);
    else root.removeAttribute('data-theme');
  };

  const lang = () => {
    const saved = store.get('atb.lang');
    if (SUPPORTED.includes(saved)) return saved;
    for (const l of navigator.languages || [navigator.language || 'en']) {
      const code = String(l).slice(0, 2).toLowerCase();
      if (SUPPORTED.includes(code)) return code;
    }
    return 'en';
  };

  window.atb = {
    get: store.get,
    set: store.set,
    remove: store.remove,
    lang,
    setLang(value) {
      if (!SUPPORTED.includes(value)) return;
      store.set('atb.lang', value);
      location.reload();
    },
    theme: () => store.get('atb.theme') || 'system',
    setTheme(value) {
      store.set('atb.theme', value);
      applyTheme(value);
    },
    async copy(text) {
      try { await navigator.clipboard.writeText(text); return true; } catch { return false; }
    },
    focus(id) {
      const el = document.getElementById(id);
      if (!el) return;
      el.scrollIntoView({ block: 'start' });
      el.focus({ preventScroll: true });
    },
  };

  applyTheme(window.atb.theme());
  root.lang = lang();

  // Offline support for the helpers (PWA). Registered after load so it never competes with startup.
  if ('serviceWorker' in navigator) {
    window.addEventListener('load', () => {
      navigator.serviceWorker.register('service-worker.js').catch(() => { /* offline mode unavailable */ });
    });
  }
})();
