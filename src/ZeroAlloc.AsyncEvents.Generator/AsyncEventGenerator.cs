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
        var raw = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "ZeroAlloc.AsyncEvents.AsyncEventAttribute",
                AsyncEventParser.IsCandidate,
                AsyncEventParser.Parse);

        var filtered  = raw.Where(m => m is not null);
        var selected  = filtered.Select((m, _) => m!);
        var collected = selected.Collect();
        var models    = collected.SelectMany((items, _) => Deduplicate(items));

        context.RegisterSourceOutput(models, Emit);
    }

    // Multiple triggers for same class (class attr + field attr) → deduplicate by the qualified name,
    // which tells apart nested and generic types that share a simple name.
    private static IEnumerable<AsyncEventClassModel> Deduplicate(
        System.Collections.Immutable.ImmutableArray<AsyncEventClassModel> items)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in items)
        {
            if (seen.Add(m.QualifiedName)) yield return m;
        }
    }

    private static void Emit(SourceProductionContext ctx, AsyncEventClassModel model)
    {
        var source = AsyncEventWriter.Write(model);
        ctx.AddSource(HintNames.ForAsyncEvents(model.QualifiedName), source);
    }
}
