namespace CopilotLedger.Core.Audit;

// Where a finding happened. Repository and branch are always set; each other part is null when the rule has none.
public sealed record AuditScope
{
    public required string Repository { get; init; }
    public required string Branch { get; init; }
    public int? PullRequestNumber { get; init; }
    public string? Agent { get; init; }                     // the agent's name, (none) for the default agent; null without one
    public string? Model { get; init; }
    public string? TraceId { get; init; }
    public string? ConversationId { get; init; }
    public string? Tool { get; init; }
    public AuditToolOutcome? ToolOutcome { get; init; }
}

public sealed record AuditMetric(string Name, decimal Value, string Unit);

// The units AuditMetric uses.
public static class AuditUnits
{
    public const string Requests = "requests";
    public const string Calls = "calls";
    public const string Tokens = "tokens";
    public const string Usd = "usd";
    public const string AiCredits = "ai_credits";
    public const string Ratio = "ratio";
    public const string Minutes = "minutes";
    public const string Flag = "flag";
    public const string UsdPer1MTokens = "usd_per_1m_tokens";
}

// One review signal: a rule matched the evidence in Scope between WindowStart and WindowEnd. It isn't proof of waste.
public sealed record AuditFinding
{
    public required string RuleId { get; init; }
    public required AuditSeverity Severity { get; init; }
    public required AuditScope Scope { get; init; }
    public required DateTimeOffset WindowStart { get; init; }
    public required DateTimeOffset WindowEnd { get; init; }
    public required IReadOnlyList<AuditMetric> Metrics { get; init; }   // a metric whose value is unknown is left out
    public decimal? EstimatedImpactUsd { get; init; }                   // billed USD; null when it can't be computed honestly
    public required string RecommendationKey { get; init; }
}

public interface IAuditRule
{
    string Id { get; }

    // The rule's findings in input, in any order (AuditAnalyzer orders them). Pure: reads only input.
    IEnumerable<AuditFinding> Evaluate(AuditInput input);
}
