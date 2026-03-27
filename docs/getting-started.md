# Getting Started

## Installation

```
dotnet add package ZeroAlloc.AsyncEvents
```

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
