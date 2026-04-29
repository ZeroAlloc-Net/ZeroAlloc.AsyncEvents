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
- **ArrayPool parallel dispatch** — rented array for fan-out, returned immediately after `WhenAll`
- **Multi-target** — `netstandard2.0`, `netstandard2.1`, `net8.0`, `net10.0`
- **AOT compatible**

## Performance

10 handlers registered, invoked once. Compared against `EventHandler<T>` (sync multicast, baseline) and naive async (`Func<string, Task>` + `Task.WhenAll`). BenchmarkDotNet v0.14.0, .NET 9, X64 RyuJIT AVX2.

| Method | Mean | Error | StdDev | Ratio | Allocated |
|---|---|---|---|---|---|
| Sync_MulticastDelegate_10Handlers | 22.96 ns | 0.926 ns | 2.597 ns | 1.01 | — |
| NaiveAsync_TaskWhenAll_10Handlers | 203.48 ns | 4.111 ns | 7.307 ns | 8.96x | 280 B |
| ZeroAlloc_Parallel_10Handlers | 70.10 ns | 1.446 ns | 4.103 ns | 3.09x | 136 B |
| ZeroAlloc_Sequential_10Handlers | 16.10 ns | 0.655 ns | 1.857 ns | 0.71x | — |

ZeroAlloc sequential mode is **30% faster than a sync multicast delegate** with zero allocations. ZeroAlloc parallel mode is **3× faster than naive async** with 51% less memory.

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

Apply `[AsyncEvent]` to the class instead to cover all `AsyncEventHandler<TArgs>` fields at once. See [Source Generator](docs/source-generator.md) for details.

## Async INotify\* Interfaces

Async `INotify*` interfaces and event args are provided by [ZeroAlloc.Notify](https://github.com/ZeroAlloc-Net/ZeroAlloc.Notify), which builds on this package.

## Design Philosophy

`AsyncEventHandler<TArgs>` is a struct wrapping a `State` reference — copy semantics are intentional and match how `event` fields work in C#. Use it as a `private` field and expose `Register`/`Unregister` methods, or use the `+=`/`-=` operators directly.

`CancellationToken` is threaded through every call site — handlers opt into cooperative cancellation at the delegate boundary. Sequential mode respects cancellation between handler invocations; parallel mode checks before dispatch.

See [docs/](docs/README.md) for full documentation.
