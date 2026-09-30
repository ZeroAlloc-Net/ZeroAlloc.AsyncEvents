using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ZeroAlloc.AsyncEvents.Generator.Tests;

/// <summary>Runs <see cref="AsyncEventGenerator"/> over sources and compiles the result.</summary>
internal static class GeneratorRunner
{
    public static RunResult Run(params string[] sources)
        => Run(sources.Select((s, i) => ($"File{i}.cs", s)).ToArray());

    public static RunResult Run(params (string Path, string Source)[] files)
    {
        var refs = new List<MetadataReference>(Basic.Reference.Assemblies.Net90.References.All);
        refs.Add(MetadataReference.CreateFromFile(
            typeof(ZeroAlloc.AsyncEvents.AsyncEventHandler<>).Assembly.Location));

        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            files.Select(f => CSharpSyntaxTree.ParseText(f.Source, parseOptions, f.Path)),
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(
                new[] { new AsyncEventGenerator().AsSourceGenerator() },
                parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        // The driver runs exactly one generator.
        var result = driver.GetRunResult().Results[0];

        return new RunResult(
            result.Exception,
            result.GeneratedSources.ToDictionary(s => s.HintName, s => s.SourceText.ToString(), StringComparer.Ordinal),
            result.Diagnostics,
            output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray());
    }
}
