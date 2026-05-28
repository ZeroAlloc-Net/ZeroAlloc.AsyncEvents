# aot-smoke AsyncEvents Paths Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Extend `samples/ZeroAlloc.AsyncEvents.AotSmoke/Program.cs` to cover three previously-uncovered behavioral paths under `PublishAot=true`: handler-thrown exception propagation under Sequential mode, unsubscribe/resubscribe lifecycle under Parallel mode, and `CancellationToken` pre-cancellation propagation. Closes backlog item B1.

**Architecture:** Three new assertion blocks appended to `Program.cs`. Each block creates a fresh `OrderService()` instance for isolation (the existing service already has Sequential `OrderPlaced` + Parallel `ItemShipped` events wired — no new fixture files needed). Reuses the existing `Fail(message)` helper for diagnostic output.

**Tech Stack:** .NET 10, `PublishAot=true`, `ZeroAlloc.AsyncEvents.AsyncEventHandler<T>` with lockless CAS register/unregister, `[AsyncEvent(InvokeMode.Sequential|Parallel)]` generator-emitted accessors.

**Design doc:** `docs/plans/2026-05-28-aot-smoke-asyncevents-paths-design.md` (committed at `0ec779b`).

**Working branch:** `chore/aot-smoke-cover-asyncevents-paths` (off `main` at `adbf572` — the 1.1.2 release; design committed).

---

## Phase 0 — Orient (5 min)

### Task 0.1: Read the existing smoke + library APIs

**Files (read-only):**

- `samples/ZeroAlloc.AsyncEvents.AotSmoke/Program.cs` — current shape (~47 LOC): setup, single-handler subscribe via `+=`, await invoke, assert count + payload. Ends with `Console.WriteLine("AOT smoke: PASS")` + `return 0` + `static int Fail(string)` helper.
- `samples/ZeroAlloc.AsyncEvents.AotSmoke/OrderService.cs` — `[AsyncEvent(InvokeMode.Sequential)] private AsyncEventHandler<string> _orderPlaced;` + `[AsyncEvent(InvokeMode.Parallel)] private AsyncEventHandler<int> _itemShipped;` — generator emits public `OrderPlaced` + `ItemShipped` accessors with `+=` / `-=` operators.
- `src/ZeroAlloc.AsyncEvents/AsyncEventHandler.cs` — confirms the semantics:
  - Sequential: `foreach { ct.ThrowIfCancellationRequested(); await cb(...) }` — first throw propagates, later handlers don't run.
  - Parallel: synchronous loop fires all `tasks[i] = callbacks[i](args, ct).AsTask()`, then `await Task.WhenAll(...)`. All handlers START before any throw is observed.
  - `Register` is idempotent (no-op on duplicate via ReferenceEquals).
  - `Unregister` removes via Array.IndexOf; no-op if not found.
  - Both use Interlocked.CompareExchange CAS loops.

---

## Phase 1 — Block 1: Sequential exception propagation (25 min, 4 tasks)

### Task 1.1: Append the Sequential exception block to Program.cs

**File (MOD):** `samples/ZeroAlloc.AsyncEvents.AotSmoke/Program.cs`

Read the current Program.cs. Structure ends with:

```csharp
if (parallelCount != 1)
    return Fail($"Parallel event expected 1 invocation, got {parallelCount}");

Console.WriteLine("AOT smoke: PASS");
return 0;

static int Fail(string message) { ... }
```

Insert the Sequential exception block AFTER the existing Parallel `parallelCount` assertion and BEFORE the `Console.WriteLine("AOT smoke: PASS")` line:

```csharp
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
```

Verify the lambda syntax `(id, ct) => throw new InvalidOperationException(...)` compiles — should be fine; C# supports throw-expressions in lambdas.

### Task 1.2: Build

```bash
cd c:/Projects/Prive/ZeroAlloc/ZeroAlloc.AsyncEvents
dotnet build samples/ZeroAlloc.AsyncEvents.AotSmoke/ZeroAlloc.AsyncEvents.AotSmoke.csproj -c Release 2>&1 | tail -10
```

Expected: build succeeds, no warnings.

If `(id, ct) => throw ...` triggers analyzer complaints (CS0815 or similar about throw-expression type inference), wrap in a statement-bodied lambda:

```csharp
seqSvc.OrderPlaced += (id, ct) =>
{
    throw new InvalidOperationException("handler1 fault");
};
```

(Statement-bodied lambdas return `void` for sync code, but the delegate signature is `AsyncEvent<TArgs>` which returns `ValueTask`. The throw-expression form may need adjustment; alternative is to construct an explicit `Func`-style block that throws.)

If the compiler complains about the lambda signature, use this fallback:

```csharp
static ValueTask Handler1Throws(string id, CancellationToken ct)
    => throw new InvalidOperationException("handler1 fault");

seqSvc.OrderPlaced += Handler1Throws;
```

Convert any local lambda to a `static` method if needed for AOT-friendliness (closure-free).

### Task 1.3: Run + verify

```bash
dotnet run -c Release --project samples/ZeroAlloc.AsyncEvents.AotSmoke/ZeroAlloc.AsyncEvents.AotSmoke.csproj 2>&1 | tail -5
```

Expected: `AOT smoke: PASS`.

If the run prints `AOT smoke: FAIL — Sequential exception: ...`:
- "expected InvalidOperationException, got AggregateException" — the library wrapped the throw; check whether `AsyncEventHandler<T>.InvokeSequentialAsync` actually rethrows unwrapped (it does per the source, but worth verifying)
- "handler2 should not run ... (got count 1)" — Sequential mode didn't bail on the first throw; a real regression in the library — STOP and surface to user.

### Task 1.4: Commit Block 1

```bash
git add samples/ZeroAlloc.AsyncEvents.AotSmoke/Program.cs
git commit -m "chore(aot-smoke): cover Sequential exception propagation

Sequential mode bails on the first handler throw — handler2 never runs.
Asserts the caught exception is InvalidOperationException (not wrapped)
and that handler2's invocation count remains 0. Validates the foreach
loop's first-throw-bails behavior under PublishAot."
```

---

## Phase 2 — Block 2: Parallel unsubscribe/resubscribe lifecycle (25 min, 4 tasks)

### Task 2.1: Append the Parallel lifecycle block to Program.cs

**File (MOD):** `samples/ZeroAlloc.AsyncEvents.AotSmoke/Program.cs`

Insert AFTER the Phase 1 Sequential exception block, BEFORE `Console.WriteLine("AOT smoke: PASS")`:

```csharp
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
```

Note: `AsyncEvent<int>` is the delegate type the library defines (`public delegate ValueTask AsyncEvent<TArgs>(TArgs args, CancellationToken ct);` or similar). Confirm by reading the existing `_orderPlaced += (id, ct) => ...` pattern — the same delegate shape is used here, just stored in a local for the unsubscribe.

### Task 2.2: Build

```bash
dotnet build samples/ZeroAlloc.AsyncEvents.AotSmoke/ZeroAlloc.AsyncEvents.AotSmoke.csproj -c Release 2>&1 | tail -10
```

Expected: build succeeds.

If `AsyncEvent<int>` is not the correct type name (e.g. the library uses `Func<int, CancellationToken, ValueTask>` instead), check `src/ZeroAlloc.AsyncEvents/` for the delegate definition. Likely candidates: `AsyncEvent<T>`, `AsyncEventCallback<T>`. Adjust the local variable type.

### Task 2.3: Run + verify

```bash
dotnet run -c Release --project samples/ZeroAlloc.AsyncEvents.AotSmoke/ZeroAlloc.AsyncEvents.AotSmoke.csproj 2>&1 | tail -5
```

Expected: `AOT smoke: PASS`.

If "phase 2 (after unsubscribe): expected count still 1, got 2" — Unregister didn't remove the handler. Real library regression — STOP and surface to user.

If "phase 3 (after resubscribe): expected 2, got 1" — Register skipped the re-add (idempotency check should NOT fire because Unregister removed the callback first). Real library regression — STOP.

### Task 2.4: Commit Block 2

```bash
git add samples/ZeroAlloc.AsyncEvents.AotSmoke/Program.cs
git commit -m "chore(aot-smoke): cover Parallel unsubscribe/resubscribe lifecycle

Subscribe-invoke-unsubscribe-invoke-resubscribe-invoke flow tests the
lockless CAS register/unregister path under PublishAot. Asserts count
progression 1 → 1 → 2 across the three phases; catches regressions in
Unregister's removal or Register's post-unregister re-add."
```

---

## Phase 3 — Block 3: Cancellation propagation (20 min, 4 tasks)

### Task 3.1: Append the cancellation block to Program.cs

**File (MOD):** `samples/ZeroAlloc.AsyncEvents.AotSmoke/Program.cs`

Insert AFTER the Phase 2 Parallel lifecycle block, BEFORE `Console.WriteLine("AOT smoke: PASS")`:

```csharp
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
```

### Task 3.2: Build

```bash
dotnet build samples/ZeroAlloc.AsyncEvents.AotSmoke/ZeroAlloc.AsyncEvents.AotSmoke.csproj -c Release 2>&1 | tail -10
```

Expected: build succeeds.

### Task 3.3: Run + verify

```bash
dotnet run -c Release --project samples/ZeroAlloc.AsyncEvents.AotSmoke/ZeroAlloc.AsyncEvents.AotSmoke.csproj 2>&1 | tail -5
```

Expected: `AOT smoke: PASS` (the full suite — existing Sequential + Parallel happy path + Phase 1 exception + Phase 2 lifecycle + Phase 3 cancellation all green).

If "expected OperationCanceledException, got no exception" — the library's `ct.ThrowIfCancellationRequested()` didn't fire. Possible cause: a different exception type was thrown (catch block scoped to OCE only). Loosen the catch to `catch (Exception ex)` temporarily to see what was thrown, then narrow back. Real regression if anything other than OCE.

If "handler should not run ... (got count 1)" — the ct check fires AFTER the first handler, not before. Real library regression.

### Task 3.4: Commit Block 3

```bash
git add samples/ZeroAlloc.AsyncEvents.AotSmoke/Program.cs
git commit -m "chore(aot-smoke): cover CancellationToken pre-cancellation propagation

Pre-cancelled token surfaces OperationCanceledException via the library's
ct.ThrowIfCancellationRequested() before the first handler runs. Asserts
OCE is caught (not swallowed) and handler count remains 0 (ct check
fires before the handler invocation, not after). Deterministic — no TCS
or timing dependency."
```

---

## Phase 4 — Strike B1 + push + PR (15 min, 3 tasks)

### Task 4.1: Update `docs/backlog.md`

Read the file to find the existing B1 entry (added by PR #75 — the open `## B1 — Extend aot-smoke to cover handler exceptions, unsubscribe/resubscribe, and cancellation` block with What / Why / Sketch / Tradeoff / Graduation signal subsections).

Replace the ENTIRE B1 block with the struck-through shipped marker (preserving the surrounding header + "How items get added here" footer if present):

```markdown
## ~~B1 — Extend aot-smoke to cover handler exceptions, unsubscribe/resubscribe, and cancellation~~ — ✅ shipped 2026-05-28

**Shipped:** Three new assertion blocks in `samples/ZeroAlloc.AsyncEvents.AotSmoke/Program.cs` (no new fixture files — reuses the existing `OrderService` for each block via fresh instances). Asserts: Sequential exception propagation with handler2 count = 0 (mode bails on first throw); Parallel unsubscribe/resubscribe count progression 1 → 1 → 2; CancellationToken pre-cancellation propagation with no handler invocation.

**Design + plan:** [`docs/plans/2026-05-28-aot-smoke-asyncevents-paths-design.md`](plans/2026-05-28-aot-smoke-asyncevents-paths-design.md) + [`docs/plans/2026-05-28-aot-smoke-asyncevents-paths.md`](plans/2026-05-28-aot-smoke-asyncevents-paths.md).
```

Replace in place. Don't add a new entry alongside.

### Task 4.2: Build + commit docs

```bash
cd c:/Projects/Prive/ZeroAlloc/ZeroAlloc.AsyncEvents
dotnet build -c Release 2>&1 | tail -5
```

Expected: full solution builds clean.

```bash
git add docs/backlog.md
git commit -m "docs(backlog): strike B1 shipped (aot-smoke coverage extension)"
```

### Task 4.3: Push + open PR + STOP

```bash
git push -u origin chore/aot-smoke-cover-asyncevents-paths

gh pr create \
  --title "chore(aot-smoke): cover handler exceptions, unsubscribe lifecycle, cancellation" \
  --body "$(cat <<'EOF'
## Summary

Closes backlog item B1. The existing aot-smoke project covered only the single-handler happy path for both Sequential + Parallel modes; this PR adds three independent assertion blocks exercising the three previously-uncovered behavioral paths under `PublishAot=true`:

- **Sequential exception propagation** — handler1 throws → `InvokeAsync` rethrows → handler2 never runs. Asserts caught exception type and handler2 count = 0.
- **Parallel unsubscribe/resubscribe lifecycle** — subscribe → invoke (count=1) → unsubscribe → invoke (count still 1) → resubscribe → invoke (count=2). Validates `Register`/`Unregister`'s lockless CAS path.
- **CancellationToken propagation** — pre-cancelled token → `OperationCanceledException` propagates before any handler runs. Deterministic (no TCS / timing fragility).

## Why now

Surfaced 2026-05-27 during the org-wide aot-smoke coverage survey done after [ZeroAlloc.Serialisation](https://github.com/ZeroAlloc-Net/ZeroAlloc.Serialisation) shipped 2.3.1 + 2.3.2 reactively. Same "smoke exists but partial" pattern applied to ZA.AsyncEvents. This PR closes it. Already-shipped siblings in the same workstream: [ZeroAlloc.Validation B4 (#51)](https://github.com/ZeroAlloc-Net/ZeroAlloc.Validation/pull/51) and [ZeroAlloc.Inject B1 (#68)](https://github.com/ZeroAlloc-Net/ZeroAlloc.Inject/pull/68).

## What changed

- 3 new assertion blocks in `Program.cs` (~50 LOC) — each block creates a fresh `OrderService()` for isolation
- `docs/backlog.md` — B1 entry struck shipped

No new fixture files. No library changes.

## Decisions ([design doc](docs/plans/2026-05-28-aot-smoke-asyncevents-paths-design.md))

- **Reuse the existing `OrderService` via fresh instances** rather than adding `ExceptionService`/`LifecycleService`/`CancellationService` fixtures — the existing service already has both Sequential + Parallel events wired
- **Cancel-before-invoke for the cancellation block** rather than mid-flight TCS choreography — deterministic, no timing fragility, exercises the same `ct.ThrowIfCancellationRequested()` path
- **Sequential + exception / Parallel + lifecycle pairings** — Sequential's bails-on-first-throw is mode-specific worth pinning; Parallel's concurrent dynamics make unsubscribe more interesting under that mode

## SemVer

No package version bump — CI-only `chore:` changes. release-please will treat as `chore:` and skip the release manifest.

## Test plan

- [x] Local build clean (`dotnet build -c Release`, 0 errors)
- [x] Local JIT run prints `AOT smoke: PASS`
- [ ] CI build clean
- [ ] CI aot-smoke job passes (the real AOT publish on ubuntu-latest)

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

**STOP** after `gh pr create` succeeds. Do NOT admin-merge.

## Constraints

- **Don't touch any library code in `src/`** — strictly smoke-project additions.
- **Don't touch the existing single-handler happy-path block** — your blocks insert AFTER the existing Parallel `parallelCount` assertion and BEFORE `Console.WriteLine("AOT smoke: PASS")`.
- **PR body via single-quoted heredoc** to avoid shell quoting fragility.
- **PR title has no `[ValueObject]`-style brackets** — the title above has none (release-please safe).
- **PowerShell on Windows**; use `Bash` for the git heredoc commits + the `gh pr create` heredoc body.

## Final report after Phase 4

After `gh pr create`:
- PR URL
- Commit hash for the docs commit
- Final build status (clean?)
- Confirmation: NOT admin-merged

---

## Verification checklist

- [ ] **Phase 1:** Sequential exception block compiles + run passes (OCE-like exception type + handler2 count = 0)
- [ ] **Phase 2:** Parallel lifecycle block compiles + count progression 1 → 1 → 2 holds across all three phases
- [ ] **Phase 3:** Cancellation block compiles + `OperationCanceledException` caught + handler count = 0
- [ ] **Phase 4:** B1 backlog struck through, PR opens with all three blocks visible in the diff

## Out of scope (deferred to backlog or future PRs)

- **Mid-flight TaskCompletionSource cancellation** — exercise of library semantics rather than generator-emission correctness
- **Parallel exception with multiple throwing handlers** — `Task.WhenAll` aggregation behavior; library not generator concern
- **Cross-mode unsubscribe** (Sequential lifecycle) — same lockless CAS path; one mode suffices
- **Concurrent subscribers stress test** — stress test, not smoke
- **`[AsyncEvent]` with custom `InvokeMode` override per call site** (`InvokeAsync(args, modeOverride, ct)`) — secondary path
