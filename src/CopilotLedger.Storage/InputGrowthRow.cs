namespace CopilotLedger.Storage;

// A prompt chain's sequence with the most input growth: one agent invocation's requests to one model in one repository,
// branch, and conversation.
public sealed record InputGrowthRow
{
    public required string TraceId { get; init; }
    public string? ConversationId { get; init; }
    public required string AgentSpanId { get; init; }     // the invoke_agent span of the sequence
    public string? Agent { get; init; }                   // gen_ai.agent.name; null when the agent has none
    public required string Model { get; init; }           // model id, or (none)
    public required string Repository { get; init; }
    public required string Branch { get; init; }
    public required DateTimeOffset StartTime { get; init; }   // the sequence's first request
    public required long TurnCount { get; init; }             // requests in the sequence
    public required long FirstInputTokens { get; init; }
    public required long PeakInputTokens { get; init; }
    public long InputGrowthTokens => PeakInputTokens - FirstInputTokens;
}
