namespace CopilotLedger.Core.Import;

public sealed record SessionLogLocation
{
    public required ImportSourceKind Source { get; init; }
    public required string LogId { get; init; }              // validated: a Copilot session ID; a Codex rollout ID ("archived/" first)
    public required string Path { get; init; }               // never stored or printed
    public required long SizeBytes { get; init; }            // at discovery; reading stops there
    public required DateTimeOffset LastWriteTime { get; init; }
    public required bool IsActive { get; init; }             // Copilot: an inuse.*.lock file in the session directory
    public bool IsCompressed { get; init; }                  // Codex .jsonl.zst, which is never opened
}
