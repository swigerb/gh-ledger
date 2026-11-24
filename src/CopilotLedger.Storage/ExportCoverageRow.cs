namespace CopilotLedger.Storage;

public sealed record ExportSourceCount(string Source, long Spans);

// What an export's selection covers, and what the ledger can't account for.
public sealed record ExportCoverageRow
{
    public required IReadOnlyList<ExportSourceCount> SpansBySource { get; init; }   // sources with spans, ordinal
    public required long SupersededMatchedImportSpans { get; init; }   // superseded by a selected live row of the same call
    public required long SupersededUnmatchedImportSpans { get; init; } // superseded without a match, selected by their own columns
    public required long LedgerAmbiguousImportRecords { get; init; } // import_sessions, the whole ledger
    public required long LedgerIncompleteImportLogs { get; init; }   // import_sessions with complete = 0, the whole ledger
    public required long SpansBeforeToolHistory { get; init; }       // selected spans that start before toolHistoryFrom
    public required long ImportedToolCallsWithoutDetail { get; init; } // tool calls of imported requests beyond the links they own
    public required long CacheReportedSpans { get; init; }
    public required long CacheAbsentSpans { get; init; }
    public required long CacheLegacyUnknownSpans { get; init; }
}
