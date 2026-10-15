using ZeroAlloc.TestHelpers;

namespace ZeroAlloc.AsyncEvents.Tests;

/// <summary>
/// Backs the README and docs/performance.md claim that <see cref="InvokeMode.Sequential"/> allocates
/// nothing when every handler completes synchronously. Each test measures the whole public
/// <c>InvokeAsync</c> call, for both handler structs and both overloads.
/// </summary>
public class SequentialInvokeAllocationTests
{
    public static TheoryData<string, bool> Cases => new()
    {
        { "plain", false },
        { "plain", true },
        { "cancelable", false },
        { "cancelable", true },
    };

    private static Func<ValueTask> Build(string kind, bool modeOverride)
    {
        var args = new CancelEventArgs();
        AsyncEvent<CancelEventArgs>[] callbacks =
        [
            static (_, _) => default,
            static (_, _) => default,
            static (_, _) => default,
            static (_, _) => default,
        ];

        // With modeOverride the field is Parallel and the call asks for Sequential.
        var fieldMode = modeOverride ? InvokeMode.Parallel : InvokeMode.Sequential;

        if (string.Equals(kind, "plain", StringComparison.Ordinal))
        {
            var handler = new AsyncEventHandler<CancelEventArgs>(fieldMode);
            foreach (var cb in callbacks) handler.Register(cb);
            return modeOverride
                ? () => handler.InvokeAsync(args, InvokeMode.Sequential, CancellationToken.None)
                : () => handler.InvokeAsync(args, CancellationToken.None);
        }

        var cancelable = new CancelableAsyncEventHandler<CancelEventArgs>(fieldMode);
        foreach (var cb in callbacks) cancelable.Register(cb);
        return modeOverride
            ? () => cancelable.InvokeAsync(args, InvokeMode.Sequential, CancellationToken.None)
            : () => cancelable.InvokeAsync(args, CancellationToken.None);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void AllSucceedSynchronously_AllocatesNothing(string kind, bool modeOverride)
    {
        var invoke = Build(kind, modeOverride);

        AllocationGate.AssertBudget(
            0,
            1_000,
            () => invoke().GetAwaiter().GetResult(),
            $"Sequential InvokeAsync, {kind}, modeOverride={modeOverride}");
    }
}
