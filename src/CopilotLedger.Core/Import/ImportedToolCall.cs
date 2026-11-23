namespace CopilotLedger.Core.Import;

// A tool call whose start and completion a session log recorded. ToolCallId is never stored; it becomes a hash.
public sealed record ImportedToolCall
{
    public required string SessionId { get; init; }
    public required string OwnerRecordKey { get; init; }     // the usage record whose response asked for the tool
    public required string ToolCallId { get; init; }
    public required string ToolName { get; init; }
    public string? AgentName { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public required bool Success { get; init; }
    public long? ExitCode { get; init; }                     // shellExecution.exitCode
}
