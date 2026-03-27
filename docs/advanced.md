# Advanced

## Struct semantics

`AsyncEventHandler<TArgs>` is a `struct` wrapping an internal `State` reference class. This means:

- Copying the struct shares the same underlying handler list.
- Assigning a new `AsyncEventHandler<TArgs>` to a field replaces the field but doesn't affect existing copies.
- Use as a `private` field; expose registration via methods or operators.

```csharp
// Correct — field is mutated in-place via CAS
private AsyncEventHandler<string> _handler = new(InvokeMode.Parallel);

public void Subscribe(AsyncEvent<string> cb) => _handler.Register(cb);
public void Unsubscribe(AsyncEvent<string> cb) => _handler.Unregister(cb);
```

`CancelableAsyncEventHandler<TArgs> where TArgs : ICancelable` follows the same struct semantics. The `ICancelable` constraint ensures the handler can read and short-circuit on `args.Cancel`. Because mutations must propagate back to the caller, `TArgs` must be a reference type (e.g. `CancelEventArgs`) — see [Cancelable Events](cancel-events.md).

## Per-call mode override

The `InvokeAsync(args, InvokeMode, ct)` overload lets a field declared as `Parallel` be invoked sequentially at specific call sites:

```csharp
private AsyncEventHandler<string> _handler = new(InvokeMode.Parallel);

// Normally parallel:
await _handler.InvokeAsync(args, ct);

// This specific call is sequential:
await _handler.InvokeAsync(args, InvokeMode.Sequential, ct);
```

## Lock-free registration

`Register` and `Unregister` use a CAS (compare-and-swap) loop on an array reference — no `lock` statement. Safe to call from multiple threads simultaneously. Duplicate registration is a no-op (reference equality check).

## Zero allocations on empty handler list

If no handlers are registered, `InvokeAsync` returns `default(ValueTask)` immediately — no allocations, no state machine created.
