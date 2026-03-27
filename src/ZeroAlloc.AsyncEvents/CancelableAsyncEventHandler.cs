using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroAlloc.AsyncEvents;

public struct CancelableAsyncEventHandler<TArgs> where TArgs : ICancelable
{
    private sealed class State
    {
        internal AsyncEvent<TArgs>[] Callbacks = Array.Empty<AsyncEvent<TArgs>>();
    }

    private State? _state;
    private readonly InvokeMode _mode;

    public CancelableAsyncEventHandler(InvokeMode mode = InvokeMode.Parallel)
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

    public ValueTask InvokeAsync(TArgs args, CancellationToken ct = default)
    {
        var callbacks = _state?.Callbacks;
        if (callbacks is null || callbacks.Length == 0) return default;

        if (_mode == InvokeMode.Sequential)
            return InvokeSequentialAsync(callbacks, args, ct);

        return InvokeParallelAsync(callbacks, args, ct);
    }

    public ValueTask InvokeAsync(TArgs args, InvokeMode modeOverride, CancellationToken ct = default)
    {
        var callbacks = _state?.Callbacks;
        if (callbacks is null || callbacks.Length == 0) return default;

        if (modeOverride == InvokeMode.Sequential)
            return InvokeSequentialAsync(callbacks, args, ct);

        return InvokeParallelAsync(callbacks, args, ct);
    }

    private static async ValueTask InvokeSequentialAsync(AsyncEvent<TArgs>[] callbacks, TArgs args, CancellationToken ct)
    {
        foreach (var cb in callbacks)
        {
            ct.ThrowIfCancellationRequested();
            if (args.Cancel) return;
            await cb(args, ct).ConfigureAwait(false);
        }
    }

    private static async ValueTask InvokeParallelAsync(AsyncEvent<TArgs>[] callbacks, TArgs args, CancellationToken ct)
    {
        var count = callbacks.Length;
        var tasks = ArrayPool<Task>.Shared.Rent(count);
        try
        {
            for (var i = 0; i < count; i++)
            {
                ct.ThrowIfCancellationRequested();
                tasks[i] = callbacks[i](args, ct).AsTask();
            }
#if NET10_0_OR_GREATER
            await Task.WhenAll(new ReadOnlySpan<Task>(tasks, 0, count)).ConfigureAwait(false);
#else
            await Task.WhenAll(new ArraySegment<Task>(tasks, 0, count)).ConfigureAwait(false);
#endif
        }
        finally
        {
            ArrayPool<Task>.Shared.Return(tasks, clearArray: true);
        }
    }

    public static CancelableAsyncEventHandler<TArgs> operator +(CancelableAsyncEventHandler<TArgs> handler, AsyncEvent<TArgs> callback)
    {
        handler.Register(callback);
        return handler;
    }

    public static CancelableAsyncEventHandler<TArgs> operator -(CancelableAsyncEventHandler<TArgs> handler, AsyncEvent<TArgs> callback)
    {
        handler.Unregister(callback);
        return handler;
    }
}
