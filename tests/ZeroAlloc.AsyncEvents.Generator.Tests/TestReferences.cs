using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.AsyncEvents.Generator.Tests;

/// <summary>
/// The framework reference assemblies the generator tests compile against. They must be at least as
/// new as the ZeroAlloc.AsyncEvents build the test host loads, or the compilation fails with CS1705:
/// the net11.0 build references System.Runtime 11.
/// </summary>
internal static class TestReferences
{
    /// <summary>Gets the reference assemblies that match the test project's own target framework.</summary>
    public static ImmutableArray<PortableExecutableReference> All =>
#if NET11_0_OR_GREATER
        Basic.Reference.Assemblies.Net110.References.All;
#else
        Basic.Reference.Assemblies.Net90.References.All;
#endif
}
