namespace CopilotLedger.Core.Audit;

// Where tool history starts, as export decides it: at the retention before generatedAt, at the earliest span link the ledger
// keeps when that's later, or at generatedAt when the ledger keeps no link.
public enum AuditToolHistoryStart
{
    Retention,
    EarliestLink,
    NoLinks,
}

public static class AuditToolHistoryStarts
{
    public static string ToName(AuditToolHistoryStart start) => start switch
    {
        AuditToolHistoryStart.EarliestLink => "earliest_link",
        AuditToolHistoryStart.NoLinks => "no_links",
        _ => "retention",
    };
}

// What the ledger can't show the rules, counted by the storage query (Sprint 9's coverage query).
public sealed record AuditCoverage
{
    public required DateTimeOffset ToolHistoryFrom { get; init; }      // where ToolHistoryStart says tool history starts
    public required AuditToolHistoryStart ToolHistoryStart { get; init; }
    public required int ToolHistoryRetentionDays { get; init; }        // 7
    public required long SpansBeforeToolHistory { get; init; }
    public required long ImportedToolCallsWithoutDetail { get; init; }  // tool calls of imported requests beyond the links they own
    public required long ImportedRequestsWithoutToolData { get; init; } // imported requests whose logs give no tool-call count
    public required long SupersededMatchedImportSpans { get; init; }
    public required long SupersededUnmatchedImportSpans { get; init; }
    public required long LedgerAmbiguousImportRecords { get; init; }  // the whole ledger
    public required long LedgerIncompleteImportLogs { get; init; }    // the whole ledger

    // Export's coverage calls tool history complete exactly when this is false.
    public bool InsufficientToolHistory =>
        SpansBeforeToolHistory > 0 || ImportedToolCallsWithoutDetail > 0 || ImportedRequestsWithoutToolData > 0;
}
