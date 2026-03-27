namespace ZeroAlloc.AsyncEvents;

public interface INotifyPropertyChangingAsync
{
#pragma warning disable MA0046 // async event delegates intentionally return ValueTask
    event AsyncEvent<AsyncPropertyChangingEventArgs> PropertyChangingAsync;
#pragma warning restore MA0046
}
