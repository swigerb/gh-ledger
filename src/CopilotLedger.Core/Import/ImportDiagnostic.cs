namespace CopilotLedger.Core.Import;

// A content-free problem in a session log, counted per log, code, event, and field. It never holds a byte, key, value, event
// type, or exception message of the log: Field is one of the parser's own path constants, and Version passed
// ProducerVersion.TryParse.
public sealed record ImportDiagnostic
{
    public required ImportSourceKind Source { get; init; }
    public required string LogId { get; init; }
    public required ImportDiagnosticCode Code { get; init; }
    public ImportEventKind Event { get; init; }
    public string? Field { get; init; }               // such as "data.usage.inputTokens"
    public string? Version { get; init; }             // the producer version the diagnostic is about
    public required long Count { get; init; }
    public long FirstLine { get; init; }              // 1-based; 0 for a diagnostic about the whole log
    public long FirstByteOffset { get; init; }        // of that line's first byte
}
