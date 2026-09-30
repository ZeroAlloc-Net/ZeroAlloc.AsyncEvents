using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.AsyncEvents.Generator.Tests;

/// <summary>What one run of the generator produced, and the errors of the compilation with its output.</summary>
internal sealed record RunResult(
    Exception? Exception,
    IReadOnlyDictionary<string, string> Sources,
    IReadOnlyList<Diagnostic> GeneratorDiagnostics,
    IReadOnlyList<Diagnostic> CompileErrors)
{
    public IReadOnlyList<string> HintNames => Sources.Keys.ToArray();

    /// <summary>The generator's only diagnostic; fails when it reported none or several.</summary>
    public Diagnostic OnlyDiagnostic()
    {
        Assert.True(
            GeneratorDiagnostics.Count == 1,
            "Expected one generator diagnostic, got: " + string.Join("; ", GeneratorDiagnostics.Select(d => d.ToString())));
        return GeneratorDiagnostics[0];
    }
}
