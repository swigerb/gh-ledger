using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Storage;

// One inference span, that is one model request, in a turn breakdown. The cache properties are null when the span
// reported no cache usage (CacheMetrics.IsReported); on a span that did, a counter it left out is 0.
public sealed record TurnTokenRow
{
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public string? ParentSpanId { get; init; }
    public string? ConversationId { get; init; }
    public string? AgentSpanId { get; init; }                  // the nearest invoke_agent ancestor in the trace; null when unknown
    public required DateTimeOffset StartTime { get; init; }
    public required string Repository { get; init; }           // same key as the Repository rollup
    public required string Branch { get; init; }               // same key as the Branch rollup
    public int? PullRequestNumber { get; init; }
    public string? Agent { get; init; }                        // gen_ai.agent.name; null when the span has none
    public required string Model { get; init; }                // model id, or (none)
    public required long InputTokens { get; init; }
    public required long OutputTokens { get; init; }
    public required CacheCounterState CacheReadState { get; init; }
    public required CacheCounterState CacheWriteState { get; init; }
    public long? CacheReadInputTokens { get; init; }
    public long? CacheWriteInputTokens { get; init; }

    // InputTokens less the previous span's in the same sequence; null for a sequence's first span and when AgentSpanId is null.
    public long? InputDeltaFromPreviousTurn { get; init; }

    public long? UncachedInputTokens => CacheReadInputTokens is null
        ? null
        : Math.Max(InputTokens - CacheReadInputTokens.Value - CacheWriteInputTokens.GetValueOrDefault(), 0);

    public decimal? CacheHitRatio => CacheReadInputTokens is null
        ? null
        : CacheMetrics.HitRatio(CacheReadInputTokens.Value, InputTokens);
}
