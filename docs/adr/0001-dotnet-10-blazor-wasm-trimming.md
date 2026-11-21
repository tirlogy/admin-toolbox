# 0001 · .NET 10 LTS, Blazor WebAssembly with trimming instead of AOT

- Status: accepted
- Date: 2026-11-21

## Context
The concept asks for the current .NET LTS, Blazor WebAssembly and a small bundle. On 2026-11-21 the
release index (`builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json`) lists
.NET 11.0 as STS and **.NET 10.0 as the active LTS** (SDK 10.0.404). Lighthouse mobile performance
must reach 90, so download size and main-thread work at startup matter more than raw compute speed.
The tools are light on CPU (parsing a few kilobytes, 128-bit integer math).

## Decision
- Target `net10.0` for all projects.
- Publish with IL trimming in the default `partial` mode: the framework and the assemblies marked
  `IsTrimmable` (Core, Dns) are trimmed, the Razor assemblies are kept whole. `TrimMode=full` was
  tried and removed component constructors that Blazor creates via reflection (`CtorNotLocated`).
  Default Brotli/gzip pre-compression. No AOT.
- `InvariantGlobalization=true` with `PredefinedCulturesOnly=false`: no ICU data download; the
  three UI cultures (de, en, tr) only drive resource lookup (see ADR 0003).
- SDK pinning is left to CI (`actions/setup-dotnet` with `10.0.x`) instead of a `global.json`, so the
  Core and Dns test suites can also be run on an older local SDK with `-p:LocalNet7=true`
  (that switch only exists to give fast local feedback; CI always builds and tests net10.0).

## Alternatives
- **AOT (`RunAOTCompilation`)**: faster execution, but the `.wasm` grows by a factor of 2 to 3 and
  needs the `wasm-tools` workload in CI. The tools do not need the speed.
- **.NET 11 (STS)**: newer, but shorter support; the concept asks for LTS.
- **Full ICU (`BlazorWebAssemblyLoadAllGlobalizationData`)**: correct culture formatting for Turkish
  and German numbers and dates, at the cost of roughly 1 MB extra download. Dates are shown in ISO
  format and numbers are mostly technical, so this is not worth it.

## Consequences
- Small bundle and fast start; CI reports the Brotli size of `_framework` on every run.
- Culture-specific number formatting is invariant (thousands separator `,` in all languages).
- Turkish text never goes through `ToUpper` in code; CSS `text-transform` would respect `lang`.
