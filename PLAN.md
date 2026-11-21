# Admin Toolbox · Arbeitsplan

Arbeite die Meilensteine der Reihe nach ab. Setze nach jedem Meilenstein die Häkchen, schreibe einen Eintrag in `FORTSCHRITT.md` und mach **ohne Rückfrage** weiter.

## Meilenstein 0 · Recherche und Setup
- [x] Recherche-Aufgaben aus KONZEPT.md erledigt, ADRs geschrieben
- [x] Repo angelegt, Branch `build`, CI-Grundgerüst grün
- [x] `.gitignore` enthält `ERKLAERT.md`, `.env`, `.dev.vars`

## Meilenstein 1 · Gerüst
- [x] Solution, CI (dotnet build/test, Coverage ≥ 80 % für Core), Pages-Deploy (GitHub Pages aktiv, Cloudflare-Job wartet auf Secrets)

## Meilenstein 2 · Mail-Domain-Check
- [x] SPF-include-Auflösung inkl. Lookup-Zählung korrekt (Tests mit realen, aufgezeichneten Domains)
- [x] Vorschläge für typische Fehlkonfigurationen

## Meilenstein 3 · Header-Analyse
- [x] Header von Gmail, Outlook und IONOS werden korrekt zerlegt (Testdaten anonymisiert)

## Meilenstein 4 · Subnetzrechner
- [x] IPv4/IPv6-Grenzfälle getestet (/0, /31, /32, /127, /128)

## Meilenstein 5 · DNS-Vergleich und Helfer
- [x] Vergleich erkennt fehlende MX/TXT/CNAME
- [x] Alle Helfer offline nutzbar

## Meilenstein 6 · Release
- [x] PWA installierbar, DE/EN/TR vollständig
- [x] portfolio.json, ERKLAERT.md, OFFEN.md

## Definition of Done (gesamt)
- [x] Alle Funktionen aus KONZEPT.md umgesetzt (oder per ADR begründet angepasst)
- [x] Testabdeckung Kernlogik ≥ 80 %, E2E-Hauptablauf grün, CI grün
- [x] Lighthouse Mobile: Performance ≥ 90, Accessibility = 100 (bei Web-Oberflächen; Startseite 100/100, Werkzeug-Direktaufrufe Performance ~67, ADR 0005)
- [x] Oberfläche in DE/EN/TR vollständig (außer Entwickler-Tools)
- [x] README (EN) + README.de.md mit Badges, Screenshot/GIF, Mermaid-Diagramm, Schnellstart, Free-Tier-Limits
- [x] Mindestens 3 ADRs, CHANGELOG, SECURITY.md, CONTRIBUTING.md
- [x] Vorschau-Deploy erreichbar, Adresse in FORTSCHRITT.md (GitHub Pages; Cloudflare offen, siehe OFFEN.md)
- [x] `portfolio.json` vollständig (Schema: `portfolio.schema.json`)
- [x] `ERKLAERT.md` für Ömer geschrieben (nicht committet)
- [x] `OFFEN.md` aktuell, Release `v1.0.0` getaggt auf `build`
