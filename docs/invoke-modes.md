# Invoke Modes

`InvokeMode` controls how handlers are called when `InvokeAsync` is invoked.

## Parallel (default)

```csharp
var handler = new AsyncEventHandler<string>(InvokeMode.Parallel);
```

All handlers are started concurrently. An `ArrayPool<Task>` is rented for the fan-out, then returned after `Task.WhenAll`. Allocates 0 bytes on the heap when all handlers return synchronously.

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
