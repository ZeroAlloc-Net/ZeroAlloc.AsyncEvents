# Performance

## Methodology

Measured 2026-10-17 with [BenchmarkDotNet](https://benchmarkdotnet.org/) v0.15.8, Release
configuration, `MemoryDiagnoser` enabled, on an otherwise idle machine:

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
12th Gen Intel Core i9-12900HK 2.50GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.402
  net10.0 : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3
  net8.0  : .NET 8.0.32 (8.0.32, 8.0.3226.50405), X64 RyuJIT x86-64-v3
```

> Allocations are exact. Speed is shown as same-run ratios; absolute timings are not published.

Each benchmark ran out of process in three launches, pinned to two performance cores
(`--affinity 20`). `Ratio` is each row's mean time relative to the class baseline in the same run;
`Launch range` is the lowest and highest ratio of the three launches. The benchmark project targets
the library's LTS frameworks, net8.0 and net10.0, which compile different parallel-dispatch code, so
it runs once per framework.

10 handlers are registered per scenario. Each handler returns `ValueTask.CompletedTask` /
`Task.CompletedTask` to isolate dispatch overhead.

Run yourself:

```bash
dotnet run --project benchmarks/AsyncEvents.Benchmarks -c Release -f net8.0 -- --filter "*" --launchCount 3
dotnet run --project benchmarks/AsyncEvents.Benchmarks -c Release -f net10.0 -- --filter "*" --launchCount 3
```

## Framework comparison (10 handlers)

The baseline is a synchronous multicast `EventHandler<T>`.

**net10.0**

| Method | Ratio | Launch range | Allocated |
|---|---:|---:|---:|
| Sync_MulticastDelegate_10Handlers | 1.00 | | 0 B |
| NaiveAsync_TaskWhenAll_10Handlers | 11.93 | 10.08–13.03 | 280 B |
| ZeroAlloc_Parallel_10Handlers | 0.44 | 0.43–0.45 | 0 B |
| ZeroAlloc_Sequential_10Handlers | 0.61 | 0.52–0.89 | 0 B |

**net8.0**

| Method | Ratio | Launch range | Allocated |
|---|---:|---:|---:|
| Sync_MulticastDelegate_10Handlers | 1.00 | | 0 B |
| NaiveAsync_TaskWhenAll_10Handlers | 6.11 | 5.83–6.63 | 288 B |
| ZeroAlloc_Parallel_10Handlers | 0.53 | 0.48–0.57 | 0 B |
| ZeroAlloc_Sequential_10Handlers | 0.56 | 0.54–0.59 | 0 B |

Both ZeroAlloc modes allocate nothing on either target when handlers complete synchronously, and
both are faster than a sync multicast delegate. Parallel mode is 11–27× faster than naive async in
the same run, which allocates 280–288 B per call.

## Cancelable events (10 handlers)

`CancelableAsyncEventHandler<T>` in sequential mode against a plain `AsyncEventHandler<T>` in
sequential mode. The args object is created once and reused, so the numbers cover dispatch only.

| Method | net10.0 Ratio | net10.0 Launch range | net8.0 Ratio | net8.0 Launch range | Allocated |
|---|---:|---:|---:|---:|---:|
| Sequential_Baseline_10Handlers | 1.00 | | 1.00 | | 0 B |
| Sequential_NoCancel_10Handlers | 1.05 | 0.97–1.09 | 1.01 | 0.91–1.07 | 0 B |
| Sequential_CancelAtFirst_10Handlers | 1.19 | 1.17–1.22 | 1.13 | 1.08–1.18 | 0 B |

Checking `Cancel` before each handler costs at most a few percent over plain sequential dispatch,
and the cancelable handler allocates nothing whether or not a handler cancels.

## Parallel and sequential dispatch

`AsyncEventHandlerBenchmarks` measures each mode on its own, with no baseline: **0 B** per call for
both modes on net8.0 and net10.0.

## Allocation notes

- **Parallel mode:** Handlers that complete synchronously and successfully are consumed in place, so when all of them do, nothing is rented and `Task.WhenAll` is never called: zero allocations on every target. Otherwise only the unfinished or failed handlers go into a `Task[]` rented from `ArrayPool<Task>.Shared`, awaited with `Task.WhenAll`, and the array is returned. Below net10.0 that `WhenAll` call binds to the `IEnumerable<Task>` overload and allocates; on net10.0 it uses the `ReadOnlySpan<Task>` overload.
- **Sequential mode:** No array rented. Pure `foreach` + `await`. Zero allocations.
- **Empty handler list:** `InvokeAsync` returns `default(ValueTask)` — no state machine, no allocation.
- **NaiveAsync baseline:** `GetInvocationList()` allocates a new `object[]` per call; `new Task[n]` allocates per call — both are unavoidable with the multicast delegate approach.
