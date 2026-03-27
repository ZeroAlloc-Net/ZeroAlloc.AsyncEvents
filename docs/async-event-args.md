# Async Event Args

Built-in event args types matching the standard `System.ComponentModel` args.

## AsyncPropertyChangedEventArgs

```csharp
var args = new AsyncPropertyChangedEventArgs(propertyName: nameof(MyProperty));
```

Equivalent to `PropertyChangedEventArgs` — carries the property name.

## AsyncPropertyChangingEventArgs

```csharp
var args = new AsyncPropertyChangingEventArgs(propertyName: nameof(MyProperty));
```

Equivalent to `PropertyChangingEventArgs`.

## AsyncCollectionChangedEventArgs

```csharp
var args = new AsyncCollectionChangedEventArgs(action, newItems, oldItems);
```

Equivalent to `NotifyCollectionChangedEventArgs`.

## AsyncErrorsChangedEventArgs

```csharp
var args = new AsyncErrorsChangedEventArgs(propertyName: nameof(MyProperty));
```

Equivalent to `DataErrorsChangedEventArgs`.

## CancellationToken

Every `AsyncEvent<TArgs>` delegate receives a `CancellationToken` as its second parameter. Handlers should pass it through to any awaitable work:

```csharp
handler.Register(async (args, ct) =>
{
    await DoWorkAsync(args, ct); // pass ct through
});
```
