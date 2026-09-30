using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ZeroAlloc.AsyncEvents.Generator.Tests;

/// <summary>
/// The model of a class compares by value, including its diagnostic, so an edit to another
/// class leaves it unchanged and its output is not rebuilt.
/// </summary>
public class IncrementalCachingTests
{
    private const string Header = "using ZeroAlloc.AsyncEvents;\nnamespace N;\n";

    [Theory]
    [InlineData("public partial class Foo<T> { [AsyncEvent] private AsyncEventHandler<T> _x; }")]
    // ZAAE001.
    [InlineData("public class Outer { public partial class Foo { [AsyncEvent] private AsyncEventHandler<string> _x; } }")]
    // ZAAE003 against N.Foo in Other.cs, which sorts first, attached anew on every run.
    [InlineData("public partial class foo { [AsyncEvent] private AsyncEventHandler<string> _x; }")]
    public void EditToAnotherClass_LeavesTheModelUnchanged(string declaration)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var refs = new List<MetadataReference>(Basic.Reference.Assemblies.Net90.References.All)
        {
            MetadataReference.CreateFromFile(typeof(ZeroAlloc.AsyncEvents.AsyncEventHandler<>).Assembly.Location),
        };
        var other = CSharpSyntaxTree.ParseText(OtherClass("string"), parseOptions, "Other.cs");
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(Header + declaration, parseOptions, "Z.cs"), other },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new AsyncEventGenerator().AsSourceGenerator() },
            parseOptions: parseOptions,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
        driver = driver.RunGenerators(compilation);

        // Editing the other class reruns the step over every class.
        var edited = compilation.ReplaceSyntaxTree(
            other, CSharpSyntaxTree.ParseText(OtherClass("int"), parseOptions, "Other.cs"));
        driver = driver.RunGenerators(edited);

        var outputs = driver.GetRunResult().Results[0]
            .TrackedSteps[AsyncEventGenerator.ModelsTrackingName]
            .SelectMany(step => step.Outputs)
            .ToArray();

        // In discovery order: the class under test, then the other class, which changed.
        Assert.Equal(2, outputs.Length);
        Assert.Equal(IncrementalStepRunReason.Unchanged, outputs[0].Reason);
        Assert.Equal(IncrementalStepRunReason.Modified, outputs[1].Reason);
    }

    private static string OtherClass(string argType) =>
        Header + "public partial class Foo { [AsyncEvent] private AsyncEventHandler<" + argType + "> _y; }";
}
