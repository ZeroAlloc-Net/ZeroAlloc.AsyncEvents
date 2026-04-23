using System.Threading;
using System.Threading.Tasks;
using ZeroAlloc.AsyncEvents;

namespace ZeroAlloc.AsyncEvents.AotSmoke;

public partial class OrderService
{
    [AsyncEvent(InvokeMode.Sequential)]
    private AsyncEventHandler<string> _orderPlaced;

    [AsyncEvent(InvokeMode.Parallel)]
    private AsyncEventHandler<int> _itemShipped;

    public ValueTask PlaceOrderAsync(string orderId, CancellationToken ct)
        => _orderPlaced.InvokeAsync(orderId, ct);

    public ValueTask ShipItemAsync(int itemId, CancellationToken ct)
        => _itemShipped.InvokeAsync(itemId, ct);
}
