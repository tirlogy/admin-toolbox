# 0003 · Localization with .resx and invariant globalization

- Status: accepted
- Date: 2026-11-21

## Context
The UI must be available in German, English and Turkish (default: browser language, else English),
and the concept asks for `.resx` files. The core library must stay free of UI concerns.

## Decision
- The core library returns **finding codes** (`spf.lookups.exceeded`) with arguments instead of
  sentences. The UI maps each code to an explanation (`code`) and an optional suggestion
  (`code.fix`) from `ToolText.resx` / `.de.resx` / `.tr.resx`.
- Shell texts (layout, home, privacy) live in `AppText*.resx` in the host project, tool texts in the
  lazily loaded `AdminToolbox.Tools` assembly.
- The language is resolved in `wwwroot/js/boot.js` (stored choice, browser languages, `en`), set as
  `CultureInfo.DefaultThreadCurrent(UI)Culture` before `RunAsync`, so Blazor loads the matching
  satellite assemblies. Switching language stores the choice and reloads the page.
- A unit test (`LocalizationTests`) fails when a finding code has no text, when a key is missing in
  one language, when placeholders differ between languages, or when German texts use dashes as
  sentence separators or "AI" instead of "KI".

## Alternatives
- **JSON dictionaries loaded via JS**: smaller, but bypasses `IStringLocalizer` and the concept's
  `.resx` requirement.
- **Sentences in the core library**: simpler, but mixes UI language into domain logic and makes
  the core untestable without culture setup.

## Consequences
- Adding a check means adding its codes to three `.resx` files; the test enforces it.
- Language switches cost one page reload (assets come from the cache, so this is fast).
