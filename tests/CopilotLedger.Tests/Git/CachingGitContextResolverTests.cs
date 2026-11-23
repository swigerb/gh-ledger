using CopilotLedger.Core.Git;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Git;

public sealed class CachingGitContextResolverTests
{
    [Fact]
    public void CG1_Hit_ReusesCachedInstance()
    {
        ManualTimeProvider time = Time();
        FakeResolver inner = new(Context("/r"));
        CachingGitContextResolver resolver = new(inner, time);

        GitWorkspaceContext? first = resolver.Resolve("/r");
        time.Advance(TimeSpan.FromSeconds(4));
        GitWorkspaceContext? second = resolver.Resolve("/r");
        Assert.Same(first, second);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public void CG2_ExpiredEntry_RefreshesInnerResolver()
    {
        ManualTimeProvider time = Time();
        FakeResolver inner = new(Context("/r"));
        CachingGitContextResolver resolver = new(inner, time);

        resolver.Resolve("/r");
        time.Advance(TimeSpan.FromSeconds(4));
        resolver.Resolve("/r");
        time.Advance(TimeSpan.FromSeconds(5));
        resolver.Resolve("/r");
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public void CG3_NullResultsAndDifferentKeys_AreCachedSeparately()
    {
        ManualTimeProvider time = new(
            new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero));
        FakeResolver inner = new(null);
        CachingGitContextResolver resolver = new(inner, time);

        Assert.Null(resolver.Resolve("/a"));
        Assert.Null(resolver.Resolve("/a"));
        Assert.Null(resolver.Resolve("/b"));
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public void CG4_CachedContext_KeepsItsPullRequestNumber()
    {
        ManualTimeProvider time = Time();
        FakeResolver inner = new(Context("/r") with { PullRequestNumber = 42 });
        CachingGitContextResolver resolver = new(inner, time);

        Assert.Equal(42, resolver.Resolve("/r")!.PullRequestNumber);
        time.Advance(TimeSpan.FromSeconds(4));
        Assert.Equal(42, resolver.Resolve("/r")!.PullRequestNumber);
        Assert.Equal(1, inner.Calls);
    }

    private static GitWorkspaceContext Context(string root) =>
        new(root, Path.Combine(root, ".git"), "main", null, null, null);

    private static ManualTimeProvider Time() => new(
        new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero));

    private sealed class FakeResolver(GitWorkspaceContext? result) : IGitContextResolver
    {
        public int Calls { get; private set; }

        public GitWorkspaceContext? Resolve(string workingDirectory)
        {
            Calls++;
            return result;
        }
    }
}
