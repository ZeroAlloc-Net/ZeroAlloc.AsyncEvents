using System.Threading;
using System.Threading.Tasks;

namespace ZeroAlloc.AsyncEvents;

public delegate ValueTask AsyncEvent(CancellationToken ct = default);
public delegate ValueTask AsyncEvent<TArgs>(TArgs args, CancellationToken ct = default);
