# ZeroAlloc.AsyncEvents — Backlog

Candidate enhancements identified during real-world usage. Each item is independent and can be implemented in any order. Order is rough priority, not commitment. Items graduate from this backlog when the friction or value is concrete enough to justify the work.

---

## B1 — Extend aot-smoke to cover handler exceptions, unsubscribe/resubscribe, and cancellation

**What.** The existing `aot-smoke` project (`samples/ZeroAlloc.AsyncEvents.AotSmoke/`) exercises sequential and parallel handler-invocation happy paths via `Publish`. It does NOT touch three corner-case code paths the generator emits: handler-thrown exceptions under both Sequential and Parallel modes, handler unsubscribe (and resubscribe) lifecycle, and `CancellationToken` propagation under concurrent subscribers. The first consumer to hit any of these under Native AOT + the trimmer will discover the gap.

**Why.** Surfaced 2026-05-27 during the org-wide AOT-smoke coverage survey done after [ZeroAlloc.Serialisation](https://github.com/ZeroAlloc-Net/ZeroAlloc.Serialisation) shipped 2.3.1 + 2.3.2 reactively. ZA.Serialisation's smoke covered only the V0 path while V1 `[ValueObject]` paths were left un-validated — two patches shipped because of that gap. Same "smoke exists but partial" pattern applies here.

**Sketch.** Extend `samples/ZeroAlloc.AsyncEvents.AotSmoke/Program.cs` with three fixtures + assertions:

- A `Sequential` event with one handler that throws and one that succeeds; assert the documented contract (whatever it is — succeeding handler still runs vs. fail-fast vs. aggregated exception) and that the thrown exception is observable to the caller.
- A `Parallel` event where the second subscriber unsubscribes between two `Publish` calls and then resubscribes; assert the right invocation counts per phase.
- A handler that observes `CancellationToken` cancellation mid-flight; assert it bails before completion.

The existing happy paths stay; this is purely additive.

**Tradeoff / risks.** No new CI infrastructure. The cancellation test is timing-sensitive (CTS firing mid-handler); use deterministic `TaskCompletionSource` choreography rather than `Thread.Sleep` to avoid flakes.

**Graduation signal.** First downstream consumer that hits a handler-exception bug. Or proactive: ship as a chore commit.

---

## How items get added here

Open a PR adding a new section in this file. Use the same `What / Why / Sketch / Tradeoff / Graduation signal` structure. Items remain open until a follow-up PR strikes them through with a `✅ shipped X.Y.Z` marker and links the release.
