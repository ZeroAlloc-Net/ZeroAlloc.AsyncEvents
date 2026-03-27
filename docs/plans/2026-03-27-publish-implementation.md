# Publish ZeroAlloc.AsyncEvents Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Prepare ZeroAlloc.AsyncEvents for public NuGet publication with CI/CD workflows, enriched README with real benchmark numbers, structured docs, and expanded target frameworks — matching ZeroAlloc.Mediator's style and infrastructure.

**Architecture:** Multi-targeted library (`netstandard2.0;netstandard2.1;net8.0;net10.0`) with conditional polyfill references, GitVersion-based semantic versioning in CI, release-please for automated changelog and releases, and eight focused docs pages.

**Tech Stack:** .NET 10, xunit, BenchmarkDotNet 0.14, GitHub Actions, release-please v4, GitVersion, NuGet

---

## Task 1: Expand target frameworks in main library

**Files:**
- Modify: `src/ZeroAlloc.AsyncEvents/ZeroAlloc.AsyncEvents.csproj`

**Step 1: Replace TargetFramework with multi-target and add AOT flag**

Replace the entire `.csproj` content with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;netstandard2.1;net8.0;net10.0</TargetFrameworks>
    <PackageId>ZeroAlloc.AsyncEvents</PackageId>
    <Description>Zero-allocation async event handler structs and async INotify* interfaces for .NET. Lock-free registration, ValueTask invocation, ArrayPool parallel dispatch.</Description>
    <Version>0.0.0-local</Version>
    <GeneratePackageOnBuild>false</GeneratePackageOnBuild>
    <IsAotCompatible>true</IsAotCompatible>
  </PropertyGroup>
  <!-- Polyfills only needed on netstandard2.0 -->
  <ItemGroup Condition="'$(TargetFramework)' == 'netstandard2.0'">
    <PackageReference Include="System.Buffers" Version="4.5.1" />
    <PackageReference Include="System.Threading.Tasks.Extensions" Version="4.5.4" />
  </ItemGroup>
</Project>
```

**Step 2: Build to verify all four targets compile**

```bash
dotnet build src/ZeroAlloc.AsyncEvents/ZeroAlloc.AsyncEvents.csproj -c Release
```

Expected: `Build succeeded.` — four targets built, zero errors.

**Step 3: Run tests to verify nothing is broken**

```bash
dotnet test tests/ZeroAlloc.AsyncEvents.Tests/ZeroAlloc.AsyncEvents.Tests.csproj -c Release
```

Expected: All tests pass.

**Step 4: Commit**

```bash
git add src/ZeroAlloc.AsyncEvents/ZeroAlloc.AsyncEvents.csproj
git commit -m "feat: multi-target netstandard2.0/2.1, net8.0, net10.0 with AOT compatibility"
```

---

## Task 2: Update analyzers in Directory.Build.props

**Files:**
- Modify: `Directory.Build.props`

**Step 1: Bump ZeroAlloc.Analyzers and add NetFabric.Hyperlinq.Analyzer**

Change the `ZeroAlloc.Analyzers` version from `1.3.1` to `1.3.6` and add `NetFabric.Hyperlinq.Analyzer`:

```xml
<PackageReference Include="ZeroAlloc.Analyzers" Version="1.3.6" PrivateAssets="all">
  <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
</PackageReference>
<PackageReference Include="NetFabric.Hyperlinq.Analyzer" Version="2.3.0" PrivateAssets="all">
  <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
</PackageReference>
```

Note: Analyzers in `Directory.Build.props` apply to all projects. The existing `Condition` blocks already gate them to non-netstandard2.0 targets — verify this condition still applies correctly after the multi-target change.

**Step 2: Build to verify no new analyzer errors**

```bash
dotnet build src/ZeroAlloc.AsyncEvents/ZeroAlloc.AsyncEvents.csproj -c Release
```

Expected: `Build succeeded.` — fix any new analyzer warnings (they are treated as errors via `TreatWarningsAsErrors`).

**Step 3: Commit**

```bash
git add Directory.Build.props
git commit -m "chore: bump ZeroAlloc.Analyzers to 1.3.6, add NetFabric.Hyperlinq.Analyzer"
```

---

## Task 3: Set up GitVersion for semantic versioning

**Files:**
- Create: `.config/dotnet-tools.json`
- Create: `GitVersion.yml`

**Step 1: Create dotnet tools manifest**

```bash
dotnet new tool-manifest
```

**Step 2: Install GitVersion**

```bash
dotnet tool install GitVersion.Tool
```

**Step 3: Create GitVersion.yml**

```yaml
mode: ContinuousDeployment
branches:
  main:
    regex: ^main$
    tag: ''
    increment: Patch
  release:
    regex: ^release[/-]
    tag: rc
    increment: Minor
  pull-request:
    tag: PullRequest
    increment: Inherit
ignore:
  sha: []
```

**Step 4: Verify GitVersion runs**

```bash
dotnet tool run dotnet-gitversion
```

Expected: JSON output with `SemVer`, `NuGetVersionV2`, etc.

**Step 5: Commit**

```bash
git add .config/dotnet-tools.json GitVersion.yml
git commit -m "chore: add GitVersion for semantic versioning"
```

---

## Task 4: Create release-please configuration

**Files:**
- Create: `release-please-config.json`
- Create: `.release-please-manifest.json`

**Step 1: Create release-please-config.json**

```json
{
  "$schema": "https://raw.githubusercontent.com/googleapis/release-please/main/schemas/config.json",
  "release-type": "simple",
  "packages": {
    ".": {}
  }
}
```

**Step 2: Create .release-please-manifest.json**

```json
{
  ".": "0.1.0"
}
```

This sets the initial version to `0.1.0`. release-please will bump this based on conventional commits.

**Step 3: Commit**

```bash
git add release-please-config.json .release-please-manifest.json
git commit -m "chore: add release-please configuration"
```

---

## Task 5: Create GitHub Actions workflows

**Files:**
- Create: `.github/workflows/ci.yml`
- Create: `.github/workflows/release-please.yml`
- Create: `.github/workflows/release.yml`
- Create: `.github/workflows/trigger-website.yml`

**Step 1: Create .github/workflows directory**

```bash
mkdir -p .github/workflows
```

**Step 2: Create ci.yml**

```yaml
name: CI

on:
  push:
    branches: [main, 'release/**']
  pull_request:
    branches: [main]
  workflow_dispatch:

jobs:
  build:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore tools
        run: dotnet tool restore

      - name: Run GitVersion
        id: gitversion
        run: |
          VERSION=$(dotnet tool run dotnet-gitversion /output json /showvariable NuGetVersionV2)
          echo "version=$VERSION" >> $GITHUB_OUTPUT

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore -c Release /p:Version=${{ steps.gitversion.outputs.version }}

      - name: Test
        run: dotnet test --no-build -c Release

      - name: Pack
        run: dotnet pack src/ZeroAlloc.AsyncEvents/ZeroAlloc.AsyncEvents.csproj --no-build -c Release /p:Version=${{ steps.gitversion.outputs.version }} -o ./artifacts

      - name: Push pre-release to NuGet
        if: github.event_name == 'push' && github.ref == 'refs/heads/main'
        run: dotnet nuget push ./artifacts/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json --skip-duplicate
```

**Step 3: Create release-please.yml**

```yaml
name: Release Please

on:
  push:
    branches: [main]

permissions:
  contents: write
  pull-requests: write

jobs:
  release-please:
    runs-on: ubuntu-latest
    outputs:
      release_created: ${{ steps.release.outputs.release_created }}
      tag_name: ${{ steps.release.outputs.tag_name }}
      version: ${{ steps.release.outputs.version }}
    steps:
      - uses: googleapis/release-please-action@v4
        id: release
        with:
          config-file: release-please-config.json
          manifest-file: .release-please-manifest.json

  publish:
    needs: release-please
    if: needs.release-please.outputs.release_created == 'true'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore -c Release /p:Version=${{ needs.release-please.outputs.version }}

      - name: Test
        run: dotnet test --no-build -c Release

      - name: Pack
        run: dotnet pack src/ZeroAlloc.AsyncEvents/ZeroAlloc.AsyncEvents.csproj --no-build -c Release /p:Version=${{ needs.release-please.outputs.version }} -o ./artifacts

      - name: Push to NuGet
        run: dotnet nuget push ./artifacts/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json --skip-duplicate

      - name: Upload packages to GitHub Release
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        run: gh release upload ${{ needs.release-please.outputs.tag_name }} ./artifacts/*.nupkg
```

**Step 4: Create release.yml**

```yaml
name: Release

on:
  release:
    types: [published]

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Extract version from tag
        id: version
        run: echo "version=${GITHUB_REF_NAME#v}" >> $GITHUB_OUTPUT

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore -c Release /p:Version=${{ steps.version.outputs.version }}

      - name: Test
        run: dotnet test --no-build -c Release

      - name: Pack
        run: dotnet pack src/ZeroAlloc.AsyncEvents/ZeroAlloc.AsyncEvents.csproj --no-build -c Release /p:Version=${{ steps.version.outputs.version }} -o ./artifacts

      - name: Push to NuGet
        run: dotnet nuget push ./artifacts/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json --skip-duplicate
```

**Step 5: Create trigger-website.yml**

```yaml
name: Trigger Website

on:
  push:
    branches: [main]
    paths: ['docs/**']

jobs:
  dispatch:
    runs-on: ubuntu-latest
    steps:
      - uses: peter-evans/repository-dispatch@v3
        with:
          token: ${{ secrets.WEBSITE_DISPATCH_TOKEN }}
          repository: ZeroAlloc-Net/ZeroAlloc.AsyncEvents.website
          event-type: docs-updated
```

**Step 6: Commit**

```bash
git add .github/workflows/
git commit -m "ci: add GitHub Actions workflows (ci, release-please, release, trigger-website)"
```

---

## Task 6: Run benchmarks and capture results

**Purpose:** Get real numbers for the README. This is a one-time run — results get embedded in `README.md` and `docs/performance.md`.

**Step 1: Build benchmarks in Release mode**

```bash
dotnet build benchmarks/AsyncEvents.Benchmarks/AsyncEvents.Benchmarks.csproj -c Release
```

**Step 2: Run EventHandlerComparisonBenchmarks**

```bash
dotnet run --project benchmarks/AsyncEvents.Benchmarks/AsyncEvents.Benchmarks.csproj -c Release -- --filter "*EventHandlerComparison*" --exporters json markdown
```

Expected: BenchmarkDotNet table output under `BenchmarkDotNet.Artifacts/results/`. Copy the markdown table — you will paste it verbatim into README.md and docs/performance.md in the next two tasks.

Note: `--exporters markdown` writes a `.md` file to `BenchmarkDotNet.Artifacts/` with the formatted table ready to copy.

---

## Task 7: Write README.md

**Files:**
- Modify: `README.md`

**Step 1: Replace README.md with full content**

Use the actual benchmark numbers captured in Task 6 for the performance table. Replace `<!-- BENCHMARK TABLE -->` with the actual markdown table from BenchmarkDotNet output.

```markdown
# ZeroAlloc.AsyncEvents

Zero-allocation async event handler structs and async `INotify*` interfaces for .NET. Lock-free registration, `ValueTask` invocation, `ArrayPool` parallel dispatch.

## Key Characteristics

- **Lock-free registration** — CAS-loop register/unregister, no locks
- **ValueTask throughout** — no `Task` allocations on hot paths
- **ArrayPool parallel dispatch** — rented array for fan-out, returned immediately after `WhenAll`
- **Async `INotify*` interfaces** — `INotifyPropertyChangedAsync`, `INotifyPropertyChangingAsync`, `INotifyCollectionChangedAsync`, `INotifyDataErrorInfoAsync`
- **Multi-target** — `netstandard2.0`, `netstandard2.1`, `net8.0`, `net10.0`
- **AOT compatible**

## Performance

10 handlers registered, invoked once. Comparison against `EventHandler<T>` (sync multicast) and naive async (`Func<string, Task>` + `Task.WhenAll`).

<!-- BENCHMARK TABLE — replace with actual output from Task 6 -->
| Method | Mean | Error | StdDev | Allocated |
|---|---|---|---|---|
| Sync_MulticastDelegate_10Handlers | — | — | — | 0 B |
| NaiveAsync_TaskWhenAll_10Handlers | — | — | — | — |
| ZeroAlloc_Parallel_10Handlers | — | — | — | 0 B |
| ZeroAlloc_Sequential_10Handlers | — | — | — | 0 B |

## Installation

```
dotnet add package ZeroAlloc.AsyncEvents
```

## Quick Start

```csharp
// Declare
private AsyncEventHandler<OrderPlacedArgs> _orderPlaced = new(InvokeMode.Parallel);

// Register
_orderPlaced.Register(async (args, ct) =>
{
    await SendConfirmationEmailAsync(args.OrderId, ct);
});

// Invoke
await _orderPlaced.InvokeAsync(new OrderPlacedArgs(orderId), cancellationToken);

// Unregister
_orderPlaced.Unregister(handler);
```

## Async INotify* Interfaces

```csharp
public class ViewModel : INotifyPropertyChangedAsync
{
    public event AsyncEvent<AsyncPropertyChangedEventArgs> PropertyChangedAsync;

    private string _name = "";
    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            _ = PropertyChangedAsync.InvokeAsync(new AsyncPropertyChangedEventArgs(nameof(Name)));
        }
    }
}
```

## Design Philosophy

`AsyncEventHandler<TArgs>` is a struct wrapping a `State` reference — copy semantics are intentional and match how `event` fields work in C#. Use it as a `private` field and expose `Register`/`Unregister` methods, or use the `+=`/`-=` operators directly.

`CancellationToken` is threaded through every call site — handlers opt into cooperative cancellation at the delegate boundary. Sequential mode respects cancellation between handler invocations; parallel mode checks before dispatch.

See [docs/](docs/README.md) for full documentation.
```

**Step 2: Commit**

```bash
git add README.md
git commit -m "docs: add full README with benchmark results and quick start"
```

---

## Task 8: Create docs folder

**Files:**
- Create: `docs/README.md`
- Create: `docs/getting-started.md`
- Create: `docs/invoke-modes.md`
- Create: `docs/async-notify-interfaces.md`
- Create: `docs/async-event-args.md`
- Create: `docs/advanced.md`
- Create: `docs/testing.md`
- Create: `docs/performance.md`

**Step 1: Create docs/README.md**

```markdown
# ZeroAlloc.AsyncEvents Documentation

## Contents

- [Getting Started](getting-started.md) — installation, first event, register/unregister
- [Invoke Modes](invoke-modes.md) — Parallel vs Sequential, when to use each
- [Async INotify* Interfaces](async-notify-interfaces.md) — MVVM and data-binding integration
- [Async Event Args](async-event-args.md) — built-in args types and cancellation
- [Advanced](advanced.md) — struct semantics, field vs property handlers, mode override
- [Testing](testing.md) — testing async event handlers with xunit
- [Performance](performance.md) — benchmark results and methodology
```

**Step 2: Create docs/getting-started.md**

```markdown
# Getting Started

## Installation

```
dotnet add package ZeroAlloc.AsyncEvents
```

## Declare a handler

`AsyncEventHandler<TArgs>` is a struct. Declare it as a private field:

```csharp
private AsyncEventHandler<OrderPlacedArgs> _orderPlaced = new(InvokeMode.Parallel);
```

## Register a handler

```csharp
_orderPlaced.Register(OnOrderPlacedAsync);

private async ValueTask OnOrderPlacedAsync(OrderPlacedArgs args, CancellationToken ct)
{
    await SendEmailAsync(args.OrderId, ct);
}
```

Or with a lambda:

```csharp
_orderPlaced.Register(async (args, ct) => await SendEmailAsync(args.OrderId, ct));
```

## Invoke

```csharp
await _orderPlaced.InvokeAsync(new OrderPlacedArgs(orderId), cancellationToken);
```

Returns `ValueTask` — await it or fire-and-forget with `_ =`.

## Unregister

```csharp
_orderPlaced.Unregister(OnOrderPlacedAsync);
```

Use `+=` / `-=` operators as shorthand:

```csharp
_orderPlaced += OnOrderPlacedAsync;
_orderPlaced -= OnOrderPlacedAsync;
```
```

**Step 3: Create docs/invoke-modes.md**

```markdown
# Invoke Modes

`InvokeMode` controls how handlers are called when `InvokeAsync` is invoked.

## Parallel (default)

```csharp
var handler = new AsyncEventHandler<string>(InvokeMode.Parallel);
```

All handlers are started concurrently. An `ArrayPool<Task>` is rented for the fan-out, then returned after `Task.WhenAll`. Allocates 0 bytes on the heap when all handlers return synchronously.

**Use when:** handlers are independent and order doesn't matter. Most event scenarios.

## Sequential

```csharp
var handler = new AsyncEventHandler<string>(InvokeMode.Sequential);
```

Handlers are awaited one-by-one in registration order. Each handler completes before the next begins. `CancellationToken` is checked between each invocation.

**Use when:** handlers must run in order, or earlier handlers affect later ones (e.g., validation pipelines).

## Per-call override

```csharp
// Field is Parallel, but this specific call is Sequential:
await _handler.InvokeAsync(args, InvokeMode.Sequential, ct);
```

Useful when the same handler is sometimes called in contexts requiring sequential semantics.
```

**Step 4: Create docs/async-notify-interfaces.md**

```markdown
# Async INotify* Interfaces

Drop-in async alternatives to the standard `INotify*` interfaces, useful for MVVM and data-binding scenarios where property or collection changes require async work.

## INotifyPropertyChangedAsync

```csharp
public interface INotifyPropertyChangedAsync
{
    event AsyncEvent<AsyncPropertyChangedEventArgs> PropertyChangedAsync;
}
```

Implement on a ViewModel:

```csharp
public class OrderViewModel : INotifyPropertyChangedAsync
{
    public event AsyncEvent<AsyncPropertyChangedEventArgs> PropertyChangedAsync;

    private string _status = "";
    public string Status
    {
        get => _status;
        set
        {
            _status = value;
            _ = PropertyChangedAsync.InvokeAsync(
                new AsyncPropertyChangedEventArgs(nameof(Status)));
        }
    }
}
```

## INotifyPropertyChangingAsync

Fires before the property changes. Same pattern as `INotifyPropertyChangedAsync` but with `AsyncPropertyChangingEventArgs`.

## INotifyCollectionChangedAsync

```csharp
public interface INotifyCollectionChangedAsync
{
    event AsyncEvent<AsyncCollectionChangedEventArgs> CollectionChangedAsync;
}
```

## INotifyDataErrorInfoAsync

```csharp
public interface INotifyDataErrorInfoAsync
{
    event AsyncEvent<AsyncErrorsChangedEventArgs> ErrorsChangedAsync;
}
```
```

**Step 5: Create docs/async-event-args.md**

```markdown
# Async Event Args

Built-in event args types matching the standard `System.ComponentModel` args, extended with async-friendly patterns.

## AsyncPropertyChangedEventArgs

```csharp
var args = new AsyncPropertyChangedEventArgs(propertyName: nameof(MyProperty));
```

Equivalent to `PropertyChangedEventArgs` — carries the property name.

## AsyncPropertyChangingEventArgs

```csharp
var args = new AsyncPropertyChangingEventArgs(propertyName: nameof(MyProperty));
```

Equivalent to `PropertyChangingEventArgs`.

## AsyncCollectionChangedEventArgs

```csharp
var args = new AsyncCollectionChangedEventArgs(action, newItems, oldItems);
```

Equivalent to `NotifyCollectionChangedEventArgs`.

## AsyncErrorsChangedEventArgs

```csharp
var args = new AsyncErrorsChangedEventArgs(propertyName: nameof(MyProperty));
```

Equivalent to `DataErrorsChangedEventArgs`.

## CancellationToken

Every `AsyncEvent<TArgs>` delegate receives a `CancellationToken` as its second parameter. Handlers should pass it through to any awaitable work:

```csharp
handler.Register(async (args, ct) =>
{
    await DoWorkAsync(args, ct); // pass ct through
});
```
```

**Step 6: Create docs/advanced.md**

```markdown
# Advanced

## Struct semantics

`AsyncEventHandler<TArgs>` is a `struct` wrapping an internal `State` reference class. This means:

- Copying the struct shares the same underlying handler list.
- Assigning a new `AsyncEventHandler<TArgs>` to a field replaces the field but doesn't affect existing copies.
- Use as a `private` field; expose registration via methods or operators.

```csharp
// Correct — field is mutated in-place via CAS
private AsyncEventHandler<string> _handler = new(InvokeMode.Parallel);

public void Subscribe(AsyncEvent<string> cb) => _handler.Register(cb);
public void Unsubscribe(AsyncEvent<string> cb) => _handler.Unregister(cb);
```

## Field-level InvokeSequentially

The `InvokeAsync(args, InvokeMode, ct)` overload lets a field declared as `Parallel` be invoked sequentially at specific call sites:

```csharp
private AsyncEventHandler<string> _handler = new(InvokeMode.Parallel);

// Normally parallel:
await _handler.InvokeAsync(args, ct);

// This specific call is sequential:
await _handler.InvokeAsync(args, InvokeMode.Sequential, ct);
```

## Lock-free registration

`Register` and `Unregister` use a CAS (compare-and-swap) loop on an array reference — no `lock` statement. Safe to call from multiple threads simultaneously. Duplicate registration is a no-op (reference equality check).

## Zero allocations on empty handler list

If no handlers are registered, `InvokeAsync` returns `default(ValueTask)` immediately — no allocations, no state machine created.
```

**Step 7: Create docs/testing.md**

```markdown
# Testing

## Testing a class that raises async events

Use a simple handler capture:

```csharp
public class OrderServiceTests
{
    [Fact]
    public async Task PlaceOrder_RaisesOrderPlacedAsync()
    {
        var service = new OrderService();
        AsyncPropertyChangedEventArgs? captured = null;

        service.OrderPlacedAsync += (args, ct) =>
        {
            captured = args;
            return ValueTask.CompletedTask;
        };

        await service.PlaceOrderAsync("order-1");

        Assert.NotNull(captured);
        Assert.Equal("order-1", captured.OrderId);
    }
}
```

## Testing that handlers are invoked in order (Sequential)

```csharp
[Fact]
public async Task Sequential_InvokesInRegistrationOrder()
{
    var handler = new AsyncEventHandler<string>(InvokeMode.Sequential);
    var order = new List<int>();

    handler.Register(async (_, ct) => { order.Add(1); await Task.Yield(); });
    handler.Register(async (_, ct) => { order.Add(2); await Task.Yield(); });
    handler.Register((_, ct) => { order.Add(3); return ValueTask.CompletedTask; });

    await handler.InvokeAsync("test");

    Assert.Equal(new[] { 1, 2, 3 }, order);
}
```

## Testing cancellation

```csharp
[Fact]
public async Task Sequential_ThrowsOnCancellation()
{
    var handler = new AsyncEventHandler<string>(InvokeMode.Sequential);
    handler.Register(async (_, ct) => await Task.Delay(1000, ct));

    var cts = new CancellationTokenSource();
    cts.Cancel();

    await Assert.ThrowsAsync<OperationCanceledException>(
        () => handler.InvokeAsync("test", cts.Token).AsTask());
}
```

## Testing register/unregister

```csharp
[Fact]
public async Task Unregister_RemovesHandler()
{
    var handler = new AsyncEventHandler<string>(InvokeMode.Parallel);
    var called = false;

    AsyncEvent<string> cb = (_, ct) => { called = true; return ValueTask.CompletedTask; };
    handler.Register(cb);
    handler.Unregister(cb);

    await handler.InvokeAsync("test");

    Assert.False(called);
}
```
```

**Step 8: Create docs/performance.md**

```markdown
# Performance

## Methodology

Benchmarks run with [BenchmarkDotNet](https://benchmarkdotnet.org/) 0.14 on .NET 9, Release configuration, MemoryDiagnoser enabled.

10 handlers registered per scenario. Each handler returns `ValueTask.CompletedTask` / `Task.CompletedTask` to isolate dispatch overhead.

Run yourself:

```bash
dotnet run --project benchmarks/AsyncEvents.Benchmarks -c Release -- --filter "*EventHandlerComparison*"
```

## Framework Comparison (10 handlers)

<!-- Paste EventHandlerComparisonBenchmarks output here after running Task 6 -->

| Method | Mean | Allocated |
|---|---|---|
| Sync_MulticastDelegate_10Handlers | — | 0 B |
| NaiveAsync_TaskWhenAll_10Handlers | — | — |
| ZeroAlloc_Parallel_10Handlers | — | 0 B |
| ZeroAlloc_Sequential_10Handlers | — | 0 B |

## Allocation notes

- **Parallel mode:** Rents a `Task[]` from `ArrayPool<Task>.Shared`. After `Task.WhenAll` the array is returned. No heap allocation when all handlers complete synchronously (array return path skips pool for completed tasks).
- **Sequential mode:** No array rented. Pure `foreach` + `await`. Zero allocations.
- **Empty handler list:** `InvokeAsync` returns `default(ValueTask)` — no state machine, no allocation.
- **NaiveAsync baseline:** `GetInvocationList()` allocates a new `object[]` per call; `new Task[n]` allocates per call.
```

**Step 9: Commit docs**

```bash
git add docs/
git commit -m "docs: add structured docs folder (getting-started, modes, INotify*, args, advanced, testing, performance)"
```

---

## Task 9: Verify full build and test

**Step 1: Full restore and build**

```bash
dotnet restore && dotnet build -c Release
```

Expected: All projects build. Zero errors.

**Step 2: Run all tests**

```bash
dotnet test -c Release
```

Expected: All tests pass.

**Step 3: Verify pack produces valid nupkg**

```bash
dotnet pack src/ZeroAlloc.AsyncEvents/ZeroAlloc.AsyncEvents.csproj -c Release /p:Version=0.1.0-preview -o ./artifacts
```

Expected: `artifacts/ZeroAlloc.AsyncEvents.0.1.0-preview.nupkg` created.
Inspect with: `dotnet nuget verify ./artifacts/ZeroAlloc.AsyncEvents.0.1.0-preview.nupkg` or unzip and check `README.md` and `icon.png` are embedded.

**Step 4: Commit if any fixes needed, otherwise done**

```bash
git add -A
git commit -m "chore: verify publish readiness"
```
