namespace ZeroAlloc.AsyncEvents.Tests;

public class AsyncEventHandlerRegistrationTests
{
    [Fact]
    public void Register_AddsCallback()
    {
        var handler = new AsyncEventHandler<string>();
        AsyncEvent<string> cb = (s, ct) => ValueTask.CompletedTask;
        handler.Register(cb);
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public void Unregister_RemovesCallback()
    {
        var handler = new AsyncEventHandler<string>();
        AsyncEvent<string> cb = (s, ct) => ValueTask.CompletedTask;
        handler.Register(cb);
        handler.Unregister(cb);
        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public void PlusEquals_RegistersCallback()
    {
        var handler = new AsyncEventHandler<string>();
        AsyncEvent<string> cb = (s, ct) => ValueTask.CompletedTask;
        handler += cb;
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public void MinusEquals_UnregistersCallback()
    {
        var handler = new AsyncEventHandler<string>();
        AsyncEvent<string> cb = (s, ct) => ValueTask.CompletedTask;
        handler += cb;
        handler -= cb;
        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public void Register_SameCallback_AddedOnce()
    {
        var handler = new AsyncEventHandler<string>();
        AsyncEvent<string> cb = (s, ct) => ValueTask.CompletedTask;
        handler.Register(cb);
        handler.Register(cb);
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public void DefaultHandler_HasZeroCount()
    {
        var handler = default(AsyncEventHandler<string>);
        Assert.Equal(0, handler.Count);
    }
}
