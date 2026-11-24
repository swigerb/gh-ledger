namespace CopilotLedger.Storage;

// One retained execute_tool link of a selected prompt chain.
public sealed record ExportToolCallRow
{
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public string? ConversationId { get; init; }
    public string? AgentName { get; init; }
    public string? ToolName { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public long? ToolExitCode { get; init; }
    public required int StatusCode { get; init; }                 // OTLP status: 0 unset, 1 ok, 2 error
    public required string IngestSource { get; init; }            // its owner's ingest_source; otlp for a live link
}
