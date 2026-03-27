# Cancel & Sourced Args Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add `IAsyncEventArgs`, `ICancelable`, `SourcedAsyncEventArgs`, `CancelEventArgs`, and `CancelableAsyncEventHandler<TArgs>` to ZeroAlloc.AsyncEvents.

**Architecture:** Two new marker/contract interfaces, two new concrete args types, and a new handler struct constrained to `ICancelable` that short-circuits Sequential invocation via a zero-boxing constrained call on `args.Cancel`. `CancelEventArgs` is a sealed class so handler mutations to `Cancel` propagate back to the invoker.

**Tech Stack:** C# 12, .NET (netstandard2.0 / netstandard2.1 / net8.0 / net10.0), xUnit, `System.Buffers.ArrayPool`

---

### Task 1: Add IAsyncEventArgs, ICancelable, SourcedAsyncEventArgs, CancelEventArgs

**Files:**
- Create: `src/ZeroAlloc.AsyncEvents/IAsyncEventArgs.cs`
- Create: `src/ZeroAlloc.AsyncEvents/ICancelable.cs`
- Create: `src/ZeroAlloc.AsyncEvents/SourcedAsyncEventArgs.cs`
- Create: `src/ZeroAlloc.AsyncEvents/CancelEventArgs.cs`
- Create: `tests/ZeroAlloc.AsyncEvents.Tests/EventArgsTests.cs`

**Step 1: Write the failing tests**

Create `tests/ZeroAlloc.AsyncEvents.Tests/EventArgsTests.cs`:

```csharp
using System.Collections.Specialized;

namespace ZeroAlloc.AsyncEvents.Tests;

public class EventArgsTests
{
    [Fact]
    public void SourcedAsyncEventArgs_StoresSource()
    {
        var source = new object();
        var args = new SourcedAsyncEventArgs { Source = source };
        Assert.Same(source, args.Source);
    }

    [Fact]
    public void SourcedAsyncEventArgs_ImplementsIAsyncEventArgs()
    {
        var args = new SourcedAsyncEventArgs { Source = this };
        Assert.IsAssignableFrom<IAsyncEventArgs>(args);
    }

    [Fact]
    public void CancelEventArgs_DefaultCancelIsFalse()
    {
        var args = new CancelEventArgs();
        Assert.False(args.Cancel);
    }

    [Fact]
    public void CancelEventArgs_Constructor_SetsCancel()
    {
        var args = new CancelEventArgs(cancel: true);
        Assert.True(args.Cancel);
    }

    [Fact]
    public void CancelEventArgs_CanSetCancel()
    {
        var args = new CancelEventArgs();
        args.Cancel = true;
        Assert.True(args.Cancel);
    }

    [Fact]
    public void CancelEventArgs_ImplementsICancelable()
    {
        var args = new CancelEventArgs();
        Assert.IsAssignableFrom<ICancelable>(args);
    }

    [Fact]
    public void CancelEventArgs_ImplementsIAsyncEventArgs()
    {
        var args = new CancelEventArgs();
        Assert.IsAssignableFrom<IAsyncEventArgs>(args);
    }
}
```

**Step 2: Run to verify it fails**

```bash
cd c:/Projects/Prive/ZeroAlloc.AsyncEvents
dotnet test tests/ZeroAlloc.AsyncEvents.Tests/ZeroAlloc.AsyncEvents.Tests.csproj
```

Expected: compile error — `IAsyncEventArgs`, `ICancelable`, `SourcedAsyncEventArgs`, `CancelEventArgs` not found.

**Step 3: Create `src/ZeroAlloc.AsyncEvents/IAsyncEventArgs.cs`**

```csharp
namespace ZeroAlloc.AsyncEvents;

public interface IAsyncEventArgs { }
```

**Step 4: Create `src/ZeroAlloc.AsyncEvents/ICancelable.cs`**

```csharp
namespace ZeroAlloc.AsyncEvents;

public interface ICancelable
{
    bool Cancel { get; set; }
}
```

**Step 5: Create `src/ZeroAlloc.AsyncEvents/SourcedAsyncEventArgs.cs`**

```csharp
namespace ZeroAlloc.AsyncEvents;

public struct SourcedAsyncEventArgs : IAsyncEventArgs
{
    public required object Source { get; init; }
}
```

**Step 6: Create `src/ZeroAlloc.AsyncEvents/CancelEventArgs.cs`**

```csharp
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
```

**Step 7: Run tests to verify they pass**

```bash
dotnet test tests/ZeroAlloc.AsyncEvents.Tests/ZeroAlloc.AsyncEvents.Tests.csproj
```

Expected: all tests pass (14 existing + 7 new = 21).

**Step 8: Commit**

```bash
git -C c:/Projects/Prive/ZeroAlloc.AsyncEvents add src/ZeroAlloc.AsyncEvents/IAsyncEventArgs.cs src/ZeroAlloc.AsyncEvents/ICancelable.cs src/ZeroAlloc.AsyncEvents/SourcedAsyncEventArgs.cs src/ZeroAlloc.AsyncEvents/CancelEventArgs.cs tests/ZeroAlloc.AsyncEvents.Tests/EventArgsTests.cs
git -C c:/Projects/Prive/ZeroAlloc.AsyncEvents commit -m "feat: add IAsyncEventArgs, ICancelable, SourcedAsyncEventArgs, CancelEventArgs"
```

---

### Task 2: Add CancelableAsyncEventHandler&lt;TArgs&gt;

**Files:**
- Create: `src/ZeroAlloc.AsyncEvents/CancelableAsyncEventHandler.cs`
- Create: `tests/ZeroAlloc.AsyncEvents.Tests/CancelableEventHandlerTests.cs`

**Step 1: Write the failing tests**

Create `tests/ZeroAlloc.AsyncEvents.Tests/CancelableEventHandlerTests.cs`:

```csharp
namespace ZeroAlloc.AsyncEvents.Tests;

public class CancelableEventHandlerTests
{
    [Fact]
    public async Task Sequential_StopsAfterHandlerSetsCancelTrue()
    {
        var handler = new CancelableAsyncEventHandler<CancelEventArgs>(InvokeMode.Sequential);
        var calls = new List<int>();
        handler += (args, ct) => { calls.Add(1); args.Cancel = true; return ValueTask.CompletedTask; };
        handler += (args, ct) => { calls.Add(2); return ValueTask.CompletedTask; };

        await handler.InvokeAsync(new CancelEventArgs());

        Assert.Equal(new[] { 1 }, calls);
    }

    [Fact]
    public async Task Sequential_AlreadyCancelled_SkipsAllHandlers()
    {
        var handler = new CancelableAsyncEventHandler<CancelEventArgs>(InvokeMode.Sequential);
        var called = false;
        handler += (args, ct) => { called = true; return ValueTask.CompletedTask; };

        await handler.InvokeAsync(new CancelEventArgs(cancel: true));

        Assert.False(called);
    }

    [Fact]
    public async Task Parallel_RunsAllHandlersDespiteCancel()
    {
        var handler = new CancelableAsyncEventHandler<CancelEventArgs>(InvokeMode.Parallel);
        var calls = new List<int>();
        handler += (args, ct) => { calls.Add(1); args.Cancel = true; return ValueTask.CompletedTask; };
        handler += (args, ct) => { calls.Add(2); return ValueTask.CompletedTask; };

        await handler.InvokeAsync(new CancelEventArgs());

        Assert.Equal(2, calls.Count);
    }

    [Fact]
    public void Register_AddsCallback()
    {
        var handler = new CancelableAsyncEventHandler<CancelEventArgs>();
        AsyncEvent<CancelEventArgs> cb = (args, ct) => ValueTask.CompletedTask;
        handler.Register(cb);
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public void Unregister_RemovesCallback()
    {
        var handler = new CancelableAsyncEventHandler<CancelEventArgs>();
        AsyncEvent<CancelEventArgs> cb = (args, ct) => ValueTask.CompletedTask;
        handler.Register(cb);
        handler.Unregister(cb);
        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public void PlusOperator_RegistersCallback()
    {
        var handler = new CancelableAsyncEventHandler<CancelEventArgs>();
        AsyncEvent<CancelEventArgs> cb = (args, ct) => ValueTask.CompletedTask;
        handler += cb;
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public void MinusOperator_UnregistersCallback()
    {
        var handler = new CancelableAsyncEventHandler<CancelEventArgs>();
        AsyncEvent<CancelEventArgs> cb = (args, ct) => ValueTask.CompletedTask;
        handler += cb;
        handler -= cb;
        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public async Task InvokeAsync_EmptyHandler_DoesNotThrow()
    {
        var handler = new CancelableAsyncEventHandler<CancelEventArgs>();
        await handler.InvokeAsync(new CancelEventArgs());
    }

    [Fact]
    public async Task InvokeAsync_DefaultStruct_DoesNotThrow()
    {
        var handler = default(CancelableAsyncEventHandler<CancelEventArgs>);
        await handler.InvokeAsync(new CancelEventArgs());
    }

    [Fact]
    public async Task InvokeAsync_CancellationToken_IsPassedToHandlers()
    {
        var handler = new CancelableAsyncEventHandler<CancelEventArgs>();
        CancellationToken received = default;
        handler += (args, ct) => { received = ct; return ValueTask.CompletedTask; };

        using var cts = new CancellationTokenSource();
        await handler.InvokeAsync(new CancelEventArgs(), cts.Token);

        Assert.Equal(cts.Token, received);
    }
}
```

**Step 2: Run to verify it fails**

```bash
dotnet test tests/ZeroAlloc.AsyncEvents.Tests/ZeroAlloc.AsyncEvents.Tests.csproj
```

Expected: compile error — `CancelableAsyncEventHandler` not found.

**Step 3: Create `src/ZeroAlloc.AsyncEvents/CancelableAsyncEventHandler.cs`**

```csharp
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
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/ZeroAlloc.AsyncEvents.Tests/ZeroAlloc.AsyncEvents.Tests.csproj
```

Expected: all tests pass (21 existing + 11 new = 32).

**Step 5: Commit**

```bash
git -C c:/Projects/Prive/ZeroAlloc.AsyncEvents add src/ZeroAlloc.AsyncEvents/CancelableAsyncEventHandler.cs tests/ZeroAlloc.AsyncEvents.Tests/CancelableEventHandlerTests.cs
git -C c:/Projects/Prive/ZeroAlloc.AsyncEvents commit -m "feat: add CancelableAsyncEventHandler with sequential cancel short-circuit"
```
