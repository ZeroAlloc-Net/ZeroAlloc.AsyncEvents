using BenchmarkDotNet.Attributes;
using ZeroAlloc.AsyncEvents;

namespace AsyncEvents.Benchmarks;

/// <summary>
/// Compares CancelableAsyncEventHandler sequential invocation:
/// no cancellation vs early cancel at handler 1 (out of 10).
/// Baseline is the equivalent AsyncEventHandler sequential for overhead comparison.
/// </summary>
[MemoryDiagnoser]
public class CancelableEventHandlerBenchmarks
{
    private AsyncEventHandler<string> _baselineSequential;
    private CancelableAsyncEventHandler<CancelEventArgs> _noCancelSequential;
    private CancelableAsyncEventHandler<CancelEventArgs> _cancelAtFirstSequential;

    // One args instance per benchmark, reset before each call, so the measurement covers the
    // dispatch and not the caller's own args allocation.
    private readonly CancelEventArgs _noCancelArgs = new();
    private readonly CancelEventArgs _cancelAtFirstArgs = new();

    [GlobalSetup]
    public void Setup()
    {
        _baselineSequential = new AsyncEventHandler<string>(InvokeMode.Sequential);
        _noCancelSequential = new CancelableAsyncEventHandler<CancelEventArgs>(InvokeMode.Sequential);
        _cancelAtFirstSequential = new CancelableAsyncEventHandler<CancelEventArgs>(InvokeMode.Sequential);

        for (var i = 0; i < 10; i++)
        {
            _baselineSequential.Register((s, ct) => ValueTask.CompletedTask);
            _noCancelSequential.Register((args, ct) => ValueTask.CompletedTask);
        }

        // First handler sets Cancel = true, remaining 9 are skipped
        _cancelAtFirstSequential.Register((args, ct) => { args.Cancel = true; return ValueTask.CompletedTask; });
        for (var i = 0; i < 9; i++)
            _cancelAtFirstSequential.Register((args, ct) => ValueTask.CompletedTask);
    }

    [Benchmark(Baseline = true)]
    public ValueTask Sequential_Baseline_10Handlers()
        => _baselineSequential.InvokeAsync("test");

    [Benchmark]
    public ValueTask Sequential_NoCancel_10Handlers()
        => _noCancelSequential.InvokeAsync(_noCancelArgs);

    [Benchmark]
    public ValueTask Sequential_CancelAtFirst_10Handlers()
    {
        _cancelAtFirstArgs.Cancel = false;
        return _cancelAtFirstSequential.InvokeAsync(_cancelAtFirstArgs);
    }
}
