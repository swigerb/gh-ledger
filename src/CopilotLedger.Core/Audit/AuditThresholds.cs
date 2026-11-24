using CopilotLedger.Core.Breaker;

namespace CopilotLedger.Core.Audit;

// The rules' triggers. No option changes them; AuditJson writes them, so a reader can reproduce every finding.
public sealed record AuditThresholds
{
    public static AuditThresholds Default { get; } = new();

    public int TurnVelocityRequests { get; init; } = 30;
    public int TurnVelocityWindowMinutes { get; init; } = 5;
    public int ToolLoopCalls { get; init; } = 8;
    public int FailedToolRetryCalls { get; init; } = 3;
    public long ContextBloatInputTokens { get; init; } = 50_000;
    public long ContextBloatRatio { get; init; } = 200;
    public int ContextGrowthRequests { get; init; } = 10;
    public decimal ContextGrowthRatio { get; init; } = 4m;
    public int CacheMissStreakRequests { get; init; } = 5;
    public long CacheMissInputTokens { get; init; } = 20_000;
    public decimal CacheMissHitRatio { get; init; } = 0.10m;
    public long TrivialOutputTokens { get; init; } = 200;
    public int TrivialRequestsPerDay { get; init; } = 10;
    public decimal NearBreakerFraction { get; init; } = 0.8m;

    // failed_tool_retry's trigger: FailedToolRetryCalls, kept below the breaker's churn threshold, and at least 2.
    public int FailedToolRetryTrigger(BreakerSettings breaker)
    {
        ArgumentNullException.ThrowIfNull(breaker);
        return Math.Max(2, Math.Min(FailedToolRetryCalls, breaker.ChurnFailures - 1));
    }
}
