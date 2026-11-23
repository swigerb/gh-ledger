using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Storage;

// One group's prompt-cache efficiency. Counts and sums cover all the group's spans, except that Reported* sums cover only
// the spans that reported cache usage (CacheMetrics.IsReported). Nothing is rounded.
public sealed record CacheEfficiencyRow
{
    public string? Repository { get; init; }        // Repository, Branch, and PullRequest groupings
    public string? Branch { get; init; }            // Branch grouping, and PullRequest grouping when PullRequestNumber is null
    public int? PullRequestNumber { get; init; }    // PullRequest grouping; null for the spans of a branch without one
    public string? Model { get; init; }             // Model grouping
    public string? Agent { get; init; }             // Agent grouping
    public required long SpanCount { get; init; }
    public required long CacheReportedSpanCount { get; init; }
    public required long InputTokens { get; init; }
    public required long OutputTokens { get; init; }
    public required long MaxInputTokens { get; init; }
    public required long ReportedInputTokens { get; init; }
    public required long ReportedCacheReadInputTokens { get; init; }
    public required long ReportedCacheWriteInputTokens { get; init; }
    public required long ReportedUncachedInputTokens { get; init; }     // input less cache reads and writes, at least 0 a span
    public required long EstimatedUncachedInputTokens { get; init; }    // the same over all spans, as pricing counts them
    public long CacheUnreportedSpanCount => SpanCount - CacheReportedSpanCount;

    // Observed uncached input; null when no span reported cache usage. It covers only the reported spans.
    public long? UncachedInputTokens => CacheReportedSpanCount == 0 ? null : ReportedUncachedInputTokens;
    public decimal? CacheHitRatio => CacheMetrics.HitRatio(ReportedCacheReadInputTokens, ReportedInputTokens);
    public decimal? AverageInputTokensPerTurn => SpanCount == 0 ? null : (decimal)InputTokens / SpanCount;
}
