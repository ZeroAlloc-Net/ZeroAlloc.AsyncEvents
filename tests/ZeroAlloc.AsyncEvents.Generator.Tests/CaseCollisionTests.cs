using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.AsyncEvents.Generator.Tests;

/// <summary>
/// Roslyn compares hint names ignoring case. Classes whose qualified names differ only in case
/// would need the same file, so the class declared first keeps it, and every later one gets
/// ZAAE003 and is skipped. Everything else is generated.
/// </summary>
public class CaseCollisionTests
{
    [Fact]
    public void NamesDifferingOnlyInCase_ReportZAAE003OnTheLaterClass_AndGenerateTheRest()
    {
        // Before: AddSource threw, CS8785 was reported, and no class in the project got events.
        var source = """
            using ZeroAlloc.AsyncEvents;
            namespace N
            {
                public partial class Foo { [AsyncEvent] private AsyncEventHandler<string> _x; }
                public partial class foo { [AsyncEvent] private AsyncEventHandler<string> _y; }
                public partial class Other { [AsyncEvent] private AsyncEventHandler<string> _z; }

                public static class Use
                {
                    public static void Run()
                    {
                        new Foo().X += static (s, ct) => default;
                        new Other().Z += static (s, ct) => default;
                    }
                }
            }
            """;
        var run = GeneratorRunner.Run(source);

        Assert.Null(run.Exception);
        Assert.Empty(run.CompileErrors);
        Assert.Equal(
            new[] { "N.Foo.AsyncEvents.g.cs", "N.Other.AsyncEvents.g.cs" },
            run.HintNames.OrderBy(n => n, StringComparer.Ordinal),
            StringComparer.Ordinal);

        var diagnostic = run.OnlyDiagnostic();
        Assert.Equal("ZAAE003", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(
            "The async events of class 'N.foo' are not generated because its file name 'N.foo.AsyncEvents.g.cs' differs only in case from that of class 'N.Foo'",
            diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture));
        var start = source.IndexOf("foo {", StringComparison.Ordinal);
        Assert.Equal(start, diagnostic.Location.SourceSpan.Start);
        Assert.Equal(3, diagnostic.Location.SourceSpan.Length);
    }

    [Fact]
    public void ThreeClassesDifferingOnlyInCase_ReportTwoAndGenerateOne()
    {
        var run = GeneratorRunner.Run("""
            using ZeroAlloc.AsyncEvents;
            public partial class App { [AsyncEvent] private AsyncEventHandler<string> _x; }
            public partial class APP { [AsyncEvent] private AsyncEventHandler<string> _x; }
            public partial class app { [AsyncEvent] private AsyncEventHandler<string> _x; }
            """);

        Assert.Null(run.Exception);
        Assert.Empty(run.CompileErrors);
        Assert.Equal(new[] { "App.AsyncEvents.g.cs" }, run.HintNames, StringComparer.Ordinal);
        Assert.Equal(new[] { "ZAAE003", "ZAAE003" }, run.GeneratorDiagnostics.Select(d => d.Id), StringComparer.Ordinal);
        Assert.DoesNotContain(run.GeneratorDiagnostics, d => string.Equals(d.Id, "CS8785", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AcrossFiles_TheClassInTheEarlierFileWins_WhateverTheCompilationOrder(bool reversed)
    {
        var a = ("A.cs", """
            using ZeroAlloc.AsyncEvents;
            namespace N { public partial class foo { [AsyncEvent] private AsyncEventHandler<string> _x; } }
            """);
        var b = ("B.cs", """
            using ZeroAlloc.AsyncEvents;
            namespace N { public partial class Foo { [AsyncEvent] private AsyncEventHandler<string> _x; } }
            """);

        var run = reversed ? GeneratorRunner.Run(b, a) : GeneratorRunner.Run(a, b);

        Assert.Null(run.Exception);
        Assert.Equal(new[] { "N.foo.AsyncEvents.g.cs" }, run.HintNames, StringComparer.Ordinal);
        var diagnostic = run.OnlyDiagnostic();
        Assert.Equal("ZAAE003", diagnostic.Id);
        Assert.Equal("B.cs", diagnostic.Location.GetLineSpan().Path);
    }

    [Fact]
    public void ClassSkippedForAnotherReason_DoesNotTakeTheName()
    {
        // A file-local class generates nothing, so it holds no file name the other class could clash with.
        var run = GeneratorRunner.Run("""
            using ZeroAlloc.AsyncEvents;
            namespace N
            {
                file partial class Foo { [AsyncEvent] private AsyncEventHandler<string> _x; }
                public partial class foo { [AsyncEvent] private AsyncEventHandler<string> _y; }
            }
            """);

        Assert.Null(run.Exception);
        Assert.Equal(new[] { "N.foo.AsyncEvents.g.cs" }, run.HintNames, StringComparer.Ordinal);
        Assert.Equal(new[] { "ZAAE002" }, run.GeneratorDiagnostics.Select(d => d.Id), StringComparer.Ordinal);
    }
}
