using BenchmarkDotNet.Attributes;
using ZeroAlloc.AsyncEvents;

namespace AsyncEvents.Benchmarks;

[MemoryDiagnoser]
public class AsyncEventHandlerBenchmarks
{
    private AsyncEventHandler<string> _parallelHandler;
    private AsyncEventHandler<string> _sequentialHandler;

    [GlobalSetup]
    public void Setup()
    {
        _parallelHandler = new AsyncEventHandler<string>(InvokeMode.Parallel);
        _sequentialHandler = new AsyncEventHandler<string>(InvokeMode.Sequential);

        for (var i = 0; i < 10; i++)
        {
            _parallelHandler.Register((s, ct) => ValueTask.CompletedTask);
            _sequentialHandler.Register((s, ct) => ValueTask.CompletedTask);
        }
    }

    [Benchmark]
    public ValueTask Parallel_10Handlers() => _parallelHandler.InvokeAsync("test");

    [Benchmark]
    public ValueTask Sequential_10Handlers() => _sequentialHandler.InvokeAsync("test");
}
