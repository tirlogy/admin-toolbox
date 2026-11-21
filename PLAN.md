# Admin Toolbox · Arbeitsplan

Arbeite die Meilensteine der Reihe nach ab. Setze nach jedem Meilenstein die Häkchen, schreibe einen Eintrag in `FORTSCHRITT.md` und mach **ohne Rückfrage** weiter.

## Meilenstein 0 · Recherche und Setup
- [ ] Recherche-Aufgaben aus KONZEPT.md erledigt, ADRs geschrieben
- [ ] Repo angelegt, Branch `build`, CI-Grundgerüst grün
- [ ] `.gitignore` enthält `ERKLAERT.md`, `.env`, `.dev.vars`

## Meilenstein 1 · Gerüst
- [ ] Solution, CI (dotnet build/test, Coverage ≥ 80 % für Core), Pages-Deploy

## Meilenstein 2 · Mail-Domain-Check
- [ ] SPF-include-Auflösung inkl. Lookup-Zählung korrekt (Tests mit realen, aufgezeichneten Domains)
- [ ] Vorschläge für typische Fehlkonfigurationen

## Meilenstein 3 · Header-Analyse
- [ ] Header von Gmail, Outlook und IONOS werden korrekt zerlegt (Testdaten anonymisiert)

## Meilenstein 4 · Subnetzrechner
- [ ] IPv4/IPv6-Grenzfälle getestet (/0, /31, /32, /127, /128)

## Meilenstein 5 · DNS-Vergleich und Helfer
- [ ] Vergleich erkennt fehlende MX/TXT/CNAME
- [ ] Alle Helfer offline nutzbar

## Meilenstein 6 · Release
- [ ] PWA installierbar, DE/EN/TR vollständig
- [ ] portfolio.json, ERKLAERT.md, OFFEN.md

## Definition of Done (gesamt)
- [ ] Alle Funktionen aus KONZEPT.md umgesetzt (oder per ADR begründet angepasst)
- [ ] Testabdeckung Kernlogik ≥ 80 %, E2E-Hauptablauf grün, CI grün
- [ ] Lighthouse Mobile: Performance ≥ 90, Accessibility = 100 (bei Web-Oberflächen)
- [ ] Oberfläche in DE/EN/TR vollständig (außer Entwickler-Tools)
- [ ] README (EN) + README.de.md mit Badges, Screenshot/GIF, Mermaid-Diagramm, Schnellstart, Free-Tier-Limits
- [ ] Mindestens 3 ADRs, CHANGELOG, SECURITY.md, CONTRIBUTING.md
- [ ] Vorschau-Deploy erreichbar, Adresse in FORTSCHRITT.md
- [ ] `portfolio.json` vollständig (Schema: `portfolio.schema.json`)
- [ ] `ERKLAERT.md` für Ömer geschrieben (nicht committet)
- [ ] `OFFEN.md` aktuell, Release `v1.0.0` getaggt auf `build`
