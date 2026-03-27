namespace ZeroAlloc.AsyncEvents;

public readonly struct SourcedAsyncEventArgs : IAsyncEventArgs
{
    public required object Source { get; init; }
}
