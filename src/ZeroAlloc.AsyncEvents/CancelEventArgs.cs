namespace ZeroAlloc.AsyncEvents;

public sealed class CancelEventArgs : IAsyncEventArgs, ICancelable
{
    public bool Cancel { get; set; }

    public CancelEventArgs() { }

    public CancelEventArgs(bool cancel)
    {
        Cancel = cancel;
    }
}
