using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ZeroAlloc.AsyncEvents.Generator.Tests;

/// <summary>
/// Every class with async events gets its own generated file, named after its namespace, its
/// containing types and its generic arity, so two classes never share a file or a dedupe key.
/// </summary>
public class HintNameTests
{
    [Fact]
    public void NamespaceAndTypeNamesWithUnderscores_DoNotCollide()
    {
        // Before: both were named A_B_C.AsyncEvents.g.cs, AddSource threw, and the generator
        // produced nothing for the whole project (CS8785).
        var run = Run("""
            using ZeroAlloc.AsyncEvents;
            namespace A_B { public partial class C { [AsyncEvent] private AsyncEventHandler<string> _x; } }
            namespace A { public partial class B_C { [AsyncEvent] private AsyncEventHandler<string> _y; } }
            """);

        Assert.Null(run.Exception);
        Assert.Equal(
            new[] { "A.B_C.AsyncEvents.g.cs", "A_B.C.AsyncEvents.g.cs" },
            run.HintNames.OrderBy(n => n, StringComparer.Ordinal),
            StringComparer.Ordinal);
        Assert.Empty(run.CompileErrors);
    }

    [Fact]
    public void TopLevelAndNestedTypeWithTheSameName_AreNotMerged()
    {
        // Before: both were keyed N:Foo, so the nested class was dropped without any diagnostic.
        var run = Run("""
            using ZeroAlloc.AsyncEvents;
            namespace N
            {
                public partial class Foo { [AsyncEvent] private AsyncEventHandler<string> _x; }
                public partial class Outer
                {
                    public partial class Foo { [AsyncEvent] private AsyncEventHandler<string> _y; }
                }
            }
            """);

        Assert.Null(run.Exception);
        Assert.Equal(
            new[] { "N.Foo.AsyncEvents.g.cs", "N.Outer+Foo.AsyncEvents.g.cs" },
            run.HintNames.OrderBy(n => n, StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    [Fact]
    public void NestedTypesWithTheSameNameInDifferentContainers_AreNotMerged()
    {
        var run = Run("""
            using ZeroAlloc.AsyncEvents;
            namespace N
            {
                public partial class A { public partial class Foo { [AsyncEvent] private AsyncEventHandler<string> _x; } }
                public partial class B { public partial class Foo { [AsyncEvent] private AsyncEventHandler<string> _y; } }
            }
            """);

        Assert.Null(run.Exception);
        Assert.Equal(
            new[] { "N.A+Foo.AsyncEvents.g.cs", "N.B+Foo.AsyncEvents.g.cs" },
            run.HintNames.OrderBy(n => n, StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    [Fact]
    public void GenericAndNonGenericTypeWithTheSameName_AreNotMerged()
    {
        var run = Run("""
            using ZeroAlloc.AsyncEvents;
            namespace N
            {
                public partial class Foo { [AsyncEvent] private AsyncEventHandler<string> _x; }
                public partial class Foo<T> { [AsyncEvent] private AsyncEventHandler<string> _y; }
                public partial class Foo<T1, T2> { [AsyncEvent] private AsyncEventHandler<string> _z; }
            }
            """);

        Assert.Null(run.Exception);
        Assert.Equal(
            new[] { "N.Foo.AsyncEvents.g.cs", "N.Foo`1.AsyncEvents.g.cs", "N.Foo`2.AsyncEvents.g.cs" },
            run.HintNames.OrderBy(n => n, StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    [Fact]
    public void ClassAndFieldAttributesOnOneClass_StillProduceOneFile()
    {
        var run = Run("""
            using ZeroAlloc.AsyncEvents;
            namespace N
            {
                [AsyncEvent]
                public partial class Foo
                {
                    [AsyncEvent] private AsyncEventHandler<string> _x;
                    [AsyncEvent] private AsyncEventHandler<int> _y;
                }
            }
            """);

        Assert.Null(run.Exception);
        Assert.Equal(new[] { "N.Foo.AsyncEvents.g.cs" }, run.HintNames, StringComparer.Ordinal);
        Assert.Empty(run.CompileErrors);
    }

    [Fact]
    public void GlobalNamespace_HasNoNamespacePart()
    {
        var run = Run("""
            using ZeroAlloc.AsyncEvents;
            public partial class Foo { [AsyncEvent] private AsyncEventHandler<string> _x; }
            """);

        Assert.Equal(new[] { "Foo.AsyncEvents.g.cs" }, run.HintNames, StringComparer.Ordinal);
    }

    [Fact]
    public void NestedNamespace_IsJoinedWithDots_AndVerbatimNamesLoseTheirAt()
    {
        var run = Run("""
            using ZeroAlloc.AsyncEvents;
            namespace Café.@event;
            public partial class Ωmega { [AsyncEvent] private AsyncEventHandler<string> _x; }
            """);

        // Before: the hint name held the '@', AddSource threw, and nothing was generated.
        Assert.Null(run.Exception);
        Assert.Equal(new[] { "Café.event.Ωmega.AsyncEvents.g.cs" }, run.HintNames, StringComparer.Ordinal);
        Assert.Empty(run.CompileErrors);
    }

    /// <summary>
    /// A C# identifier cannot hold a character that is invalid in a hint name, and the
    /// compiler drops formatting characters from it, so escaping guards names the generator
    /// is handed rather than names users write. The escape starts with '-', which no
    /// identifier contains, so an escaped name cannot collide with one that needed none.
    /// </summary>
    [Theory]
    [InlineData("M", "M")]
    [InlineData("App.M", "App.M")]
    [InlineData("App.Outer+M`1", "App.Outer+M`1")]
    [InlineData("Café.Ωmega_1", "Café.Ωmega_1")]
    [InlineData("a/b|c:d*e?f<g>h", "a-u002Fb-u007Cc-u003Ad-u002Ae-u003Ff-u003Cg-u003Eh")]
    [InlineData("a-b", "a-u002Db")]
    public void Sanitize_KeepsIdentifierCharactersAndEscapesTheRest(string name, string expected)
    {
        Assert.Equal(expected, HintNames.Sanitize(name));
    }

    [Fact]
    public void Sanitize_EscapesControlAndSeparatorCharacters_AndKeepsAstralLetters()
    {
        var backslash = ((char)92).ToString();
        var quote = ((char)34).ToString();
        var tab = ((char)9).ToString();
        var mathBoldA = char.ConvertFromUtf32(0x1D400);

        Assert.Equal("a-u005Cb", HintNames.Sanitize("a" + backslash + "b"));
        Assert.Equal("a-u0022b", HintNames.Sanitize("a" + quote + "b"));
        Assert.Equal("a-u0009b", HintNames.Sanitize("a" + tab + "b"));
        Assert.Equal(mathBoldA + "x", HintNames.Sanitize(mathBoldA + "x"));
        Assert.Equal("-uD835x", HintNames.Sanitize(mathBoldA.Substring(0, 1) + "x"));
    }

    private static RunResult Run(string source)
    {
        var refs = new List<MetadataReference>(Basic.Reference.Assemblies.Net90.References.All);
        refs.Add(MetadataReference.CreateFromFile(
            typeof(ZeroAlloc.AsyncEvents.AsyncEventHandler<>).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new AsyncEventGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        // The driver runs exactly one generator.
        var result = driver.GetRunResult().Results[0];

        return new RunResult(
            result.Exception,
            result.GeneratedSources.Select(s => s.HintName).ToArray(),
            output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray());
    }

    private sealed record RunResult(
        Exception? Exception,
        IReadOnlyList<string> HintNames,
        IReadOnlyList<Diagnostic> CompileErrors);
}
