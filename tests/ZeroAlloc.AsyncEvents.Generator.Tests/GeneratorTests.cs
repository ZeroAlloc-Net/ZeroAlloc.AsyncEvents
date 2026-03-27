using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ZeroAlloc.AsyncEvents.Generator.Tests;

public class GeneratorTests
{
    [Fact]
    public Task FieldAttribute_Parallel_GeneratesEvent()
        => Verify("""
            using ZeroAlloc.AsyncEvents;
            public partial class MyService
            {
                [AsyncEvent(InvokeMode.Parallel)]
                private AsyncEventHandler<string> _orderPlaced;
            }
            """);

    [Fact]
    public Task FieldAttribute_Sequential_GeneratesEvent()
        => Verify("""
            using ZeroAlloc.AsyncEvents;
            public partial class MyService
            {
                [AsyncEvent(InvokeMode.Sequential)]
                private AsyncEventHandler<string> _orderPlaced;
            }
            """);

    [Fact]
    public Task ClassAttribute_AllHandlerFieldsGetEvents()
        => Verify("""
            using ZeroAlloc.AsyncEvents;
            [AsyncEvent(InvokeMode.Parallel)]
            public partial class MyService
            {
                private AsyncEventHandler<string> _orderPlaced;
                private AsyncEventHandler<int> _itemShipped;
            }
            """);

    [Fact]
    public Task FieldAttribute_OverridesClassAttribute()
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
    public Task ClassAttribute_MixedModes()
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
    public Task NonPartialClass_NoOutput()
        => Verify("""
            using ZeroAlloc.AsyncEvents;
            public class MyService
            {
                [AsyncEvent(InvokeMode.Parallel)]
                private AsyncEventHandler<string> _orderPlaced;
            }
            """);

    private static Task Verify(string source)
    {
        var compilation = CreateCompilation(source);
        var generator = new AsyncEventGenerator();
        var driver = CSharpGeneratorDriver.Create(generator).RunGenerators(compilation);
        return Verifier.Verify(driver).UseDirectory("Snapshots");
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
