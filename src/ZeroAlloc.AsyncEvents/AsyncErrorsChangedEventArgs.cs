namespace ZeroAlloc.AsyncEvents;

public sealed class AsyncErrorsChangedEventArgs
{
    public string? PropertyName { get; }
    public AsyncErrorsChangedEventArgs(string? propertyName) => PropertyName = propertyName;
}
