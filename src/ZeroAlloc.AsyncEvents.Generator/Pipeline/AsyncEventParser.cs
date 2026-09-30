using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ZeroAlloc.AsyncEvents.Generator.Models;

namespace ZeroAlloc.AsyncEvents.Generator.Pipeline;

internal static class AsyncEventParser
{
    private const string AsyncEventAttrFqn    = "ZeroAlloc.AsyncEvents.AsyncEventAttribute";
    private const string AsyncEventHandlerFqn = "ZeroAlloc.AsyncEvents.AsyncEventHandler<TArgs>";

    public static bool IsCandidate(SyntaxNode node, CancellationToken _)
    {
        // Class-level attribute: [AsyncEvent] on the class itself
        if (node is ClassDeclarationSyntax cls)
            return cls.Modifiers.Any(m => string.Equals(m.ValueText, "partial", StringComparison.Ordinal));

        // Field-level attribute: [AsyncEvent] on a field inside a partial class
        if (node is VariableDeclaratorSyntax)
        {
            var fieldDecl = node.Parent?.Parent; // VariableDeclaratorSyntax -> VariableDeclarationSyntax -> FieldDeclarationSyntax
            var classDecl = fieldDecl?.Parent;   // FieldDeclarationSyntax -> ClassDeclarationSyntax
            return classDecl is ClassDeclarationSyntax pc &&
                   pc.Modifiers.Any(m => string.Equals(m.ValueText, "partial", StringComparison.Ordinal));
        }

        return false;
    }

    public static AsyncEventClassModel? Parse(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        // Resolve the containing class regardless of whether attr is on class or field
        INamedTypeSymbol? type = ctx.TargetSymbol switch
        {
            INamedTypeSymbol t => t,
            IFieldSymbol f     => f.ContainingType,
            _                  => null
        };

        if (type is null) return null;

        var ns = type.ContainingNamespace.IsGlobalNamespace
            ? null
            : type.ContainingNamespace.ToDisplayString();

        var classHasAttr = HasAttr(type.GetAttributes(), AsyncEventAttrFqn);

        var fields = new List<AsyncEventFieldModel>();
        foreach (var member in type.GetMembers())
        {
            if (member is not IFieldSymbol field) continue;
            if (field.Type is not INamedTypeSymbol fieldType) continue;

            // Must be AsyncEventHandler<TArgs>
            if (!string.Equals(
                    fieldType.OriginalDefinition.ToDisplayString(),
                    AsyncEventHandlerFqn,
                    StringComparison.Ordinal)) continue;

            // Include if field has [AsyncEvent] OR class has [AsyncEvent]
            var fieldHasAttr = HasAttr(field.GetAttributes(), AsyncEventAttrFqn);
            if (!fieldHasAttr && !classHasAttr) continue;

            if (fieldType.TypeArguments.Length != 1) continue;

            var argTypeFqn = fieldType.TypeArguments[0]
                .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var eventName = ToEventName(field.Name);

            fields.Add(new AsyncEventFieldModel(field.Name, eventName, argTypeFqn));
        }

        if (fields.Count == 0) return null;

        var displayName = type.ToDisplayString();
        var location = FirstLocation(type);
        return new AsyncEventClassModel(
            HintNames.QualifiedName(type),
            displayName,
            location,
            ns,
            TypeDeclarations.Headers(type),
            new EquatableArray<AsyncEventFieldModel>(fields.ToImmutableArray()),
            Check(type, displayName, location, ct));
    }

    /// <summary>
    /// Why nothing can be generated for <paramref name="type"/>, or null when it can: ZAAE002 when
    /// it is file-local, otherwise ZAAE001 when a containing type is not partial.
    /// </summary>
    private static DiagnosticInfo? Check(
        INamedTypeSymbol type, string displayName, LocationInfo? location, CancellationToken ct)
    {
        if (TypeDeclarations.IsFileLocalOrNestedInOne(type))
            return DiagnosticInfo.Create(AsyncEventDiagnostics.FileLocalType, location, displayName);

        var nonPartial = TypeDeclarations.FirstNonPartialContainingType(type, ct);
        return nonPartial is null
            ? null
            : DiagnosticInfo.Create(
                AsyncEventDiagnostics.ContainingTypeNotPartial, location, displayName, nonPartial.ToDisplayString());
    }

    /// <summary>
    /// The name of the type's first declaration, by file path and then position, so a type
    /// declared in several parts is reported and ordered the same way on every run.
    /// </summary>
    private static LocationInfo? FirstLocation(INamedTypeSymbol type)
    {
        LocationInfo? first = null;
        foreach (var location in type.Locations)
        {
            var info = LocationInfo.From(location);
            if (info is null) continue;
            if (first is null || CompareLocations(info, first) < 0) first = info;
        }
        return first;
    }

    internal static int CompareLocations(LocationInfo? x, LocationInfo? y)
    {
        var byPath = string.CompareOrdinal(x?.FilePath, y?.FilePath);
        return byPath != 0 ? byPath : (x?.Span.Start ?? 0).CompareTo(y?.Span.Start ?? 0);
    }

    private static bool HasAttr(
        System.Collections.Immutable.ImmutableArray<AttributeData> attrs, string fqn)
    {
        foreach (var a in attrs)
            if (string.Equals(a.AttributeClass?.ToDisplayString(), fqn, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static string ToEventName(string fieldName)
    {
        var name = fieldName;
        if (name.StartsWith("m_", StringComparison.Ordinal)) name = name.Substring(2);
        else if (name.StartsWith("_", StringComparison.Ordinal)) name = name.Substring(1);
        if (name.Length == 0) return fieldName;
        return char.ToUpperInvariant(name[0]).ToString() + name.Substring(1);
    }
}
