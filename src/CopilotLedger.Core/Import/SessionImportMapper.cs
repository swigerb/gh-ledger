using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Core.Import;

// Turns session-log records into what the ledger records for live spans.
public static class SessionImportMapper
{
    // A chat span of the record's session (its conversation), with the record's IDs (ImportIds), no endpoint, no parent, and
    // no working directory: its Git context comes from the log, never from today's working tree.
    public static NormalizedGenAiSpan ToSpan(ImportedUsageRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new NormalizedGenAiSpan
        {
            TraceId = ImportIds.TraceId(record.Source, record.SessionId, record.TurnKey, record.Kind, record.RecordKey),
            SpanId = ImportIds.SpanId(record.Source, record.SessionId, record.Kind, record.RecordKey),
            ParentSpanId = null,
            OperationName = GenAiOperationNames.Chat,
            StartTime = record.StartTime,
            EndTime = record.EndTime,
            ProviderName = record.ProviderName,
            RequestModel = record.RequestModel,
            ResponseModel = record.ResponseModel,
            AgentName = record.AgentName,
            ConversationId = record.SessionId,
            InputTokens = record.InputTokens,
            OutputTokens = record.OutputTokens,
            CacheReadInputTokens = record.CacheReadInputTokens,
            CacheWriteInputTokens = record.CacheWriteInputTokens,
            CacheReadState = record.CacheReadState,
            CacheWriteState = record.CacheWriteState,
            ReportedCharge = record.ReportedCharge,
            ServiceRequestId = record.ServiceRequestId,
            BillingSource = record.BillingSource,
        };
    }

    // An execute_tool link in the trace of the record that asked for the tool: its name and exit code, and status 2 (error)
    // when it failed.
    public static GenAiSpanLink ToToolLink(ImportedToolCall toolCall, ImportedUsageRecord owner)
    {
        ArgumentNullException.ThrowIfNull(toolCall);
        ArgumentNullException.ThrowIfNull(owner);
        return new GenAiSpanLink
        {
            TraceId = ImportIds.TraceId(owner.Source, owner.SessionId, owner.TurnKey, owner.Kind, owner.RecordKey),
            SpanId = ImportIds.ToolSpanId(owner.Source, toolCall.SessionId, toolCall.ToolCallId),
            ParentSpanId = null,
            OperationName = GenAiOperationNames.ExecuteTool,
            AgentName = toolCall.AgentName,
            ConversationId = toolCall.SessionId,
            StartTime = toolCall.StartTime,
            ToolName = toolCall.ToolName,
            ToolExitCode = toolCall.ExitCode,
            StatusCode = toolCall.Success ? 0 : 2,
        };
    }
}
