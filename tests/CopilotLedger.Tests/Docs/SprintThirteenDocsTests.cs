namespace CopilotLedger.Tests.Docs;

public sealed class SprintThirteenDocsTests
{
    [Fact]
    public void DC1_TheDocsMatchTheSprintsBehavior()
    {
        string cli = File.ReadAllText(RepositoryFile("docs", "cli.md")).ReplaceLineEndings("\n");
        string guide = File.ReadAllText(RepositoryFile("docs", "guide.md")).ReplaceLineEndings("\n");

        Assert.DoesNotContain("Run: gh-ledger ", cli);
        Assert.DoesNotContain("Run: gh-ledger ", guide);
        Assert.Contains(
            "gh-ledger daemon not started: this terminal runs in a Windows job that stops the processes it started when it closes, so the daemon would stop with it.\n" +
            "Run gh ledger daemon start from a regular terminal, or sign out and back in to start it from the sign-in item.",
            cli);
        Assert.Contains(
            "| `context_bloat` | Requests of at least 50,000 input tokens with at least 200 times as much input as output, or no output: one finding for each agent invocation and model (or prompt chain, agent, and model when the invocation is unknown), and a separate one for its requests without output | Info only | | None |",
            cli);
        Assert.Contains(
            "| `tool_loop` | One agent invocation calling the same tool again and again with the same outcome | 8 calls in a row | 16 calls, when the estimated impact is known and more than $0 | The billed cost of the invocation's requests between the first call and the last |",
            cli);
        Assert.Contains(
            "By rule: failed_tool_retry 1, context_bloat 1, context_growth 1, cache_miss_streak 1, premium_model_trivial_turn 1, velocity_near_breaker 1.",
            guide);
        Assert.Contains("Run: gh ledger budget list", guide);
    }

    private static string RepositoryFile(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CopilotLedger.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine([directory.FullName, .. parts]);
    }
}
