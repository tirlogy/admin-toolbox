# 0002 · DNS over HTTPS: Cloudflare JSON API with Google fallback

- Status: accepted
- Date: 2026-11-21

## Context
The mail domain check and the DNS diff need DNS data from the browser. Browsers cannot send UDP DNS,
so only DNS over HTTPS (DoH) is possible, and only if the endpoint allows cross-origin requests.
The concept requires checking whether the Cloudflare DoH JSON API sends CORS headers.

Verified on 2026-11-21 with `curl -H "Origin: https://example.pages.dev"`:

| Endpoint | `GET` response | Preflight (`OPTIONS`) |
|---|---|---|
| `https://cloudflare-dns.com/dns-query?name=…&type=…` with `accept: application/dns-json` | `access-control-allow-origin: *` | `allow-methods: POST, GET`, `allow-headers: accept` |
| `https://dns.google/resolve?name=…&type=…` | `access-control-allow-origin: *` | not needed |

`Accept: application/dns-json` is a CORS-safelisted header value, so requests are "simple" and need no preflight.

## Decision
- `AdminToolbox.Dns.DohClient` queries the JSON API of Cloudflare first and falls back to Google on
  HTTP errors, SERVFAIL, invalid JSON, network errors or a 5 s timeout.
- Numeric record types (`type=16`) work with both providers.
- Answers are cached per name and type for their TTL (5 s to 5 min, NXDOMAIN/NODATA 30 s).
  Identical concurrent queries share one request (the SPF tree and DKIM probing ask for the same
  names several times).
- TXT data is normalized (Cloudflare returns quoted strings, Google unquoted ones).
- The CSP allows exactly these two hosts in `connect-src`. The UI states that only DNS queries
  leave the browser.

## Alternatives
- **Own proxy (Cloudflare Worker)**: would allow fetching MTA-STS policy files and TLS checks, but
  adds a server component, which the concept excludes, and would see all queried domains.
- **RFC 8484 wire format (`application/dns-message`)**: standard, but needs a DNS message
  encoder/decoder and a preflight for `POST`. The JSON API is simpler and enough.
- **Quad9 / NextDNS**: no or inconsistent CORS support at the time of writing.

## Consequences
- No backend, zero running cost.
- Resolution results reflect the public view of Cloudflare/Google, not a corporate split-horizon DNS.
- The MTA-STS policy file (HTTPS on a foreign host) cannot be fetched from the browser; the check
  verifies the DNS part and links to the file for manual review.
