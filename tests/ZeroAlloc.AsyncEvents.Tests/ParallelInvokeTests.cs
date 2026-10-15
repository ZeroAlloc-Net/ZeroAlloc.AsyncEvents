using System.Threading.Tasks.Sources;
using ZeroAlloc.TestHelpers;

namespace ZeroAlloc.AsyncEvents.Tests;

/// <summary>
/// Pins the observable behaviour of <see cref="InvokeMode.Parallel"/> for every mix of handler
/// outcomes, for both handler structs: already succeeded, already faulted, already cancelled,
/// still pending, and throwing before returning a <see cref="ValueTask"/>. The parallel path has a
/// fast path for handlers that all complete synchronously and successfully; these tests make sure
/// it never changes what a caller sees when they do not.
/// </summary>
public class ParallelInvokeTests
{
    public static TheoryData<string> Kinds => new() { "plain", "cancelable" };

    private static Func<CancellationToken, ValueTask> Build(string kind, params AsyncEvent<CancelEventArgs>[] callbacks)
    {
        var args = new CancelEventArgs();
        if (string.Equals(kind, "plain", StringComparison.Ordinal))
        {
            var handler = new AsyncEventHandler<CancelEventArgs>(InvokeMode.Parallel);
            foreach (var cb in callbacks) handler.Register(cb);
            return ct => handler.InvokeAsync(args, ct);
        }

        var cancelable = new CancelableAsyncEventHandler<CancelEventArgs>(InvokeMode.Parallel);
        foreach (var cb in callbacks) cancelable.Register(cb);
        return ct => cancelable.InvokeAsync(args, ct);
    }

    private static AsyncEvent<CancelEventArgs> Succeeds(List<int> calls, int id) =>
        (_, _) => { calls.Add(id); return default; };

    private static AsyncEvent<CancelEventArgs> Faults(List<int> calls, int id, Exception ex) =>
        (_, _) => { calls.Add(id); return new ValueTask(Task.FromException(ex)); };

    private static AsyncEvent<CancelEventArgs> Cancels(List<int> calls, int id, CancellationToken token) =>
        (_, _) => { calls.Add(id); return new ValueTask(Task.FromCanceled(token)); };

    private static AsyncEvent<CancelEventArgs> Pending(List<int> calls, int id, TaskCompletionSource tcs) =>
        (_, _) => { calls.Add(id); return new ValueTask(tcs.Task); };

    [Theory]
    [MemberData(nameof(Kinds))]
    public void AllSucceedSynchronously_CompletesSynchronously_AndCallsEveryHandler(string kind)
    {
        var calls = new List<int>();
        var invoke = Build(kind, Succeeds(calls, 1), Succeeds(calls, 2), Succeeds(calls, 3));

        var vt = invoke(default);

        Assert.True(vt.IsCompletedSuccessfully);
        Assert.Equal(new[] { 1, 2, 3 }, calls);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void AllSucceedSynchronously_AllocatesNothing(string kind)
    {
        AsyncEvent<CancelEventArgs> noop = static (_, _) => default;
        var invoke = Build(kind, noop, (_, _) => default, (_, _) => default, (_, _) => default);

        var bytes = AllocationGate.MeasureBytesPerCall(1_000, () => invoke(default).GetAwaiter().GetResult());

        Assert.Equal(0, bytes);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task OneFaultedSynchronously_ThrowsIt_AfterCallingEveryHandler(string kind)
    {
        var calls = new List<int>();
        var boom = new InvalidOperationException("boom");
        var invoke = Build(kind, Succeeds(calls, 1), Faults(calls, 2, boom), Succeeds(calls, 3));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => invoke(default).AsTask());

        Assert.Same(boom, thrown);
        Assert.Equal(new[] { 1, 2, 3 }, calls);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task SeveralFaultedSynchronously_ThrowsTheFirstInRegistrationOrder(string kind)
    {
        var calls = new List<int>();
        var first = new InvalidOperationException("first");
        var second = new NotSupportedException("second");
        var invoke = Build(kind, Succeeds(calls, 1), Faults(calls, 2, first), Faults(calls, 3, second));

        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => invoke(default).AsTask());

        Assert.Same(first, thrown);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task CancelledSynchronously_ThrowsOperationCanceled(string kind)
    {
        var calls = new List<int>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var invoke = Build(kind, Succeeds(calls, 1), Cancels(calls, 2, cts.Token), Succeeds(calls, 3));

        var vt = invoke(default);

        Assert.True(vt.IsCanceled);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => vt.AsTask());
        Assert.Equal(new[] { 1, 2, 3 }, calls);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task FaultedAndCancelled_FaultWinsRegardlessOfOrder(string kind)
    {
        var calls = new List<int>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var boom = new InvalidOperationException("boom");
        var invoke = Build(kind, Cancels(calls, 1, cts.Token), Faults(calls, 2, boom));

        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => invoke(default).AsTask());

        Assert.Same(boom, thrown);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task SucceededAndPending_CompletesOnlyWhenThePendingHandlerDoes(string kind)
    {
        var calls = new List<int>();
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var invoke = Build(kind, Succeeds(calls, 1), Pending(calls, 2, tcs), Succeeds(calls, 3));

        var task = invoke(default).AsTask();

        Assert.False(task.IsCompleted);
        Assert.Equal(new[] { 1, 2, 3 }, calls);
        tcs.SetResult();
        await task;
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task FaultedAndPending_WaitsForThePendingHandler_ThenThrowsTheFault(string kind)
    {
        var calls = new List<int>();
        var boom = new InvalidOperationException("boom");
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var invoke = Build(kind, Faults(calls, 1, boom), Pending(calls, 2, tcs));

        var task = invoke(default).AsTask();

        Assert.False(task.IsCompleted);
        tcs.SetResult();
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Same(boom, thrown);
    }

    // Task.WhenAll records faults in the order it observes the tasks complete, not the order they
    // were passed in, so a handler that had already faulted when it returned is reported ahead of one that
    // faults later, wherever each was registered.
    public static TheoryData<string, bool> KindsAndOrder => new()
    {
        { "plain", true }, { "plain", false }, { "cancelable", true }, { "cancelable", false },
    };

    [Theory]
    [MemberData(nameof(KindsAndOrder))]
    public async Task SynchronousFault_WinsOverAPendingFault_WhicheverIsRegisteredFirst(string kind, bool pendingFirst)
    {
        var calls = new List<int>();
        var pendingFault = new InvalidOperationException("pending");
        var syncFault = new NotSupportedException("sync");
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var invoke = pendingFirst
            ? Build(kind, Pending(calls, 1, tcs), Faults(calls, 2, syncFault))
            : Build(kind, Faults(calls, 1, syncFault), Pending(calls, 2, tcs));

        var task = invoke(default).AsTask();
        Assert.False(task.IsCompleted);
        tcs.SetException(pendingFault);

        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => task);
        Assert.Same(syncFault, thrown);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task TwoPendingFaults_TheFirstToCompleteWins(string kind)
    {
        var calls = new List<int>();
        var early = new InvalidOperationException("early");
        var late = new NotSupportedException("late");

        // WhenAll learns that a task completed through a continuation on it. With
        // RunContinuationsAsynchronously each SetException would queue that continuation to the
        // thread pool, where the two can run in either order, so "first to complete" would not be
        // the order the test faults them in. Without the option WhenAll observes each fault inside
        // its SetException call, so it sees them in exactly that order.
        var first = new TaskCompletionSource();
        var second = new TaskCompletionSource();
        var invoke = Build(kind, Pending(calls, 1, first), Pending(calls, 2, second));

        var task = invoke(default).AsTask();
        second.SetException(early);
        first.SetException(late);

        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => task);
        Assert.Same(early, thrown);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task HandlerThrowingBeforeReturning_FaultsTheInvocation_AndSkipsLaterHandlers(string kind)
    {
        var calls = new List<int>();
        var boom = new InvalidOperationException("boom");
        AsyncEvent<CancelEventArgs> throws = (_, _) => { calls.Add(2); throw boom; };
        var invoke = Build(kind, Succeeds(calls, 1), throws, Succeeds(calls, 3));

        var vt = invoke(default);

        Assert.True(vt.IsFaulted);
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => vt.AsTask());
        Assert.Same(boom, thrown);
        Assert.Equal(new[] { 1, 2 }, calls);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task TokenAlreadyCancelled_ThrowsWithoutCallingHandlers(string kind)
    {
        var calls = new List<int>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var invoke = Build(kind, Succeeds(calls, 1));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invoke(cts.Token).AsTask());

        Assert.Empty(calls);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task SourceBackedValueTasksThatSucceeded_AreConsumed(string kind)
    {
        var sources = new[] { new CompletedSource(), new CompletedSource() };
        var invoke = Build(
            kind,
            (_, _) => new ValueTask(sources[0], sources[0].Token),
            (_, _) => new ValueTask(sources[1], sources[1].Token));

        await invoke(default);

        Assert.All(sources, s => Assert.Equal(1, s.GetResultCalls));
    }

    /// <summary>
    /// A pooled-style <see cref="IValueTaskSource"/> that is already complete. A pooling source is
    /// only recycled when its result is read, so a dispatcher must call GetResult exactly once.
    /// </summary>
    private sealed class CompletedSource : IValueTaskSource
    {
        private ManualResetValueTaskSourceCore<bool> _core;

        public CompletedSource() => _core.SetResult(true);

        public short Token => _core.Version;

        public int GetResultCalls { get; private set; }

        public void GetResult(short token)
        {
            GetResultCalls++;
            _core.GetResult(token);
        }

        public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);

        public void OnCompleted(
            Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags) =>
            _core.OnCompleted(continuation, state, token, flags);
    }
}
