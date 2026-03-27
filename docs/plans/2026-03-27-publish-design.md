# Publish Design: ZeroAlloc.AsyncEvents

**Date:** 2026-03-27
**Status:** Approved

## Overview

Prepare ZeroAlloc.AsyncEvents for public NuGet publication with full parity to ZeroAlloc.Mediator: GitHub Actions CI/CD, enriched README with real benchmark numbers, structured docs folder, and expanded target frameworks.

## Approach

Full Mediator parity — mirror the CI/CD pipeline, README style, and docs structure from ZeroAlloc.Mediator adapted to this library's surface area.

---

## Section 1 — GitHub Workflows

Four files under `.github/workflows/`:

| File | Trigger | Purpose |
|------|---------|---------|
| `ci.yml` | push to `main`/release branches, PRs, `workflow_dispatch` | Full history checkout, .NET 10 setup, restore, Release build, test, pack, push pre-release to NuGet on push to main |
| `release-please.yml` | push to `main` | Auto-creates release PR with changelog from conventional commits; on release created → build, test, pack, push stable to NuGet, attach `.nupkg` to GitHub Release |
| `release.yml` | GitHub release published | Fallback manual path — extract version from tag, build, test, pack, push to NuGet |
| `trigger-website.yml` | push to `main` with `docs/**` changes | Dispatches event to `.website` repo (no-op until website repo exists) |

**Secrets required:** `NUGET_API_KEY`, `WEBSITE_DISPATCH_TOKEN`
**Pack target:** `src/ZeroAlloc.AsyncEvents/` only (single package, no generator)

---

## Section 2 — README

Performance-first style matching Mediator. Sections:

1. **Title + one-liner** — zero-alloc async events for .NET
2. **Key Characteristics** — lock-free CAS registration, ValueTask throughout, ArrayPool parallel dispatch, async INotify* interfaces, multi-target
3. **Performance** — benchmark table with real numbers from `EventHandlerComparisonBenchmarks` (Sync multicast, NaiveAsync TaskWhenAll, ZeroAlloc Parallel, ZeroAlloc Sequential) — 10 handlers registered
4. **Installation** — `dotnet add package ZeroAlloc.AsyncEvents`
5. **Quick Start** — register/invoke code sample
6. **Design Philosophy** — struct semantics, CancellationToken propagation, async events vs mediator

Benchmark numbers are obtained by running `EventHandlerComparisonBenchmarks` in Release mode and embedding actual output.

---

## Section 3 — Docs Folder

```
docs/
├── README.md                  (overview + table of contents)
├── getting-started.md         (install, first event, register/unregister)
├── invoke-modes.md            (Parallel vs Sequential, when to use each)
├── async-notify-interfaces.md (INotifyPropertyChangedAsync etc., MVVM usage)
├── async-event-args.md        (AsyncPropertyChangedEventArgs, cancellation)
├── advanced.md                (struct semantics, field vs property handlers, mode override)
├── testing.md                 (testing async event handlers)
└── performance.md             (full benchmark results, methodology, comparison notes)
```

Concise, focused files. No cookbook directory.

---

## Section 4 — Target Frameworks & Packaging

**Target frameworks:** `netstandard2.0;netstandard2.1;net8.0;net10.0`
- `netstandard2.1` — native ValueTask, drops `System.Threading.Tasks.Extensions` polyfill
- `net8.0` / `net10.0` — modern optimizations, AOT compatibility

**`Directory.Build.props` changes:**
- Bump `ZeroAlloc.Analyzers` `1.3.1` → `1.3.6`
- Add `NetFabric.Hyperlinq.Analyzer` (async enumerable pattern analysis)
- Add `IsAotCompatible=true` to `ZeroAlloc.AsyncEvents.csproj`

**NuGet metadata:** already complete — no changes needed.
**`assets/icon.png`:** added by user.

---

## Implementation Order

1. Update `Directory.Build.props` and `.csproj` (frameworks, analyzers, AOT)
2. Run benchmarks in Release mode, capture output
3. Write updated `README.md` with real benchmark numbers
4. Create `.github/workflows/` — all four workflow files
5. Create `docs/` folder with all eight markdown files
6. Commit everything, verify CI passes
