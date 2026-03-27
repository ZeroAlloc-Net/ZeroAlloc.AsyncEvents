# Cancel & Sourced Args Design

**Date:** 2026-03-27
**Status:** Approved

## Overview

Add `IAsyncEventArgs`, `ICancelable`, `SourcedAsyncEventArgs`, `CancelEventArgs`, and `CancelableAsyncEventHandler<TArgs>` to ZeroAlloc.AsyncEvents.

---

## Section 1 — New interfaces and types

```csharp
// Marker interface
public interface IAsyncEventArgs { }

// Cancelable contract
public interface ICancelable
{
    bool Cancel { get; set; }
}

// Struct: event with a source object
public struct SourcedAsyncEventArgs : IAsyncEventArgs
{
    public required object Source { get; init; }
}

// Class: cancelable event args (class so handler mutations propagate)
public sealed class CancelEventArgs : IAsyncEventArgs, ICancelable
{
    public bool Cancel { get; set; }
    public CancelEventArgs() { }
    public CancelEventArgs(bool cancel) => Cancel = cancel;
}
```

Notes:
- `[Serializable]` omitted — BinaryFormatter is obsolete/removed in .NET 9
- `required` (C# 11) on `SourcedAsyncEventArgs.Source` — compiler auto-polyfills `RequiredMemberAttribute` for older TFMs
- `CancelEventArgs` is a `sealed class` so handler mutations to `Cancel` propagate to the invoker

---

## Section 2 — CancelableAsyncEventHandler&lt;TArgs&gt;

A new struct mirroring `AsyncEventHandler<TArgs>`, constrained to `where TArgs : ICancelable`. Identical Register/Unregister/Count/operators. The only behavioral difference is the sequential path:

```csharp
public struct CancelableAsyncEventHandler<TArgs> where TArgs : ICancelable
{
    private static async ValueTask InvokeSequentialAsync(
        AsyncEvent<TArgs>[] callbacks, TArgs args, CancellationToken ct)
    {
        foreach (var cb in callbacks)
        {
            ct.ThrowIfCancellationRequested();
            if (args.Cancel) return;   // constrained call — zero boxing
            await cb(args, ct).ConfigureAwait(false);
        }
    }

    // InvokeParallelAsync — unchanged (all handlers run, Cancel ignored)
}
```

`args.Cancel` uses a constrained call — zero boxing for both class and struct `TArgs`. The Register/Unregister/CAS loop is copy-identical to `AsyncEventHandler<TArgs>`; no shared base is introduced to avoid overhead.

---

## Section 3 — File placement & tests

**New source files** (`src/ZeroAlloc.AsyncEvents/`):
- `IAsyncEventArgs.cs`
- `ICancelable.cs`
- `SourcedAsyncEventArgs.cs`
- `CancelEventArgs.cs`
- `CancelableAsyncEventHandler.cs`

**Tests** (`tests/ZeroAlloc.AsyncEvents.Tests/`):
- `CancelableEventHandlerTests.cs` — new: cancel short-circuits sequential, parallel ignores cancel, Register/Unregister, operators
- `EventArgsTests.cs` — extend existing: add `SourcedAsyncEventArgs` and `CancelEventArgs` cases

---

## Implementation Order

1. Add `IAsyncEventArgs.cs` and `ICancelable.cs`
2. Add `SourcedAsyncEventArgs.cs` and `CancelEventArgs.cs`
3. Add `CancelableAsyncEventHandler.cs`
4. Extend `EventArgsTests.cs` and add `CancelableEventHandlerTests.cs`
5. Verify all tests pass across all TFMs
6. Commit
