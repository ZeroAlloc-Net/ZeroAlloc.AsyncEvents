using System.Collections.Generic;

namespace ZeroAlloc.AsyncEvents.Generator.Models;

internal sealed record AsyncEventClassModel(
    string? Namespace,
    string TypeName,
    IReadOnlyList<AsyncEventFieldModel> Fields);
