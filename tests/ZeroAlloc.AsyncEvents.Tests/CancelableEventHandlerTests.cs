namespace ZeroAlloc.AsyncEvents.Tests;

public class CancelableEventHandlerTests
{
    [Fact]
    public async Task Sequential_StopsAfterHandlerSetsCancelTrue()
    {
        var handler = new CancelableAsyncEventHandler<CancelEventArgs>(InvokeMode.Sequential);
        var calls = new List<int>();
        handler += (args, ct) => { calls.Add(1); args.Cancel = true; return ValueTask.CompletedTask; };
        handler += (args, ct) => { calls.Add(2); return ValueTask.CompletedTask; };

        await handler.InvokeAsync(new CancelEventArgs());

        Assert.Equal(new[] { 1 }, calls);
    }

    [Fact]
    public async Task Sequential_AlreadyCancelled_SkipsAllHandlers()
    {
        var handler = new CancelableAsyncEventHandler<CancelEventArgs>(InvokeMode.Sequential);
        var called = false;
        handler += (args, ct) => { called = true; return ValueTask.CompletedTask; };

        await handler.InvokeAsync(new CancelEventArgs(cancel: true));

        Assert.False(called);
    }

    [Fact]
    public async Task Parallel_RunsAllHandlersDespiteCancel()
    {
        var handler = new CancelableAsyncEventHandler<CancelEventArgs>(InvokeMode.Parallel);
        var calls = new List<int>();
        handler += (args, ct) => { calls.Add(1); args.Cancel = true; return ValueTask.CompletedTask; };
        handler += (args, ct) => { calls.Add(2); return ValueTask.CompletedTask; };

        await handler.InvokeAsync(new CancelEventArgs());

        Assert.Equal(2, calls.Count);
    }

    [Fact]
    public void Register_AddsCallback()
    {
        var handler = new CancelableAsyncEventHandler<CancelEventArgs>();
        AsyncEvent<CancelEventArgs> cb = (args, ct) => ValueTask.CompletedTask;
        handler.Register(cb);
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public void Unregister_RemovesCallback()
    {
        var handler = new CancelableAsyncEventHandler<CancelEventArgs>();
        AsyncEvent<CancelEventArgs> cb = (args, ct) => ValueTask.CompletedTask;
        handler.Register(cb);
        handler.Unregister(cb);
        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public void PlusOperator_RegistersCallback()
    {
        var handler = new CancelableAsyncEventHandler<CancelEventArgs>();
        AsyncEvent<CancelEventArgs> cb = (args, ct) => ValueTask.CompletedTask;
        handler += cb;
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public void MinusOperator_UnregistersCallback()
    {
        var handler = new CancelableAsyncEventHandler<CancelEventArgs>();
        AsyncEvent<CancelEventArgs> cb = (args, ct) => ValueTask.CompletedTask;
        handler += cb;
        handler -= cb;
        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public async Task InvokeAsync_EmptyHandler_DoesNotThrow()
    {
        var handler = new CancelableAsyncEventHandler<CancelEventArgs>();
        await handler.InvokeAsync(new CancelEventArgs());
    }

    [Fact]
    public async Task InvokeAsync_DefaultStruct_DoesNotThrow()
    {
        var handler = default(CancelableAsyncEventHandler<CancelEventArgs>);
        await handler.InvokeAsync(new CancelEventArgs());
    }

    [Fact]
    public async Task InvokeAsync_CancellationToken_IsPassedToHandlers()
    {
        var handler = new CancelableAsyncEventHandler<CancelEventArgs>();
        CancellationToken received = default;
        handler += (args, ct) => { received = ct; return ValueTask.CompletedTask; };

        using var cts = new CancellationTokenSource();
        await handler.InvokeAsync(new CancelEventArgs(), cts.Token);

        Assert.Equal(cts.Token, received);
    }

    [Fact]
    public async Task Sequential_PreCancelledToken_ThrowsBeforeFirstHandler()
    {
        var handler = new CancelableAsyncEventHandler<CancelEventArgs>(InvokeMode.Sequential);
        var called = false;
        handler += (args, ct) => { called = true; return ValueTask.CompletedTask; };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => handler.InvokeAsync(new CancelEventArgs(), cts.Token).AsTask());

        Assert.False(called);
    }

    [Fact]
    public async Task InvokeAsync_ModeOverride_Sequential_ShortCircuitsOnCancel()
    {
        var handler = new CancelableAsyncEventHandler<CancelEventArgs>(InvokeMode.Parallel);
        var calls = new List<int>();
        handler += (args, ct) => { calls.Add(1); args.Cancel = true; return ValueTask.CompletedTask; };
        handler += (args, ct) => { calls.Add(2); return ValueTask.CompletedTask; };

        await handler.InvokeAsync(new CancelEventArgs(), InvokeMode.Sequential);

        Assert.Equal(new[] { 1 }, calls);
    }
}
