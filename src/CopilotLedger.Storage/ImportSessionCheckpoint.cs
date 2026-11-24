using CopilotLedger.Core.Import;

namespace CopilotLedger.Storage;

// What the last import of one session log saw, so an unchanged, completely imported log can be skipped: its size and
// last-write time hint that it's unchanged, and the SHA-256 of its content decides.
public sealed record ImportSessionCheckpoint
{
    public required ImportSourceKind Source { get; init; }
    public required string LogId { get; init; }
    public required long LogSizeBytes { get; init; }
    public required DateTimeOffset LogLastWriteTime { get; init; }   // stored in Unix milliseconds
    public required string LogSha256 { get; init; }                  // SessionLogReadResult.ContentSha256: 64 lowercase hex
    public required bool Complete { get; init; }                     // every record of the whole log was resolved
    public required long Recorded { get; init; }
    public required long Duplicates { get; init; }
    public required long OverlapSkipped { get; init; }
    public required long AmbiguousOverlap { get; init; }
    public DateTimeOffset ImportedAt { get; init; }                  // set by the store when it writes the checkpoint
}
