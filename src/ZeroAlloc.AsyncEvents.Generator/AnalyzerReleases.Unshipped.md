; Unshipped analyzer release.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### Changed Rules

Rule ID | New Category          | New Severity | Old Category          | Old Severity | Notes
--------|-----------------------|--------------|-----------------------|--------------|------------------------------------------------------------
ZAAE001 | ZeroAlloc.AsyncEvents | Error        | ZeroAlloc.AsyncEvents | Warning      | Containing type of a class with async events is not partial
