using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroAlloc.AsyncEvents;

/// <summary>
/// The <see cref="InvokeMode.Parallel"/> fan-out shared by <see cref="AsyncEventHandler{TArgs}"/>
/// and <see cref="CancelableAsyncEventHandler{TArgs}"/>.
/// </summary>
internal static class ParallelDispatch
{
    /// <summary>
    /// Starts every callback, then waits for the ones that did not already succeed.
    /// </summary>
    /// <remarks>
    /// A callback whose <see cref="ValueTask"/> has already completed successfully is consumed on
    /// the spot and never becomes a <see cref="Task"/>. When every callback completes that way, no
    /// pool array is rented and <see cref="Task.WhenAll(Task[])"/> is never called, so the call
    /// allocates nothing on any target. Below net10.0 the only <c>WhenAll</c> overload that takes a
    /// slice of an array is the <c>IEnumerable&lt;Task&gt;</c> one, which boxes the segment, copies
    /// it and allocates a promise even when every task has finished.
    /// <para>
    /// Every other callback, faulted, cancelled or still pending, goes to <c>WhenAll</c> in
    /// registration order exactly as before. A successful task contributes no exception and no
    /// cancellation to <c>WhenAll</c>, so leaving those out does not change which exception the
    /// caller observes, whether the result is cancelled, or when it completes.
    /// </para>
    /// </remarks>
    internal static async ValueTask InvokeAsync<TArgs>(AsyncEvent<TArgs>[] callbacks, TArgs args, CancellationToken ct)
    {
        var count = callbacks.Length;
        Task[]? tasks = null;
        var pending = 0;
        try
        {
            for (var i = 0; i < count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var result = callbacks[i](args, ct);
                if (result.IsCompletedSuccessfully)
                {
                    // Read the result, as AsTask would have, so a pooled IValueTaskSource behind the
                    // ValueTask is released.
                    result.GetAwaiter().GetResult();
                    continue;
                }

                tasks ??= ArrayPool<Task>.Shared.Rent(count - i);
                tasks[pending++] = result.AsTask();
            }

            if (tasks is null) return;

#if NET10_0_OR_GREATER
            await Task.WhenAll(new ReadOnlySpan<Task>(tasks, 0, pending)).ConfigureAwait(false);
#else
            await Task.WhenAll(new ArraySegment<Task>(tasks, 0, pending)).ConfigureAwait(false);
#endif
        }
        finally
        {
            if (tasks is not null)
                ArrayPool<Task>.Shared.Return(tasks, clearArray: true);
        }
    }
}
