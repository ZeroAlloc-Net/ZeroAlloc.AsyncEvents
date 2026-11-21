# Performance

## Methodology

Measured 2026-10-17 with [BenchmarkDotNet](https://benchmarkdotnet.org/) v0.15.8, Release
configuration, `MemoryDiagnoser` enabled, on an otherwise idle machine:

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
12th Gen Intel Core i9-12900HK 2.50GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.402
  net10.0 : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3
```

> Allocations are exact. Speed is shown as same-run ratios; absolute timings are not published.

Each benchmark ran out of process in three launches, pinned to two performance cores
(`--affinity 20`). `Ratio` is each row's mean time relative to the class baseline in the same run;
`Launch range` is the lowest and highest ratio of the three launches. The benchmark project targets
the library's LTS framework, net10.0. The library also targets net11.0, which BenchmarkDotNet does not
support in a stable release yet.

10 handlers are registered per scenario. Each handler returns `ValueTask.CompletedTask` /
`Task.CompletedTask` to isolate dispatch overhead.

Run yourself:

```bash
dotnet run --project benchmarks/AsyncEvents.Benchmarks -c Release -- --filter "*" --launchCount 3
```

## Comparison with plain events (10 handlers)

The baseline is a synchronous multicast `EventHandler<T>`.

| Method | Ratio | Launch range | Allocated |
|---|---:|---:|---:|
| Sync_MulticastDelegate_10Handlers | 1.00 | | 0 B |
| NaiveAsync_TaskWhenAll_10Handlers | 11.93 | 10.08–13.03 | 280 B |
| ZeroAlloc_Parallel_10Handlers | 0.44 | 0.43–0.45 | 0 B |
| ZeroAlloc_Sequential_10Handlers | 0.61 | 0.52–0.89 | 0 B |

Both ZeroAlloc modes allocate nothing when handlers complete synchronously, and both are faster than
a sync multicast delegate. Parallel mode is 27× faster than naive async in the same run, which
allocates 280 B per call.

## Cancelable events (10 handlers)

`CancelableAsyncEventHandler<T>` in sequential mode against a plain `AsyncEventHandler<T>` in
sequential mode. The args object is created once and reused, so the numbers cover dispatch only.

| Method | Ratio | Launch range | Allocated |
|---|---:|---:|---:|
| Sequential_Baseline_10Handlers | 1.00 | | 0 B |
| Sequential_NoCancel_10Handlers | 1.05 | 0.97–1.09 | 0 B |
| Sequential_CancelAtFirst_10Handlers | 1.19 | 1.17–1.22 | 0 B |

Checking `Cancel` before each handler costs at most a few percent over plain sequential dispatch,
and the cancelable handler allocates nothing whether or not a handler cancels.

## Parallel and sequential dispatch

`AsyncEventHandlerBenchmarks` measures each mode on its own, with no baseline: **0 B** per call for
both modes.

## Allocation notes

- **Parallel mode:** Handlers that complete synchronously and successfully are consumed in place, so when all of them do, nothing is rented and `Task.WhenAll` is never called: zero allocations. Otherwise only the unfinished or failed handlers go into a `Task[]` rented from `ArrayPool<Task>.Shared`, awaited with the `ReadOnlySpan<Task>` overload of `Task.WhenAll`, and the array is returned.
- **Sequential mode:** No array rented. Pure `foreach` + `await`. Zero allocations.
- **Empty handler list:** `InvokeAsync` returns `default(ValueTask)` — no state machine, no allocation.
- **NaiveAsync baseline:** `GetInvocationList()` allocates a new `object[]` per call; `new Task[n]` allocates per call — both are unavoidable with the multicast delegate approach.
