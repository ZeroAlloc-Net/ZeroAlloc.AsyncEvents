using BenchmarkDotNet.Attributes;
using ZeroAlloc.AsyncEvents;

namespace AsyncEvents.Benchmarks;

/// <summary>
/// Compares ZeroAlloc AsyncEventHandler against framework-native alternatives.
/// All variants register 10 handlers.
/// </summary>
[MemoryDiagnoser]
public class EventHandlerComparisonBenchmarks
{
    // 1. Standard sync multicast delegate (EventHandler<T>)
    private event EventHandler<EventArgs>? _syncEvent;

    // 2. Naive async: Func<string, Task> multicast delegate via Task.WhenAll
    private Func<string, Task>? _naiveAsyncDelegate;

    // 3. ZeroAlloc parallel
    private AsyncEventHandler<string> _zeroAllocParallel;

    // 4. ZeroAlloc sequential
    private AsyncEventHandler<string> _zeroAllocSequential;

    [GlobalSetup]
    public void Setup()
    {
        _zeroAllocParallel   = new AsyncEventHandler<string>(InvokeMode.Parallel);
        _zeroAllocSequential = new AsyncEventHandler<string>(InvokeMode.Sequential);

        for (var i = 0; i < 10; i++)
        {
            _syncEvent += (sender, e) => { };
            _naiveAsyncDelegate += s => Task.CompletedTask;
            _zeroAllocParallel.Register((s, ct)  => ValueTask.CompletedTask);
            _zeroAllocSequential.Register((s, ct) => ValueTask.CompletedTask);
        }
    }

    [Benchmark(Baseline = true)]
    public void Sync_MulticastDelegate_10Handlers()
        => _syncEvent?.Invoke(this, EventArgs.Empty);

    [Benchmark]
    public Task NaiveAsync_TaskWhenAll_10Handlers()
    {
        if (_naiveAsyncDelegate is null) return Task.CompletedTask;
        var handlers = _naiveAsyncDelegate.GetInvocationList();
        var tasks = new Task[handlers.Length];
        for (var i = 0; i < handlers.Length; i++)
            tasks[i] = ((Func<string, Task>)handlers[i])("test");
        return Task.WhenAll(tasks);
    }

    [Benchmark]
    public ValueTask ZeroAlloc_Parallel_10Handlers()
        => _zeroAllocParallel.InvokeAsync("test");

    [Benchmark]
    public ValueTask ZeroAlloc_Sequential_10Handlers()
        => _zeroAllocSequential.InvokeAsync("test");
}
