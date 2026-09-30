using System.Collections.Generic;

namespace ZeroAlloc.AsyncEvents.Generator.Models;

/// <param name="QualifiedName">
/// The type's namespace, containing types and arity, unique within the compilation. It keys
/// deduplication and names the generated file.
/// </param>
internal sealed record AsyncEventClassModel(
    string QualifiedName,
    string? Namespace,
    string TypeName,
    IReadOnlyList<AsyncEventFieldModel> Fields);
