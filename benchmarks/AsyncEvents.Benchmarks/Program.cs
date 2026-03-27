using BenchmarkDotNet.Running;
using AsyncEvents.Benchmarks;

BenchmarkRunner.Run(new[]
{
    BenchmarkConverter.TypeToBenchmarks(typeof(AsyncEventHandlerBenchmarks)),
    BenchmarkConverter.TypeToBenchmarks(typeof(EventHandlerComparisonBenchmarks)),
});
