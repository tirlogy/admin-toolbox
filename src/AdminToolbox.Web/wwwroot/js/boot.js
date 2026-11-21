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
  const current = lang();
  root.lang = current;

  // Deep links show a loading line instead of the static home page until Blazor renders the tool.
  // Path relative to <base href>, so the app also works under a sub-path (GitHub Pages preview).
  const basePath = new URL(document.baseURI).pathname;
  const path = (location.pathname.startsWith(basePath) ? location.pathname.slice(basePath.length) : location.pathname)
    .replace(/^\/+|\/+$/g, '');
  const isHome = path === '' || path === 'index.html';
  if (!isHome) root.classList.add('atb-deeplink');

  // Static shell texts for DE and TR. Kept equal to AppText.*.resx by LocalizationTests.
  /*SHELL*/
  const SHELL = {
    "de": {
      "nav": "Werkzeuge",
      "loading": "Lädt …",
      "tool.mail": "Mail-Domain-Check",
      "tool.mail.desc": "MX, SPF mit Lookup-Zählung, DKIM, DMARC, BIMI, MTA-STS und TLS-RPT mit Ampel und Vorschlägen.",
      "tool.header": "Header-Analyse",
      "tool.header.desc": "Route als Zeitleiste, SPF/DKIM/DMARC-Ergebnisse, Spam-Hinweise und verdächtige Abweichungen.",
      "tool.subnet": "Subnetzrechner",
      "tool.subnet.desc": "IPv4 und IPv6, Aufteilen, Zusammenfassen und Prüfung, ob eine IP im Netz liegt.",
      "tool.diff": "DNS-Vergleich",
      "tool.diff.desc": "Zonendatei von vorher gegen die Live-Abfrage. Ideal beim DNS-Umzug.",
      "tool.helpers": "Helfer",
      "tool.helpers.desc": "Passwörter, JWT, Base64/URL/Hex, Hashes und Zertifikate. Offline nutzbar.",
      "home.title": "Werkzeuge für IT-Admins",
      "home.lead": "Mail-Sicherheit, DNS und Netzwerke direkt im Browser prüfen. Ohne Konto, ohne Server, Eingaben bleiben lokal.",
      "home.privacy": "Nur Mail-Domain-Check und DNS-Vergleich stellen DNS-Abfragen an Cloudflare DNS (Fallback Google Public DNS). Alles andere läuft offline.",
      "privacy": "Datenschutz",
      "imprint": "Impressum"
    },
    "tr": {
      "nav": "Araçlar",
      "loading": "Yükleniyor …",
      "tool.mail": "E-posta alan adı kontrolü",
      "tool.mail.desc": "MX, sorgu sayımlı SPF, DKIM, DMARC, BIMI, MTA-STS ve TLS-RPT; trafik ışıkları ve önerilerle.",
      "tool.header": "Başlık analizi",
      "tool.header.desc": "Zaman çizelgesi olarak yol, SPF/DKIM/DMARC sonuçları, spam işaretleri ve şüpheli uyumsuzluklar.",
      "tool.subnet": "Alt ağ hesaplayıcı",
      "tool.subnet.desc": "IPv4 ve IPv6, bölme, birleştirme ve IP'nin ağda olup olmadığının kontrolü.",
      "tool.diff": "DNS karşılaştırma",
      "tool.diff.desc": "Önceki bölge dosyasını canlı DNS ile karşılaştırır. DNS taşımaları için ideal.",
      "tool.helpers": "Yardımcılar",
      "tool.helpers.desc": "Parolalar, JWT, Base64/URL/hex, özetler ve sertifikalar. Çevrimdışı çalışır.",
      "home.title": "BT yöneticileri için araçlar",
      "home.lead": "E-posta güvenliğini, DNS'i ve ağları doğrudan tarayıcıda kontrol edin. Hesap yok, sunucu yok, girdiler yerel kalır.",
      "home.privacy": "Yalnızca e-posta alan adı kontrolü ve DNS karşılaştırma, Cloudflare DNS'e (yedek Google Public DNS) DNS sorguları gönderir. Geri kalan her şey çevrimdışı çalışır.",
      "privacy": "Gizlilik",
      "imprint": "Künye"
    }
  };
  /*END*/

  // Title and intro of each tool page, shown statically on deep links until the tool has rendered.
  /*ROUTES*/
  const ROUTES = {
    "en": {
      "mail": [
        "Mail domain check",
        "Checks MX, SPF, DKIM, DMARC, BIMI, MTA-STS and TLS-RPT of a domain and suggests concrete improvements."
      ],
      "header": [
        "Mail header analysis",
        "Paste the headers of an e-mail. The analysis shows the route as a timeline, the SPF, DKIM and DMARC results, spam hints and suspicious mismatches."
      ],
      "subnet": [
        "Subnet calculator",
        "IPv4 and IPv6: network, broadcast, host range, splitting into subnets, summarizing and checking whether an IP is in the network."
      ],
      "dns-diff": [
        "DNS diff",
        "Compare records from before a migration (zone file or list) with live DNS. Shows missing, changed and new values."
      ],
      "helpers": [
        "Helpers",
        "Tools that run completely offline in the browser."
      ]
    },
    "de": {
      "mail": [
        "Mail-Domain-Check",
        "Prüft MX, SPF, DKIM, DMARC, BIMI, MTA-STS und TLS-RPT einer Domain und schlägt konkrete Verbesserungen vor."
      ],
      "header": [
        "Mail-Header-Analyse",
        "Header einer E-Mail einfügen. Die Analyse zeigt den Weg der Mail als Zeitleiste, die Ergebnisse von SPF, DKIM und DMARC, Spam-Hinweise und verdächtige Abweichungen."
      ],
      "subnet": [
        "Subnetzrechner",
        "IPv4 und IPv6: Netz, Broadcast, Hostbereich, Aufteilen in Subnetze, Zusammenfassen und Prüfen, ob eine IP im Netz liegt."
      ],
      "dns-diff": [
        "DNS-Vergleich",
        "Einträge vor einem Umzug (Zonendatei oder Liste) mit der Live-Abfrage vergleichen. Zeigt fehlende, geänderte und neue Werte."
      ],
      "helpers": [
        "Helfer",
        "Werkzeuge, die vollständig offline im Browser laufen."
      ]
    },
    "tr": {
      "mail": [
        "E-posta alan adı kontrolü",
        "Bir alan adının MX, SPF, DKIM, DMARC, BIMI, MTA-STS ve TLS-RPT kayıtlarını kontrol eder ve somut iyileştirmeler önerir."
      ],
      "header": [
        "E-posta başlığı analizi",
        "Bir e-postanın başlıklarını yapıştırın. Analiz; yolu zaman çizelgesi olarak, SPF, DKIM ve DMARC sonuçlarını, spam işaretlerini ve şüpheli uyumsuzlukları gösterir."
      ],
      "subnet": [
        "Alt ağ hesaplayıcı",
        "IPv4 ve IPv6: ağ, yayın adresi, ana bilgisayar aralığı, alt ağlara bölme, birleştirme ve bir IP'nin ağda olup olmadığını kontrol etme."
      ],
      "dns-diff": [
        "DNS karşılaştırma",
        "Taşıma öncesindeki kayıtları (bölge dosyası veya liste) canlı DNS ile karşılaştırın. Eksik, değişen ve yeni değerleri gösterir."
      ],
      "helpers": [
        "Yardımcılar",
        "Tamamen çevrimdışı olarak tarayıcıda çalışan araçlar."
      ]
    }
  };
  /*ENDROUTES*/

  const startBlazor = () => {
    if (window.__atbStarted || !window.Blazor) return;
    window.__atbStarted = true;
    window.Blazor.start();
  };

  document.addEventListener('DOMContentLoaded', () => {
    const texts = SHELL[current];
    if (texts) {
      document.querySelectorAll('[data-i18n]').forEach(el => { el.textContent = texts[el.dataset.i18n] ?? el.textContent; });
      document.querySelectorAll('[data-i18n-label]').forEach(el => { el.setAttribute('aria-label', texts[el.dataset.i18nLabel] ?? ''); });
    }
    if (!isHome) {
      const route = (ROUTES[current] ?? ROUTES.en)[path.split('/')[0]];
      const box = document.querySelector('.shell-route');
      if (route && box) {
        box.querySelector('h1').textContent = route[0];
        box.querySelector('p').textContent = route[1];
        root.classList.add('atb-route');
      }
      startBlazor();
      return;
    }
    // The home page is complete as static HTML. .NET starts on the first interaction or when the page has been idle.
    for (const type of ['pointerdown', 'keydown', 'touchstart', 'focusin']) {
      window.addEventListener(type, startBlazor, { once: true, passive: true });
    }
    window.addEventListener('load', () => setTimeout(startBlazor, 5000));
  });

  // Offline support for the helpers (PWA). Registered after load so it never competes with startup.
  if ('serviceWorker' in navigator) {
    window.addEventListener('load', () => {
      navigator.serviceWorker.register('service-worker.js').catch(() => { /* offline mode unavailable */ });
    });
  }
})();
