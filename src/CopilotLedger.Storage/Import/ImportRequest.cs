using CopilotLedger.Core.Import;

namespace CopilotLedger.Storage.Import;

public sealed record ImportSourceRequest(ISessionLogSource Source, string Home);

public sealed record ImportRequest
{
    public required string DatabasePath { get; init; }
    public required IReadOnlyList<ImportSourceRequest> Sources { get; init; }   // at most one per kind, never Claude
    public string? RepositoryRoot { get; init; }     // --repo: a fully qualified working-tree root
    public string? RepositorySlug { get; init; }     // with RepositoryRoot: its owner/repo slug, for rows without a root
    public DateTimeOffset? Since { get; init; }      // --since; null for all time
    public bool DryRun { get; init; }
    public bool IncludeActive { get; init; }
    public int BatchSize { get; init; } = 500;       // 1 to 10,000 records per transaction
    public Action<int>? BatchCommitted { get; init; } // after each committed batch, with its size; never in a dry run
}
