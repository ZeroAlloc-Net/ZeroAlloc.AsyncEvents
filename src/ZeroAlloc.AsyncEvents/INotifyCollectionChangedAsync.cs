namespace ZeroAlloc.AsyncEvents;

public interface INotifyCollectionChangedAsync
{
#pragma warning disable MA0046 // async event delegates intentionally return ValueTask
    event AsyncEvent<AsyncCollectionChangedEventArgs> CollectionChangedAsync;
#pragma warning restore MA0046
}
