# OFFEN · Admin Toolbox

Was Ömer tun muss. Claude ergänzt diese Liste während des Baus.

## Von Anfang an bekannt
- [ ] Merge und Custom Domain tools.tiryaki.eu eintragen (Schritte 2 und 3 unten)
- [ ] Ergebnis prüfen: Vorschau-Adresse https://tirlogy.github.io/admin-toolbox/ öffnen, `ERKLAERT.md` lesen
- [ ] Pull Request `build` → `main` mergen (Schritt 2)

## Während des Baus ergänzt

### 1. Cloudflare-Vorschau freigeben (blockiert)
Claude durfte das Cloudflare-Konto in `/Users/a9338/Downloads/Ömer Tiryaki Webseite/.cf-konto` nicht verwenden (die Rechteprüfung von Claude Code hat `wrangler` mit diesen Zugangsdaten blockiert; nicht erneut versucht). Als Ersatz läuft die Vorschau auf **GitHub Pages**: https://tirlogy.github.io/admin-toolbox/ (ADR 0008, ohne Secrets, kostenlos). Für Cloudflare reicht eine der beiden Varianten:

**a) Einmalig selbst deployen (etwa 3 Minuten, braucht das .NET 10 SDK, siehe Punkt 5):**
```bash
cd /Users/a9338/Downloads/portfolio/admin-toolbox
dotnet publish src/AdminToolbox.Web -c Release -o publish
export XDG_CONFIG_HOME="/Users/a9338/Downloads/Ömer Tiryaki Webseite/.cf-konto"
npx wrangler@4 pages project create admin-toolbox --production-branch main
npx wrangler@4 pages deploy publish/wwwroot --project-name admin-toolbox --branch build
```
Ohne lokales SDK geht auch das fertige CI-Artefakt: `gh run download --repo tirlogy/admin-toolbox -n site -D publish/wwwroot` (lädt den letzten Build von `build`), danach die drei `wrangler`-Zeilen.
Vorschau-Adresse danach: `https://build.admin-toolbox.pages.dev`

**b) Automatisch bei jedem Push (empfohlen):** In GitHub unter `tirlogy/admin-toolbox` → Settings → Secrets and variables → Actions zwei Repository-Secrets anlegen:
- `CLOUDFLARE_API_TOKEN`: Cloudflare Dashboard → My Profile → API Tokens → Create Token → eigenes Token mit „Account · Cloudflare Pages · Edit“
- `CLOUDFLARE_ACCOUNT_ID`: Cloudflare Dashboard → Workers & Pages → rechte Spalte „Account ID“

Das Projekt `admin-toolbox` einmal wie in a) anlegen (`pages project create`), danach deployt der Job „Deploy preview (Cloudflare Pages)“ jeden Push auf `build`. Ohne Secrets wird der Job übersprungen (grün). Sobald Cloudflare läuft, kann der Job „Preview (GitHub Pages)“ in `.github/workflows/ci.yml` gelöscht und GitHub Pages unter Settings → Pages deaktiviert werden.

Oder Claude in einer neuen Sitzung erlauben, `wrangler` mit dem `.cf-konto` auszuführen.

### 2. Branch `main` anlegen und mergen
Auf GitHub gibt es nur `build` (Claude darf nicht auf `main` pushen), deshalb ist `build` gerade Standard-Branch. So entsteht der Pull Request:
```bash
cd /Users/a9338/Downloads/portfolio/admin-toolbox
git switch --orphan main
git commit --allow-empty -m "chore: initial commit"
git push -u origin main
git switch build
gh repo edit tirlogy/admin-toolbox --default-branch main
gh pr create --repo tirlogy/admin-toolbox --base main --head build --title "Admin Toolbox v1.0.0" --body "Erste vollständige Version, siehe FORTSCHRITT.md"
```
Den PR mit „Create a merge commit“ mergen. Falls GitHub wegen der fehlenden gemeinsamen Basis nicht anbietet zu mergen: `git switch main && git merge build --allow-unrelated-histories && git push`.

Danach in `.github/workflows/ci.yml` beim Job „Preview (GitHub Pages)“ nichts ändern: Die Umgebung `github-pages` erlaubt Deploys nur vom Standard-Branch. Wenn der Standard-Branch `main` ist, unter Settings → Environments → github-pages → Deployment branches zusätzlich `build` erlauben (oder den Job löschen, siehe Punkt 1).

### 3. Produktiv-Domain
- [ ] Cloudflare Dashboard → Workers & Pages → `admin-toolbox` → Settings → Production branch `main` prüfen und einmal von `main` deployen (Variante 1b deployt nur `build`; für Produktion `--branch main` statt `--branch build`)
- [ ] Custom domains → `tools.tiryaki.eu` hinzufügen (den DNS-Eintrag legt Cloudflare an, wenn tiryaki.eu dort liegt)
- [ ] Nach dem Go-live in `portfolio.json` `"status": "in-arbeit"` auf `"live"` setzen

### 4. Release
- [ ] Tag `v1.0.0` liegt auf `build`. Optional auf GitHub ein Release dazu anlegen: `gh release create v1.0.0 --repo tirlogy/admin-toolbox --title "v1.0.0" --notes-file CHANGELOG.md`

### 5. Lokales .NET 10 SDK (optional)
Auf diesem Mac sind nur die SDKs 6 und 7 installiert. Claude durfte das .NET 10 SDK nicht installieren (Installation per `dotnet-install.sh` wurde gestoppt bzw. von der Rechteprüfung blockiert). Gebaut und getestet wurde deshalb in GitHub Actions; lokal liefen die Core- und Dns-Tests mit `-p:LocalNet7=true`. Für eigene Arbeit am Projekt:
```bash
brew install --cask dotnet-sdk
```
oder den Installer von https://dotnet.microsoft.com/download/dotnet/10.0 verwenden.

### 6. Aufräumen
- [ ] Dependabot-PRs #1 bis #5 (GitHub-Actions-Versionen) sind durch den Workflow bereits erledigt und schließen sich beim nächsten Dependabot-Lauf selbst; sonst einfach schließen.
