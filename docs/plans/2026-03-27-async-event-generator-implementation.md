# AsyncEvent Source Generator Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a Roslyn source generator to ZeroAlloc.AsyncEvents that generates public `event AsyncEvent<TArgs>` with `add`/`remove` accessors from `AsyncEventHandler<TArgs>` fields annotated with `[AsyncEvent]`.

**Architecture:** A new `ZeroAlloc.AsyncEvents.Generator` project (netstandard2.0, `IsRoslynComponent=true`) is bundled into the main NuGet package as an analyzer. The generator uses `ForAttributeWithMetadataName` to find partial classes with `[AsyncEvent]` on the class or its fields, parses them into a model, and emits one generated file per class. Identical pattern to `ZeroAlloc.Notify.Generator`.

**Tech Stack:** Roslyn incremental generators, `Microsoft.CodeAnalysis.CSharp` 4.11.0, `Verify.SourceGenerators` for snapshot tests, xUnit.

---

### Task 1: Add AsyncEventAttribute

**Files:**
- Create: `src/ZeroAlloc.AsyncEvents/Attributes/AsyncEventAttribute.cs`
- Modify: `tests/ZeroAlloc.AsyncEvents.Tests/EventArgsTests.cs`

**Step 1: Write the failing test**

Add to `tests/ZeroAlloc.AsyncEvents.Tests/EventArgsTests.cs` (inside the `EventArgsTests` class):

```csharp
[Fact]
public void AsyncEventAttribute_DefaultModeIsParallel()
{
    var attr = new AsyncEventAttribute();
    Assert.Equal(InvokeMode.Parallel, attr.Mode);
}

[Fact]
public void AsyncEventAttribute_CanSetSequential()
{
    var attr = new AsyncEventAttribute(InvokeMode.Sequential);
    Assert.Equal(InvokeMode.Sequential, attr.Mode);
}
```

**Step 2: Run to verify it fails**

```bash
dotnet test c:/Projects/Prive/ZeroAlloc.AsyncEvents/tests/ZeroAlloc.AsyncEvents.Tests/ZeroAlloc.AsyncEvents.Tests.csproj
```

Expected: compile error — `AsyncEventAttribute` not found.

**Step 3: Create the attribute**

Create `src/ZeroAlloc.AsyncEvents/Attributes/AsyncEventAttribute.cs`:

```csharp
namespace ZeroAlloc.AsyncEvents;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Field, AllowMultiple = false)]
public sealed class AsyncEventAttribute : Attribute
{
    public InvokeMode Mode { get; }
    public AsyncEventAttribute(InvokeMode mode = InvokeMode.Parallel) => Mode = mode;
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test c:/Projects/Prive/ZeroAlloc.AsyncEvents/tests/ZeroAlloc.AsyncEvents.Tests/ZeroAlloc.AsyncEvents.Tests.csproj
```

Expected: 35 tests pass.

**Step 5: Commit**

```bash
git -C c:/Projects/Prive/ZeroAlloc.AsyncEvents add src/ZeroAlloc.AsyncEvents/Attributes/AsyncEventAttribute.cs tests/ZeroAlloc.AsyncEvents.Tests/EventArgsTests.cs
git -C c:/Projects/Prive/ZeroAlloc.AsyncEvents commit -m "feat: add AsyncEventAttribute"
```

---

### Task 2: Create ZeroAlloc.AsyncEvents.Generator project

**Files:**
- Create: `src/ZeroAlloc.AsyncEvents.Generator/ZeroAlloc.AsyncEvents.Generator.csproj`
- Create: `src/ZeroAlloc.AsyncEvents.Generator/IsExternalInit.cs`
- Modify: `src/ZeroAlloc.AsyncEvents/ZeroAlloc.AsyncEvents.csproj`
- Modify: `ZeroAlloc.AsyncEvents.slnx` (add new project)

**Step 1: Create the generator csproj**

Create `src/ZeroAlloc.AsyncEvents.Generator/ZeroAlloc.AsyncEvents.Generator.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
    <IsRoslynComponent>true</IsRoslynComponent>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.11.0" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.3.4" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

**Step 2: Add IsExternalInit polyfill**

Create `src/ZeroAlloc.AsyncEvents.Generator/IsExternalInit.cs` (required for `record` types on netstandard2.0):

```csharp
// Polyfill for record types on netstandard2.0
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit { }
```

**Step 3: Bundle the generator into the main package**

In `src/ZeroAlloc.AsyncEvents/ZeroAlloc.AsyncEvents.csproj`, add inside `<Project>`:

```xml
<ItemGroup>
  <ProjectReference Include="..\ZeroAlloc.AsyncEvents.Generator\ZeroAlloc.AsyncEvents.Generator.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false"
                    PrivateAssets="all" />
</ItemGroup>
```

**Step 4: Add the generator project to the solution**

```bash
dotnet sln c:/Projects/Prive/ZeroAlloc.AsyncEvents/ZeroAlloc.AsyncEvents.slnx add src/ZeroAlloc.AsyncEvents.Generator/ZeroAlloc.AsyncEvents.Generator.csproj
```

**Step 5: Verify the generator project builds**

```bash
dotnet build c:/Projects/Prive/ZeroAlloc.AsyncEvents/src/ZeroAlloc.AsyncEvents.Generator/ZeroAlloc.AsyncEvents.Generator.csproj
```

Expected: success (no errors).

**Step 6: Commit**

```bash
git -C c:/Projects/Prive/ZeroAlloc.AsyncEvents add src/ZeroAlloc.AsyncEvents.Generator/ src/ZeroAlloc.AsyncEvents/ZeroAlloc.AsyncEvents.csproj ZeroAlloc.AsyncEvents.slnx
git -C c:/Projects/Prive/ZeroAlloc.AsyncEvents commit -m "feat: add ZeroAlloc.AsyncEvents.Generator project"
```

---

### Task 3: Implement models and parser

**Files:**
- Create: `src/ZeroAlloc.AsyncEvents.Generator/Models/AsyncEventFieldModel.cs`
- Create: `src/ZeroAlloc.AsyncEvents.Generator/Models/AsyncEventClassModel.cs`
- Create: `src/ZeroAlloc.AsyncEvents.Generator/Pipeline/AsyncEventParser.cs`

**Step 1: Create the field model**

Create `src/ZeroAlloc.AsyncEvents.Generator/Models/AsyncEventFieldModel.cs`:

```csharp
namespace ZeroAlloc.AsyncEvents.Generator.Models;

internal sealed record AsyncEventFieldModel(
    string FieldName,    // e.g. "_orderPlaced"
    string EventName,    // e.g. "OrderPlaced"
    string ArgTypeFqn);  // e.g. "global::My.Namespace.OrderPlacedArgs"
```

**Step 2: Create the class model**

Create `src/ZeroAlloc.AsyncEvents.Generator/Models/AsyncEventClassModel.cs`:

```csharp
using System.Collections.Generic;

namespace ZeroAlloc.AsyncEvents.Generator.Models;

internal sealed record AsyncEventClassModel(
    string? Namespace,
    string TypeName,
    IReadOnlyList<AsyncEventFieldModel> Fields);
```

**Step 3: Create the parser**

Create `src/ZeroAlloc.AsyncEvents.Generator/Pipeline/AsyncEventParser.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ZeroAlloc.AsyncEvents.Generator.Models;

namespace ZeroAlloc.AsyncEvents.Generator.Pipeline;

internal static class AsyncEventParser
{
    private const string AsyncEventAttrFqn    = "ZeroAlloc.AsyncEvents.AsyncEventAttribute";
    private const string AsyncEventHandlerFqn = "ZeroAlloc.AsyncEvents.AsyncEventHandler<TArgs>";

    public static bool IsCandidate(SyntaxNode node, CancellationToken _)
    {
        // Class-level attribute: [AsyncEvent] on the class itself
        if (node is ClassDeclarationSyntax cls)
            return cls.Modifiers.Any(m => string.Equals(m.ValueText, "partial", StringComparison.Ordinal));

        // Field-level attribute: [AsyncEvent] on a field inside a partial class
        if (node is VariableDeclaratorSyntax)
        {
            var fieldDecl = node.Parent?.Parent; // VariableDeclaratorSyntax -> VariableDeclarationSyntax -> FieldDeclarationSyntax
            var classDecl = fieldDecl?.Parent;   // FieldDeclarationSyntax -> ClassDeclarationSyntax
            return classDecl is ClassDeclarationSyntax pc &&
                   pc.Modifiers.Any(m => string.Equals(m.ValueText, "partial", StringComparison.Ordinal));
        }

        return false;
    }

    public static AsyncEventClassModel? Parse(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        // Resolve the containing class regardless of whether attr is on class or field
        INamedTypeSymbol? type = ctx.TargetSymbol switch
        {
            INamedTypeSymbol t => t,
            IFieldSymbol f     => f.ContainingType,
            _                  => null
        };

        if (type is null) return null;

        var ns = type.ContainingNamespace.IsGlobalNamespace
            ? null
            : type.ContainingNamespace.ToDisplayString();

        var classHasAttr = HasAttr(type.GetAttributes(), AsyncEventAttrFqn);

        var fields = new List<AsyncEventFieldModel>();
        foreach (var member in type.GetMembers())
        {
            if (member is not IFieldSymbol field) continue;
            if (field.Type is not INamedTypeSymbol fieldType) continue;

            // Must be AsyncEventHandler<TArgs>
            if (!string.Equals(
                    fieldType.OriginalDefinition.ToDisplayString(),
                    AsyncEventHandlerFqn,
                    StringComparison.Ordinal)) continue;

            // Include if field has [AsyncEvent] OR class has [AsyncEvent]
            var fieldHasAttr = HasAttr(field.GetAttributes(), AsyncEventAttrFqn);
            if (!fieldHasAttr && !classHasAttr) continue;

            if (fieldType.TypeArguments.Length != 1) continue;

            var argTypeFqn = fieldType.TypeArguments[0]
                .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var eventName = ToEventName(field.Name);

            fields.Add(new AsyncEventFieldModel(field.Name, eventName, argTypeFqn));
        }

        return fields.Count == 0 ? null : new AsyncEventClassModel(ns, type.Name, fields);
    }

    private static bool HasAttr(
        System.Collections.Immutable.ImmutableArray<AttributeData> attrs, string fqn)
    {
        foreach (var a in attrs)
            if (string.Equals(a.AttributeClass?.ToDisplayString(), fqn, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static string ToEventName(string fieldName)
    {
        var name = fieldName;
        if (name.StartsWith("m_", StringComparison.Ordinal)) name = name.Substring(2);
        else if (name.StartsWith("_", StringComparison.Ordinal)) name = name.Substring(1);
        if (name.Length == 0) return fieldName;
        return char.ToUpperInvariant(name[0]) + name.Substring(1);
    }
}
```

**Step 4: Build to verify no errors**

```bash
dotnet build c:/Projects/Prive/ZeroAlloc.AsyncEvents/src/ZeroAlloc.AsyncEvents.Generator/ZeroAlloc.AsyncEvents.Generator.csproj
```

Expected: success.

**Step 5: Commit**

```bash
git -C c:/Projects/Prive/ZeroAlloc.AsyncEvents add src/ZeroAlloc.AsyncEvents.Generator/
git -C c:/Projects/Prive/ZeroAlloc.AsyncEvents commit -m "feat: add AsyncEventParser and models"
```

---

### Task 4: Implement writer and generator

**Files:**
- Create: `src/ZeroAlloc.AsyncEvents.Generator/Writers/AsyncEventWriter.cs`
- Create: `src/ZeroAlloc.AsyncEvents.Generator/AsyncEventGenerator.cs`

**Step 1: Create the writer**

Create `src/ZeroAlloc.AsyncEvents.Generator/Writers/AsyncEventWriter.cs`:

```csharp
using System.Text;
using ZeroAlloc.AsyncEvents.Generator.Models;

namespace ZeroAlloc.AsyncEvents.Generator.Writers;

internal static class AsyncEventWriter
{
    public static string Write(AsyncEventClassModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (model.Namespace is not null)
        {
            sb.AppendLine($"namespace {model.Namespace};");
            sb.AppendLine();
        }

        sb.AppendLine($"partial class {model.TypeName}");
        sb.AppendLine("{");

        foreach (var field in model.Fields)
        {
            sb.AppendLine("    #pragma warning disable MA0046");
            sb.AppendLine($"    public event global::ZeroAlloc.AsyncEvents.AsyncEvent<{field.ArgTypeFqn}> {field.EventName}");
            sb.AppendLine("    {");
            sb.AppendLine($"        add    => {field.FieldName}.Register(value);");
            sb.AppendLine($"        remove => {field.FieldName}.Unregister(value);");
            sb.AppendLine("    }");
            sb.AppendLine("    #pragma warning restore MA0046");
            sb.AppendLine();
        }

        sb.Append("}");
        return sb.ToString();
    }
}
```

**Step 2: Create the generator**

Create `src/ZeroAlloc.AsyncEvents.Generator/AsyncEventGenerator.cs`:

```csharp
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using ZeroAlloc.AsyncEvents.Generator.Models;
using ZeroAlloc.AsyncEvents.Generator.Pipeline;
using ZeroAlloc.AsyncEvents.Generator.Writers;

namespace ZeroAlloc.AsyncEvents.Generator;

[Generator]
public sealed class AsyncEventGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "ZeroAlloc.AsyncEvents.AsyncEventAttribute",
                AsyncEventParser.IsCandidate,
                AsyncEventParser.Parse)
            .Where(m => m is not null)
            .Select((m, _) => m!)
            .Collect()
            .SelectMany((items, _) => Deduplicate(items));

        context.RegisterSourceOutput(models, Emit);
    }

    // Multiple triggers for same class (class attr + field attr) → deduplicate by namespace:type key
    private static IEnumerable<AsyncEventClassModel> Deduplicate(
        System.Collections.Immutable.ImmutableArray<AsyncEventClassModel> items)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in items)
        {
            var key = $"{m.Namespace}:{m.TypeName}";
            if (seen.Add(key)) yield return m;
        }
    }

    private static void Emit(SourceProductionContext ctx, AsyncEventClassModel model)
    {
        var source = AsyncEventWriter.Write(model);
        var hint = string.IsNullOrEmpty(model.Namespace)
            ? $"{model.TypeName}.AsyncEvents.g.cs"
            : $"{model.Namespace}_{model.TypeName}.AsyncEvents.g.cs";
        ctx.AddSource(hint, source);
    }
}
```

**Step 3: Build the full solution**

```bash
dotnet build c:/Projects/Prive/ZeroAlloc.AsyncEvents
```

Expected: success across all TFMs.

**Step 4: Commit**

```bash
git -C c:/Projects/Prive/ZeroAlloc.AsyncEvents add src/ZeroAlloc.AsyncEvents.Generator/
git -C c:/Projects/Prive/ZeroAlloc.AsyncEvents commit -m "feat: implement AsyncEventWriter and AsyncEventGenerator"
```

---

### Task 5: Create generator test project with snapshot tests

**Files:**
- Create: `tests/ZeroAlloc.AsyncEvents.Generator.Tests/ZeroAlloc.AsyncEvents.Generator.Tests.csproj`
- Create: `tests/ZeroAlloc.AsyncEvents.Generator.Tests/ModuleInit.cs`
- Create: `tests/ZeroAlloc.AsyncEvents.Generator.Tests/GeneratorTests.cs`
- Modify: `ZeroAlloc.AsyncEvents.slnx`

**Step 1: Create the test csproj**

Create `tests/ZeroAlloc.AsyncEvents.Generator.Tests/ZeroAlloc.AsyncEvents.Generator.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.9.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" PrivateAssets="all" />
    <PackageReference Include="Verify.Xunit" Version="31.12.5" />
    <PackageReference Include="Verify.SourceGenerators" Version="2.4.0" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.11.0" />
    <PackageReference Include="Basic.Reference.Assemblies.Net90" Version="1.7.9" />
    <ProjectReference Include="..\..\src\ZeroAlloc.AsyncEvents.Generator\ZeroAlloc.AsyncEvents.Generator.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="true" />
    <ProjectReference Include="..\..\src\ZeroAlloc.AsyncEvents\ZeroAlloc.AsyncEvents.csproj" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="VerifyXunit" />
  </ItemGroup>
</Project>
```

**Step 2: Create ModuleInit**

Create `tests/ZeroAlloc.AsyncEvents.Generator.Tests/ModuleInit.cs`:

```csharp
using System.Runtime.CompilerServices;

namespace ZeroAlloc.AsyncEvents.Generator.Tests;

public static class ModuleInit
{
    [ModuleInitializer]
    public static void Init() => VerifySourceGenerators.Initialize();
}
```

**Step 3: Create GeneratorTests with all 6 snapshot scenarios**

Create `tests/ZeroAlloc.AsyncEvents.Generator.Tests/GeneratorTests.cs`:

```csharp
using System.Collections.Generic;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ZeroAlloc.AsyncEvents.Generator;

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
```

**Step 4: Add to solution**

```bash
dotnet sln c:/Projects/Prive/ZeroAlloc.AsyncEvents/ZeroAlloc.AsyncEvents.slnx add tests/ZeroAlloc.AsyncEvents.Generator.Tests/ZeroAlloc.AsyncEvents.Generator.Tests.csproj
```

**Step 5: Run tests to create initial snapshots**

```bash
dotnet test c:/Projects/Prive/ZeroAlloc.AsyncEvents/tests/ZeroAlloc.AsyncEvents.Generator.Tests/ZeroAlloc.AsyncEvents.Generator.Tests.csproj
```

First run will FAIL and create snapshot files under `tests/ZeroAlloc.AsyncEvents.Generator.Tests/Snapshots/`. Review the generated snapshots to verify they look correct (check that events are generated with proper add/remove, non-partial class produces no output, etc.).

**Step 6: Accept snapshots and run again to verify pass**

```bash
dotnet test c:/Projects/Prive/ZeroAlloc.AsyncEvents/tests/ZeroAlloc.AsyncEvents.Generator.Tests/ZeroAlloc.AsyncEvents.Generator.Tests.csproj
```

Expected: all 6 tests pass.

**Step 7: Commit**

```bash
git -C c:/Projects/Prive/ZeroAlloc.AsyncEvents add tests/ZeroAlloc.AsyncEvents.Generator.Tests/ ZeroAlloc.AsyncEvents.slnx
git -C c:/Projects/Prive/ZeroAlloc.AsyncEvents commit -m "test: add AsyncEventGenerator snapshot tests"
```
