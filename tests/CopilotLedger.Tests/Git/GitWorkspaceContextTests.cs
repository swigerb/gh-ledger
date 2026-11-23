using CopilotLedger.Core.Git;

namespace CopilotLedger.Tests.Git;

public sealed class GitWorkspaceContextTests
{
    public static TheoryData<string?, string?, bool> IsDetachedHeadCases => new()
    {
        { null, "0123456789012345678901234567890123456789", true },
        { "main", "0123456789012345678901234567890123456789", false },
        { "main", null, false },
        { null, null, false },
    };

    [Theory]
    [MemberData(nameof(IsDetachedHeadCases))]
    public void IsDetachedHead_ReturnsExpectedValue(string? branchName, string? headCommitSha, bool expected)
    {
        var context = new GitWorkspaceContext(
            RepositoryRoot: "/repo",
            GitDirectory: "/repo/.git",
            BranchName: branchName,
            HeadCommitSha: headCommitSha,
            RemoteOriginUrl: null,
            RepositorySlug: null);

        Assert.Equal(expected, context.IsDetachedHead);
    }

    [Fact]
    public void PullRequestNumber_DefaultsToNullAndTakesPartInEquality()
    {
        GitWorkspaceContext context = new("/repo", "/repo/.git", "feature/x", null, null, null);
        GitWorkspaceContext pullRequest = context with { PullRequestNumber = 42 };

        Assert.Null(context.PullRequestNumber);
        Assert.Equal(42, pullRequest.PullRequestNumber);
        Assert.NotEqual(context, pullRequest);
        Assert.Equal(pullRequest, context with { PullRequestNumber = 42 });
    }
}
