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
