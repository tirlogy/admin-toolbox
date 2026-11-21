# 0004 · Static hosting on Cloudflare Pages with a strict CSP

- Status: accepted
- Date: 2026-11-21

## Context
No server component, zero running cost, strict CSP and security headers are required.

## Decision
- The published `wwwroot` is deployed as static files to Cloudflare Pages (free plan). Without a
  `404.html` Pages serves `index.html` for unknown paths, which gives SPA routing for deep links.
- `wwwroot/_headers` sets: `Content-Security-Policy` (`script-src 'self' 'wasm-unsafe-eval'`, no
  inline scripts or styles, `connect-src` limited to the two DoH hosts, `frame-ancestors 'none'`),
  `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy: no-referrer`, `Permissions-Policy`,
  `Cross-Origin-Opener-Policy` and HSTS.
- All JavaScript lives in `js/boot.js`; Razor components never use inline `style` attributes.
- The E2E server (`e2e/server.mjs`) applies the same `_headers` file, so Playwright tests run under
  the production CSP and fail on CSP violations (console errors).
- Preview deploys go to the `build` branch alias (`build.admin-toolbox.pages.dev`). The production
  domain `tools.tiryaki.eu` is attached later by Ömer (OFFEN.md).

## Alternatives
- **GitHub Pages**: free, but no custom headers (CSP only via `<meta>`, no HSTS/COOP) and a sub-path
  base URL.
- **Cloudflare Workers static assets**: equivalent, but Pages is simpler for a pure static site.

## Consequences
- Free tier limits apply (500 builds/month, 20 000 files, 25 MiB per file); the app uses about 100 files.
- The service worker and the CSP must be kept in sync when new external hosts are added.
