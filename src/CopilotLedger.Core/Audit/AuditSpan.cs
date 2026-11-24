using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Core.Audit;

// One effective inference span (a model request). Money is billed USD, what cost_usd_pico holds.
public sealed record AuditSpan
{
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public string? ParentSpanId { get; init; }
    public string? ConversationId { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public required DateTimeOffset EndTime { get; init; }
    public string? AgentName { get; init; }                 // gen_ai.agent.name; null for the default agent
    public string? AgentInvocationId { get; init; }         // the agent invocation in TraceId that made it; null when unknown
    public required string Model { get; init; }             // model_id, or (none)
    public string? RequestModel { get; init; }
    public required long InputTokens { get; init; }         // includes cache reads and writes
    public required long CacheReadInputTokens { get; init; }
    public required long CacheWriteInputTokens { get; init; }
    public required long OutputTokens { get; init; }
    public required CacheCounterState CacheReadState { get; init; }
    public required CacheCounterState CacheWriteState { get; init; }
    public required BillingChannel Channel { get; init; }
    public required PricingStatus PricingStatus { get; init; }
    public required decimal CostUsd { get; init; }          // GitHub AI credits or Foundry BYOK; 0 otherwise
    public required decimal AiCredits { get; init; }        // CostUsd / 0.01 on GitHub AI credits; 0 otherwise
    public decimal? ApiEquivalentUsd { get; init; }         // not used by the billed lens
    public long? ToolCallCount { get; init; }               // the tool calls it asked for; null when unknown
    public required string Repository { get; init; }        // COALESCE(repo_slug, repo_root, '(none)')
    public required string Branch { get; init; }            // COALESCE(branch_name, '(none)')
    public int? PullRequestNumber { get; init; }
    public required string IngestSource { get; init; }      // otlp, import:copilot, import:codex

    // Its billed price is known: priced, or local (billed 0).
    public bool BilledKnown => PricingStatus is PricingStatus.Priced or PricingStatus.LocalOffload;
}
