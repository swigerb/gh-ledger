using CopilotLedger.Core.Pricing;
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
    public required ExportTimeline Timeline { get; init; }
    public required IReadOnlyList<ExportChainTurns> ChainTurns { get; init; }           // the first GrowthChains chains with 2+ requests
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

// Where tool history starts: at the retention before GeneratedAt, at the earliest span link the ledger keeps when that's
// later, or at GeneratedAt when the ledger keeps no link.
internal enum ExportToolHistoryStart
{
    Retention,
    EarliestLink,
    NoLinks,
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
    public required DateTimeOffset ToolHistoryFromUtc { get; init; }    // where ToolHistoryStart says tool history starts
    public required ExportToolHistoryStart ToolHistoryStart { get; init; }
    public required long SpansBeforeToolHistory { get; init; }
    public required long ImportedToolCallsWithoutDetail { get; init; }
    public required long ImportedRequestsWithoutToolData { get; init; } // imported requests whose logs give no tool-call count
    public required long ToolCalls { get; init; }                       // CountToolCallsAsync
    public required int MaxTimelineSpans { get; init; }
    public required long TimelineSpans { get; init; }                   // drawn requests
    public required long TimelineToolCalls { get; init; }               // drawn tool calls
    public bool TimelineTruncated => TimelineSpans < Spans || TimelineToolCalls < ToolCalls;
    // Imports that the ledger couldn't account for (deferred, unfinished, or replaced without a match) may hide requests.
    public bool RequestsComplete =>
        LedgerAmbiguousImportRecords == 0 && LedgerIncompleteImportLogs == 0 && SupersededUnmatchedImportSpans == 0;
    public bool ToolHistoryComplete =>
        SpansBeforeToolHistory == 0 && ImportedToolCallsWithoutDetail == 0 && ImportedRequestsWithoutToolData == 0;

    // The limitations that apply, ordinal.
    public IReadOnlyList<string> Warnings =>
    [
        .. new (bool Applies, string Name)[]
        {
            (ApiEquivalentEstimatedSpans > 0, "api-equivalent-estimated"),
            (ApiEquivalentUnpricedSpans > 0, "api-equivalent-unpriced"),
            (CacheAbsentSpans + CacheLegacyUnknownSpans > 0, "cache-unreported"),
            (LedgerAmbiguousImportRecords > 0, "import-ambiguous"),
            (LedgerIncompleteImportLogs > 0, "import-incomplete"),
            (SupersededUnmatchedImportSpans > 0, "import-superseded-unmatched"),
            (ImportedRequestsWithoutToolData > 0, "imported-requests-without-tool-data"),
            (ImportedToolCallsWithoutDetail > 0, "imported-tool-calls-without-detail"),
            (TimelineTruncated, "timeline-truncated"),
            (SpansBeforeToolHistory > 0, "tool-history-retention"),
            (UnpricedSpans > 0, "unpriced"),
        }.Where(warning => warning.Applies).Select(warning => warning.Name),
    ];
}

// One selected request, as the bundle and the timeline show it.
internal sealed record ExportSpanRecord
{
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public string? ParentSpanId { get; init; }
    public required string OperationName { get; init; }
    public string? ConversationId { get; init; }
    public string? Agent { get; init; }
    public string? Provider { get; init; }
    public string? RequestModel { get; init; }
    public string? ResponseModel { get; init; }
    public string? Model { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public required DateTimeOffset EndTime { get; init; }
    public required long InputTokens { get; init; }
    public required long CacheReadInputTokens { get; init; }
    public required long CacheWriteInputTokens { get; init; }
    public required long UncachedInputTokens { get; init; }        // max(input - cache read - cache write, 0), as pricing counts it
    public required long OutputTokens { get; init; }
    public required string CacheReadState { get; init; }           // reported, absent, legacy_unknown
    public required string CacheWriteState { get; init; }
    public required BillingChannel BillingChannel { get; init; }
    public required string PricingStatus { get; init; }
    public required string RatesVersion { get; init; }
    public required decimal CostUsd { get; init; }
    public required decimal AiCredits { get; init; }               // CostUsd in AI credits on GitHub AI credits; 0 otherwise
    public required decimal SavedUsd { get; init; }
    public decimal? ApiEquivalentUsd { get; init; }
    public string? ApiEquivalentStatus { get; init; }
    public string? ApiRatesVersion { get; init; }
    public string? ApiRateCardSha256 { get; init; }
    public required string Repository { get; init; }               // slug, root's folder name (the root with local paths), or (none)
    public string? RepositoryRoot { get; init; }                   // set only with --include-local-paths
    public string? Branch { get; init; }
    public string? HeadCommit { get; init; }
    public int? PullRequest { get; init; }
    public required string IngestSource { get; init; }
}

// One retained tool call of a selected prompt chain.
internal sealed record ExportToolCallRecord
{
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public string? ConversationId { get; init; }
    public string? Agent { get; init; }
    public string? ToolName { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public long? ExitCode { get; init; }
    public required string Status { get; init; }      // unset, ok, error
    public required bool Failed { get; init; }        // an exit code other than 0, or status error
    public required string IngestSource { get; init; }
}

// The timeline's drawing: x runs from 0 at FromUnixMs to 1000 at ToUnixMs, both 0 when nothing is drawn.
internal sealed record ExportTimeline
{
    public required long FromUnixMs { get; init; }
    public required long ToUnixMs { get; init; }
    public required IReadOnlyList<ExportTimelineRow> Rows { get; init; }
    public required IReadOnlyList<ExportTimelineSpan> Spans { get; init; }        // by start, trace, span
    public required IReadOnlyList<ExportTimelineTool> ToolCalls { get; init; }
    public required IReadOnlyList<UtcBand> NightBands { get; init; }
    public required IReadOnlyList<ExportAxisTick> Ticks { get; init; }
    public required decimal MinWidth { get; init; }                               // the narrowest view the script zooms to
    public required int AxisLevel { get; init; }                                  // the finest tick level the axis draws a line for
    public required IReadOnlyList<string> Models { get; init; }                   // the models of colors m0-m6: the model rollup's first 7
    public required bool OtherModels { get; init; }                               // some drawn request has color m7
    public bool IsEmpty => Spans.Count == 0 && ToolCalls.Count == 0;
}

internal sealed record ExportTimelineRow(
    int Index,
    string Label,
    string Title,
    long Requests,
    long ToolCalls,
    long FailedToolCalls,
    DateTimeOffset? First,
    DateTimeOffset? Last,
    IReadOnlyList<string> Models);

internal sealed record ExportTimelineSpan(ExportSpanRecord Span, int Row, decimal X, decimal Width, int Color);

internal sealed record ExportTimelineTool(ExportToolCallRecord Call, int Row, decimal X);

internal sealed record ExportAxisTick(decimal X, int Level, string Label);   // Level 0 days, 1 six hours, 2 hours, 3 ten minutes

// A prompt chain's requests for its context-growth chart: at most GrowthTurns of TotalTurns, in start order. StartTime is the
// chain's first request; PeakInputTokens is the most input of any of its requests.
internal sealed record ExportChainTurns(
    int Rank,
    string TraceId,
    DateTimeOffset StartTime,
    long TotalTurns,
    long PeakInputTokens,
    IReadOnlyList<ExportTurnPoint> Turns);

// Sequence numbers an (agent invocation, model) pair by its first request in the chain, from 0.
internal sealed record ExportTurnPoint(int Index, DateTimeOffset StartTime, string? Agent, string Model, int Sequence, long InputTokens);

internal sealed record ExportAuditFinding(string Severity, string Title, string Detail);
