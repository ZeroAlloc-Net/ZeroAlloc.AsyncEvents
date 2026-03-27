# Async INotify* Interfaces

Drop-in async alternatives to the standard `INotify*` interfaces, useful for MVVM and data-binding scenarios where property or collection changes require async work.

## INotifyPropertyChangedAsync

```csharp
public interface INotifyPropertyChangedAsync
{
    event AsyncEvent<AsyncPropertyChangedEventArgs> PropertyChangedAsync;
}
```

Implement on a ViewModel:

```csharp
public class OrderViewModel : INotifyPropertyChangedAsync
{
    public event AsyncEvent<AsyncPropertyChangedEventArgs> PropertyChangedAsync;

    private string _status = "";
    public string Status
    {
        get => _status;
        set
        {
            _status = value;
            _ = PropertyChangedAsync.InvokeAsync(
                new AsyncPropertyChangedEventArgs(nameof(Status)));
        }
    }
}
```

## INotifyPropertyChangingAsync

Fires before the property changes. Same pattern as `INotifyPropertyChangedAsync` but with `AsyncPropertyChangingEventArgs`.

## INotifyCollectionChangedAsync

```csharp
public interface INotifyCollectionChangedAsync
{
    event AsyncEvent<AsyncCollectionChangedEventArgs> CollectionChangedAsync;
}
```

## INotifyDataErrorInfoAsync

```csharp
public interface INotifyDataErrorInfoAsync
{
    event AsyncEvent<AsyncErrorsChangedEventArgs> ErrorsChangedAsync;
}
```
