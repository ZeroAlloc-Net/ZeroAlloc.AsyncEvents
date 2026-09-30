namespace ZeroAlloc.AsyncEvents.Generator.Models;

/// <param name="QualifiedName">
/// The type's namespace, containing types and arity, unique within the compilation. It keys
/// deduplication and names the generated file.
/// </param>
/// <param name="DisplayName">The type as diagnostics name it, for example <c>N.Outer.Foo&lt;T&gt;</c>.</param>
/// <param name="Location">The type's first declaration, by file path and then position.</param>
/// <param name="Declarations">
/// The headers of the partial declarations the events go into: the containing types, outermost
/// first, then the type itself.
/// </param>
/// <param name="Diagnostic">Why nothing is generated for the type, or null when it is generated.</param>
internal sealed record AsyncEventClassModel(
    string QualifiedName,
    string DisplayName,
    LocationInfo? Location,
    string? Namespace,
    EquatableArray<string> Declarations,
    EquatableArray<AsyncEventFieldModel> Fields,
    DiagnosticInfo? Diagnostic)
{
    public string HintName => HintNames.ForAsyncEvents(QualifiedName);
}
