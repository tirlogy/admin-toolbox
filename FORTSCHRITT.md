# Fortschritt · Admin Toolbox

(Claude trägt hier nach jedem Meilenstein ein, was gebaut und getestet wurde.)

**Vorschau-Adresse:** https://tirlogy.github.io/admin-toolbox/ (GitHub Pages, ADR 0008).
Cloudflare-Vorschau `https://build.admin-toolbox.pages.dev` folgt, sobald Ömer den Deploy freigibt (OFFEN.md, Punkt 1).
Repo: https://github.com/tirlogy/admin-toolbox (Branch `build`, Tag `v1.0.0`).

## Meilenstein 0 · Recherche und Setup (21.11.2026)
- Recherche: .NET 10 ist die aktuelle LTS (Release-Index vom 10.11.2026: 10.0 LTS, 11.0 STS). Cloudflare-DoH-JSON-API und Google DoH senden `access-control-allow-origin: *` (per curl geprüft, Preflight ebenfalls). ADR 0001 und 0002.
- Repo `tirlogy/admin-toolbox` öffentlich angelegt, Arbeit auf `build`, `.gitignore` mit `ERKLAERT.md`, `.env`, `.dev.vars`.
- Hinweis: Lokal ist nur .NET SDK 6/7 vorhanden, die Installation von SDK 10 war nicht erlaubt. Core und Dns wurden lokal mit `-p:LocalNet7=true` getestet, alles andere (Build, Publish, E2E, Lighthouse) läuft in GitHub Actions auf .NET 10.

## Meilenstein 1 · Gerüst
- Solution mit `AdminToolbox.Core`, `AdminToolbox.Dns`, `AdminToolbox.Tools` (neu, ADR 0005), `AdminToolbox.Web` und zwei xUnit-Projekten. `Nullable`, Warnungen als Fehler, `dotnet format` in der CI.
- CI: Format, Build, Tests mit Coverage-Schwelle 80 % pro Testprojekt, Publish, Bundle-Größe, Playwright, Lighthouse CI, Coverage-Badge, Vorschau-Deploys. Dependabot für NuGet, npm und Actions.
- ADRs: 0001, 0004, 0007.

## Meilenstein 2 · Mail-Domain-Check
- MX, SPF (Baum aus include/redirect, Lookup-Zählung mit Limit 10, Void Lookups mit Limit 2, Schleifen, +all/?all/ptr/Makros), DKIM (24 Selektoren plus eigener, Schlüssellänge aus SPKI oder PKCS#1, t=y, widerrufen), DMARC (Organisationsdomain-Fallback, p/sp/pct/rua/ruf/adkim/aspf), BIMI, MTA-STS (DNS-Teil, Richtliniendatei als Link), TLS-RPT. Jede Meldung mit Ampel, Erklärung und Vorschlag in DE/EN/TR.
- Tests mit realen aufgezeichneten Domains: google.com (1 Lookup, Google hat die Netblocks 2026 zusammengelegt), outlook.com, atlassian.com (6), github.com (genau 10, Warnung „knapp am Limit“). Zählung mit einem unabhängigen Skript gegengeprüft.

## Meilenstein 3 · Header-Analyse
- Header von Gmail, Outlook (Exchange Online) und IONOS werden korrekt zerlegt: Route als Zeitleiste (älteste Station zuerst, Verzögerungen, IP aus Klammern oder Exchange-Format), Authentication-Results/ARC/Received-SPF inkl. compauth, Spam-Header (SpamAssassin, SCL/SFV/CAT, IONOS-Filter, Gmail), Abweichungen (Return-Path, Reply-To, Anzeigename mit fremder Adresse, DKIM-Ausrichtung, Message-ID). Testdaten anonymisiert (example-Domains, Dokumentations-IPs).

## Meilenstein 4 · Subnetzrechner
- IPv4 und IPv6 mit `UInt128`, CIDR oder Netzmaske, Netz/Broadcast/Hosts, Adresstyp, Reverse-Zone, Binärdarstellung, Aufteilen, Zusammenfassen, Supernetz, IP-im-Netz. Grenzfälle /0, /31, /32, /127, /128 getestet.

## Meilenstein 5 · DNS-Vergleich und Helfer
- Zonendatei-Parser ($ORIGIN, $TTL mit Einheiten, Klammern, Kommentare, geerbter Name, relative Namen, Anführungszeichen) und einfache Listen. Vergleich gegen Live-DNS erkennt fehlende, geänderte und neue MX/TXT/CNAME-Werte (Unit-Test und E2E).
- Helfer: Passwort/Passphrase (EFF-Wortliste, CC BY 3.0), JWT, Base64/Base64url/URL/Hex, MD5 (eigene Implementierung, ADR 0006)/SHA-1/SHA-256/SHA-512, PEM-Zertifikate per ASN.1. Offline-Test in Playwright (Service Worker, Netzwerk aus).

## Meilenstein 6 · Release
- PWA: Manifest, Icons (192, 512, maskable), Service Worker mit Integritätsprüfung; Installierbarkeit per Chrome-DevTools-Protokoll im E2E-Test geprüft.
- DE/EN/TR vollständig (363 Werkzeug- und 31 Shell-Texte), per Unit-Test abgesichert (Vollständigkeit, Platzhalter, deutsche Stilregeln, statische Startseite).
- Lighthouse mobil (Median aus 3 Läufen, CI): Startseite Performance 100, Accessibility 100, Best Practices 100, SEO 100. Direkt geöffnete Werkzeugseiten: Accessibility 100, Performance etwa 67 (Start der .NET-Runtime, ADR 0005).
- Tests: 201 Unit-Tests (Core 98 % Zeilen, Dns 100 %), 12 Playwright-Tests auf Desktop- und Mobil-Chrome, unter der Produktions-CSP.
- ADRs 0001 bis 0008, README (EN) und README.de.md, CHANGELOG, SECURITY, CONTRIBUTING, docs/DESIGN.md, portfolio.json, ERKLAERT.md (lokal), OFFEN.md. Tag `v1.0.0` auf `build`.
