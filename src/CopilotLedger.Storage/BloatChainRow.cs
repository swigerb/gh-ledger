using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Storage;

// One prompt chain (one trace) ranked by uncached input in a ContextBloatReport. Reported* sums cover only the spans that
// reported cache usage (CacheMetrics.IsReported).
public sealed record BloatChainRow
{
    public required string TraceId { get; init; }
    public string? ConversationId { get; init; }
    public required DateTimeOffset StartTime { get; init; }             // earliest span start
    public required string Repository { get; init; }                    // same key as the Repository rollup
    public required string Branch { get; init; }                        // same key as the Branch rollup
    public required long SpanCount { get; init; }
    public required long CacheReportedSpanCount { get; init; }
    public required long InputTokens { get; init; }
    public required long ReportedInputTokens { get; init; }
    public required long ReportedCacheReadInputTokens { get; init; }
    public required long ReportedUncachedInputTokens { get; init; }     // input less cache reads and writes, at least 0 a span
    public required long EstimatedUncachedInputTokens { get; init; }    // the same over all spans, as pricing counts them
    public decimal? CacheHitRatio => CacheMetrics.HitRatio(ReportedCacheReadInputTokens, ReportedInputTokens);
}
