using System.Collections;
using System.Collections.Specialized;

namespace ZeroAlloc.AsyncEvents;

public sealed class AsyncCollectionChangedEventArgs
{
    public NotifyCollectionChangedAction Action { get; }
    public IList? NewItems { get; }
    public IList? OldItems { get; }
    public int NewStartingIndex { get; }
    public int OldStartingIndex { get; }

    public AsyncCollectionChangedEventArgs(
        NotifyCollectionChangedAction action,
        IList? newItems,
        IList? oldItems,
        int newStartingIndex,
        int oldStartingIndex)
    {
        Action = action;
        NewItems = newItems;
        OldItems = oldItems;
        NewStartingIndex = newStartingIndex;
        OldStartingIndex = oldStartingIndex;
    }
}
