namespace CopilotLedger.Core.Import;

// What one session log recorded, as metadata only.
public sealed record SessionLogReadResult
{
    public required SessionLogStatus Status { get; init; }
    public string? ProducerVersion { get; init; }                           // valid (ProducerVersion.TryParse), or null
    public required IReadOnlyList<ImportedUsageRecord> Records { get; init; }
    public required IReadOnlyList<ImportedToolCall> ToolCalls { get; init; }
    public required IReadOnlyList<ImportDiagnostic> Diagnostics { get; init; }
    public required long RecordsParsed { get; init; }             // usage lines of a Read log, valid or not
    public required long UnsupportedUsageShapes { get; init; }
    public required long MalformedLines { get; init; }            // invalid JSON, oversized, or an unterminated last line
    public required long SkippedEvents { get; init; }             // well-formed lines of any other type, known or not
}
