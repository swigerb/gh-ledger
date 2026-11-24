namespace CopilotLedger.Core.Import;

// A session-log event type the parsers know. Every other type, including one that holds content, is Unknown to diagnostics.
public enum ImportEventKind
{
    Unknown,
    SessionStart,
    SessionResume,
    UsageRecord,
    CompactionComplete,
    SubagentStarted,
    ToolExecutionStart,
    ToolExecutionComplete,
    JournalLink,
    SessionMeta,
    TurnContext,
    TokenUsageRecord,
    AssistantMessage,
}
