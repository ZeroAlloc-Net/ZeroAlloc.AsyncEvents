; Unshipped analyzer release.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category              | Severity | Notes
--------|-----------------------|----------|-----------------------------------------------------------------
ZAAE001 | ZeroAlloc.AsyncEvents | Warning  | Containing type of a class with async events is not partial
ZAAE002 | ZeroAlloc.AsyncEvents | Error    | File-local class with async events is not generated
ZAAE003 | ZeroAlloc.AsyncEvents | Error    | Class name differs only in case from another class with async events
