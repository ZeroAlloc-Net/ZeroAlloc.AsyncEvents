using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using ZeroAlloc.TestHelpers;

namespace ZeroAlloc.AsyncEvents.Generator.Tests;

public class GeneratorTests
{
    [Fact]
    public void FieldAttribute_Parallel_GeneratesEvent()
        => Verify("""
            using ZeroAlloc.AsyncEvents;
            public partial class MyService
            {
                [AsyncEvent(InvokeMode.Parallel)]
                private AsyncEventHandler<string> _orderPlaced;
            }
            """);

    // Generated output is structurally identical to the Parallel variant.
    // InvokeMode is a runtime property of AsyncEventHandler<T> (set on the field declaration),
    // not reflected in the generated add/remove accessors.
    [Fact]
    public void FieldAttribute_Sequential_GeneratesEvent()
        => Verify("""
            using ZeroAlloc.AsyncEvents;
            public partial class MyService
            {
                [AsyncEvent(InvokeMode.Sequential)]
                private AsyncEventHandler<string> _orderPlaced;
            }
            """);

    [Fact]
    public void ClassAttribute_AllHandlerFieldsGetEvents()
        => Verify("""
            using ZeroAlloc.AsyncEvents;
            [AsyncEvent(InvokeMode.Parallel)]
            public partial class MyService
            {
                private AsyncEventHandler<string> _orderPlaced;
                private AsyncEventHandler<int> _itemShipped;
            }
            """);

    // "Overrides" here refers to parser logic: the field-level [AsyncEvent] is respected even when
    // the class also has [AsyncEvent]. The generated accessor is structurally the same as other cases.
    [Fact]
    public void FieldAttribute_OverridesClassAttribute()
        => Verify("""
            using ZeroAlloc.AsyncEvents;
            [AsyncEvent(InvokeMode.Parallel)]
            public partial class MyService
            {
                [AsyncEvent(InvokeMode.Sequential)]
                private AsyncEventHandler<string> _orderPlaced;
            }
            """);

    [Fact]
    public void ClassAttribute_MixedModes()
        => Verify("""
            using ZeroAlloc.AsyncEvents;
            [AsyncEvent(InvokeMode.Parallel)]
            public partial class MyService
            {
                private AsyncEventHandler<string> _orderPlaced;
                [AsyncEvent(InvokeMode.Sequential)]
                private AsyncEventHandler<int> _itemShipped;
            }
            """);

    [Fact]
    public void NonPartialClass_NoOutput()
        => Verify("""
            using ZeroAlloc.AsyncEvents;
            public class MyService
            {
                [AsyncEvent(InvokeMode.Parallel)]
                private AsyncEventHandler<string> _orderPlaced;
            }
            """);

    // Verifies the parser guard: a field without [AsyncEvent] in a class without [AsyncEvent]
    // must not be included — guards against accidental removal of the attribute-presence check.
    [Fact]
    public void NoAttribute_NoOutput()
        => Verify("""
            using ZeroAlloc.AsyncEvents;
            public partial class MyService
            {
                private AsyncEventHandler<string> _orderPlaced;
            }
            """);

    private static void Verify(string source)
    {
        var compilation = CreateCompilation(source);
        var generator = new AsyncEventGenerator();
        var driver = CSharpGeneratorDriver.Create(generator).RunGenerators(compilation);
        GeneratorSnapshot.Verify(driver);
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var refs = new List<MetadataReference>(Basic.Reference.Assemblies.Net90.References.All);
        refs.Add(MetadataReference.CreateFromFile(
            typeof(ZeroAlloc.AsyncEvents.AsyncEventHandler<>).Assembly.Location));

        return CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
