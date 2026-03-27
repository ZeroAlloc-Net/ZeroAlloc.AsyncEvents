using System.Collections;

namespace ZeroAlloc.AsyncEvents.Tests;

public class InterfaceTests
{
    private class Model : INotifyPropertyChangedAsync, INotifyPropertyChangingAsync,
                          INotifyCollectionChangedAsync, INotifyDataErrorInfoAsync
    {
        private AsyncEventHandler<AsyncPropertyChangedEventArgs> _changed;
        private AsyncEventHandler<AsyncPropertyChangingEventArgs> _changing;
        private AsyncEventHandler<AsyncCollectionChangedEventArgs> _collectionChanged;
        private AsyncEventHandler<AsyncErrorsChangedEventArgs> _errorsChanged;

        public event AsyncEvent<AsyncPropertyChangedEventArgs> PropertyChangedAsync
        {
            add => _changed.Register(value);
            remove => _changed.Unregister(value);
        }
        public event AsyncEvent<AsyncPropertyChangingEventArgs> PropertyChangingAsync
        {
            add => _changing.Register(value);
            remove => _changing.Unregister(value);
        }
        public event AsyncEvent<AsyncCollectionChangedEventArgs> CollectionChangedAsync
        {
            add => _collectionChanged.Register(value);
            remove => _collectionChanged.Unregister(value);
        }
        public event AsyncEvent<AsyncErrorsChangedEventArgs> ErrorsChangedAsync
        {
            add => _errorsChanged.Register(value);
            remove => _errorsChanged.Unregister(value);
        }
        public bool HasErrors => false;
        public IEnumerable GetErrors(string? propertyName) => Array.Empty<object>();
    }

    [Fact]
    public void Model_ImplementsAllFourInterfaces()
    {
        var m = new Model();
        Assert.IsAssignableFrom<INotifyPropertyChangedAsync>(m);
        Assert.IsAssignableFrom<INotifyPropertyChangingAsync>(m);
        Assert.IsAssignableFrom<INotifyCollectionChangedAsync>(m);
        Assert.IsAssignableFrom<INotifyDataErrorInfoAsync>(m);
    }
}
