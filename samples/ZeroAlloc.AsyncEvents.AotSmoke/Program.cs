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

// Sequential exception propagation: handler1 throws → InvokeAsync rethrows
// → handler2 never runs. Validates the foreach loop's "first-throw-bails"
// behavior under PublishAot.
var seqSvc = new OrderService();
var seqHandler2Count = 0;
seqSvc.OrderPlaced += (id, ct) => throw new InvalidOperationException("handler1 fault");
seqSvc.OrderPlaced += (id, ct) =>
{
    Interlocked.Increment(ref seqHandler2Count);
    return ValueTask.CompletedTask;
};

Exception? seqCaught = null;
try
{
    await seqSvc.PlaceOrderAsync("seq-1", CancellationToken.None).ConfigureAwait(false);
}
catch (Exception ex)
{
    seqCaught = ex;
}

if (seqCaught is not InvalidOperationException)
    return Fail($"Sequential exception: expected InvalidOperationException, got {seqCaught?.GetType().Name ?? "no exception"}");
if (seqHandler2Count != 0)
    return Fail($"Sequential exception: handler2 should not run when handler1 throws (got count {seqHandler2Count})");

Console.WriteLine("AOT smoke: PASS");
return 0;

static int Fail(string message)
{
    Console.Error.WriteLine($"AOT smoke: FAIL — {message}");
    return 1;
}
