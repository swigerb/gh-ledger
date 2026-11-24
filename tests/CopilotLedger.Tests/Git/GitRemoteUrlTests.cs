using CopilotLedger.Core.Git;

namespace CopilotLedger.Tests.Git;

public sealed class GitRemoteUrlTests
{
    public static TheoryData<string, string, string?> Cases => new()
    {
        { "https://github.com/swigerb/copilot-ledger.git", "https://github.com/swigerb/copilot-ledger.git", "swigerb/copilot-ledger" },
        { "https://github.com/swigerb/copilot-ledger", "https://github.com/swigerb/copilot-ledger", "swigerb/copilot-ledger" },
        { "https://github.com/swigerb/copilot-ledger/", "https://github.com/swigerb/copilot-ledger/", "swigerb/copilot-ledger" },
        { "https://x-access-token:s3cr3t@github.com/swigerb/copilot-ledger.git", "https://github.com/swigerb/copilot-ledger.git", "swigerb/copilot-ledger" },
        { "git@github.com:swigerb/copilot-ledger.git", "git@github.com:swigerb/copilot-ledger.git", "swigerb/copilot-ledger" },
        { "ssh://git@github.com/swigerb/copilot-ledger.git", "ssh://git@github.com/swigerb/copilot-ledger.git", "swigerb/copilot-ledger" },
        { "https://ghe.contoso.com/platform/billing.git", "https://ghe.contoso.com/platform/billing.git", "platform/billing" },
        { "https://dev.azure.com/contoso/project/_git/repo", "https://dev.azure.com/contoso/project/_git/repo", null },
        { @"C:\src\mirror.git", @"C:\src\mirror.git", null },
        { "file:///srv/git/mirror.git", "file:///srv/git/mirror.git", null },
        { "/srv/git/mirror.git", "/srv/git/mirror.git", null },
        { "https://git.example/o/r.git?token=CANARY-TOKEN-7c1d", "https://git.example/o/r.git?token=CANARY-TOKEN-7c1d", "o/r" },
        { "https://git.example/o/r#CANARY-TOKEN-7c1d", "https://git.example/o/r#CANARY-TOKEN-7c1d", "o/r" },
        { "git@git.example:o/r.git?token=CANARY-TOKEN-7c1d", "git@git.example:o/r.git?token=CANARY-TOKEN-7c1d", "o/r" },
        { "https://git.example/o/r%3Ftoken.git", "https://git.example/o/r%3Ftoken.git", null },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void UrlHelpers_ReturnExpectedValues(string remote, string sanitized, string? slug)
    {
        Assert.Equal(sanitized, GitRemoteUrl.Sanitize(remote));
        Assert.Equal(slug, GitRemoteUrl.GetRepositorySlug(remote));
    }
}
