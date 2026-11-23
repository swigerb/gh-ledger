namespace CopilotLedger.Storage;

public sealed record PromptChainRow
{
    public required string TraceId { get; init; }
    public string? ConversationId { get; init; }
    public required DateTimeOffset StartTime { get; init; }            // earliest span start
    public required DateTimeOffset EndTime { get; init; }              // latest span end
    public required string Repository { get; init; }                   // same key as the Repository rollup
    public required string Branch { get; init; }                       // same key as the Branch rollup
    public required IReadOnlyList<string> Agents { get; init; }        // distinct non-null agent names, ordinal order
    public required IReadOnlyList<string> Models { get; init; }        // distinct non-null model ids, ordinal order
    public required long SpanCount { get; init; }
    public required long UnpricedSpanCount { get; init; }
    public required long InputTokens { get; init; }
    public required long OutputTokens { get; init; }
    public required long CacheReadInputTokens { get; init; }
    public required long CacheWriteInputTokens { get; init; }
    public required decimal AiCredits { get; init; }                   // AiCreditMath.UsdToAiCredits(AiCreditsUsd)
    public required decimal AiCreditsUsd { get; init; }                // GitHubAiCredits channel
    public required decimal FoundryByokUsd { get; init; }
    public required decimal LocalSavedUsd { get; init; }
    public required decimal? ApiEquivalentUsd { get; init; }               // as in LedgerRollupRow
    public required long ApiEquivalentUnpricedSpanCount { get; init; }
    public required long ApiEquivalentEstimatedSpanCount { get; init; }
}
