namespace CopilotLedger.Core.Import;

// A log's diagnostics, counted per code, event, and field, each with the line and byte offset where it first occurred.
internal sealed class ImportDiagnosticBag(ImportSourceKind source, string logId)
{
    private readonly Dictionary<(ImportDiagnosticCode Code, ImportEventKind Event, string? Field), ImportDiagnostic> _items =
        [];

    internal void Add(
        ImportDiagnosticCode code,
        ImportEventKind kind,
        string? field,
        long line,
        long byteOffset,
        string? version = null)
    {
        (ImportDiagnosticCode, ImportEventKind, string?) key = (code, kind, field);
        _items[key] = _items.TryGetValue(key, out ImportDiagnostic? item)
            ? item with { Count = item.Count + 1 }
            : new ImportDiagnostic
            {
                Source = source,
                LogId = logId,
                Code = code,
                Event = kind,
                Field = field,
                Version = version,
                Count = 1,
                FirstLine = line,
                FirstByteOffset = byteOffset,
            };
    }

    // In order of first occurrence, then code.
    internal IReadOnlyList<ImportDiagnostic> ToList() =>
        [.. _items.Values.OrderBy(item => item.FirstLine).ThenBy(item => item.Code)];
}
