using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ZeroAlloc.AsyncEvents.Generator;

/// <summary>
/// The partial declarations a class's events are generated into: those of its containing types,
/// outermost first, then the class itself. A port of ZeroAlloc.Mapping's <c>HostDeclarations</c>.
/// </summary>
internal static class TypeDeclarations
{
    /// <summary>
    /// The headers of the partial declarations, for example <c>partial record struct Orders&lt;T&gt;</c>.
    /// Each carries the kind and the type parameter names. Accessibility is left out, as it always
    /// was for the class itself, and so are constraints: partial parts may omit both.
    /// </summary>
    public static EquatableArray<string> Headers(INamedTypeSymbol type)
    {
        var chain = new List<INamedTypeSymbol>();
        for (var t = type; t is not null; t = t.ContainingType) chain.Add(t);

        // Outermost first.
        var headers = ImmutableArray.CreateBuilder<string>(chain.Count);
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var t = chain[i];
            var sb = new StringBuilder();
            if (t.IsRefLikeType) sb.Append("ref ");
            sb.Append("partial ").Append(Keyword(t)).Append(' ').Append(Identifier(t.Name));
            if (t.TypeParameters.Length > 0)
            {
                sb.Append('<');
                sb.Append(string.Join(", ", t.TypeParameters.Select(static p => Identifier(p.Name))));
                sb.Append('>');
            }
            headers.Add(sb.ToString());
        }
        return new EquatableArray<string>(headers.MoveToImmutable());
    }

    /// <summary>
    /// True when the type or one of its containing types is file-local. Only a top-level type can
    /// be declared <c>file</c>, but everything nested in it is file-local too.
    /// </summary>
    public static bool IsFileLocalOrNestedInOne(INamedTypeSymbol type)
    {
        for (var t = type; t is not null; t = t.ContainingType)
        {
            if (t.IsFileLocal) return true;
        }
        return false;
    }

    /// <summary>
    /// The outermost containing type that is not declared <c>partial</c>, or null when all of
    /// them are. The generated file reopens every containing type, which only a partial type allows.
    /// </summary>
    public static INamedTypeSymbol? FirstNonPartialContainingType(INamedTypeSymbol type, CancellationToken ct)
    {
        INamedTypeSymbol? outermost = null;
        for (var t = type.ContainingType; t is not null; t = t.ContainingType)
        {
            if (!IsPartial(t, ct)) outermost = t;
        }
        return outermost;
    }

    private static bool IsPartial(INamedTypeSymbol type, CancellationToken ct) =>
        type.DeclaringSyntaxReferences.Any(r =>
            r.GetSyntax(ct) is TypeDeclarationSyntax declaration &&
            declaration.Modifiers.Any(static m => m.IsKind(SyntaxKind.PartialKeyword)));

    private static string Keyword(INamedTypeSymbol type) => type switch
    {
        { IsRecord: true, TypeKind: TypeKind.Struct } => "record struct",
        { IsRecord: true } => "record",
        { TypeKind: TypeKind.Struct } => "struct",
        { TypeKind: TypeKind.Interface } => "interface",
        _ => "class",
    };

    private static string Identifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;
}
