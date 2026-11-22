---
id: getting-started
title: Getting Started
slug: /
sidebar_position: 1
---

# Getting Started

## Installation

```
dotnet add package ZeroAlloc.AsyncEvents
```

The generator needs the .NET 10 SDK 10.0.100 or later, or Visual Studio 2026 or later. It is built against Roslyn 5.0, and an older compiler skips it with warning CS9057, after which the generated code is missing. Version 2.0.0 was built against Roslyn 5.12 and loads only in the .NET 11 SDK; on .NET 10, use 2.0.1 or later.

## Declare a handler

`AsyncEventHandler<TArgs>` is a struct. Declare it as a private field:

```csharp
private AsyncEventHandler<OrderPlacedArgs> _orderPlaced = new(InvokeMode.Parallel);
```

## Register a handler

```csharp
_orderPlaced.Register(OnOrderPlacedAsync);

private async ValueTask OnOrderPlacedAsync(OrderPlacedArgs args, CancellationToken ct)
{
    await SendEmailAsync(args.OrderId, ct);
}
```

Or with a lambda:

```csharp
_orderPlaced.Register(async (args, ct) => await SendEmailAsync(args.OrderId, ct));
```

## Invoke

```csharp
await _orderPlaced.InvokeAsync(new OrderPlacedArgs(orderId), cancellationToken);
```

Returns `ValueTask` — await it or fire-and-forget with `_ =`.

## Unregister

```csharp
_orderPlaced.Unregister(OnOrderPlacedAsync);
```

Use `+=` / `-=` operators as shorthand:

```csharp
_orderPlaced += OnOrderPlacedAsync;
_orderPlaced -= OnOrderPlacedAsync;
```

## Expose as a C# event

Use explicit `add`/`remove` accessors to expose the handler as a standard C# `event`:

```csharp
private AsyncEventHandler<OrderPlacedArgs> _orderPlaced = new(InvokeMode.Parallel);

public event AsyncEvent<OrderPlacedArgs> OrderPlaced
{
    add    => _orderPlaced.Register(value);
    remove => _orderPlaced.Unregister(value);
}
```

Callers subscribe and unsubscribe with `+=` / `-=` as usual; internally it routes to `Register`/`Unregister`.

## Use the source generator

Instead of writing the `add`/`remove` accessors manually, annotate the field with `[AsyncEvent]` on a `partial` class:

```csharp
public partial class OrderService
{
    [AsyncEvent(InvokeMode.Parallel)]
    private AsyncEventHandler<OrderPlacedArgs> _orderPlaced = new(InvokeMode.Parallel);

    // Generated automatically — no need to write this:
    // public event AsyncEvent<OrderPlacedArgs> OrderPlaced { ... }
}
```

Apply `[AsyncEvent]` on the class to cover all `AsyncEventHandler<TArgs>` fields at once:

```csharp
[AsyncEvent(InvokeMode.Parallel)]
public partial class OrderService
{
    private AsyncEventHandler<OrderPlacedArgs> _orderPlaced = new(InvokeMode.Parallel);
    private AsyncEventHandler<ItemShippedArgs> _itemShipped = new(InvokeMode.Parallel);
}
```

See [Source Generator](source-generator.md) for full details including field-overrides class mode.

## Cancelable events

For sequential pipelines where an early handler should abort the rest, use `CancelableAsyncEventHandler<TArgs>` with `CancelEventArgs`. See [Cancelable Events](cancel-events.md) for details.
