using System.Collections.Specialized;

namespace ZeroAlloc.AsyncEvents.Tests;

public class EventArgsTests
{
    [Fact]
    public void AsyncPropertyChangedEventArgs_StoresPropertyName()
    {
        var args = new AsyncPropertyChangedEventArgs("Name");
        Assert.Equal("Name", args.PropertyName);
    }

    [Fact]
    public void AsyncPropertyChangingEventArgs_StoresPropertyName()
    {
        var args = new AsyncPropertyChangingEventArgs("Name");
        Assert.Equal("Name", args.PropertyName);
    }

    [Fact]
    public void AsyncErrorsChangedEventArgs_StoresNullablePropertyName()
    {
        var args = new AsyncErrorsChangedEventArgs(null);
        Assert.Null(args.PropertyName);
    }

    [Fact]
    public void AsyncCollectionChangedEventArgs_StoresAction()
    {
        var args = new AsyncCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Reset,
            null, null, -1, -1);
        Assert.Equal(NotifyCollectionChangedAction.Reset, args.Action);
    }
}
