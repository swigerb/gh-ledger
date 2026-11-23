using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Storage;

public sealed record LedgerRollupRow
{
    public string? Repository { get; init; }
    public string? Branch { get; init; }
    public string? Model { get; init; }
    public string? Agent { get; init; }
    public BillingChannel? Channel { get; init; }
    public required long SpanCount { get; init; }
    public required long UnpricedSpanCount { get; init; }
    public required long InputTokens { get; init; }
    public required long OutputTokens { get; init; }
    public required long CacheReadInputTokens { get; init; }
    public required long CacheWriteInputTokens { get; init; }
    public required long CacheReportedSpanCount { get; init; }         // spans that reported cache usage (CacheMetrics.IsReported)
    public required long ReportedInputTokens { get; init; }            // input tokens of those spans
    public required long ReportedCacheReadInputTokens { get; init; }   // cache-read tokens of those spans
    public required long ReportedUncachedInputTokens { get; init; }    // their input less cache reads and writes, at least 0 a span
    public required decimal AiCredits { get; init; }
    public required decimal AiCreditsUsd { get; init; }
    public required decimal FoundryByokUsd { get; init; }
    public required decimal LocalSavedUsd { get; init; }
    public long CacheUnreportedSpanCount => SpanCount - CacheReportedSpanCount;
    public decimal? CacheHitRatio => CacheMetrics.HitRatio(ReportedCacheReadInputTokens, ReportedInputTokens);
}
