using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace ZeroAlloc.AsyncEvents.Generator;

/// <summary>
/// A diagnostic location the pipeline can compare: the syntax tree and the span within it.
/// </summary>
/// <remarks>
/// The tree is kept, not just its file path, because the rebuilt diagnostic must be a source
/// location: <c>Location.Create(filePath, span, lineSpan)</c> gives an external-file location,
/// for which the compiler ignores <c>#pragma warning disable</c>. A tree compares by reference,
/// and a compilation reuses the tree of every file that did not change, so the location still
/// compares equal across runs until its own file is edited. See ZeroAlloc-Net/.github#46.
/// </remarks>
internal sealed record LocationInfo(SyntaxTree Tree, TextSpan Span)
{
    public string FilePath => Tree.FilePath;

    public static LocationInfo? From(Location location) =>
        location.SourceTree is { } tree ? new LocationInfo(tree, location.SourceSpan) : null;

    public Location ToLocation() => Location.Create(Tree, Span);
}
