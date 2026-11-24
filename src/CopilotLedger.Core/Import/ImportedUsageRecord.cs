using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Core.Import;

// One model call from a session log. It deliberately has no free-text field.
public sealed record ImportedUsageRecord
{
    public required ImportSourceKind Source { get; init; }
    public required ImportedRecordKind Kind { get; init; }
    public required string SessionId { get; init; }          // the conversation: the Copilot session ID; Codex payload.session_id
    public required string RecordKey { get; init; }          // Copilot usageId, or a compaction's service request ID; Codex
                                                             // response_id. Never stored.
    public string? TurnKey { get; init; }                    // the user prompt whose trace the call joins; null: its own trace
    public string? ServiceRequestId { get; init; }           // normalized; GitHub-billed Copilot records only. Never stored.
    public required DateTimeOffset StartTime { get; init; }
    public required DateTimeOffset EndTime { get; init; }
    public required string RequestModel { get; init; }       // as logged
    public string? ResponseModel { get; init; }              // as logged; null for compactions and Codex
    public string? ProviderName { get; init; }               // "github" for GitHub-billed Copilot calls; else the provider
    public required BillingSource BillingSource { get; init; }
    public string? AgentName { get; init; }                  // null for the main agent
    public required long InputTokens { get; init; }          // includes cache reads and writes
    public required long OutputTokens { get; init; }
    public required long CacheReadInputTokens { get; init; }
    public required long CacheWriteInputTokens { get; init; }
    public required CacheCounterState CacheReadState { get; init; }    // Reported or Absent; an Absent counter is 0
    public required CacheCounterState CacheWriteState { get; init; }
    public long? ToolCallCount { get; init; }                // the log's own count; never expanded into calls
    public ImportedGitContext? Git { get; init; }
    public required long LineNumber { get; init; }           // 1-based; for diagnostics only
}
