namespace CopilotLedger.Core.Git;

public sealed record GitWorkspaceContext(
    string RepositoryRoot,      // absolute working-tree root (the directory that contains .git)
    string GitDirectory,        // absolute per-worktree git directory (contains HEAD)
    string? BranchName,         // for example "feature/x"; null when HEAD is detached
    string? HeadCommitSha,      // lowercase hex (40 or 64 chars); null for an unborn branch
    string? RemoteOriginUrl,    // remote.origin.url with userinfo removed; null if absent
    string? RepositorySlug)     // "owner/repo"; null if not derivable
{
    // n when the repository's config has [branch "<BranchName>"] merge = refs/pull/<n>/head, as gh pr checkout writes for
    // a pull request whose head it fetched from the base repository; null otherwise, including for detached HEADs.
    public int? PullRequestNumber { get; init; }

    public bool IsDetachedHead => BranchName is null && HeadCommitSha is not null;
}
