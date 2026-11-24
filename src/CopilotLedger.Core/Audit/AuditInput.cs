using CopilotLedger.Core.Breaker;

namespace CopilotLedger.Core.Audit;

// Everything the rules read. The storage query fills Spans, ToolCalls, Coverage, and Breaker; the caller adds the rest.
public sealed record AuditInput
{
    public required IReadOnlyList<AuditSpan> Spans { get; init; }
    public required IReadOnlyList<AuditToolCall> ToolCalls { get; init; }
    public required AuditCoverage Coverage { get; init; }
    public required BreakerSettings Breaker { get; init; }
    public AuditThresholds Thresholds { get; init; } = AuditThresholds.Default;
    public IAuditPricing? Pricing { get; init; }            // null: premium_model_trivial_turn doesn't run; repriced impacts are null
    public string? EconomyModel { get; init; }              // a githubAiCredits model; null: premium impacts are null
    public TimeZoneInfo TimeZone { get; init; } = TimeZoneInfo.Utc;   // premium_model_trivial_turn's days
    public IReadOnlyList<string> Rules { get; init; } = AuditRuleIds.All;   // unknown IDs are ignored
}
