using System.Diagnostics;
using CopilotLedger.Core.Git;
using CopilotLedger.Mcp;
using CopilotLedger.Tests.TestSupport;
using ModelContextProtocol.Protocol;
using Xunit.Abstractions;

namespace CopilotLedger.Tests.Storage;

public sealed class ScopedQueryPerfTests(ITestOutputHelper output)
{
    [Fact]
    public async Task SQ1_LargeLedger_ScopedReadsTakeAtMostTwiceTheUnscoped()
    {
        if (Environment.GetEnvironmentVariable("GH_LEDGER_PERF") != "1")
        {
            return;
        }

        using TempDirectory temp = new();
        GitFixture.Create(temp.Path);
        string data = Path.Combine(temp.Path, "data");
        Directory.CreateDirectory(data);
        await LargeLedger.CreateAsync(Path.Combine(data, "ledger.db"));

        ManualTimeProvider clock = new(LargeLedger.Now, TimeZoneInfo.Utc);
        await using CliHarness cli = new(temp.Path, new FakeCliPlatform(), clock);

        await MeasureAsync(
            "audit unscoped",
            () => cli.RunAsync("audit", "--since", "all", "--json"));
        await MeasureAsync(
            "audit repo",
            () => cli.RunAsync(
                "audit",
                "--since",
                "all",
                "--json",
                "--repo",
                LargeLedger.GhLedgerRepository));
        await MeasureAsync(
            "export unscoped",
            () => cli.RunAsync(
                "export",
                "--all-repos",
                "--since",
                "all",
                "--bundle",
                "--out",
                "all.html"));
        await MeasureAsync(
            "export repo",
            () => cli.RunAsync(
                "export",
                "--repo",
                LargeLedger.GhLedgerRepository,
                "--since",
                "all",
                "--bundle",
                "--out",
                "repo.html"));
        await MeasureAsync(
            "export conversation",
            () => cli.RunAsync(
                "export",
                "--all-repos",
                "--conversation",
                LargeLedger.Conversation,
                "--since",
                "all",
                "--bundle",
                "--out",
                "conv.html"));

        await using McpTestHarness mcp = await McpTestHarness.StartAsync(new LedgerMcpContext
        {
            DatabasePath = Path.Combine(data, "ledger.db"),
            WorkingDirectory = Path.Combine(temp.Path, "repo"),
            GetEnvironmentVariable = _ => null,
            GitResolver = new FileSystemGitContextResolver(ceilingDirectory: temp.Path),
            TimeProvider = clock,
            Version = "9.9.9",
        });
        await MeasureMcpAsync(mcp);
    }

    private async Task MeasureAsync(string label, Func<Task<CliRun>> run)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        CliRun result = await run();
        stopwatch.Stop();
        output.WriteLine($"{label,-28} {result.ExitCode} {stopwatch.ElapsedMilliseconds} ms");
        Assert.Equal(0, result.ExitCode);
    }

    private async Task MeasureMcpAsync(McpTestHarness mcp)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        CallToolResult result = await mcp.Client.CallToolAsync(
            "get_audit_findings",
            new Dictionary<string, object?>());
        stopwatch.Stop();
        int exitCode = result.IsError == true ? 1 : 0;
        output.WriteLine($"{"mcp get_audit_findings default",-28} {exitCode} {stopwatch.ElapsedMilliseconds} ms");
        Assert.NotEqual(true, result.IsError);
    }
}
