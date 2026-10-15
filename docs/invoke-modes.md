# Invoke Modes

`InvokeMode` controls how handlers are called when `InvokeAsync` is invoked.

## Parallel (default)

```csharp
var handler = new AsyncEventHandler<string>(InvokeMode.Parallel);
```

All handlers are started concurrently. A handler whose `ValueTask` has already completed successfully is consumed on the spot. Only the handlers that faulted, were cancelled or are still running are collected into an array rented from `ArrayPool<Task>` and awaited with `Task.WhenAll`; the array is returned afterwards. When every handler completes synchronously and successfully, no array is rented, `Task.WhenAll` is not called, and the call allocates 0 bytes on every target framework.

If handlers fail, the invocation completes once every started handler has finished, and it throws the exception `Task.WhenAll` reports first: a handler that had already faulted when it returned comes ahead of one that faults later. Among handlers that fault later, the first is the one whose fault `Task.WhenAll` observes first. If a handler's task runs its continuations asynchronously, for example a `TaskCompletionSource` created with `RunContinuationsAsynchronously`, `Task.WhenAll` observes the fault on the thread pool, so two faults that happen close together can be reported in either order. A handler that throws before returning its `ValueTask` stops the fan-out, and later handlers are not started.

**Use when:** handlers are independent and order doesn't matter. Most event scenarios.

## Sequential

```csharp
var handler = new AsyncEventHandler<string>(InvokeMode.Sequential);
```

Handlers are awaited one-by-one in registration order. Each handler completes before the next begins. `CancellationToken` is checked between each invocation.

**Use when:** handlers must run in order, or earlier handlers affect later ones (e.g., validation pipelines).

## Per-call override

```csharp
// Field is Parallel, but this specific call is Sequential:
await _handler.InvokeAsync(args, InvokeMode.Sequential, ct);
```

Useful when the same handler is sometimes called in contexts requiring sequential semantics.
