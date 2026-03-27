# Performance

## Methodology

Benchmarks run with [BenchmarkDotNet](https://benchmarkdotnet.org/) v0.14.0 on .NET 9, Release configuration, MemoryDiagnoser enabled.

10 handlers registered per scenario. Each handler returns `ValueTask.CompletedTask` / `Task.CompletedTask` to isolate dispatch overhead.

Run yourself:

```bash
dotnet run --project benchmarks/AsyncEvents.Benchmarks -c Release -- --filter "*EventHandlerComparison*"
```

## Framework Comparison (10 handlers)

| Method | Mean | Error | StdDev | Ratio | Allocated |
|---|---|---|---|---|---|
| Sync_MulticastDelegate_10Handlers | 22.96 ns | 0.926 ns | 2.597 ns | 1.01x | — |
| NaiveAsync_TaskWhenAll_10Handlers | 203.48 ns | 4.111 ns | 7.307 ns | 8.96x | 280 B |
| ZeroAlloc_Parallel_10Handlers | 70.10 ns | 1.446 ns | 4.103 ns | 3.09x | 136 B |
| ZeroAlloc_Sequential_10Handlers | 16.10 ns | 0.655 ns | 1.857 ns | 0.71x | — |

ZeroAlloc sequential mode is **30% faster than a sync multicast delegate** with zero allocations. ZeroAlloc parallel mode is **3× faster than naive async** with 51% less memory.

## Allocation notes

- **Parallel mode:** Rents a `Task[]` from `ArrayPool<Task>.Shared`. After `Task.WhenAll` the array is returned. On net10.0, uses the allocation-free `ReadOnlySpan<Task>` overload.
- **Sequential mode:** No array rented. Pure `foreach` + `await`. Zero allocations.
- **Empty handler list:** `InvokeAsync` returns `default(ValueTask)` — no state machine, no allocation.
- **NaiveAsync baseline:** `GetInvocationList()` allocates a new `object[]` per call; `new Task[n]` allocates per call — both are unavoidable with the multicast delegate approach.
