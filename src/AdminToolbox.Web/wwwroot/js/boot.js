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
      "tool.mail": "Mail-Domain-Check",
      "tool.mail.short": "Mail",
      "tool.header": "Header-Analyse",
      "tool.header.short": "Header",
      "tool.subnet": "Subnetzrechner",
      "tool.subnet.short": "Subnetz",
      "tool.diff": "DNS-Vergleich",
      "tool.diff.short": "DNS",
      "tool.helpers": "Helfer",
      "tool.helpers.short": "Helfer",
      "loading": "Lädt …",
      "home.eyebrow": "Kostenlos · Open Source · ohne Konto",
      "home.title": "Werkzeuge für IT-Admins",
      "home.lead": "Mail-Sicherheit, DNS und Netzwerke direkt im Browser prüfen. Ohne Konto, ohne Server, Eingaben bleiben lokal.",
      "home.cta.mail": "Mail-Domain prüfen",
      "home.cta.header": "Header analysieren",
      "home.trust.1": "Läuft im Browser",
      "home.trust.2": "Nichts wird hochgeladen",
      "home.trust.3": "Helfer auch offline",
      "home.trust.4": "MIT-Lizenz",
      "home.tools.title": "Fünf Werkzeuge",
      "tool.mail.desc": "MX, SPF mit Lookup-Zählung, DKIM, DMARC, BIMI, MTA-STS und TLS-RPT mit Ampel und Vorschlägen.",
      "tool.header.desc": "Route als Zeitleiste, SPF/DKIM/DMARC-Ergebnisse, Spam-Hinweise und verdächtige Abweichungen.",
      "tool.subnet.desc": "IPv4 und IPv6, Aufteilen, Zusammenfassen und Prüfung, ob eine IP im Netz liegt.",
      "tool.diff.desc": "Zonendatei von vorher gegen die Live-Abfrage. Ideal beim DNS-Umzug.",
      "tool.helpers.desc": "Passwörter, JWT, Base64/URL/Hex, Hashes und Zertifikate. Offline nutzbar.",
      "home.why.title": "Warum diese Toolbox?",
      "home.why.1": "Als IT-Administrator kommen jede Woche die gleichen Fragen: Warum landet unsere Mail im Spam? Stimmen SPF, DKIM und DMARC? Fehlt nach dem DNS-Umzug ein Eintrag? Passt das Subnetz noch?",
      "home.why.2": "Seit Februar 2024 verlangen Google und Yahoo von Massenversendern gültige SPF-, DKIM- und DMARC-Einträge, Microsoft setzt das seit Mai 2025 für Outlook.com durch. Fehlt ein Eintrag, werden Mails abgelehnt oder landen im Spam. Auch kleine Firmen merken das, sobald Newsletter oder Rechnungen per Mail nicht mehr ankommen.",
      "home.why.3": "Die üblichen Online-Prüfer zeigen Werbung, wollen ein Konto oder schicken Mail-Header mit internen Adressen an fremde Server. Diese Toolbox rechnet alles im Browser. Nur DNS-Abfragen gehen an Cloudflare DNS, so wie bei jedem Resolver.",
      "home.rules.title": "Was Postfach-Anbieter erwarten",
      "home.rules.spf": "Legt fest, welche Server für die Domain senden dürfen. Höchstens 10 DNS-Lookups.",
      "home.rules.dkim": "Signiert jede Mail, damit Änderungen unterwegs auffallen. Schlüssel mit mindestens 1024, besser 2048 Bit.",
      "home.rules.dmarc": "Sagt Empfängern, was bei Fehlern von SPF und DKIM passieren soll, und schickt Berichte. Mit p=none starten, dann verschärfen.",
      "home.rules.note": "Google und Yahoo verlangen das ab etwa 5.000 Mails pro Tag an ihre Nutzer, Microsoft für Outlook.com. Quellen:",
      "home.source.google": "Google: Richtlinien für E-Mail-Absender",
      "home.source.yahoo": "Yahoo: Best Practices für Absender",
      "home.guide.title": "Typische Abläufe",
      "home.guide.1.title": "Mail landet im Spam",
      "home.guide.1": "Mail-Domain-Check für die Absenderdomain starten. Rote und gelbe Befunde kommen mit einem konkreten Vorschlag für den DNS-Eintrag.",
      "home.guide.2.title": "Eine einzelne Mail ist verdächtig",
      "home.guide.2": "Header kopieren (Outlook: Datei, Eigenschaften; Gmail: Original anzeigen) und in die Header-Analyse einfügen. Sie zeigt den Weg, Verzögerungen und ob der Absender gefälscht ist.",
      "home.guide.3.title": "DNS-Umzug",
      "home.guide.3": "Vor dem Umzug die Zonendatei beim alten Anbieter exportieren. Danach vergleicht der DNS-Vergleich sie mit der Live-Abfrage und listet jeden fehlenden Eintrag.",
      "home.guide.4.title": "Netz planen",
      "home.guide.4": "Netz in den Subnetzrechner eingeben, in VLANs aufteilen oder prüfen, ob eine Adresse dazugehört. IPv4 und IPv6.",
      "home.privacy.title": "Was lokal bleibt",
      "home.privacy": "Nur Mail-Domain-Check und DNS-Vergleich stellen DNS-Abfragen an Cloudflare DNS (Fallback Google Public DNS). Alles andere läuft offline.",
      "home.tech.title": "Technik",
      "home.tech": "C# und .NET 10, kompiliert zu WebAssembly (Blazor). Kein Backend: Die App wird als statische Dateien über Cloudflare Pages ausgeliefert.",
      "home.tech.link": "Quellcode und Tests auf GitHub",
      "privacy": "Datenschutz",
      "imprint": "Impressum"
    },
    "tr": {
      "nav": "Araçlar",
      "tool.mail": "E-posta alan adı kontrolü",
      "tool.mail.short": "E-posta",
      "tool.header": "Başlık analizi",
      "tool.header.short": "Başlık",
      "tool.subnet": "Alt ağ hesaplayıcı",
      "tool.subnet.short": "Alt ağ",
      "tool.diff": "DNS karşılaştırma",
      "tool.diff.short": "DNS",
      "tool.helpers": "Yardımcılar",
      "tool.helpers.short": "Yardımcı",
      "loading": "Yükleniyor …",
      "home.eyebrow": "Ücretsiz · açık kaynak · hesap gerekmez",
      "home.title": "BT yöneticileri için araçlar",
      "home.lead": "E-posta güvenliğini, DNS'i ve ağları doğrudan tarayıcıda kontrol edin. Hesap yok, sunucu yok, girdiler yerel kalır.",
      "home.cta.mail": "E-posta alan adını kontrol et",
      "home.cta.header": "Başlığı analiz et",
      "home.trust.1": "Tarayıcıda çalışır",
      "home.trust.2": "Hiçbir şey yüklenmez",
      "home.trust.3": "Yardımcılar çevrimdışı da çalışır",
      "home.trust.4": "MIT lisansı",
      "home.tools.title": "Beş araç",
      "tool.mail.desc": "MX, sorgu sayımlı SPF, DKIM, DMARC, BIMI, MTA-STS ve TLS-RPT; trafik ışıkları ve önerilerle.",
      "tool.header.desc": "Zaman çizelgesi olarak yol, SPF/DKIM/DMARC sonuçları, spam işaretleri ve şüpheli uyumsuzluklar.",
      "tool.subnet.desc": "IPv4 ve IPv6, bölme, birleştirme ve IP'nin ağda olup olmadığının kontrolü.",
      "tool.diff.desc": "Önceki bölge dosyasını canlı DNS ile karşılaştırır. DNS taşımaları için ideal.",
      "tool.helpers.desc": "Parolalar, JWT, Base64/URL/hex, özetler ve sertifikalar. Çevrimdışı çalışır.",
      "home.why.title": "Neden bu araç kutusu?",
      "home.why.1": "BT yöneticisi olarak her hafta aynı sorular gelir: E-postalarımız neden spam klasörüne düşüyor? SPF, DKIM ve DMARC doğru mu? DNS taşımasından sonra bir kayıt eksik mi? Alt ağ hâlâ yeterli mi?",
      "home.why.2": "Şubat 2024'ten beri Google ve Yahoo, toplu gönderim yapanlardan geçerli SPF, DKIM ve DMARC kayıtları istiyor; Microsoft da Mayıs 2025'ten beri Outlook.com için aynısını uyguluyor. Bir kayıt eksikse e-postalar reddedilir veya spam klasörüne düşer. Bülten ya da faturalar e-postayla ulaşmamaya başladığında küçük şirketler de bunu fark eder.",
      "home.why.3": "Bilinen çevrimiçi kontrol araçları reklam gösterir, hesap ister ya da iç adresler içeren e-posta başlıklarını yabancı sunuculara gönderir. Bu araç kutusu her şeyi tarayıcıda hesaplar. Yalnızca DNS sorguları, her çözümleyicide olduğu gibi Cloudflare DNS'e gider.",
      "home.rules.title": "E-posta sağlayıcılarının beklentileri",
      "home.rules.spf": "Alan adı adına hangi sunucuların gönderebileceğini belirler. En fazla 10 DNS sorgusu.",
      "home.rules.dkim": "Her e-postayı imzalar, böylece yoldaki değişiklikler fark edilir. Anahtar en az 1024, tercihen 2048 bit.",
      "home.rules.dmarc": "SPF ve DKIM başarısız olduğunda alıcılara ne yapacaklarını söyler ve rapor gönderir. p=none ile başlayın, sonra sıkılaştırın.",
      "home.rules.note": "Google ve Yahoo bunu kullanıcılarına günde yaklaşık 5.000 e-postadan itibaren, Microsoft ise Outlook.com için istiyor. Kaynaklar:",
      "home.source.google": "Google: E-posta gönderen yönergeleri",
      "home.source.yahoo": "Yahoo: Gönderenler için en iyi uygulamalar",
      "home.guide.title": "Tipik iş akışları",
      "home.guide.1.title": "E-posta spam klasörüne düşüyor",
      "home.guide.1": "Gönderen alan adı için e-posta alan adı kontrolünü çalıştırın. Kırmızı ve sarı bulgular, DNS kaydı için somut bir öneriyle gelir.",
      "home.guide.2.title": "Tek bir e-posta şüpheli görünüyor",
      "home.guide.2": "Başlıkları kopyalayın (Outlook: Dosya, Özellikler; Gmail: Orijinali göster) ve başlık analizine yapıştırın. Yolu, gecikmeleri ve gönderenin sahte olup olmadığını gösterir.",
      "home.guide.3.title": "DNS taşıma",
      "home.guide.3": "Taşımadan önce eski sağlayıcıdan bölge dosyasını dışa aktarın. Ardından DNS karşılaştırma onu canlı DNS ile karşılaştırır ve eksik her kaydı listeler.",
      "home.guide.4.title": "Ağ planlama",
      "home.guide.4": "Ağı alt ağ hesaplayıcıya girin, VLAN'lara bölün veya bir adresin ona ait olup olmadığını kontrol edin. IPv4 ve IPv6.",
      "home.privacy.title": "Yerelde kalanlar",
      "home.privacy": "Yalnızca e-posta alan adı kontrolü ve DNS karşılaştırma, Cloudflare DNS'e (yedek Google Public DNS) DNS sorguları gönderir. Geri kalan her şey çevrimdışı çalışır.",
      "home.tech.title": "Teknik",
      "home.tech": "C# ve .NET 10, WebAssembly'ye derlenmiş (Blazor). Arka uç yok: Uygulama, Cloudflare Pages üzerinden statik dosyalar olarak sunulur.",
      "home.tech.link": "GitHub'da kaynak kodu ve testler",
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
