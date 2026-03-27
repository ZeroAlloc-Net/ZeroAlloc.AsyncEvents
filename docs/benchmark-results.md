```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8037)
Unknown processor
.NET SDK 10.0.104
  [Host]     : .NET 9.0.14 (9.0.1426.11910), X64 RyuJIT AVX2
  DefaultJob : .NET 9.0.14 (9.0.1426.11910), X64 RyuJIT AVX2


```
| Method                            | Mean      | Error    | StdDev   | Median    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------------------------------- |----------:|---------:|---------:|----------:|------:|--------:|-------:|----------:|------------:|
| Sync_MulticastDelegate_10Handlers |  22.96 ns | 0.926 ns | 2.597 ns |  22.35 ns |  1.01 |    0.15 |      - |         - |          NA |
| NaiveAsync_TaskWhenAll_10Handlers | 203.48 ns | 4.111 ns | 7.307 ns | 202.49 ns |  8.96 |    0.97 | 0.0222 |     280 B |          NA |
| ZeroAlloc_Parallel_10Handlers     |  70.10 ns | 1.446 ns | 4.103 ns |  68.66 ns |  3.09 |    0.36 | 0.0107 |     136 B |          NA |
| ZeroAlloc_Sequential_10Handlers   |  16.10 ns | 0.655 ns | 1.857 ns |  15.31 ns |  0.71 |    0.11 |      - |         - |          NA |
