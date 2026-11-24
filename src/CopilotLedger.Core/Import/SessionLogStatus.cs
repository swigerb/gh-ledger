namespace CopilotLedger.Core.Import;

public enum SessionLogStatus
{
    Read,                          // its records follow
    MissingSessionStart,           // Copilot: no session.start first; Codex: no session_meta first
    SessionIdMismatch,             // Copilot: session.start names another session
    UnsupportedProducer,           // another Copilot producer or schema version; a Codex session_meta without its IDs
    UnsupportedProducerVersion,    // a version outside the supported ones, or none that can be read
    AggregateOnly,                 // Copilot before 1.0.103, which logs only session totals; nothing after session.start is read
    Compressed,                    // Codex .jsonl.zst
    Unreadable,                    // the file couldn't be opened or read
}
