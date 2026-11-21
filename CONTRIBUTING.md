# Contributing

Thanks for helping! Issues and pull requests are welcome.

## Setup
- .NET SDK 10 (LTS)
- Node.js 24 for the end-to-end tests

```bash
dotnet restore
dotnet test                      # unit tests (Core, Dns)
dotnet run --project src/AdminToolbox.Web
```

End-to-end tests run against a published build:

```bash
dotnet publish src/AdminToolbox.Web -c Release -o publish
cd e2e && npm ci && npx playwright install chromium && npx playwright test
```

## Rules
- Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, `ci:`, `chore:`).
- C# with `Nullable` enabled and warnings as errors; `dotnet format --verify-no-changes` must pass.
- Core logic needs tests; CI enforces at least 80 % line coverage per test project.
- New checks return finding codes, not sentences. Add the texts to `ToolText.resx`, `ToolText.de.resx`
  and `ToolText.tr.resx` (the `LocalizationTests` fail otherwise). German texts: "KI" instead of
  "AI", no dashes as sentence separators.
- No inputs may be sent to third parties except DNS queries. Do not add analytics.
- No real company names, logos or personal data in examples; use `example.com`, `192.0.2.0/24`,
  `2001:db8::/32`.
- Add an entry to `CHANGELOG.md` for user-facing changes and an ADR in `docs/adr/` for decisions.

## Re-recording DNS fixtures
```bash
RECORD_DOH=1 dotnet test tests/AdminToolbox.Dns.Tests --filter RecordedDomainTests
```
Review the changed expectations (lookup counts) afterwards.
