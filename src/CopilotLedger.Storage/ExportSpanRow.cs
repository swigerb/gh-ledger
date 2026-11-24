using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Storage;

// One effective inference span, with exactly the columns that gh ledger export may use.
public sealed record ExportSpanRow
{
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public string? ParentSpanId { get; init; }
    public required string OperationName { get; init; }
    public string? ConversationId { get; init; }
    public string? AgentName { get; init; }
    public string? ProviderName { get; init; }
    public string? RequestModel { get; init; }
    public string? ResponseModel { get; init; }
    public string? ModelId { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public required DateTimeOffset EndTime { get; init; }
    public required long InputTokens { get; init; }
    public required long OutputTokens { get; init; }
    public required long CacheReadInputTokens { get; init; }
    public required long CacheWriteInputTokens { get; init; }
    public required CacheCounterState CacheReadState { get; init; }
    public required CacheCounterState CacheWriteState { get; init; }
    public required BillingChannel BillingChannel { get; init; }
    public required string PricingStatus { get; init; }           // as stored: priced, local_offload, byok_unpriced, …
    public required string RatesVersion { get; init; }
    public required decimal CostUsd { get; init; }
    public required decimal SavedUsd { get; init; }
    public decimal? ApiEquivalentUsd { get; init; }
    public string? ApiEquivalentStatus { get; init; }             // priced, unpriced, not_applicable, or null (recorded before version 5)
    public string? ApiRatesVersion { get; init; }
    public string? ApiRateCardSha256 { get; init; }
    public string? RepositorySlug { get; init; }
    public string? RepositoryRoot { get; init; }
    public string? BranchName { get; init; }
    public string? HeadCommitSha { get; init; }
    public int? PullRequestNumber { get; init; }
    public required string IngestSource { get; init; }            // otlp, import:copilot, import:codex
}
