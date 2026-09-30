using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.AsyncEvents.Generator;

/// <summary>A diagnostic as values, so the model that carries it keeps comparing by value.</summary>
internal sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    LocationInfo? Location,
    EquatableArray<string> Arguments)
{
    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, LocationInfo? location, params string[] arguments)
        => new(descriptor, location, new EquatableArray<string>(ImmutableArray.Create(arguments)));

    public Diagnostic ToDiagnostic() =>
        Diagnostic.Create(
            Descriptor,
            Location?.ToLocation() ?? Microsoft.CodeAnalysis.Location.None,
            Arguments.Cast<object>().ToArray());

    // Descriptors are static singletons; comparing their IDs keeps equality independent of that.
    public bool Equals(DiagnosticInfo? other) =>
        other is not null &&
        string.Equals(Descriptor.Id, other.Descriptor.Id, StringComparison.Ordinal) &&
        Equals(Location, other.Location) &&
        Arguments.Equals(other.Arguments);

    public override int GetHashCode() =>
        unchecked((StringComparer.Ordinal.GetHashCode(Descriptor.Id) * 397) ^ Arguments.GetHashCode());
}
