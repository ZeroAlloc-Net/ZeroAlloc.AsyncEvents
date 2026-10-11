# Benchmark results

`EventHandlerComparisonBenchmarks`, 10 handlers, on each of the library's LTS targets.

## net8.0

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i9-12900HK 2.50GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.401
  [Host] : .NET 8.0.31 (8.0.31, 8.0.3126.42015), X64 RyuJIT x86-64-v3

Toolchain=InProcessEmitToolchain  

```
| Method                            | Mean      | Error    | StdDev    | Median    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------------------------------- |----------:|---------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| Sync_MulticastDelegate_10Handlers |  57.81 ns | 1.246 ns |  3.675 ns |  57.78 ns |  1.00 |    0.09 |      - |         - |          NA |
| NaiveAsync_TaskWhenAll_10Handlers | 476.80 ns | 9.552 ns | 12.080 ns | 476.47 ns |  8.28 |    0.57 | 0.0229 |     288 B |          NA |
| ZeroAlloc_Parallel_10Handlers     |  35.52 ns | 1.271 ns |  3.728 ns |  36.94 ns |  0.62 |    0.08 |      - |         - |          NA |
| ZeroAlloc_Sequential_10Handlers   |  47.75 ns | 0.350 ns |  0.328 ns |  47.69 ns |  0.83 |    0.05 |      - |         - |          NA |

## net10.0

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i9-12900HK 2.50GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Toolchain=InProcessEmitToolchain  

```
| Method                            | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------------------------------- |----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| Sync_MulticastDelegate_10Handlers |  40.69 ns |  0.501 ns |  0.444 ns |  1.00 |    0.01 |      - |         - |          NA |
| NaiveAsync_TaskWhenAll_10Handlers | 440.47 ns | 20.112 ns | 59.300 ns | 10.83 |    1.46 | 0.0219 |     280 B |          NA |
| ZeroAlloc_Parallel_10Handlers     |  18.31 ns |  1.093 ns |  3.207 ns |  0.45 |    0.08 |      - |         - |          NA |
| ZeroAlloc_Sequential_10Handlers   |  29.60 ns |  1.067 ns |  3.147 ns |  0.73 |    0.08 |      - |         - |          NA |
