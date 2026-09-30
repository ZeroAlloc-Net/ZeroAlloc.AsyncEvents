using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.AsyncEvents.Generator.Tests;

/// <summary>
/// A nested or generic class gets its events generated into the class itself: the generated file
/// reopens every containing type as partial, outermost first, then the class with its type
/// parameters. Each case compiles code that subscribes to the events on the real class.
/// </summary>
public class NestedClassTests
{
    [Fact]
    public void SameNamedNestedClassesInDifferentContainers_EachGetTheirOwnEvents()
    {
        // Before: both went to a stray top-level N.Foo, which had neither field: CS0103.
        var run = GeneratorRunner.Run("""
            using ZeroAlloc.AsyncEvents;
            namespace N
            {
                public partial class A { public partial class Foo { [AsyncEvent] private AsyncEventHandler<string> _x; } }
                public partial class B { public partial class Foo { [AsyncEvent] private AsyncEventHandler<int> _y; } }

                public static class Use
                {
                    public static void Run()
                    {
                        new A.Foo().X += static (s, ct) => default;
                        new B.Foo().Y += static (i, ct) => default;
                    }
                }
            }
            """);

        AssertCompiles(run);
        Assert.Equal(
            new[] { "N.A+Foo.AsyncEvents.g.cs", "N.B+Foo.AsyncEvents.g.cs" },
            run.HintNames.OrderBy(n => n, StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    [Fact]
    public void NestedClassNextToATopLevelClassWithTheSameField_DoesNotMixTheirEvents()
    {
        // Before: the nested class's event went into the top-level N.Foo, which compiled because
        // it had a field of the same name, so N.Foo got the event and N.Outer.Foo had none.
        var run = GeneratorRunner.Run("""
            using ZeroAlloc.AsyncEvents;
            namespace N
            {
                public partial class Foo { [AsyncEvent] private AsyncEventHandler<string> _x; }
                public partial class Outer
                {
                    public partial class Foo { [AsyncEvent] private AsyncEventHandler<string> _x; [AsyncEvent] private AsyncEventHandler<int> _y; }
                }

                public static class Use
                {
                    public static void Run()
                    {
                        new Foo().X += static (s, ct) => default;
                        new Outer.Foo().X += static (s, ct) => default;
                        new Outer.Foo().Y += static (i, ct) => default;
                    }
                }
            }
            """);

        AssertCompiles(run);
    }

    [Fact]
    public void GenericClassesOfArityOneAndTwo_WithConstraints_GetTheirEvents()
    {
        // Before: the events of Bar<T> went to a new non-generic class Bar: CS0103.
        var run = GeneratorRunner.Run("""
            using System.Collections.Generic;
            using ZeroAlloc.AsyncEvents;
            namespace N
            {
                public partial class Bar<T> where T : class, new()
                {
                    [AsyncEvent] private AsyncEventHandler<T> _item;
                    [AsyncEvent] private AsyncEventHandler<List<T>> _batch;
                }

                public partial class Pair<TKey, TValue> where TKey : notnull where TValue : struct
                {
                    [AsyncEvent] private AsyncEventHandler<KeyValuePair<TKey, TValue>> _entry;
                }

                public static class Use
                {
                    public static void Run()
                    {
                        new Bar<object>().Item += static (o, ct) => default;
                        new Bar<object>().Batch += static (l, ct) => default;
                        new Pair<string, int>().Entry += static (kv, ct) => default;
                    }
                }
            }
            """);

        AssertCompiles(run);
    }

    [Fact]
    public void ClassNestedInGenericContainers_UsesTheirTypeParameters()
    {
        var run = GeneratorRunner.Run("""
            using ZeroAlloc.AsyncEvents;
            namespace N
            {
                public partial class Outer<T>
                {
                    public partial class Middle<U> where U : struct
                    {
                        public partial class Inner<V>
                        {
                            [AsyncEvent] private AsyncEventHandler<(T, U, V)> _changed;
                        }
                    }
                }

                public static class Use
                {
                    public static void Run()
                        => new Outer<string>.Middle<int>.Inner<byte>().Changed += static (t, ct) => default;
                }
            }
            """);

        AssertCompiles(run);
        Assert.Equal(
            """
            // <auto-generated/>
            #nullable enable

            namespace N;

            partial class Outer<T>
            {
                partial class Middle<U>
                {
                    partial class Inner<V>
                    {
                        #pragma warning disable MA0046
                        public event global::ZeroAlloc.AsyncEvents.AsyncEvent<(T, U, V)> Changed
                        {
                            add    => _changed.Register(value);
                            remove => _changed.Unregister(value);
                        }
                        #pragma warning restore MA0046

                    }
                }
            }
            """.ReplaceLineEndings("\n"),
            run.Sources["N.Outer`1+Middle`1+Inner`1.AsyncEvents.g.cs"].ReplaceLineEndings("\n"));
    }

    [Theory]
    [InlineData("public partial struct Holder<T> where T : unmanaged", "Holder<int>")]
    [InlineData("public partial interface Holder", "Holder")]
    [InlineData("public partial record struct Holder(int Id)", "Holder")]
    [InlineData("public partial record Holder(string Name)", "Holder")]
    [InlineData("public readonly ref partial struct Holder", "Holder")]
    [InlineData("public abstract partial class Holder", "Holder")]
    [InlineData("public static partial class Holder", "Holder")]
    public void ContainersOfEveryKind_AreReopenedWithTheirKind(string container, string use)
    {
        var run = GeneratorRunner.Run($$"""
            using ZeroAlloc.AsyncEvents;
            namespace N
            {
                {{container}}
                {
                    public partial class Foo { [AsyncEvent] private AsyncEventHandler<string> _x; }
                }

                public static class Use
                {
                    public static void Run() => new {{use}}.Foo().X += static (s, ct) => default;
                }
            }
            """);

        AssertCompiles(run);
    }

    [Fact]
    public void VerbatimNames_AreWrittenAsVerbatimIdentifiers()
    {
        var run = GeneratorRunner.Run("""
            using ZeroAlloc.AsyncEvents;
            namespace N
            {
                public partial class @class<@int>
                {
                    public partial class @static<@void> { [AsyncEvent] private AsyncEventHandler<@int> _x; }
                }

                public static class Use
                {
                    public static void Run() => new @class<string>.@static<long>().X += static (s, ct) => default;
                }
            }
            """);

        AssertCompiles(run);
    }

    [Fact]
    public void ContainerDeclaredInSeveralParts_IsReopenedOnce()
    {
        var run = GeneratorRunner.Run(
            """
            namespace N { public partial class Outer { public int A; } }
            """,
            """
            using ZeroAlloc.AsyncEvents;
            namespace N
            {
                partial class Outer
                {
                    public partial class Foo { [AsyncEvent] private AsyncEventHandler<string> _x; }
                }

                public static class Use
                {
                    public static void Run() => new Outer.Foo().X += static (s, ct) => default;
                }
            }
            """);

        AssertCompiles(run);
    }

    [Fact]
    public void ClassInTheGlobalNamespace_IsNestedWithoutANamespace()
    {
        var run = GeneratorRunner.Run("""
            using ZeroAlloc.AsyncEvents;
            public partial class Outer
            {
                private partial class Foo { [AsyncEvent] private AsyncEventHandler<string> _x; }

                public static void Run() => new Foo().X += static (s, ct) => default;
            }
            """);

        AssertCompiles(run);
    }

    [Fact]
    public void ContainingTypeNotPartial_ReportsZAAE001_AndGeneratesNothingForIt()
    {
        var source = """
            using ZeroAlloc.AsyncEvents;
            namespace N
            {
                public class Outer
                {
                    public partial class Foo { [AsyncEvent] private AsyncEventHandler<string> _x; }
                }

                public partial class Other { [AsyncEvent] private AsyncEventHandler<string> _y; }
            }
            """;
        var run = GeneratorRunner.Run(source);

        Assert.Null(run.Exception);
        Assert.Empty(run.CompileErrors);
        Assert.Equal(new[] { "N.Other.AsyncEvents.g.cs" }, run.HintNames, StringComparer.Ordinal);

        var diagnostic = run.OnlyDiagnostic();
        Assert.Equal("ZAAE001", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(
            "The async events of class 'N.Outer.Foo' are not generated because its containing type 'N.Outer' is not partial",
            diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture));
        AssertLocatedOn(diagnostic, source, "Foo {");
    }

    [Fact]
    public void NonPartialOuterTypeAbovePartialMiddleType_ReportsZAAE001_NamingTheOuterType()
    {
        var source = """
            using ZeroAlloc.AsyncEvents;
            namespace N
            {
                public class Outer
                {
                    public partial class Middle
                    {
                        public partial class Foo { [AsyncEvent] private AsyncEventHandler<string> _x; }
                    }
                }
            }
            """;
        var run = GeneratorRunner.Run(source);

        Assert.Null(run.Exception);
        Assert.Empty(run.CompileErrors);
        Assert.Empty(run.HintNames);

        var diagnostic = run.OnlyDiagnostic();
        Assert.Equal("ZAAE001", diagnostic.Id);
        Assert.Equal(
            "The async events of class 'N.Outer.Middle.Foo' are not generated because its containing type 'N.Outer' is not partial",
            diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture));
        AssertLocatedOn(diagnostic, source, "Foo {");
    }

    [Fact]
    public void NonPartialContainer_WithoutAsyncEventFields_ReportsNothing()
    {
        var run = GeneratorRunner.Run("""
            using ZeroAlloc.AsyncEvents;
            namespace N
            {
                public class Outer
                {
                    [AsyncEvent] public partial class Foo { private int _x; }
                }
            }
            """);

        Assert.Empty(run.GeneratorDiagnostics);
        Assert.Empty(run.HintNames);
    }

    [Fact]
    public void FileLocalClass_ReportsZAAE002_AndOtherClassesAreStillGenerated()
    {
        var source = """
            using ZeroAlloc.AsyncEvents;
            namespace N
            {
                file partial class Foo { [AsyncEvent] private AsyncEventHandler<string> _x; }

                public partial class Other { [AsyncEvent] private AsyncEventHandler<string> _y; }
            }
            """;
        var run = GeneratorRunner.Run(source);

        Assert.Null(run.Exception);
        Assert.Equal(new[] { "N.Other.AsyncEvents.g.cs" }, run.HintNames, StringComparer.Ordinal);

        var diagnostic = run.OnlyDiagnostic();
        Assert.Equal("ZAAE002", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(
            "The async events of class 'N.Foo' are not generated because it is file-local or nested in a file-local type, and a generated file cannot extend a file-local type",
            diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture));
        AssertLocatedOn(diagnostic, source, "Foo {");
        Assert.Empty(run.CompileErrors);
    }

    [Fact]
    public void ClassNestedInAFileLocalType_ReportsZAAE002()
    {
        var run = GeneratorRunner.Run("""
            using ZeroAlloc.AsyncEvents;
            namespace N
            {
                file partial class Outer
                {
                    public partial class Foo { [AsyncEvent] private AsyncEventHandler<string> _x; }
                }
            }
            """);

        Assert.Null(run.Exception);
        Assert.Empty(run.HintNames);
        var diagnostic = run.OnlyDiagnostic();
        Assert.Equal("ZAAE002", diagnostic.Id);
    }

    private static void AssertCompiles(RunResult run)
    {
        Assert.Null(run.Exception);
        Assert.Empty(run.GeneratorDiagnostics);
        Assert.Empty(run.CompileErrors);
    }

    private static void AssertLocatedOn(Diagnostic diagnostic, string source, string marker)
    {
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"marker '{marker}' not found");
        var name = marker.Split(' ')[0];
        Assert.Equal(start, diagnostic.Location.SourceSpan.Start);
        Assert.Equal(name.Length, diagnostic.Location.SourceSpan.Length);
        Assert.True(diagnostic.Location.IsInSource);
        Assert.Equal("File0.cs", diagnostic.Location.GetLineSpan().Path);
    }
}
