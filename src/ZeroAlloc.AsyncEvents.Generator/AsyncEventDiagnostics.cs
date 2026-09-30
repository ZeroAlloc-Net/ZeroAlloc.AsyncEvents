using Microsoft.CodeAnalysis;

namespace ZeroAlloc.AsyncEvents.Generator;

/// <summary>
/// Diagnostics of the async event generator. Each one means that nothing is generated for the
/// class it is reported on; every other class is still generated.
/// </summary>
internal static class AsyncEventDiagnostics
{
    private const string Category = "ZeroAlloc.AsyncEvents";
    private const string HelpLink = "https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/blob/main/docs/diagnostics.md#";

    /// <summary>
    /// A nested class whose containing type is not <c>partial</c>. The generated file has to
    /// reopen every containing type, which only a partial type allows.
    /// </summary>
    public static readonly DiagnosticDescriptor ContainingTypeNotPartial = new(
        id: "ZAAE001",
        title: "Containing type of a class with async events is not partial",
        messageFormat: "The async events of class '{0}' are not generated because its containing type '{1}' is not partial",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "zaae001");

    /// <summary>
    /// A file-local class, or one nested in a file-local type. A file-local type is visible only
    /// in its own file, so a generated file cannot extend it.
    /// </summary>
    public static readonly DiagnosticDescriptor FileLocalType = new(
        id: "ZAAE002",
        title: "File-local class with async events is not generated",
        messageFormat: "The async events of class '{0}' are not generated because it is file-local or nested in a file-local type, and a generated file cannot extend a file-local type",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "zaae002");

    /// <summary>
    /// Roslyn compares hint names ignoring case, so a class whose qualified name differs only in
    /// case from an earlier class's cannot get its own file.
    /// </summary>
    public static readonly DiagnosticDescriptor NameDiffersOnlyInCase = new(
        id: "ZAAE003",
        title: "Class name differs only in case from another class with async events",
        messageFormat: "The async events of class '{0}' are not generated because its file name '{1}' differs only in case from that of class '{2}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "zaae003");
}
