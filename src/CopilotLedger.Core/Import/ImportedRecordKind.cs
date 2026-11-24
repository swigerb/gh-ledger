namespace CopilotLedger.Core.Import;

// What kind of log entry a record comes from. Part of the record's alias and span ID, so keys of different kinds never collide.
public enum ImportedRecordKind
{
    Usage,        // Copilot session.usage_record; Codex token_usage_record
    Compaction,   // Copilot session.compaction_complete with compactionTokensUsed
}
