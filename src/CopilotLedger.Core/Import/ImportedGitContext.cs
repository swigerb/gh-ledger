namespace CopilotLedger.Core.Import;

// The Git context a session log recorded for its requests. Never filled from today's working tree.
public sealed record ImportedGitContext
{
    public string? WorkingDirectory { get; init; }   // the recorded cwd; matches --repo, and is never stored
    public string? RepositoryRoot { get; init; }     // the recorded Git root, fully qualified; else null
    public string? RepositorySlug { get; init; }     // "owner/repo"; remote URLs and credentials are never kept
    public string? Branch { get; init; }
    public string? HeadCommitSha { get; init; }      // lowercase, 40 or 64 hex characters
}
