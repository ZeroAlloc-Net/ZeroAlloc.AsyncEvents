namespace ZeroAlloc.AsyncEvents.Tests;

public class AsyncEventDelegateTests
{
    [Fact]
    public async Task AsyncEvent_CanBeAssignedLambda()
    {
        AsyncEvent e = ct => ValueTask.CompletedTask;
        await e(CancellationToken.None);
    }

    [Fact]
    public async Task AsyncEventOfT_CanBeAssignedLambda()
    {
        AsyncEvent<string> e = (s, ct) => ValueTask.CompletedTask;
        await e("hello", CancellationToken.None);
    }

    [Fact]
    public void InvokeMode_HasParallelAndSequential()
    {
        Assert.Equal(0, (int)InvokeMode.Parallel);
        Assert.Equal(1, (int)InvokeMode.Sequential);
    }
}
