# Testing

## Testing a class that raises async events

Use a simple handler capture:

```csharp
[Fact]
public async Task PlaceOrder_RaisesOrderPlacedAsync()
{
    var service = new OrderService();
    OrderPlacedArgs? captured = null;

    service.OrderPlacedAsync += (args, ct) =>
    {
        captured = args;
        return ValueTask.CompletedTask;
    };

    await service.PlaceOrderAsync("order-1");

    Assert.NotNull(captured);
    Assert.Equal("order-1", captured.OrderId);
}
```

## Testing invocation order (Sequential)

```csharp
[Fact]
public async Task Sequential_InvokesInRegistrationOrder()
{
    var handler = new AsyncEventHandler<string>(InvokeMode.Sequential);
    var order = new List<int>();

    handler.Register(async (_, ct) => { order.Add(1); await Task.Yield(); });
    handler.Register(async (_, ct) => { order.Add(2); await Task.Yield(); });
    handler.Register((_, ct) => { order.Add(3); return ValueTask.CompletedTask; });

    await handler.InvokeAsync("test");

    Assert.Equal(new[] { 1, 2, 3 }, order);
}
```

## Testing cancellation

```csharp
[Fact]
public async Task Sequential_ThrowsOnCancellation()
{
    var handler = new AsyncEventHandler<string>(InvokeMode.Sequential);
    handler.Register(async (_, ct) => await Task.Delay(1000, ct));

    var cts = new CancellationTokenSource();
    cts.Cancel();

    await Assert.ThrowsAsync<OperationCanceledException>(
        () => handler.InvokeAsync("test", cts.Token).AsTask());
}
```

## Testing register/unregister

```csharp
[Fact]
public async Task Unregister_RemovesHandler()
{
    var handler = new AsyncEventHandler<string>(InvokeMode.Parallel);
    var called = false;

    AsyncEvent<string> cb = (_, ct) => { called = true; return ValueTask.CompletedTask; };
    handler.Register(cb);
    handler.Unregister(cb);

    await handler.InvokeAsync("test");

    Assert.False(called);
}
```
