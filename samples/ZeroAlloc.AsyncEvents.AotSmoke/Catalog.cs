using System.Threading;
using System.Threading.Tasks;
using ZeroAlloc.AsyncEvents;

namespace ZeroAlloc.AsyncEvents.AotSmoke;

/// <summary>A generic class nested in another type, whose events use its type parameter.</summary>
public static partial class Catalog
{
    public partial class Feed<TItem>
    {
        [AsyncEvent(InvokeMode.Sequential)]
        private AsyncEventHandler<TItem> _published;

        public ValueTask PublishAsync(TItem item, CancellationToken ct)
            => _published.InvokeAsync(item, ct);
    }
}
