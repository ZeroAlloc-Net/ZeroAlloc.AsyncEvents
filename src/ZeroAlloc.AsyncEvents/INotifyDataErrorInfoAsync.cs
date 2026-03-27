using System.Collections;

namespace ZeroAlloc.AsyncEvents;

public interface INotifyDataErrorInfoAsync
{
#pragma warning disable MA0046 // async event delegates intentionally return ValueTask
    event AsyncEvent<AsyncErrorsChangedEventArgs> ErrorsChangedAsync;
#pragma warning restore MA0046
    bool HasErrors { get; }
    IEnumerable GetErrors(string? propertyName);
}
