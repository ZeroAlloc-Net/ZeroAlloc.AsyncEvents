namespace ZeroAlloc.AsyncEvents;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Field, AllowMultiple = false)]
public sealed class AsyncEventAttribute : Attribute
{
    public InvokeMode Mode { get; }
    public AsyncEventAttribute(InvokeMode mode = InvokeMode.Parallel) => Mode = mode;
}
