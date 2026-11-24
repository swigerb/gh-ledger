namespace CopilotLedger.Core.Import;

public enum ImportDiagnosticCode
{
    MalformedLine,                // not one JSON object with one string "type"
    OversizedLine,                // longer than ImportLimits.MaxLineBytes; discarded unread
    UnterminatedLine,             // a last line without "\n", such as one being written
    UnsupportedUsageShape,        // a usage record that fails validation; Field names the first failing field
    MissingSessionStart,
    SessionIdMismatch,
    UnsupportedProducer,
    UnsupportedProducerVersion,
    AggregateOnlySession,
    JournalPredecessor,           // a journal.link: earlier journal files aren't read
    CompressedLog,
    UnreadableLog,
    UnknownSubagent,              // informational: a sub-agent request recorded without an agent name
    InheritedRecord,              // informational: a Codex record of another thread, skipped
    AmbiguousOverlap,             // set by the importer
    CostOutOfRange,               // set by the importer
    UnreadableStore,              // set by the importer: entries of the store that couldn't be listed or examined
    SupersededUnmatched,          // set by the importer: LogId is the conversation whose imported rows live rows superseded
}
