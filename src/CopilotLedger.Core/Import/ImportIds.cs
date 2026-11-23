using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Core.Import;

// The IDs an imported record gets: hashes of its source, session, and keys (UsageIdentity.Sha256Hex), so the same record
// always gets the same IDs and a raw key is never stored.
public static class ImportIds
{
    // copilot, claude, or codex: the import_sessions and import_record_aliases source, and ingest_source after "import:".
    public static string SourceName(ImportSourceKind source) => source switch
    {
        ImportSourceKind.Copilot => "copilot",
        ImportSourceKind.Claude => "claude",
        ImportSourceKind.Codex => "codex",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
    };

    public static string KindName(ImportedRecordKind kind) => kind switch
    {
        ImportedRecordKind.Usage => "usage",
        ImportedRecordKind.Compaction => "compaction",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    // import_record_aliases.record_key_sha256: 64 lowercase hex characters.
    public static string RecordKeySha256(ImportSourceKind source, ImportedRecordKind kind, string recordKey) =>
        UsageIdentity.Sha256Hex("gh-ledger-import-record", "v1", SourceName(source), KindName(kind), recordKey);

    // A trace per user prompt (turnKey), or per record without one: 32 lowercase hex characters.
    public static string TraceId(
        ImportSourceKind source,
        string sessionId,
        string? turnKey,
        ImportedRecordKind kind,
        string recordKey) =>
        (turnKey is null
            ? UsageIdentity.Sha256Hex(
                "gh-ledger-import-trace", "v1", SourceName(source), sessionId, "record", KindName(kind), recordKey)
            : UsageIdentity.Sha256Hex("gh-ledger-import-trace", "v1", SourceName(source), sessionId, "turn", turnKey))[..32];

    // 16 lowercase hex characters.
    public static string SpanId(ImportSourceKind source, string sessionId, ImportedRecordKind kind, string recordKey) =>
        UsageIdentity.Sha256Hex("gh-ledger-import-span", "v1", SourceName(source), sessionId, KindName(kind), recordKey)[..16];

    // An imported tool call's execute_tool link: 16 lowercase hex characters.
    public static string ToolSpanId(ImportSourceKind source, string sessionId, string toolCallId) =>
        UsageIdentity.Sha256Hex("gh-ledger-import-tool", "v1", SourceName(source), sessionId, toolCallId)[..16];
}
