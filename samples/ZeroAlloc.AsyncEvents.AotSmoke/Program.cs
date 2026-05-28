using System;
using System.Threading;
using System.Threading.Tasks;
using ZeroAlloc.AsyncEvents;
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

// Parallel unsubscribe/resubscribe lifecycle: subscribe → invoke (count=1)
// → unsubscribe → invoke (count still 1) → resubscribe → invoke (count=2).
// Validates Register's idempotency-aware add + Unregister's CAS-based
// remove + Re-Register working after Unregister, under PublishAot.
var parSvc = new OrderService();
var parCount = 0;
AsyncEvent<int> parHandler = (id, ct) =>
{
    Interlocked.Increment(ref parCount);
    return ValueTask.CompletedTask;
};

parSvc.ItemShipped += parHandler;
await parSvc.ShipItemAsync(1, CancellationToken.None).ConfigureAwait(false);
if (parCount != 1)
    return Fail($"Parallel lifecycle phase 1 (subscribed): expected 1, got {parCount}");

parSvc.ItemShipped -= parHandler;
await parSvc.ShipItemAsync(2, CancellationToken.None).ConfigureAwait(false);
if (parCount != 1)
    return Fail($"Parallel lifecycle phase 2 (after unsubscribe): expected count still 1, got {parCount}");

parSvc.ItemShipped += parHandler;
await parSvc.ShipItemAsync(3, CancellationToken.None).ConfigureAwait(false);
if (parCount != 2)
    return Fail($"Parallel lifecycle phase 3 (after resubscribe): expected 2, got {parCount}");

// Cancellation propagation: pre-cancelled token → InvokeAsync surfaces
// OperationCanceledException via ct.ThrowIfCancellationRequested() before
// the first handler runs. Deterministic (no TCS choreography, no timing
// fragility) — exercises the same code path in-flight cancellation uses.
var cancelSvc = new OrderService();
var cancelHandlerCount = 0;
cancelSvc.OrderPlaced += (id, ct) =>
{
    Interlocked.Increment(ref cancelHandlerCount);
    return ValueTask.CompletedTask;
};

using var cts = new CancellationTokenSource();
cts.Cancel();

Exception? cancelCaught = null;
try
{
    await cancelSvc.PlaceOrderAsync("cancel-1", cts.Token).ConfigureAwait(false);
}
catch (OperationCanceledException ex)
{
    cancelCaught = ex;
}

if (cancelCaught is null)
    return Fail("Cancellation: expected OperationCanceledException, got no exception");
if (cancelHandlerCount != 0)
    return Fail($"Cancellation: handler should not run when token pre-cancelled (got count {cancelHandlerCount})");

Console.WriteLine("AOT smoke: PASS");
return 0;

static int Fail(string message)
{
    Console.Error.WriteLine($"AOT smoke: FAIL — {message}");
    return 1;
}
