using System.Threading;

namespace ZeroAlloc.AsyncEvents;

public struct AsyncEventHandler<TArgs>
{
    private sealed class State
    {
        internal AsyncEvent<TArgs>[] Callbacks = Array.Empty<AsyncEvent<TArgs>>();
    }

    private State? _state;
    private readonly InvokeMode _mode;

    public AsyncEventHandler(InvokeMode mode = InvokeMode.Parallel)
    {
        _mode = mode;
        _state = new State();
    }

    private State GetOrCreateState()
    {
        if (_state is not null) return _state;
        var s = new State();
        return Interlocked.CompareExchange(ref _state, s, null) ?? s;
    }

    public int Count => _state?.Callbacks.Length ?? 0;

    public void Register(AsyncEvent<TArgs> callback)
    {
        var state = GetOrCreateState();
        AsyncEvent<TArgs>[] current, updated;
        do
        {
            current = state.Callbacks;
            foreach (var cb in current)
                if (ReferenceEquals(cb, callback)) return;
            updated = new AsyncEvent<TArgs>[current.Length + 1];
            Array.Copy(current, updated, current.Length);
            updated[current.Length] = callback;
        }
        while (Interlocked.CompareExchange(ref state.Callbacks, updated, current) != current);
    }

    public void Unregister(AsyncEvent<TArgs> callback)
    {
        var state = _state;
        if (state is null) return;
        AsyncEvent<TArgs>[] current, updated;
        do
        {
            current = state.Callbacks;
            int idx = Array.IndexOf(current, callback);
            if (idx < 0) return;
            updated = new AsyncEvent<TArgs>[current.Length - 1];
            Array.Copy(current, 0, updated, 0, idx);
            Array.Copy(current, idx + 1, updated, idx, current.Length - idx - 1);
        }
        while (Interlocked.CompareExchange(ref state.Callbacks, updated, current) != current);
    }

    public static AsyncEventHandler<TArgs> operator +(AsyncEventHandler<TArgs> handler, AsyncEvent<TArgs> callback)
    {
        handler.Register(callback);
        return handler;
    }

    public static AsyncEventHandler<TArgs> operator -(AsyncEventHandler<TArgs> handler, AsyncEvent<TArgs> callback)
    {
        handler.Unregister(callback);
        return handler;
    }
}
