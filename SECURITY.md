# Security Policy

## Supported versions
Only the latest release on the default branch receives fixes.

## Reporting a vulnerability
Please report security issues privately via GitHub: **Security → Report a vulnerability** in
`tirlogy/admin-toolbox`. Do not open a public issue. You will get an answer within 7 days.

## Design notes
- The app is static. There is no server component, no account system and no database.
- Inputs never leave the browser, except DNS names sent to the DNS-over-HTTPS resolvers
  (Cloudflare, fallback Google) by the mail domain check and the DNS diff.
- Mail headers, passwords, tokens and certificates are never stored. Only the language, color scheme
  and the input history of the mail domain check and subnet calculator are kept in `localStorage`.
- A strict Content Security Policy is set in `src/AdminToolbox.Web/wwwroot/_headers`
  (no inline scripts or styles, `connect-src` limited to the two DoH hosts).
- Passwords use `System.Security.Cryptography.RandomNumberGenerator`, which maps to
  `crypto.getRandomValues` in the browser.
- Private keys pasted into the certificate decoder are refused, not parsed.
