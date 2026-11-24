using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Export;

// What the report shows. HtmlReportWriter renders it; the bundle's JSON files serialize it.
internal sealed record ExportReportModel
{
    public required ExportHeader Header { get; init; }
    public required ExportSummary Summary { get; init; }
    public required ExportCoverage Coverage { get; init; }
    public required IReadOnlyList<ExportAuditFinding> AuditFindings { get; init; }      // always empty until gh ledger audit (#65)
    public required IReadOnlyList<LedgerRollupRow> ByModel { get; init; }               // GetRollupAsync's order
    public required IReadOnlyList<LedgerRollupRow> ByAgent { get; init; }
    public required IReadOnlyList<LedgerRollupRow> ByBranch { get; init; }              // Repository is the shown key
    public required IReadOnlyList<LedgerRollupRow> ByChannelModel { get; init; }
    public required IReadOnlyList<CacheEfficiencyRow> CacheByModel { get; init; }
    public required IReadOnlyList<CacheEfficiencyRow> CacheByAgent { get; init; }
    public required IReadOnlyList<PromptChainRow> PromptChains { get; init; }           // the top ExportLimits.PromptChains; shown keys
    public required IReadOnlyList<BreakerTrip> BreakerTrips { get; init; }              // shown keys; RepositoryRoot only with local paths
    public required IReadOnlyList<BranchBudgetRow> Budgets { get; init; }               // shown keys
}

internal sealed record ExportHeader
{
    public required string ToolVersion { get; init; }                         // GhLedgerCli.Version
    public required DateTimeOffset GeneratedAt { get; init; }
    public required ExportFilter Filter { get; init; }
    public required TimeZoneInfo TimeZone { get; init; }
    public string? RequestedTimeZone { get; init; }
    public required IReadOnlyList<string> RatesVersions { get; init; }
    public required IReadOnlyList<ExportApiRateCard> ApiRateCards { get; init; }
    public required string Scope { get; init; }                                // "swigerb/copilot-ledger, branch feature/ledger", or "all repositories"
    public required int MaxTimelineSpans { get; init; }
    public required bool IncludeLocalPaths { get; init; }
}

internal sealed record ExportFilter
{
    public string? Repository { get; init; }      // the shown repository key; null for every repository
    public required bool AllRepositories { get; init; }
    public string? Branch { get; init; }
    public int? PullRequest { get; init; }
    public string? Conversation { get; init; }
    public required string Since { get; init; }
    public required string Period { get; init; }  // LedgerPeriod.Label: "last 30 days", "today", "since 2026-11-01", "all time"
    public DateTimeOffset? FromUtc { get; init; }
    public required DateTimeOffset ToUtc { get; init; }
}

internal sealed record ExportSummary
{
    public required LedgerRollupRow Totals { get; init; }     // ReportCommands.Sum of the ChannelModel rollup
    public required long PromptChains { get; init; }          // GetPromptCountAsync
}

internal sealed record ExportCoverage
{
    public required long Spans { get; init; }
    public required IReadOnlyList<ExportSourceCount> SpansBySource { get; init; }
    public required long SupersededMatchedImportSpans { get; init; }    // left out: a selected live span recorded the same call
    public required long SupersededUnmatchedImportSpans { get; init; }  // left out: live spans of their sessions replaced them unmatched
    public required long LedgerAmbiguousImportRecords { get; init; }
    public required long LedgerIncompleteImportLogs { get; init; }
    public required long UnpricedSpans { get; init; }
    public required long ApiEquivalentUnpricedSpans { get; init; }
    public required long ApiEquivalentEstimatedSpans { get; init; }
    public required long CacheReportedSpans { get; init; }
    public required long CacheAbsentSpans { get; init; }
    public required long CacheLegacyUnknownSpans { get; init; }
    public required int ToolHistoryRetentionDays { get; init; }        // LedgerImporter.ToolLinkRetention.Days (7)
    public required DateTimeOffset ToolHistoryFromUtc { get; init; }    // GeneratedAt less the retention
    public required long SpansBeforeToolHistory { get; init; }
    public required long ImportedToolCallsWithoutDetail { get; init; }
    // Imports that the ledger couldn't account for (deferred, unfinished, or replaced without a match) may hide requests.
    public bool RequestsComplete =>
        LedgerAmbiguousImportRecords == 0 && LedgerIncompleteImportLogs == 0 && SupersededUnmatchedImportSpans == 0;
    public bool ToolHistoryComplete => SpansBeforeToolHistory == 0 && ImportedToolCallsWithoutDetail == 0;
}

internal sealed record ExportAuditFinding(string Severity, string Title, string Detail);
