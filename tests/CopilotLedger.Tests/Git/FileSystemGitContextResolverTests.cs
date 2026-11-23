using CopilotLedger.Core.Git;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Git;

public sealed class FileSystemGitContextResolverTests
{
    private const string Sha1 = "0123456789abcdef0123456789abcdef01234567";
    private const string Sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string OriginUrl = "https://github.com/swigerb/copilot-ledger.git";

    [Fact]
    public void Resolve_StandardRepository_UsesLooseRefAndConfig()
    {
        using TempDirectory temp = new();
        string repo = CreateDirectory(temp, "repo");
        string git = CreateDirectory(repo, ".git");
        Write(git, "HEAD", "ref: refs/heads/feature/ledger\n");
        Write(git, "refs/heads/feature/ledger", Sha1 + "\n");
        WriteOriginConfig(git);
        string working = CreateDirectory(repo, "src", "deep");

        GitWorkspaceContext? result = Resolve(temp, working);

        Assert.NotNull(result);
        Assert.Equal(Path.GetFullPath(repo), Path.GetFullPath(result.RepositoryRoot));
        Assert.Equal(Path.GetFullPath(git), Path.GetFullPath(result.GitDirectory));
        Assert.Equal("feature/ledger", result.BranchName);
        Assert.Equal(Sha1, result.HeadCommitSha);
        Assert.Equal(OriginUrl, result.RemoteOriginUrl);
        Assert.Equal("swigerb/copilot-ledger", result.RepositorySlug);
        Assert.False(result.IsDetachedHead);
    }

    [Fact]
    public void Resolve_PackedRef_ReturnsSha()
    {
        using TempDirectory temp = new();
        string repo = CreateDirectory(temp, "repo");
        string git = CreateDirectory(repo, ".git");
        Write(git, "HEAD", "ref: refs/heads/main\n");
        Write(
            git,
            "packed-refs",
            """
            # pack-refs with: peeled fully-peeled sorted
            fedcba9876543210fedcba9876543210fedcba98 refs/heads/develop
            0123456789abcdef0123456789abcdef01234567 refs/heads/main
            1111111111111111111111111111111111111111 refs/tags/v1.0.0
            ^2222222222222222222222222222222222222222

            """);

        GitWorkspaceContext? result = Resolve(temp, repo);
        Assert.Equal("main", result!.BranchName);
        Assert.Equal(Sha1, result.HeadCommitSha);
    }

    [Theory]
    [InlineData(Sha1)]
    [InlineData(Sha256)]
    public void Resolve_DetachedHead_NormalizesSha(string sha)
    {
        using TempDirectory temp = new();
        string repo = CreateDirectory(temp, "repo");
        string git = CreateDirectory(repo, ".git");
        Write(git, "HEAD", sha + "\n");

        GitWorkspaceContext? result = Resolve(temp, repo);
        Assert.Null(result!.BranchName);
        Assert.Equal(sha, result.HeadCommitSha);
        Assert.True(result.IsDetachedHead);
    }

    [Fact]
    public void Resolve_UnbornBranch_HasNullCommit()
    {
        using TempDirectory temp = new();
        string repo = CreateDirectory(temp, "repo");
        string git = CreateDirectory(repo, ".git");
        Write(git, "HEAD", "ref: refs/heads/main\n");

        GitWorkspaceContext? result = Resolve(temp, repo);
        Assert.Equal("main", result!.BranchName);
        Assert.Null(result.HeadCommitSha);
        Assert.False(result.IsDetachedHead);
    }

    [Fact]
    public void Resolve_LinkedWorktree_UsesCommonDirectory()
    {
        using TempDirectory temp = new();
        string main = CreateDirectory(temp, "main");
        string mainGit = CreateDirectory(main, ".git");
        Write(mainGit, "HEAD", "ref: refs/heads/main\n");
        WriteOriginConfig(mainGit);
        Write(mainGit, "refs/heads/wt-branch", Sha1 + "\n");
        string worktreeGit = CreateDirectory(mainGit, "worktrees", "wt");
        Write(worktreeGit, "HEAD", "ref: refs/heads/wt-branch\n");
        Write(worktreeGit, "commondir", "../..\n");
        string worktree = CreateDirectory(temp, "wt");
        Write(worktree, ".git", "gitdir: " + worktreeGit + "\n");

        GitWorkspaceContext? result = Resolve(temp, worktree);

        Assert.NotNull(result);
        Assert.Equal(Path.GetFullPath(worktree), Path.GetFullPath(result.RepositoryRoot));
        Assert.Equal(Path.GetFullPath(worktreeGit), Path.GetFullPath(result.GitDirectory));
        Assert.Equal("wt-branch", result.BranchName);
        Assert.Equal(Sha1, result.HeadCommitSha);
        Assert.Equal("swigerb/copilot-ledger", result.RepositorySlug);
    }

    [Fact]
    public void Resolve_ConfigSyntax_HandlesCaseAndComments()
    {
        using TempDirectory temp = new();
        string repo = CreateDirectory(temp, "repo");
        string git = CreateDirectory(repo, ".git");
        Write(git, "HEAD", "ref: refs/heads/main\n");
        Write(git, "refs/heads/main", Sha1 + "\n");
        Write(
            git,
            "config",
            "# comment\n" +
            "; comment\n" +
            "[core]\n" +
            "\trepositoryformatversion = 0\n" +
            "[remote \"upstream\"]\n" +
            "\turl = https://github.com/contoso/upstream.git\n" +
            "[Remote \"origin\"]\n" +
            "\tURL = \"https://github.com/swigerb/copilot-ledger.git\" ; trailing comment\n" +
            "\tfetch = +refs/heads/*:refs/remotes/origin/*\n");

        GitWorkspaceContext? result = Resolve(temp, repo);
        Assert.Equal(OriginUrl, result!.RemoteOriginUrl);
        Assert.Equal("swigerb/copilot-ledger", result.RepositorySlug);
    }

    [Fact]
    public void Resolve_SanitizesOriginCredentials()
    {
        using TempDirectory temp = new();
        string repo = CreateDirectory(temp, "repo");
        string git = CreateDirectory(repo, ".git");
        Write(git, "HEAD", "ref: refs/heads/main\n");
        Write(git, "refs/heads/main", Sha1 + "\n");
        Write(
            git,
            "config",
            "[remote \"origin\"]\n\turl = https://user:s3cr3t@github.com/swigerb/copilot-ledger.git\n");

        GitWorkspaceContext? result = Resolve(temp, repo);
        Assert.Equal(OriginUrl, result!.RemoteOriginUrl);
        Assert.DoesNotContain("s3cr3t", result.RemoteOriginUrl);
    }

    [Fact]
    public void Resolve_OutsideRepository_ReturnsNull()
    {
        using TempDirectory temp = new();
        string plain = CreateDirectory(temp, "plain", "sub");
        Assert.Null(Resolve(temp, plain));
    }

    [Fact]
    public void Resolve_MissingWorkingDirectory_ReturnsNull()
    {
        using TempDirectory temp = new();
        Assert.Null(Resolve(temp, System.IO.Path.Combine(temp.Path, "does-not-exist")));
    }

    [Fact]
    public void Resolve_ConfigWithoutOrigin_ReturnsContextWithoutRemote()
    {
        using TempDirectory temp = new();
        string repo = CreateDirectory(temp, "repo");
        string git = CreateDirectory(repo, ".git");
        Write(git, "HEAD", "ref: refs/heads/main\n");
        Write(git, "refs/heads/main", Sha1 + "\n");
        Write(git, "config", "[core]\n");

        GitWorkspaceContext? result = Resolve(temp, repo);
        Assert.Equal("main", result!.BranchName);
        Assert.Equal(Sha1, result.HeadCommitSha);
        Assert.Null(result.RemoteOriginUrl);
        Assert.Null(result.RepositorySlug);
    }

    [Fact]
    public void Resolve_FilesystemRootCeiling_StillFindsRepository()
    {
        using TempDirectory temp = new();
        string repo = CreateDirectory(temp, "repo");
        string git = CreateDirectory(repo, ".git");
        Write(git, "HEAD", "ref: refs/heads/main\n");
        Write(git, "refs/heads/main", Sha1 + "\n");
        string ceiling = Path.GetPathRoot(temp.Path)!;

        GitWorkspaceContext? result = new FileSystemGitContextResolver(ceiling).Resolve(repo);

        Assert.NotNull(result);
        Assert.Equal(Path.GetFullPath(repo), Path.GetFullPath(result.RepositoryRoot));
    }

    [Fact]
    public void Resolve_EmptyHead_ReturnsContextWithoutBranchOrSha()
    {
        using TempDirectory temp = new();
        string repo = CreateDirectory(temp, "repo");
        string git = CreateDirectory(repo, ".git");
        Write(git, "HEAD", string.Empty);

        GitWorkspaceContext? result = Resolve(temp, repo);

        Assert.NotNull(result);
        Assert.Null(result.BranchName);
        Assert.Null(result.HeadCommitSha);
    }

    [Fact]
    public void Resolve_GitFileWithLeadingSpaceBeforeGitdir_ReturnsNull()
    {
        using TempDirectory temp = new();
        string repo = CreateDirectory(temp, "repo");
        string target = CreateDirectory(temp, "target");
        Write(target, "HEAD", "ref: refs/heads/main\n");
        Write(repo, ".git", " gitdir: " + target + "\n");

        Assert.Null(Resolve(temp, repo));
    }

    [Fact]
    public void Resolve_PullRequestCheckout_ReadsTheNumberFromTheMergeRef()
    {
        using TempDirectory temp = new();
        using TempDirectory plain = new();
        string repo = GitFixture.Create(temp.Path, pullRequest: 42);
        string other = GitFixture.Create(plain.Path);

        GitWorkspaceContext? result = Resolve(temp, Path.Combine(repo, "src"));
        GitWorkspaceContext? withoutPullRequest = Resolve(plain, other);

        Assert.NotNull(result);
        Assert.Equal("feature/ledger", result.BranchName);
        Assert.Equal("swigerb/copilot-ledger", result.RepositorySlug);
        Assert.Equal(42, result.PullRequestNumber);
        Assert.NotNull(withoutPullRequest);
        Assert.Null(withoutPullRequest.PullRequestNumber);
    }

    public static TheoryData<string, int?> MergeConfigs => new()
    {
        { "[branch \"feature/x\"]\n\tremote = origin\n\tmerge = refs/pull/42/head\n", 42 },
        { "[branch \"feature/x\"]\n\tremote = origin\n\tmerge = refs/heads/feature/x\n", null },
        { "[branch \"other\"]\n\tmerge = refs/pull/42/head\n", null },
        { "[branch \"Feature/X\"]\n\tmerge = refs/pull/42/head\n", null },
        { "[Branch \"feature/x\"]\n\tMERGE = \"refs/pull/7/head\" ; gh pr checkout\n", 7 },
        { "[branch \"feature/x\"]\n\tmerge = refs/pull/42/head\n\tmerge = refs/heads/feature/x\n", null },
        { "[branch \"feature/x\"]\n\tmerge = refs/pull/42/head\n[branch \"feature/x\"]\n\tremote = origin\n", 42 },
        { "[branch \"feature/x\"]\n\tmerge = refs/pull/42/head\n[branch \"feature/x\"]\n\tmerge = refs/pull/43/head\n", 43 },
        { "[branch \"feature/x\"]\n\tmerge = refs/pull/2147483647/head\n", 2147483647 },
        { "[branch \"feature/x\"]\n\tmerge = refs/pull/2147483648/head\n", null },
        { "[branch \"feature/x\"]\n\tmerge = refs/pull/0/head\n", null },
        { "[branch \"feature/x\"]\n\tmerge = refs/pull/042/head\n", null },
        { "[branch \"feature/x\"]\n\tmerge = refs/pull/-1/head\n", null },
        { "[branch \"feature/x\"]\n\tmerge = refs/pull/+1/head\n", null },
        { "[branch \"feature/x\"]\n\tmerge = refs/pull/4 2/head\n", null },
        { "[branch \"feature/x\"]\n\tmerge = refs/pull/42/merge\n", null },
        { "[branch \"feature/x\"]\n\tmerge = refs/pull/head\n", null },
        { "[branch \"feature/x\"]\n\tmerge = refs/pull//head\n", null },
        { "[branch \"feature/x\"]\n\tmerge = \"refs/pull/42/head\n", null },
    };

    [Theory]
    [MemberData(nameof(MergeConfigs))]
    public void Resolve_MergeConfig_YieldsOnlyExplicitPullRequestRefs(string branchConfig, int? expected)
    {
        using TempDirectory temp = new();
        string repo = CreateDirectory(temp, "repo");
        string git = CreateDirectory(repo, ".git");
        Write(git, "HEAD", "ref: refs/heads/feature/x\n");
        Write(git, "refs/heads/feature/x", Sha1 + "\n");
        Write(git, "config", $"[remote \"origin\"]\n\turl = {OriginUrl}\n" + branchConfig);

        GitWorkspaceContext? result = Resolve(temp, repo);

        Assert.NotNull(result);
        Assert.Equal(expected, result.PullRequestNumber);
        Assert.Equal(OriginUrl, result.RemoteOriginUrl);
        Assert.Equal("swigerb/copilot-ledger", result.RepositorySlug);
    }

    [Fact]
    public void Resolve_DetachedHead_HasNoPullRequest()
    {
        using TempDirectory temp = new();
        string repo = CreateDirectory(temp, "repo");
        string git = CreateDirectory(repo, ".git");
        Write(git, "HEAD", Sha1 + "\n");
        Write(git, "config", "[branch \"feature/x\"]\n\tmerge = refs/pull/42/head\n");

        GitWorkspaceContext? result = Resolve(temp, repo);

        Assert.NotNull(result);
        Assert.True(result.IsDetachedHead);
        Assert.Null(result.PullRequestNumber);
    }

    [Fact]
    public void Resolve_LinkedWorktree_ReadsTheMergeRefFromTheCommonConfig()
    {
        using TempDirectory temp = new();
        string main = CreateDirectory(temp, "main");
        string mainGit = CreateDirectory(main, ".git");
        Write(mainGit, "HEAD", "ref: refs/heads/main\n");
        Write(
            mainGit,
            "config",
            $"[remote \"origin\"]\n\turl = {OriginUrl}\n[branch \"wt-branch\"]\n\tremote = origin\n\tmerge = refs/pull/9/head\n");
        Write(mainGit, "refs/heads/wt-branch", Sha1 + "\n");
        string worktreeGit = CreateDirectory(mainGit, "worktrees", "wt");
        Write(worktreeGit, "HEAD", "ref: refs/heads/wt-branch\n");
        Write(worktreeGit, "commondir", "../..\n");
        string worktree = CreateDirectory(temp, "wt");
        Write(worktree, ".git", "gitdir: " + worktreeGit + "\n");

        GitWorkspaceContext? worktreeResult = Resolve(temp, worktree);
        GitWorkspaceContext? mainResult = Resolve(temp, main);

        Assert.Equal(9, worktreeResult!.PullRequestNumber);
        Assert.Equal("main", mainResult!.BranchName);
        Assert.Null(mainResult.PullRequestNumber);
    }

    private static GitWorkspaceContext? Resolve(TempDirectory temp, string workingDirectory) =>
        new FileSystemGitContextResolver(temp.Path).Resolve(workingDirectory);

    private static string CreateDirectory(TempDirectory temp, params string[] segments) =>
        CreateDirectory(temp.Path, segments);

    private static string CreateDirectory(string root, params string[] segments)
    {
        string path = segments.Aggregate(root, Path.Combine);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Write(string root, string relativePath, string contents)
    {
        string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    private static void WriteOriginConfig(string gitDirectory) =>
        Write(
            gitDirectory,
            "config",
            $"[remote \"origin\"]\n\turl = {OriginUrl}\n");
}
