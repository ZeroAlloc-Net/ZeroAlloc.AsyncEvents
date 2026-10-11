# Performance

## Methodology

Benchmarks run with [BenchmarkDotNet](https://benchmarkdotnet.org/) v0.15.8, Release configuration, MemoryDiagnoser enabled. The benchmark project targets the library's LTS frameworks, net8.0 and net10.0, which compile different parallel-dispatch code, so run it once per framework.

10 handlers registered per scenario. Each handler returns `ValueTask.CompletedTask` / `Task.CompletedTask` to isolate dispatch overhead.

Run yourself:

```bash
dotnet run --project benchmarks/AsyncEvents.Benchmarks -c Release -f net8.0 -- --filter "*EventHandlerComparison*"
dotnet run --project benchmarks/AsyncEvents.Benchmarks -c Release -f net10.0 -- --filter "*EventHandlerComparison*"
```

## Framework Comparison (10 handlers)

**net8.0**

| Method | Mean | Error | StdDev | Ratio | Allocated |
|---|---|---|---|---|---|
| Sync_MulticastDelegate_10Handlers | 57.81 ns | 1.246 ns | 3.675 ns | 1.00 | — |
| NaiveAsync_TaskWhenAll_10Handlers | 476.80 ns | 9.552 ns | 12.080 ns | 8.28x | 288 B |
| ZeroAlloc_Parallel_10Handlers | 35.52 ns | 1.271 ns | 3.728 ns | 0.62x | — |
| ZeroAlloc_Sequential_10Handlers | 47.75 ns | 0.350 ns | 0.328 ns | 0.83x | — |

**net10.0**

| Method | Mean | Error | StdDev | Ratio | Allocated |
|---|---|---|---|---|---|
| Sync_MulticastDelegate_10Handlers | 40.69 ns | 0.501 ns | 0.444 ns | 1.00 | — |
| NaiveAsync_TaskWhenAll_10Handlers | 440.47 ns | 20.112 ns | 59.300 ns | 10.83x | 280 B |
| ZeroAlloc_Parallel_10Handlers | 18.31 ns | 1.093 ns | 3.207 ns | 0.45x | — |
| ZeroAlloc_Sequential_10Handlers | 29.60 ns | 1.067 ns | 3.147 ns | 0.73x | — |

Both ZeroAlloc modes allocate nothing on either target when handlers complete synchronously, and both are faster than a sync multicast delegate. Parallel mode is more than 10× faster than naive async, which allocates 280–288 B per call.

## Allocation notes

- **Parallel mode:** Handlers that complete synchronously and successfully are consumed in place, so when all of them do, nothing is rented and `Task.WhenAll` is never called: zero allocations on every target. Otherwise only the unfinished or failed handlers go into a `Task[]` rented from `ArrayPool<Task>.Shared`, awaited with `Task.WhenAll`, and the array is returned. Below net10.0 that `WhenAll` call binds to the `IEnumerable<Task>` overload and allocates; on net10.0 it uses the `ReadOnlySpan<Task>` overload.
- **Sequential mode:** No array rented. Pure `foreach` + `await`. Zero allocations.
- **Empty handler list:** `InvokeAsync` returns `default(ValueTask)` — no state machine, no allocation.
- **NaiveAsync baseline:** `GetInvocationList()` allocates a new `object[]` per call; `new Task[n]` allocates per call — both are unavoidable with the multicast delegate approach.
