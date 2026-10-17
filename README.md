# ZeroAlloc.AsyncEvents

[![NuGet](https://img.shields.io/nuget/v/ZeroAlloc.AsyncEvents.svg)](https://www.nuget.org/packages/ZeroAlloc.AsyncEvents)
[![Build](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/actions/workflows/ci.yml/badge.svg)](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/graph/badge.svg)](https://codecov.io/gh/ZeroAlloc-Net/ZeroAlloc.AsyncEvents)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/blob/main/LICENSE)
[![AOT](https://img.shields.io/badge/AOT--Compatible-passing-brightgreen)](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
[![GitHub Sponsors](https://img.shields.io/github/sponsors/MarcelRoozekrans?style=flat&logo=githubsponsors&color=ea4aaa&label=Sponsor)](https://github.com/sponsors/MarcelRoozekrans)

Async events for .NET that you can actually `await`: a drop-in replacement for `EventHandler<T>` and `Func<T, Task>` + `Task.WhenAll`, with no allocations per invocation when handlers complete synchronously.

## Why ZeroAlloc.AsyncEvents

- **0 B per invocation.** Raising an event whose handlers complete synchronously allocates nothing, in parallel and in sequential mode; tests assert it on net8.0 and net10.0. Naive async allocates 280–288 B per call.
- **More than 10× faster than `Task.WhenAll` fan-out**, and faster than a plain synchronous multicast delegate, measured in the same run.
- **Real `async` semantics.** Handlers return `ValueTask`, receive a `CancellationToken`, and run in parallel or in registration order. Exceptions reach the caller instead of vanishing in an `async void`.
- **Works under NativeAOT and trimming.** No reflection; CI publishes and runs a NativeAOT sample on every build.

## Install

```bash
dotnet add package ZeroAlloc.AsyncEvents
```

## Quick start

```csharp
using ZeroAlloc.AsyncEvents;

var orders = new OrderService();

orders.OrderPlaced += async (order, ct) =>
{
    await Task.Delay(10, ct);                        // e.g. send a confirmation email
    Console.WriteLine($"Emailed order {order.Id}");
};
orders.OrderPlaced += (order, ct) =>
{
    Console.WriteLine($"Audited order {order.Id}");  // completes synchronously
    return ValueTask.CompletedTask;
};

await orders.PlaceAsync(new OrderPlaced(42));

public sealed record OrderPlaced(int Id);

public partial class OrderService
{
    // The generator writes `public event AsyncEvent<OrderPlaced> OrderPlaced` for this field.
    [AsyncEvent]
    private AsyncEventHandler<OrderPlaced> _orderPlaced = new(InvokeMode.Parallel);

    public ValueTask PlaceAsync(OrderPlaced order, CancellationToken ct = default)
        => _orderPlaced.InvokeAsync(order, ct);
}
```

`PlaceAsync` returns once both handlers have finished. Subscribers use the familiar `+=` and `-=`.

## What you get

- **Parallel or sequential dispatch**, chosen per field and overridable per call. [Invoke modes](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/blob/main/docs/invoke-modes.md)
- **Cancelable events.** A handler sets `Cancel` and the remaining handlers are skipped, for validation and veto pipelines. [Cancelable events](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/blob/main/docs/cancel-events.md)
- **Generated `event` accessors** with `[AsyncEvent]` on a field or on the whole class. Mistakes are reported at compile time. [Source generator](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/blob/main/docs/source-generator.md), [diagnostics](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/blob/main/docs/diagnostics.md)
- **Lock-free, thread-safe subscription.** Registering and unregistering never takes a lock.
- **Broad reach:** `netstandard2.0`, `netstandard2.1`, `net8.0` and `net10.0`.
- **Async property and collection notifications**, the async `INotifyPropertyChanged` family, come from [ZeroAlloc.Notify](https://github.com/ZeroAlloc-Net/ZeroAlloc.Notify), which builds on this package.

## Performance

Ten handlers, each returning a completed task, invoked once. net10.0, BenchmarkDotNet, same run; the ratio is against a synchronous multicast delegate. Allocations are exact; speed is a same-run ratio.

| Method | Ratio | Allocated |
|---|---|---|
| `EventHandler<T>`, sync multicast | 1.00 | 0 B |
| `Func<T, Task>` + `Task.WhenAll` | 11.93 | 280 B |
| ZeroAlloc.AsyncEvents, parallel | 0.44 | 0 B |
| ZeroAlloc.AsyncEvents, sequential | 0.61 | 0 B |

Full results for net8.0 and net10.0, methodology and allocation notes: [docs/performance.md](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/blob/main/docs/performance.md).

## Learn more

- Documentation: [asyncevents.zeroalloc.net](https://asyncevents.zeroalloc.net)
- Related packages: [ZeroAlloc.Notify](https://github.com/ZeroAlloc-Net/ZeroAlloc.Notify) for async property and collection notifications, and the rest of the [ZeroAlloc family](https://github.com/ZeroAlloc-Net)
- Contributing: issues and pull requests are welcome on [GitHub](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/issues)
- License: [MIT](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/blob/main/LICENSE)
