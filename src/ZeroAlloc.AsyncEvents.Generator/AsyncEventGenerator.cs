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
#pragma warning disable EPS06 // IncrementalValuesProvider/IncrementalValueProvider are structs; hidden copies are unavoidable in the pipeline API
        var raw = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "ZeroAlloc.AsyncEvents.AsyncEventAttribute",
                AsyncEventParser.IsCandidate,
                AsyncEventParser.Parse);

        var filtered  = raw.Where(m => m is not null);
        var selected  = filtered.Select((m, _) => m!);
        var collected = selected.Collect();
        var models    = collected.SelectMany((items, _) => Deduplicate(items));
#pragma warning restore EPS06

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
