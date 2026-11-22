# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed
- Offline mode: returning visitors got ERR_FAILED on Cloudflare Pages because the cached
  index.html was a redirected response (308 to /). The service worker now serves a clean copy.

## [1.0.0] - 2026-11-21

### Added
- Mail domain check: MX, SPF (include and redirect tree, DNS lookup count with limit 10, void
  lookups), DKIM (24 common selectors plus a custom one, key size), DMARC (incl. organizational
  domain fallback), BIMI, MTA-STS and TLS-RPT with traffic light, explanation and suggestion per finding.
- Mail header analysis: route timeline with delays, SPF/DKIM/DMARC/compauth results from
  Authentication-Results, ARC and Received-SPF, spam filter headers (SpamAssassin, Exchange Online,
  IONOS, Gmail), anomalies (Return-Path, Reply-To, display name spoofing, DKIM alignment).
- Subnet calculator for IPv4 and IPv6: details, split, summarize (supernetting), IP-in-network check.
- DNS diff: BIND zone files and simple lists against live DNS, showing missing, changed and new values.
- Offline helpers: password and passphrase generator with strength meter, JWT decoder with expiry
  check, Base64/Base64url/URL/hex, MD5/SHA-1/SHA-256/SHA-512, PEM certificate decoder.
- DNS-over-HTTPS client (Cloudflare, Google fallback) with caching and timeouts.
- PWA with offline support, local history, light and dark mode, UI in German, English and Turkish.
- CI with format check, build, tests, coverage gate, Playwright E2E, Lighthouse and preview deploy.

[1.0.0]: https://github.com/tirlogy/admin-toolbox/releases/tag/v1.0.0
