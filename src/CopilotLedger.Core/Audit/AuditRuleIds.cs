namespace CopilotLedger.Core.Audit;

public static class AuditRuleIds
{
    public const string TurnVelocity = "turn_velocity";
    public const string ToolLoop = "tool_loop";
    public const string FailedToolRetry = "failed_tool_retry";
    public const string ContextBloat = "context_bloat";
    public const string ContextGrowth = "context_growth";
    public const string CacheMissStreak = "cache_miss_streak";
    public const string PremiumModelTrivialTurn = "premium_model_trivial_turn";
    public const string VelocityNearBreaker = "velocity_near_breaker";

    // The rule IDs in the issue's order, which every message and rules list uses.
    public static IReadOnlyList<string> All { get; } =
    [
        TurnVelocity,
        ToolLoop,
        FailedToolRetry,
        ContextBloat,
        ContextGrowth,
        CacheMissStreak,
        PremiumModelTrivialTurn,
        VelocityNearBreaker,
    ];

    // Ordinal; false for null.
    public static bool IsKnown(string? id) => id is not null && All.Contains(id, StringComparer.Ordinal);
}
