using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Tests.Docs;

public sealed class SprintFifteenDocsTests
{
    [Fact]
    public void DF1_TheReferenceDescribesMcpToolNames()
    {
        string cli = Text("docs", "cli.md");

        Assert.Contains("### MCP tool names", cli, StringComparison.Ordinal);
        Assert.Contains(
            McpToolNames.ShortForm(McpToolNames.Hash("computer-use", "exec_js")),
            cli,
            StringComparison.Ordinal);
        Assert.Contains("[MCP tool names](#mcp-tool-names)", cli, StringComparison.Ordinal);
        Assert.DoesNotContain("129-character", cli, StringComparison.Ordinal);
    }

    [Fact]
    public void DF2_TheReferenceSaysWhatTheInstallDryRunReportsForTheDaemon()
    {
        string cli = Text("docs", "cli.md");

        Assert.DoesNotContain(
            "but always says `Would start the gh-ledger daemon.`",
            cli,
            StringComparison.Ordinal);
        Assert.Contains(
            "`Wouldn't start the gh-ledger daemon:`",
            cli,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DF3_HelpIsNoLongerDescribedAsNamingGhLedger()
    {
        Assert.DoesNotContain(
            "`--help` usage lines say",
            Text("docs", "cli.md"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "The help shows the program's own name",
            Text("docs", "quickstart.md"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void DF4_TheGuideShowsTheExtensionsCommandInHints()
    {
        string guide = Text("docs", "guide.md");

        Assert.Contains(
            "Reset a trip with: gh ledger breaker reset --id <id>",
            guide,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Where a message or `--help` says",
            guide,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "gh-ledger breaker reset",
            guide,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DF5_TheDryRunAndTheShortFormAreDescribedPrecisely()
    {
        string[] lines = Text("docs", "cli.md").Split('\n');

        string dryRun = Assert.Single(
            lines,
            line => line.StartsWith(
                "Then restart the GitHub Copilot app, VS Code, and open terminals. `--dry-run` prints",
                StringComparison.Ordinal));
        Assert.Contains(
            "`Wouldn't start the gh-ledger daemon:` with the reason and hint that `daemon start` gives, and exits with 1, as `install` would",
            dryRun,
            StringComparison.Ordinal);
        Assert.Contains(
            "It starts nothing, so it can't tell whether the daemon would fail to start",
            dryRun,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "exits with the code that `install` would",
            dryRun,
            StringComparison.Ordinal);

        string shortForm = McpToolNames.ShortForm(McpToolNames.Hash("computer-use", "exec_js"));
        string troubleshooting = Assert.Single(
            lines,
            line => line.StartsWith("- **A tool shows as `" + shortForm + "`.**", StringComparison.Ordinal));
        Assert.Contains(
            "by two SHA-256 hashes, of its server's name and of its own name",
            troubleshooting,
            StringComparison.Ordinal);
        Assert.Contains(
            "the first 8 digits of each",
            troubleshooting,
            StringComparison.Ordinal);
        Assert.Contains(
            "until an import learns the tool's name from a session log that ran it",
            troubleshooting,
            StringComparison.Ordinal);
        Assert.Contains(
            "run `gh ledger import --all`",
            troubleshooting,
            StringComparison.Ordinal);
        Assert.Contains(
            "Logs that import doesn't read",
            troubleshooting,
            StringComparison.Ordinal);
        Assert.Contains(
            "[MCP tool names](#mcp-tool-names)",
            troubleshooting,
            StringComparison.Ordinal);
    }

    private static string Text(params string[] parts) =>
        File.ReadAllText(RepositoryFile(parts)).ReplaceLineEndings("\n");

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
