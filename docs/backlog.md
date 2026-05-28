# ZeroAlloc.AsyncEvents — Backlog

Candidate enhancements identified during real-world usage. Each item is independent and can be implemented in any order. Order is rough priority, not commitment. Items graduate from this backlog when the friction or value is concrete enough to justify the work.

---

## ~~B1 — Extend aot-smoke to cover handler exceptions, unsubscribe/resubscribe, and cancellation~~ — ✅ shipped 2026-05-28

**Shipped:** Three new assertion blocks in `samples/ZeroAlloc.AsyncEvents.AotSmoke/Program.cs` (no new fixture files — reuses the existing `OrderService` for each block via fresh instances). Asserts: Sequential exception propagation with handler2 count = 0 (mode bails on first throw); Parallel unsubscribe/resubscribe count progression 1 → 1 → 2; CancellationToken pre-cancellation propagation with no handler invocation.

**Findings worth flagging** (durable record):

- **Explicit delegate type requires `using ZeroAlloc.AsyncEvents;`** — Phase 2's `AsyncEvent<int>` local variable needed the namespace imported, while Phase 1's inline lambdas didn't (they were inferred via the `+=` operator behind a generated accessor).
- **Sequential mode propagates the throw type unwrapped** — `InvalidOperationException` thrown in handler1 surfaces from `InvokeAsync` directly, not wrapped in `AggregateException`. Confirmed by the smoke's `catch (Exception ex)` + `seqCaught is not InvalidOperationException` assertion combo.
- **`ct.ThrowIfCancellationRequested()` fires BEFORE the handler loop** — `cancelHandlerCount` stays at 0 with a pre-cancelled token. A regression that moved the check to after the handler invocation would fail this assertion clearly.

**Design + plan:** [`docs/plans/2026-05-28-aot-smoke-asyncevents-paths-design.md`](plans/2026-05-28-aot-smoke-asyncevents-paths-design.md) + [`docs/plans/2026-05-28-aot-smoke-asyncevents-paths.md`](plans/2026-05-28-aot-smoke-asyncevents-paths.md).

---

## How items get added here

Open a PR adding a new section in this file. Use the same `What / Why / Sketch / Tradeoff / Graduation signal` structure. Items remain open until a follow-up PR strikes them through with a `✅ shipped X.Y.Z` marker and links the release.
