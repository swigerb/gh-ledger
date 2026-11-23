namespace CopilotLedger.Core.Telemetry;

public sealed record NormalizedGenAiSpan
{
    public required string TraceId { get; init; }            // 32 lowercase hex chars
    public required string SpanId { get; init; }             // 16 lowercase hex chars
    public string? ParentSpanId { get; init; }
    public required string OperationName { get; init; }      // gen_ai.operation.name
    public required DateTimeOffset StartTime { get; init; }
    public required DateTimeOffset EndTime { get; init; }
    public string? ProviderName { get; init; }               // gen_ai.provider.name
    public string? RequestModel { get; init; }               // gen_ai.request.model
    public string? ResponseModel { get; init; }              // gen_ai.response.model
    public string? AgentName { get; init; }                  // gen_ai.agent.name
    public string? ConversationId { get; init; }             // gen_ai.conversation.id (Copilot session id)
    public string? ServerAddress { get; init; }              // server.address
    public int? ServerPort { get; init; }                    // server.port
    public string? WorkingDirectory { get; init; }           // set by ingestion in Sprint 2
    public long InputTokens { get; init; }                   // gen_ai.usage.input_tokens; INCLUDES cache read and cache write
    public long OutputTokens { get; init; }                  // gen_ai.usage.output_tokens
    public long CacheReadInputTokens { get; init; }          // gen_ai.usage.cache_read.input_tokens
    public long CacheWriteInputTokens { get; init; }         // gen_ai.usage.cache_write.input_tokens (legacy: cache_creation)
    public CacheCounterState CacheReadState { get; init; }   // Reported when the span carried the cache-read attribute
    public CacheCounterState CacheWriteState { get; init; }  // Reported when it carried cache_write (or legacy cache_creation)
}
