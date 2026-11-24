namespace CopilotLedger.Core.Audit;

// One execute_tool call of a selected prompt chain. It has a start and no end.
public sealed record AuditToolCall
{
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public string? ParentSpanId { get; init; }
    public string? ConversationId { get; init; }
    public string? AgentName { get; init; }
    public string? AgentInvocationId { get; init; }         // null when unknown
    public required DateTimeOffset StartTime { get; init; }
    public string? ToolName { get; init; }
    public long? ExitCode { get; init; }
    public required int StatusCode { get; init; }           // OTLP status: 0 unset, 1 ok, 2 error
    public required bool Imported { get; init; }            // its owner is an imported request
    public required string Repository { get; init; }        // its prompt chain's: the trace's first selected request
    public required string Branch { get; init; }
    public int? PullRequestNumber { get; init; }

    public AuditToolOutcome Outcome => AuditToolOutcomes.Classify(ExitCode, StatusCode, Imported);
}
