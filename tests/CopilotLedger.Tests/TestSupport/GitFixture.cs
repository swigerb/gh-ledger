using System.Globalization;
using CopilotLedger.Core.Git;

namespace CopilotLedger.Tests.TestSupport;

public static class GitFixture
{
    public const string Sha1 = "0123456789abcdef0123456789abcdef01234567";
    public const string Sha2 = "89abcdef0123456789abcdef0123456789abcdef";

    // Creates <root>/repo with .git/HEAD "ref: refs/heads/feature/ledger\n", .git/refs/heads/feature/ledger Sha1 + "\n",
    // .git/config "[remote \"origin\"]\n\turl = https://github.com/swigerb/copilot-ledger.git\n", and an empty <root>/repo/src.
    // With pullRequest, the config also has "[branch \"feature/ledger\"]\n\tremote = origin\n\tmerge = refs/pull/<n>/head\n",
    // as gh pr checkout writes it. Returns <root>/repo.
    public static string Create(string root, int? pullRequest = null)
    {
        string repo = Path.Combine(root, "repo");
        string git = Path.Combine(repo, ".git");
        string branch = Path.Combine(git, "refs", "heads", "feature", "ledger");
        Directory.CreateDirectory(Path.GetDirectoryName(branch)!);
        Directory.CreateDirectory(Path.Combine(repo, "src"));
        File.WriteAllText(
            Path.Combine(git, "HEAD"),
            "ref: refs/heads/feature/ledger\n");
        File.WriteAllText(branch, Sha1 + "\n");
        File.WriteAllText(
            Path.Combine(git, "config"),
            "[remote \"origin\"]\n\turl = https://github.com/swigerb/copilot-ledger.git\n" +
            (pullRequest is null
                ? string.Empty
                : $"[branch \"feature/ledger\"]\n\tremote = origin\n\tmerge = refs/pull/{pullRequest.Value.ToString(CultureInfo.InvariantCulture)}/head\n"));

        // Reading the files back waits out a brief antivirus lock, so the resolver sees them when the test runs.
        _ = FileRetry.ReadAllText(Path.Combine(git, "HEAD"));
        _ = FileRetry.ReadAllText(branch);
        _ = FileRetry.ReadAllText(Path.Combine(git, "config"));
        return repo;
    }

    // new GitWorkspaceContext(<root>/repo, <root>/repo/.git, "feature/ledger", Sha1, "https://github.com/swigerb/copilot-ledger.git", "swigerb/copilot-ledger").
    // It creates no files.
    public static GitWorkspaceContext Feature(string root) => new(
        Path.Combine(root, "repo"),
        Path.Combine(root, "repo", ".git"),
        "feature/ledger",
        Sha1,
        "https://github.com/swigerb/copilot-ledger.git",
        "swigerb/copilot-ledger");
}
