using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Tests.TestSupport;

public static class TestSpans
{
    public static NormalizedGenAiSpan Chat(
        string? parentSpanId = null,
        string operationName = "chat",
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        string? providerName = null,
        string? requestModel = null,
        string? responseModel = null,
        string? agentName = null,
        string? conversationId = null,
        string? serverAddress = null,
        int? serverPort = null,
        string? workingDirectory = null,
        long inputTokens = 0,
        long outputTokens = 0,
        long cacheReadInputTokens = 0,
        long cacheWriteInputTokens = 0,
        CacheCounterState? cacheReadState = null,
        CacheCounterState? cacheWriteState = null)
    {
        DateTimeOffset start = startTime ?? new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
        return new NormalizedGenAiSpan
        {
            TraceId = Guid.NewGuid().ToString("N"),
            SpanId = Guid.NewGuid().ToString("N")[..16],
            ParentSpanId = parentSpanId,
            OperationName = operationName,
            StartTime = start,
            EndTime = endTime ?? start.AddSeconds(2),
            ProviderName = providerName,
            RequestModel = requestModel,
            ResponseModel = responseModel,
            AgentName = agentName,
            ConversationId = conversationId,
            ServerAddress = serverAddress,
            ServerPort = serverPort,
            WorkingDirectory = workingDirectory,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CacheReadInputTokens = cacheReadInputTokens,
            CacheWriteInputTokens = cacheWriteInputTokens,

            // Only a reported counter can be nonzero. Pass a state to record a reported 0.
            CacheReadState = cacheReadState
                ?? (cacheReadInputTokens == 0 ? CacheCounterState.Absent : CacheCounterState.Reported),
            CacheWriteState = cacheWriteState
                ?? (cacheWriteInputTokens == 0 ? CacheCounterState.Absent : CacheCounterState.Reported),
        };
    }
}
