using System;
using System.Threading;
using System.Threading.Tasks;
using ZeroAlloc.AsyncEvents.AotSmoke;

// Exercise the generator-emitted event accessors wrapping AsyncEventHandler<T>
// backing fields under PublishAot=true. Both Sequential (OrderPlaced) and
// Parallel (ItemShipped) InvokeModes are covered.

var svc = new OrderService();

var sequentialCount = 0;
var parallelCount = 0;
string? lastOrder = null;

svc.OrderPlaced += (id, ct) =>
{
    Interlocked.Increment(ref sequentialCount);
    lastOrder = id;
    return ValueTask.CompletedTask;
};

svc.ItemShipped += (id, ct) =>
{
    Interlocked.Increment(ref parallelCount);
    return ValueTask.CompletedTask;
};

await svc.PlaceOrderAsync("ord-42", CancellationToken.None).ConfigureAwait(false);
if (sequentialCount != 1)
    return Fail($"Sequential event expected 1 invocation, got {sequentialCount}");
if (!string.Equals(lastOrder, "ord-42", StringComparison.Ordinal))
    return Fail($"Sequential event payload expected 'ord-42', got '{lastOrder}'");

await svc.ShipItemAsync(7, CancellationToken.None).ConfigureAwait(false);
if (parallelCount != 1)
    return Fail($"Parallel event expected 1 invocation, got {parallelCount}");

Console.WriteLine("AOT smoke: PASS");
return 0;

static int Fail(string message)
{
    Console.Error.WriteLine($"AOT smoke: FAIL — {message}");
    return 1;
}
