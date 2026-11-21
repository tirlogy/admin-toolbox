# 0008 · Interim preview on GitHub Pages

- Status: accepted
- Date: 2026-11-21

## Context
The project rules ask for a reachable preview deploy. The target host is Cloudflare Pages (ADR 0004),
but during the build the Cloudflare account could not be used from the build session (the tool
permission check blocked `wrangler` with the shared account credentials) and no API token is stored
as a GitHub secret yet.

## Decision
- Every push to `build` that passes E2E and Lighthouse is also deployed to **GitHub Pages**
  (`https://tirlogy.github.io/admin-toolbox/`) with the built-in `GITHUB_TOKEN`. No secret and no
  extra account are needed, the repository is public, so it is free.
- `tools/pages-subpath.py` adapts the published site to the `/admin-toolbox/` sub-path: `<base href>`,
  `404.html` for deep links, and the `index.html` hash in the service worker manifest.
- `boot.js` and the service worker derive paths from `<base href>` and the registration scope, so
  the same build works at `/` and under a sub-path.
- GitHub Pages cannot send custom headers, so the CSP is additionally set as `<meta http-equiv>` in
  `index.html` (everything except `frame-ancestors`, which only works as a header). On Cloudflare
  both apply.
- The Cloudflare job stays in place and deploys to `build.admin-toolbox.pages.dev` as soon as the
  secrets exist (OFFEN.md).

## Alternatives
- **Wait for Cloudflare credentials**: leaves the preview unreachable.
- **Netlify / Vercel**: would need new accounts in Ömer's name, which the rules exclude.

## Consequences
- Two preview URLs once Cloudflare is configured; the GitHub Pages job can then be removed.
- On GitHub Pages, HSTS, `X-Frame-Options` and the other header-only protections are missing; the
  production domain on Cloudflare has them.
