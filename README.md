# ZeroAlloc.AsyncEvents

[![NuGet](https://img.shields.io/nuget/v/ZeroAlloc.AsyncEvents.svg)](https://www.nuget.org/packages/ZeroAlloc.AsyncEvents)
[![Build](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/actions/workflows/ci.yml/badge.svg)](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![AOT](https://img.shields.io/badge/AOT--Compatible-passing-brightgreen)](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
[![GitHub Sponsors](https://img.shields.io/github/sponsors/MarcelRoozekrans?style=flat&logo=githubsponsors&color=ea4aaa&label=Sponsor)](https://github.com/sponsors/MarcelRoozekrans)

Zero-allocation async event handler structs for .NET. Lock-free registration, `ValueTask` invocation, `ArrayPool` parallel dispatch.

## Key Characteristics

- **Lock-free registration** — CAS-loop register/unregister, no locks
- **ValueTask throughout** — no `Task` allocations on hot paths
- **ArrayPool parallel dispatch** — handlers that complete synchronously cost nothing; only unfinished or failed ones go to a rented array and `WhenAll`
- **Multi-target** — `netstandard2.0`, `netstandard2.1`, `net8.0`, `net10.0`
- **AOT compatible**

## Performance

10 handlers registered, invoked once, each returning a completed task. Compared against `EventHandler<T>` (sync multicast, baseline) and naive async (`Func<string, Task>` + `Task.WhenAll`). BenchmarkDotNet v0.15.8, X64 RyuJIT, Intel Core i9-12900HK, in-process toolchain.

**net8.0**

| Method | Mean | Error | StdDev | Ratio | Allocated |
|---|---|---|---|---|---|
| Sync_MulticastDelegate_10Handlers | 57.81 ns | 1.246 ns | 3.675 ns | 1.00 | — |
| NaiveAsync_TaskWhenAll_10Handlers | 476.80 ns | 9.552 ns | 12.080 ns | 8.28x | 288 B |
| ZeroAlloc_Parallel_10Handlers | 35.52 ns | 1.271 ns | 3.728 ns | 0.62x | — |
| ZeroAlloc_Sequential_10Handlers | 47.75 ns | 0.350 ns | 0.328 ns | 0.83x | — |

**net10.0**

| Method | Mean | Error | StdDev | Ratio | Allocated |
|---|---|---|---|---|---|
| Sync_MulticastDelegate_10Handlers | 40.69 ns | 0.501 ns | 0.444 ns | 1.00 | — |
| NaiveAsync_TaskWhenAll_10Handlers | 440.47 ns | 20.112 ns | 59.300 ns | 10.83x | 280 B |
| ZeroAlloc_Parallel_10Handlers | 18.31 ns | 1.093 ns | 3.207 ns | 0.45x | — |
| ZeroAlloc_Sequential_10Handlers | 29.60 ns | 1.067 ns | 3.147 ns | 0.73x | — |

Both ZeroAlloc modes allocate nothing on either target when handlers complete synchronously, and both are faster than a sync multicast delegate. Parallel mode is more than 10× faster than naive async, which allocates 280–288 B per call.

## Installation

```
dotnet add package ZeroAlloc.AsyncEvents
```

## Quick Start

```csharp
// Declare the backing field
private AsyncEventHandler<OrderPlacedArgs> _orderPlaced = new(InvokeMode.Parallel);

// Expose as a C# event — or use [AsyncEvent] to let the source generator do this
public event AsyncEvent<OrderPlacedArgs> OrderPlaced
{
    add    => _orderPlaced.Register(value);
    remove => _orderPlaced.Unregister(value);
}

// Invoke
await _orderPlaced.InvokeAsync(new OrderPlacedArgs(orderId), cancellationToken);
```

## Source Generator

Annotate fields with `[AsyncEvent]` on a `partial` class and the generator writes the `event` property for you:

```csharp
public partial class OrderService
{
    [AsyncEvent(InvokeMode.Parallel)]
    private AsyncEventHandler<OrderPlacedArgs> _orderPlaced = new(InvokeMode.Parallel);
}
```

Generates:

```csharp
public event AsyncEvent<OrderPlacedArgs> OrderPlaced
{
    add    => _orderPlaced.Register(value);
    remove => _orderPlaced.Unregister(value);
}
```

Apply `[AsyncEvent]` to the class instead to cover all `AsyncEventHandler<TArgs>` fields at once. See [Source Generator](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/blob/main/docs/source-generator.md) for details.

## Async INotify\* Interfaces

Async `INotify*` interfaces and event args are provided by [ZeroAlloc.Notify](https://github.com/ZeroAlloc-Net/ZeroAlloc.Notify), which builds on this package.

## Design Philosophy

`AsyncEventHandler<TArgs>` is a struct wrapping a `State` reference — copy semantics are intentional and match how `event` fields work in C#. Use it as a `private` field and expose `Register`/`Unregister` methods, or use the `+=`/`-=` operators directly.

`CancellationToken` is threaded through every call site — handlers opt into cooperative cancellation at the delegate boundary. Sequential mode respects cancellation between handler invocations; parallel mode checks before dispatch.

See [docs/](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/blob/main/docs/README.md) for full documentation.
