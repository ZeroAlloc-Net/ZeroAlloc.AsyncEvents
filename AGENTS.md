# AGENTS.md: ZeroAlloc.AsyncEvents

ZeroAlloc.AsyncEvents is a NuGet library for awaitable .NET events: `AsyncEventHandler<TArgs>` structs with
lock-free registration, `ValueTask` handlers and zero allocation per invocation when handlers complete
synchronously. A Roslyn source generator, bundled into the same package, writes `event` accessors for
fields marked `[AsyncEvent]`. One package ships: `ZeroAlloc.AsyncEvents`.

## Layout

- `src/ZeroAlloc.AsyncEvents.Generator` is `netstandard2.0` and `IsPackable=false`. It ships inside the
  library nupkg under `analyzers/dotnet/cs` through the `IncludeGeneratorInPackage` target in the library
  csproj. Never pack it on its own.
- The library targets `netstandard2.0;netstandard2.1;net8.0;net10.0;net11.0`. `Polyfills.cs` and the
  `System.Buffers` and `System.Threading.Tasks.Extensions` references exist only for `netstandard2.0`.
  Code must compile on all five targets.
- `tests/ZeroAlloc.AsyncEvents.Generator.Tests/Snapshots/*.verified.cs` are Verify snapshots of generated
  output. They are data: the test csproj removes `*.verified.*` from compilation. A generator change
  that alters output means updating them on purpose, never accepting `*.received.*` blindly.
- `samples/ZeroAlloc.AsyncEvents.AotSmoke` is a NativeAOT canary, not a sample for users.
  `benchmarks/AsyncEvents.Benchmarks` is not shipped.
- `docs/` holds the user documentation. `docs/plans` and `docs/backlog.md` are internal notes.

## Build and test

`global.json` pins SDK 11.0.100 with `rollForward: latestFeature`, so the .NET 11 SDK is required. Test
runtimes by project:

- `ZeroAlloc.AsyncEvents.Tests` targets `net8.0;net10.0;net11.0`: needs the .NET 8, 10 and 11 runtimes.
- `ZeroAlloc.AsyncEvents.Generator.Tests` targets `net9.0;net11.0`: needs the .NET 9 and 11 runtimes.

CI job `build` in `.github/workflows/ci.yml`, `$VERSION` being the GitVersion `fullSemVer`:

```bash
dotnet restore
dotnet build --no-restore -c Release /p:Version=$VERSION
dotnet test --no-build -c Release --collect "XPlat Code Coverage" --settings coverage.runsettings --results-directory TestResults
dotnet pack src/ZeroAlloc.AsyncEvents/ZeroAlloc.AsyncEvents.csproj --no-build -c Release /p:Version=$VERSION -o ./artifacts
```

CI job `aot-smoke` in `ci.yml` (needs clang and zlib1g-dev):

```bash
dotnet restore samples/ZeroAlloc.AsyncEvents.AotSmoke/ZeroAlloc.AsyncEvents.AotSmoke.csproj
dotnet publish samples/ZeroAlloc.AsyncEvents.AotSmoke/ZeroAlloc.AsyncEvents.AotSmoke.csproj -r linux-x64 -c Release -o ./aot-out
./aot-out/ZeroAlloc.AsyncEvents.AotSmoke
```

Fast inner loop, run in this repo and passing:

```bash
dotnet test tests/ZeroAlloc.AsyncEvents.Tests -c Release -f net8.0 --filter "FullyQualifiedName~SequentialInvokeAllocationTests"
```

The `Release` build sets `TreatWarningsAsErrors`, so analyzer warnings from Meziantou, Roslynator and
ZeroAlloc.Analyzers fail the build. Fix them, do not suppress them.

## Conventions and gotchas

- Pass `-p:Version` to every command that builds before a pack. `Directory.Build.props` fails `pack` with
  `ZAVERSION001` when no version was supplied and `ZAVERSION002` when the assembly was built without it.
  Building without a version and then running `pack --no-build` is the failure it guards against.
- Public API is tracked by `Microsoft.CodeAnalysis.PublicApiAnalyzers` (RS0016 and RS0017 are errors). A
  new public member goes into `src/ZeroAlloc.AsyncEvents/PublicAPI.Unshipped.txt`. Never edit
  `PublicAPI.Shipped.txt` by hand: release automation moves entries across, and the `api-compat` and
  `release-tracking` jobs in `ci.yml` check it.
- Generator diagnostics (`ZAAE001` to `ZAAE003`) are tracked in `src/ZeroAlloc.AsyncEvents.Generator/AnalyzerReleases.Unshipped.md`.
  A new diagnostic needs an entry there and a section in `docs/diagnostics.md`. Changing the severity or
  category of a shipped rule, or removing its ID, fails the build (RS2000 to RS2008).
- Generator code must stay incremental: models in `Models/` are equatable (`EquatableArray`) and carry no
  `Compilation` or `ISymbol` references. `IncrementalCachingTests` guards this.
- The zero-allocation claim is asserted by tests on every target (`SequentialInvokeAllocationTests`,
  `ParallelInvokeTests`). A change on the invoke path that allocates when handlers complete synchronously
  must fail those tests, not be loosened.
- The `AotSmoke` project sets `IL2026`, `IL2067`, `IL2075`, `IL2091`, `IL3050` and `IL3051` as errors.
  Generated accessors and the library must stay reflection-free.
- CI gates line coverage at 80 percent through `coverage-gate`. Generated and `*.g.cs` code, test and
  benchmark assemblies are excluded in `coverage.runsettings`.
- The benchmark project targets only `net8.0` and `net10.0`: BenchmarkDotNet has no stable `net11.0`
  support. The `smoke` job in `benchmarks.yml` proves every benchmark still runs.

<!-- org-core:start sha256=295bb965a292aae97d5f2608dfafb8bff097e736fb7656ef760b142a9a66db68 (generated from docker-nuc scripts/agents-md/org-core-zeroalloc.md; do not edit here) -->
## Org rules

These rules hold in every ZeroAlloc-Net repo. Each library makes two promises: it works under
NativeAOT and trimming, and its hot path allocates nothing. The 2026-10-11 sweep found 16 of 28 repos
allocating on the hot path, most with docs claiming 0 B.

### Hot path

The operation a consumer runs repeatedly: `Send`, a cache hit, `Validate`, `Map`, `Serialize`, a
resilience call, a state transition, an append, a worker poll, including the idle poll. Startup and
registration are not hot path. For a source generator, the hot path is the code it emits: judge the
snapshots and the generated output.

### Allocation traps the sweep found

| Pattern | Instead |
|---|---|
| `async ValueTask` that only awaits and returns | `return new ValueTask<T>(inner.XAsync(ct));` |
| `Task.WhenAll` over tasks that already completed | skip the await only when every task has `IsCompletedSuccessfully` (`Status == TaskStatus.RanToCompletion` on `netstandard2.0`); otherwise `await Task.WhenAll`, which also reports faults of completed tasks |
| `TagList` or log `params object[]` built before `Enabled`/`IsEnabled` | check first, or `LoggerMessage.Define` |
| event args allocated before checking for subscribers | copy the handler to a local, check, then allocate |
| `new Serializer()` / `new Utf8JsonWriter()` per call | cache stateless instances, reuse writers with `Reset` |
| `GetString()` / `ToString()` round-trips in JSON converters | parse and format through spans |
| `ToArray()` / `ToList()` copies between layers | pass `ReadOnlyMemory<T>` or spans, copy once at most |
| LINQ in emitted code | emit `for` loops |
| `yield` iterator behind a public `GetEnumerator` | public struct enumerator, from the first release |
| `Enum.IsDefined(typeof(E), v)`, `decimal.GetBits(v)` | `Enum.IsDefined<E>`, `v.Scale` (`Scale` needs .NET 7 or later: keep a target-specific alternative on older targets and on `netstandard2.0` and `netstandard2.1`) |
| configuration parsed or read per call | parse once and cache |
| an `AsyncLocal` write per call | pass state explicitly |
| a capturing lambda or method-group delegate per call | cache the delegate in a field, or a static lambda |
| a per-key object that is never removed | remove on release; otherwise it is a leak |

Inherent allocations are fine when documented: the consumer's own result, an exception on a failure
path, a one-time cache fill.

### Tests and benchmarks

- Every hot path has an allocation gate test (`AllocationGate` from ZeroAlloc.TestHelpers) that measures
  the whole public call, including any state setup.
- Budgets are exact. If a path allocates, assert that number and comment what it is.
- Test the generated code, not a hand-written stand-in, and cover every shape: each attribute, nested
  collections, and the asynchronous case with a fake that returns pending tasks.
- Benchmarks run in CI through the shared `benchmark-smoke.yml` and target the library's LTS
  frameworks. The benchmark body allocates nothing besides the library itself. Run locally with
  `--inProcess`.
- Publish exact allocations and same-run ratios against a `Baseline`, never absolute timings. Run with
  `--launchCount 3`, pin CPUs with `taskset`, give the spread, and label every table.

### NativeAOT and trimming

- Packable runtime projects, and projects consumers publish, set `IsAotCompatible=true` on modern
  target frameworks. Analyzers and source generators target `netstandard2.0`.
- No reflection or runtime code generation on a supported path. An opt-in reflection fallback carries
  honest `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]`. Every suppression must be true;
  remove dead ones.
- The AOT smoke sample publishes with `TreatWarningsAsErrors=true`. If a third-party warning really
  blocks it, use a narrow, justified suppression for that warning. `WarningsNotAsErrors` selects codes,
  not assemblies, so it would also downgrade the same warning from generated code.
- The smoke runs the hot path of every package in the repo. A package without a smoke has unknown AOT
  status. Adapters run against real services in CI service containers; publish-only is a last resort.
  App and template smokes make authenticated requests.

### Docs and shipping

- Docs match measurements. After a hot-path change, re-measure and update `docs/performance.md` and the
  README. Never write "0 B" without a test that asserts it.
- New public API is `feat:`, even for a performance change. Public API changes are additive: never add
  an optional parameter to a shipped signature. A breaking end state goes in a `next-major` issue.
- A behaviour change gets a `## Behaviour change` section in the PR body.
- Conventional Commits with release-please. Subjects start lowercase, body lines stay within 100
  characters, and a body never contains nested parentheses: release-please drops the whole commit.
- Local packs use `9.9.9-local.<sha>`. After a release, verify the package on NuGet.
- Fix review findings in the PR; file an issue only for what cannot go in it.

Full rules, template wiring lessons and the reasons behind each rule: https://github.com/ZeroAlloc-Net/.github/blob/main/AGENTS.md
<!-- org-core:end -->
