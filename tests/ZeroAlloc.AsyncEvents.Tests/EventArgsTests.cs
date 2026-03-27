namespace ZeroAlloc.AsyncEvents.Tests;

public class EventArgsTests
{
    [Fact]
    public void SourcedAsyncEventArgs_StoresSource()
    {
        var source = new object();
        var args = new SourcedAsyncEventArgs { Source = source };
        Assert.Same(source, args.Source);
    }

    [Fact]
    public void SourcedAsyncEventArgs_ImplementsIAsyncEventArgs()
    {
        var args = new SourcedAsyncEventArgs { Source = this };
        Assert.IsAssignableFrom<IAsyncEventArgs>(args);
    }

    [Fact]
    public void CancelEventArgs_DefaultCancelIsFalse()
    {
        var args = new CancelEventArgs();
        Assert.False(args.Cancel);
    }

    [Fact]
    public void CancelEventArgs_Constructor_SetsCancel()
    {
        var args = new CancelEventArgs(cancel: true);
        Assert.True(args.Cancel);
    }

    [Fact]
    public void CancelEventArgs_CanSetCancel()
    {
        var args = new CancelEventArgs();
        args.Cancel = true;
        Assert.True(args.Cancel);
    }

    [Fact]
    public void CancelEventArgs_ImplementsICancelable()
    {
        var args = new CancelEventArgs();
        Assert.IsAssignableFrom<ICancelable>(args);
    }

    [Fact]
    public void CancelEventArgs_ImplementsIAsyncEventArgs()
    {
        var args = new CancelEventArgs();
        Assert.IsAssignableFrom<IAsyncEventArgs>(args);
    }

    [Fact]
    public void AsyncEventAttribute_DefaultModeIsParallel()
    {
        var attr = new AsyncEventAttribute();
        Assert.Equal(InvokeMode.Parallel, attr.Mode);
    }

    [Fact]
    public void AsyncEventAttribute_CanSetSequential()
    {
        var attr = new AsyncEventAttribute(InvokeMode.Sequential);
        Assert.Equal(InvokeMode.Sequential, attr.Mode);
    }
}
