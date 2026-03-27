# Cancelable Events

Use `CancelableAsyncEventHandler<TArgs>` when a sequential pipeline should stop early if a handler signals cancellation — for example, a validation chain where the first failure aborts the rest.

## Declare and invoke

```csharp
private CancelableAsyncEventHandler<CancelEventArgs> _validating = new();

// Invoke — handlers run in registration order; stops at the first Cancel = true
var args = new CancelEventArgs();
await _validating.InvokeAsync(args, cancellationToken);

if (args.Cancel)
    return; // pipeline aborted
```

## Register handlers

```csharp
_validating.Register(CheckInventoryAsync);
_validating.Register(CheckCreditLimitAsync);

private async ValueTask CheckInventoryAsync(CancelEventArgs args, CancellationToken ct)
{
    if (!await inventory.IsAvailableAsync(ct))
        args.Cancel = true; // subsequent handlers are skipped
}
```

Once a handler sets `args.Cancel = true`, `CancelableAsyncEventHandler` skips all remaining handlers in the sequence.

## Why `CancelEventArgs` must be a class

`CancelEventArgs` is a `sealed class`. If it were a struct, each handler would receive a copy and mutations would not be visible to the invoker or to later handlers. Passing a class instance shares a single reference across the call chain.

## Parallel mode ignores `Cancel`

`CancelableAsyncEventHandler` always runs sequentially. If you call `InvokeAsync` with `InvokeMode.Parallel` (via the mode-override overload), all handlers are dispatched concurrently and the `Cancel` flag is not checked between them.

## `SourcedAsyncEventArgs`

When handlers need to know which object raised the event, use `SourcedAsyncEventArgs`:

```csharp
var args = new SourcedAsyncEventArgs { Source = this };
await _changed.InvokeAsync(args, ct);
```

`SourcedAsyncEventArgs` is a `readonly struct` implementing `IAsyncEventArgs`. It does not implement `ICancelable`, so it cannot be used with `CancelableAsyncEventHandler`.

## Custom args types

| Interface | Purpose |
|---|---|
| `IAsyncEventArgs` | Marker interface. Constrain handler fields to event-args types. |
| `ICancelable` | Contract: `bool Cancel { get; set; }`. Required by `CancelableAsyncEventHandler<TArgs>`. |

Implement both to create a custom cancelable args type:

```csharp
public class OrderCancelEventArgs : IAsyncEventArgs, ICancelable
{
    public required int OrderId { get; init; }
    public bool Cancel { get; set; }
}
```
