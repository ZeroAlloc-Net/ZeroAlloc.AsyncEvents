namespace ZeroAlloc.AsyncEvents.Tests;

public class AsyncEventHandlerInvokeTests
{
    [Fact]
    public async Task InvokeAsync_CallsAllRegisteredHandlers()
    {
        var handler = new AsyncEventHandler<string>();
        var called = new List<string>();
        handler += (s, ct) => { called.Add(s + "1"); return ValueTask.CompletedTask; };
        handler += (s, ct) => { called.Add(s + "2"); return ValueTask.CompletedTask; };

        await handler.InvokeAsync("x");

        Assert.Contains("x1", called);
        Assert.Contains("x2", called);
    }

    [Fact]
    public async Task InvokeAsync_EmptyHandler_DoesNotThrow()
    {
        var handler = new AsyncEventHandler<string>();
        await handler.InvokeAsync("x");
    }

    [Fact]
    public async Task InvokeAsync_Sequential_CallsInOrder()
    {
        var handler = new AsyncEventHandler<string>(InvokeMode.Sequential);
        var order = new List<int>();
        handler += async (s, ct) => { await Task.Delay(10, ct).ConfigureAwait(false); order.Add(1); };
        handler += (s, ct) => { order.Add(2); return ValueTask.CompletedTask; };

        await handler.InvokeAsync("x");

        Assert.Equal(new[] { 1, 2 }, order);
    }

    [Fact]
    public async Task InvokeAsync_CancellationToken_IsPassedToHandlers()
    {
        var handler = new AsyncEventHandler<string>();
        CancellationToken received = default;
        handler += (s, ct) => { received = ct; return ValueTask.CompletedTask; };

        using var cts = new CancellationTokenSource();
        await handler.InvokeAsync("x", cts.Token);

        Assert.Equal(cts.Token, received);
    }

    [Fact]
    public async Task InvokeAsync_DefaultStruct_DoesNotThrow()
    {
        var handler = default(AsyncEventHandler<string>);
        await handler.InvokeAsync("x");
    }
}
