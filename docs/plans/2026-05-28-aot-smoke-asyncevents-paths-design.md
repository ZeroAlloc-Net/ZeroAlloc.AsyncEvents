# aot-smoke Coverage Extension — Design

**Date:** 2026-05-28
**Scope:** ZeroAlloc.AsyncEvents aot-smoke project (`samples/ZeroAlloc.AsyncEvents.AotSmoke/`) — extend coverage from the existing happy-path Sequential + Parallel single-handler baseline to also exercise three previously-uncovered behavioral paths: handler-thrown exception propagation under Sequential, unsubscribe/resubscribe lifecycle under Parallel, and CancellationToken propagation. Closes backlog item B1.

## Background

The existing aot-smoke project covers single-handler happy-path invocation for both `[AsyncEvent(InvokeMode.Sequential)]` (OrderPlaced) and `[AsyncEvent(InvokeMode.Parallel)]` (ItemShipped). Each is invoked once, asserts the handler ran and the payload propagated.

It does NOT exercise:

- **Handler-thrown exception propagation** — `Sequential` propagates immediately and skips later handlers; `Parallel` aggregates via `Task.WhenAll` but all handlers run. The smoke should validate the propagation path under AOT — if the generator's emitted accessor mangled the exception flow, the next consumer would silently lose exceptions.
- **Unsubscribe + resubscribe lifecycle** — `Register`/`Unregister` are lockless CAS operations; idempotency + correctness under repeated subscribe/unsubscribe matters for any real consumer.
- **CancellationToken propagation** — `ct.ThrowIfCancellationRequested()` is called before each handler. A regression that dropped the ct check would silently break cancellation; the smoke should fail loudly.

Surfaced 2026-05-27 during the org-wide aot-smoke coverage survey done after [ZeroAlloc.Serialisation](https://github.com/ZeroAlloc-Net/ZeroAlloc.Serialisation) shipped 2.3.1 + 2.3.2 reactively. Already-shipped siblings in the same workstream: [ZeroAlloc.Validation B4 (#51)](https://github.com/ZeroAlloc-Net/ZeroAlloc.Validation/pull/51) and [ZeroAlloc.Inject B1 (#68)](https://github.com/ZeroAlloc-Net/ZeroAlloc.Inject/pull/68).

## Goal

A regression in any of the three behavioral paths fails the aot-smoke job locally. The existing single-handler happy-path block stays green; the three new assertion blocks are strictly additive.

## Decisions

### D-1: Program.cs blocks only — no new fixture files

The existing `OrderService` (1 file) already declares both Sequential and Parallel events. Each new assertion block creates a fresh `OrderService()` instance for isolation; no new types needed.

**Considered and rejected:**

- **Three separate fixture services** (`ExceptionService`, `LifecycleService`, `CancellationService`). Wastes ~30 LOC re-declaring the same `[AsyncEvent]` boilerplate. The existing `OrderService` is the natural test bed.

### D-2: Cancel-before-invoke for the cancellation block (not mid-flight TCS)

The library's cancellation path is `ct.ThrowIfCancellationRequested()` before each handler invocation. Pre-cancelling the token exercises that exact path deterministically — no TaskCompletionSource choreography, no flake risk. The regression net catches a generator regression that bypassed ct propagation, which is the load-bearing concern.

**Considered and rejected:**

- **Mid-flight TCS choreography** — handler awaits a TCS; main code cancels then completes TCS; handler checks `ct.IsCancellationRequested`. More faithful to the backlog's "mid-flight" wording, but adds ~10 LOC for a scenario that exercises library semantics (which haven't changed) rather than generator-emission correctness.

### D-3: Sequential exception + Parallel lifecycle — not the inverse pairings

The backlog's bullets pair Sequential with exceptions and Parallel with unsubscribe/resubscribe. These pairings are deliberate: Sequential's bails-on-first-throw behavior is mode-specific and worth pinning; Parallel's concurrent subscriber dynamics make unsubscribe more interesting under that mode. Sequential + lifecycle would be redundant (same Register/Unregister CAS path), and Parallel + exception would test `Task.WhenAll` semantics (library, not generator).

### D-4: No library API changes, no NuGet release

CI hygiene only. `src/` untouched. PR ships smoke project changes + backlog strikethrough. release-please skips the release manifest.

## Design

### Block 1 — Sequential exception propagation

```csharp
// Sequential mode: handler1 throws → exception propagates → handler2 never runs.
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
```

Two assertions: exception type matches (catches "wrong exception kind"), handler2 count = 0 (catches "Sequential mode didn't bail").

### Block 2 — Parallel unsubscribe/resubscribe lifecycle

```csharp
// Parallel mode: subscribe → invoke (count=1) → unsubscribe → invoke (count=1, no new fire)
//              → resubscribe → invoke (count=2). Tests Register's idempotency-free
// add-once + Unregister's removal under the lockless CAS path.
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
```

Three phase-counted assertions track the count progression `1 → 1 → 2`. A regression in Unregister (e.g. removes wrong slot) would fail phase 2; a regression in Register that prevented re-adding would fail phase 3.

### Block 3 — Cancellation propagation (cancel-before-invoke)

```csharp
// Cancel BEFORE invoke → OperationCanceledException propagates, no handler runs.
// Exercises the same ct.ThrowIfCancellationRequested() path the library uses
// for in-flight cancellation, but deterministically (no timing fragility).
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
```

Two assertions: OCE propagated (catches "ct was ignored"), no handler ran (catches "ct check happened too late, after handler started").

### Backlog update

Replace the existing B1 entry in `docs/backlog.md` with:

```markdown
## ~~B1 — Extend aot-smoke to cover handler exceptions, unsubscribe/resubscribe, and cancellation~~ — ✅ shipped 2026-05-28

**Shipped:** Three new assertion blocks in `samples/ZeroAlloc.AsyncEvents.AotSmoke/Program.cs` (no new fixture files — reuses the existing `OrderService` for each block via fresh instances). Asserts: Sequential exception propagation with handler2 count = 0 (mode bails on first throw); Parallel unsubscribe/resubscribe count progression 1 → 1 → 2; CancellationToken pre-cancellation propagation with no handler invocation.

**Design + plan:** [`docs/plans/2026-05-28-aot-smoke-asyncevents-paths-design.md`](plans/2026-05-28-aot-smoke-asyncevents-paths-design.md) + [`docs/plans/2026-05-28-aot-smoke-asyncevents-paths.md`](plans/2026-05-28-aot-smoke-asyncevents-paths.md).
```

### Files touched

- **MOD:** `samples/ZeroAlloc.AsyncEvents.AotSmoke/Program.cs` — three new assertion blocks (~50 LOC)
- **MOD:** `docs/backlog.md` — strike B1 shipped

Total commit footprint: ~60 LOC. No new files. No library changes.

## Out of scope

- **Mid-flight TaskCompletionSource cancellation** — defer until a regression in mid-flight ct observation surfaces
- **Parallel exception with multiple throwing handlers** — `Task.WhenAll` aggregation behavior; library semantics
- **Cross-mode unsubscribe** (Sequential lifecycle) — same Register/Unregister CAS path; one mode suffices
- **Concurrent subscribers stress test** — that's a stress test, not a smoke
- **`[AsyncEvent]` with custom InvokeMode override per call site** — secondary path
