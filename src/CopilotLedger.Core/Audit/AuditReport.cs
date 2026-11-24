using CopilotLedger.Core.Breaker;

namespace CopilotLedger.Core.Audit;

public sealed record AuditSourceCount(string Source, long Spans);

// What the rules saw, and what they couldn't.
public sealed record AuditSummary
{
    public required long Spans { get; init; }
    public required IReadOnlyList<AuditSourceCount> SpansBySource { get; init; }   // ordinal by Source
    public required long ToolCalls { get; init; }
    public required long UnpricedSpans { get; init; }                  // !BilledKnown
    public required long CacheReportedSpans { get; init; }
    public required long CacheAbsentSpans { get; init; }               // neither counter reported; neither state legacy_unknown
    public required long CacheLegacyUnknownSpans { get; init; }        // neither reported; a state is legacy_unknown
    public required long SpansWithoutAgentIdentity { get; init; }
    public required long ToolCallsWithoutAgentIdentity { get; init; }
    public required long SpansWithUnknownToolCount { get; init; }
    public required AuditCoverage Coverage { get; init; }
}

public sealed record AuditReportFilter
{
    public string? Repository { get; init; }
    public string? Branch { get; init; }
    public int? PullRequestNumber { get; init; }
    public string? Conversation { get; init; }              // export's --conversation; null for audit and MCP
    public required string Since { get; init; }
    public required string Period { get; init; }            // LedgerPeriod.Label
    public DateTimeOffset? FromUtc { get; init; }
    public required DateTimeOffset ToUtc { get; init; }
}

// One audit run, as AuditJson writes it.
public sealed record AuditReport
{
    public required DateTimeOffset GeneratedAt { get; init; }
    public required AuditReportFilter Filter { get; init; }
    public string? RatesVersion { get; init; }               // the card that repriced; null without one
    public string? EconomyModel { get; init; }               // null without a card
    public required AuditThresholds Thresholds { get; init; }
    public required BreakerSettings Breaker { get; init; }
    public required IReadOnlyList<string> Rules { get; init; }          // evaluated, in AuditRuleIds.All order
    public required AuditSummary Summary { get; init; }
    public required IReadOnlyList<AuditFinding> Findings { get; init; } // every finding, in AuditAnalyzer.Order
    public required AuditSeverity MinSeverity { get; init; }
    public int? Limit { get; init; }                                     // null: every finding

    // The findings shown: those at MinSeverity or above, the first Limit of them.
    public IReadOnlyList<AuditFinding> Shown =>
        [.. Findings.Where(finding => finding.Severity >= MinSeverity).Take(Limit ?? int.MaxValue)];
}
