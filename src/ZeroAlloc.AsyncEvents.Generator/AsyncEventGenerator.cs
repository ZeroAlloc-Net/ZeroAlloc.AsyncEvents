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
    /// <summary>The tracking name of the step that yields one model per class.</summary>
    internal const string ModelsTrackingName = "AsyncEventModels";

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
        var models    = collected
            .SelectMany((items, _) => Resolve(items))
            .WithTrackingName(ModelsTrackingName);

        context.RegisterSourceOutput(models, Emit);
    }

    /// <summary>
    /// One model per class, in discovery order, with the case collisions marked.
    /// </summary>
    private static IEnumerable<AsyncEventClassModel> Resolve(
        System.Collections.Immutable.ImmutableArray<AsyncEventClassModel> items)
    {
        var models = Deduplicate(items);
        var skipped = FindCaseCollisions(models);
        for (var i = 0; i < models.Count; i++)
        {
            var m = models[i];
            yield return skipped.TryGetValue(m.QualifiedName, out var diagnostic) ? m with { Diagnostic = diagnostic } : m;
        }
    }

    // Multiple triggers for same class (class attr + field attr) → deduplicate by the qualified name,
    // which tells apart nested and generic types that share a simple name.
    private static List<AsyncEventClassModel> Deduplicate(
        System.Collections.Immutable.ImmutableArray<AsyncEventClassModel> items)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var models = new List<AsyncEventClassModel>(items.Length);
        foreach (var m in items)
        {
            if (seen.Add(m.QualifiedName)) models.Add(m);
        }
        return models;
    }

    /// <summary>
    /// Roslyn compares hint names ignoring case, so two classes whose qualified names differ only
    /// in case cannot both get a file. Among the classes that are generated, the one declared first,
    /// by file path and then position, keeps its file; every later one gets ZAAE003 instead. The
    /// result maps the qualified name of each later class to its diagnostic.
    /// </summary>
    private static Dictionary<string, DiagnosticInfo> FindCaseCollisions(List<AsyncEventClassModel> models)
    {
        var generated = models.FindAll(static m => m.Diagnostic is null);
        generated.Sort(static (x, y) =>
        {
            var byLocation = AsyncEventParser.CompareLocations(x.Location, y.Location);
            return byLocation != 0 ? byLocation : string.CompareOrdinal(x.QualifiedName, y.QualifiedName);
        });

        var first = new Dictionary<string, AsyncEventClassModel>(StringComparer.OrdinalIgnoreCase);
        var skipped = new Dictionary<string, DiagnosticInfo>(StringComparer.Ordinal);
        for (var i = 0; i < generated.Count; i++)
        {
            var m = generated[i];
            if (!first.TryGetValue(m.QualifiedName, out var earlier))
            {
                first.Add(m.QualifiedName, m);
                continue;
            }

            skipped.Add(m.QualifiedName, DiagnosticInfo.Create(
                AsyncEventDiagnostics.NameDiffersOnlyInCase, m.Location, m.DisplayName, m.HintName, earlier.DisplayName));
        }
        return skipped;
    }

    private static void Emit(SourceProductionContext ctx, AsyncEventClassModel model)
    {
        if (model.Diagnostic is not null)
        {
            ctx.ReportDiagnostic(model.Diagnostic.ToDiagnostic());
            return;
        }

        ctx.AddSource(model.HintName, AsyncEventWriter.Write(model));
    }
}
