# 0005 · Lazy loading of the tools and a static first paint

- Status: accepted
- Date: 2026-11-21

## Context
The concept asks for lazy loading of the tools to keep Blazor start-up fast, and Lighthouse mobile
performance must be at least 90. Blazor can lazy load whole assemblies; routable components must
then live in a separate assembly that the host does not reference at start-up.

## Decision
- Four projects instead of three: the tool pages moved from `AdminToolbox.Web` into the Razor class
  library **`AdminToolbox.Tools`**. `AdminToolbox.Web` only contains the shell (layout, home,
  privacy, not found).
- `AdminToolbox.Tools`, `AdminToolbox.Core` and `AdminToolbox.Dns` are marked
  `BlazorWebAssemblyLazyLoad`. `App.razor` loads them in `OnNavigateAsync` on the first navigation
  to any tool route and adds them to the router.
- To keep the host free of references to lazy types, the tools create their own services
  (`ToolServices.Dns`, `BrowserStore`) instead of registering them in `Program.cs`.
- `index.html` contains a static copy of the English home page, so the first contentful and largest
  contentful paint happen before WebAssembly starts. Blazor renders the same markup on top.

## Alternatives
- **Pages in the host with lazy Core/Dns only**: fewer projects, but page types would reference lazy
  types, which the runtime may resolve during route discovery.
- **One lazy assembly per tool**: finer grained, but five extra projects for very small gains
  (each tool is a few kilobytes).

## Consequences
- The home page downloads only the framework and the shell; the tools follow on demand and are
  cached by the service worker for offline use.
- The static shell has to be kept in sync with `Home.razor` (comment in both files).
