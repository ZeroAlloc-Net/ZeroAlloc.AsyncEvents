using ZeroAlloc.AsyncEvents;

// Uses the event the bundled generator emits. If the compiler skips the generator,
// OrderPlaced does not exist and this does not build.
var service = new OrderService();
var received = 0;
service.OrderPlaced += (id, _) =>
{
    received += id;
    return ValueTask.CompletedTask;
};

await service.PlaceAsync(42, CancellationToken.None);

if (received != 42)
{
    Console.Error.WriteLine("SDK 10 consumer: the generated event did not reach the handler.");
    return 1;
}

Console.WriteLine("SDK 10 consumer: generator loaded and the generated event works.");
return 0;

public partial class OrderService
{
    [AsyncEvent(InvokeMode.Sequential)]
    private AsyncEventHandler<int> _orderPlaced = new(InvokeMode.Sequential);

    public ValueTask PlaceAsync(int id, CancellationToken ct) => _orderPlaced.InvokeAsync(id, ct);
}
