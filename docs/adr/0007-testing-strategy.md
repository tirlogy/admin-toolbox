# 0007 · Testing: xUnit with recorded DoH answers, Playwright for Node, Lighthouse CI

- Status: accepted
- Date: 2026-11-21

## Context
Core logic needs at least 80 % coverage, DNS behavior must be tested with real but reproducible
data, and the main UI flow needs an end-to-end test.

## Decision
- **xUnit + coverlet** for Core and Dns, with a coverage gate of 80 % lines per test project in CI
  (`Threshold` in `tests/Directory.Build.props`).
- **Recorded DoH answers**: `FixtureHandler` replays JSON responses recorded from
  `cloudflare-dns.com` (google.com, outlook.com, github.com, atlassian.com). `RECORD_DOH=1` re-records
  them. Lookup counts were cross-checked with an independent script.
- **Playwright for Node** (not Playwright for .NET): the browser install is simpler in CI and the
  tests are plain TypeScript with `strict` typechecking. DoH requests are intercepted with
  `page.route`, so the E2E suite never depends on live DNS.
- **Lighthouse CI** runs three mobile runs against the published build served with Brotli and the
  production headers; performance >= 90 and accessibility = 100 are asserted.

## Consequences
- Fixtures are a snapshot; real-world records change, so the recorded tests pin the snapshot and
  never call the network in CI.
