namespace ZeroAlloc.AsyncEvents.Generator.Models;

internal sealed record AsyncEventFieldModel(
    string FieldName,    // e.g. "_orderPlaced"
    string EventName,    // e.g. "OrderPlaced"
    string ArgTypeFqn);  // e.g. "global::My.Namespace.OrderPlacedArgs"
