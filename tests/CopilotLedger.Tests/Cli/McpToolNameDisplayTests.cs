using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using CopilotLedger.Core.Git;
using CopilotLedger.Mcp;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using ModelContextProtocol.Protocol;

namespace CopilotLedger.Tests.Cli;

public sealed class McpToolNameDisplayTests
{
    private const string H = "48484848484848484848484848484848";
    private const string ExecJs =
        "10be6beef496b89dae799c37bd04e5eba2a6264a0a4d605f5661b50d7dc4d1ea/8453dc19fda259c463d7aaffb07becf26b7ce2cfc690a2b0a957e201653dc85a";
    private const string Short = "MCP tool 10be6bee/8453dc19";
    private const string Learned = "computer-use-exec_js";

    [Fact]
    public async Task MD1_AuditJson_ShowsTheShortForm_ThenTheLearnedName_WithTheSameFindings()
    {
        using TempDirectory temp = new();
        await SeedAsync(temp.Path);
        await using CliHarness cli = Harness(temp.Path);

        CliRun before = await cli.RunAsync("audit", "--json");
        await LearnAsync(cli);
        CliRun after = await cli.RunAsync("audit", "--json");

        Assert.Equal(0, before.ExitCode);
        Assert.Equal(0, after.ExitCode);
        using JsonDocument beforeJson = JsonDocument.Parse(before.Out);
        using JsonDocument afterJson = JsonDocument.Parse(after.Out);
        AssertFinding(HFinding(beforeJson.RootElement.GetProperty("findings")), Short);
        AssertFinding(HFinding(afterJson.RootElement.GetProperty("findings")), Learned);
        Assert.Equal(
            before.Out.Replace("\"tool\": \"" + Short + "\"", "\"tool\": \"" + Learned + "\"", StringComparison.Ordinal),
            after.Out);
        Assert.DoesNotContain(ExecJs, before.Out, StringComparison.Ordinal);
        Assert.DoesNotContain(ExecJs, after.Out, StringComparison.Ordinal);
        Assert.Equal(
            [ExecJs],
            await LedgerSql.RowsAsync(
                Path.Combine(temp.Path, "data", "ledger.db"),
                $"SELECT DISTINCT tool_name FROM span_links WHERE trace_id = '{H}' AND operation_name = 'execute_tool';"));
    }

    [Fact]
    public async Task MD2_TheAuditTable_NamesTheTool()
    {
        using TempDirectory temp = new();
        await SeedAsync(temp.Path);
        await using CliHarness cli = Harness(temp.Path);

        CliRun before = await cli.RunAsync("audit");
        await LearnAsync(cli);
        CliRun after = await cli.RunAsync("audit");

        Assert.Equal(0, before.ExitCode);
        Assert.Equal(0, after.ExitCode);
        Assert.Contains(
            "swigerb/copilot-ledger @ feature/ledger, (none), MCP tool 10be6bee/8453dc19",
            before.Out,
            StringComparison.Ordinal);
        Assert.Contains(
            "swigerb/copilot-ledger @ feature/ledger, (none), computer-use-exec_js",
            after.Out,
            StringComparison.Ordinal);
        Assert.DoesNotContain(Short, after.Out, StringComparison.Ordinal);
        Assert.DoesNotContain(ExecJs, before.Out, StringComparison.Ordinal);
        Assert.DoesNotContain(ExecJs, after.Out, StringComparison.Ordinal);
        Assert.DoesNotContain(ExecJs[..64], before.Out, StringComparison.Ordinal);
        Assert.DoesNotContain(ExecJs[..64], after.Out, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MD3_TheLocalModelPayload_NamesTheTool()
    {
        using TempDirectory temp = new();
        await SeedAsync(temp.Path);
        await using CliHarness cli = Harness(temp.Path);
        string[] args =
        [
            "audit",
            "--local-llm",
            "http://127.0.0.1:11434/v1",
            "--local-llm-model",
            "llama3.3",
            "--show-llm-payload",
        ];

        CliRun before = await cli.RunAsync(args);
        await LearnAsync(cli);
        CliRun after = await cli.RunAsync(args);

        Assert.Equal(0, before.ExitCode);
        Assert.Equal(0, after.ExitCode);
        Assert.Equal(["MCP tool 10be6bee/8453dc19", "bash"], PayloadTools(before.Out));
        Assert.Equal(["bash", "computer-use-exec_js"], PayloadTools(after.Out));
        Assert.DoesNotContain(ExecJs[..64], before.Out, StringComparison.Ordinal);
        Assert.DoesNotContain(ExecJs[..64], after.Out, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MD4_TheExport_NamesTheTool_OnTheTimelineInTheBundleAndInTheFindings()
    {
        using TempDirectory temp = new();
        await SeedAsync(temp.Path);
        await using CliHarness cli = Harness(temp.Path);

        CliRun before = await cli.RunAsync("export", "--all-repos", "--since", "7d", "--bundle");
        Assert.Equal(0, before.ExitCode);
        AssertExport(temp.Path, Short);

        await LearnAsync(cli);
        CliRun after = await cli.RunAsync("export", "--all-repos", "--since", "7d", "--bundle", "--force");
        Assert.Equal(0, after.ExitCode);
        AssertExport(temp.Path, Learned);
    }

    [Fact]
    public async Task MD5_GetAuditFindings_NamesTheTool()
    {
        using TempDirectory temp = new();
        GitFixture.Create(temp.Path);
        await SeedAsync(temp.Path);
        await using McpTestHarness mcp = await McpTestHarness.StartAsync(McpContext(temp.Path));
        await using CliHarness cli = Harness(temp.Path);

        CallToolResult beforeResult = await mcp.Client.CallToolAsync("get_audit_findings", new Dictionary<string, object?>());
        string before = McpText(beforeResult);
        await LearnAsync(cli);
        CallToolResult afterResult = await mcp.Client.CallToolAsync("get_audit_findings", new Dictionary<string, object?>());
        string after = McpText(afterResult);

        Assert.NotEqual(true, beforeResult.IsError);
        Assert.NotEqual(true, afterResult.IsError);
        using JsonDocument beforeJson = JsonDocument.Parse(before);
        using JsonDocument afterJson = JsonDocument.Parse(after);
        Assert.Equal(Short, HFinding(beforeJson.RootElement.GetProperty("findings")).GetProperty("scope").GetProperty("tool").GetString());
        Assert.Equal(Learned, HFinding(afterJson.RootElement.GetProperty("findings")).GetProperty("scope").GetProperty("tool").GetString());
        Assert.DoesNotContain(ExecJs, before, StringComparison.Ordinal);
        Assert.DoesNotContain(ExecJs, after, StringComparison.Ordinal);
    }

    private static CliHarness Harness(string root) =>
        new(root, new FakeCliPlatform(), Clock());

    private static ManualTimeProvider Clock() =>
        new(AuditSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5);

    private static async Task SeedAsync(string root)
    {
        SqliteLedgerStore store = await AuditSeeds.CreateAsync(root);
        AuditSeeds.LiveTrace h = new(
            H,
            "conv-h",
            GitFixture.Feature(root) with { PullRequestNumber = 42 });
        string h0 = h.Agent(0, AuditSeeds.At("19:00:00"));
        h.Request(1, h0, AuditSeeds.At("19:00:00"), "claude-haiku-4.5", 10_000, 8_000, 300);
        h.Tool(1, h0, AuditSeeds.At("19:00:10"), ExecJs, 1, 2);
        h.Request(2, h0, AuditSeeds.At("19:00:15"), "claude-haiku-4.5", 10_000, 8_000, 300);
        h.Tool(2, h0, AuditSeeds.At("19:00:20"), ExecJs, 1, 2);
        h.Request(3, h0, AuditSeeds.At("19:00:25"), "claude-haiku-4.5", 10_000, 8_000, 300);
        h.Tool(3, h0, AuditSeeds.At("19:00:30"), ExecJs, 1, 2);
        await h.RecordAsync(store);
    }

    private static async Task LearnAsync(CliHarness cli)
    {
        new CopilotSessionLogs("s-mcp")
            .ToolStart("toolu-1", Learned, mcpServerName: "computer-use", mcpToolName: "exec_js")
            .ToolComplete("toolu-1", true)
            .Write(cli.Context.CopilotHome);

        CliRun imported = await cli.RunAsync("import", "--all");
        Assert.Equal(0, imported.ExitCode);
    }

    private static JsonElement HFinding(JsonElement findings) =>
        Assert.Single(
            findings.EnumerateArray(),
            finding => finding.GetProperty("scope").GetProperty("traceId").GetString() == H);

    private static void AssertFinding(JsonElement finding, string tool)
    {
        Assert.Equal("failed_tool_retry", finding.GetProperty("ruleId").GetString());
        Assert.Equal("warning", finding.GetProperty("severity").GetString());
        JsonElement scope = finding.GetProperty("scope");
        Assert.Equal("swigerb/copilot-ledger", scope.GetProperty("repository").GetString());
        Assert.Equal("feature/ledger", scope.GetProperty("branch").GetString());
        Assert.Equal(42, scope.GetProperty("pullRequest").GetInt32());
        Assert.Equal("(none)", scope.GetProperty("agent").GetString());
        Assert.Equal(tool, scope.GetProperty("tool").GetString());
        Assert.Equal(
            [
                ("failures", 3m, "calls"),
                ("requests", 2m, "requests"),
                ("breakerChurnFailures", 5m, "calls"),
            ],
            finding.GetProperty("metrics").EnumerateArray().Select(metric =>
                (
                    metric.GetProperty("name").GetString(),
                    metric.GetProperty("value").GetDecimal(),
                    metric.GetProperty("unit").GetString())));
        Assert.Equal(0.0086m, finding.GetProperty("estimatedImpactUsd").GetDecimal());
        Assert.Equal("2026-11-21T19:00:10.0000000+00:00", finding.GetProperty("windowStart").GetString());
        Assert.Equal("2026-11-21T19:00:30.0000000+00:00", finding.GetProperty("windowEnd").GetString());
    }

    private static string[] PayloadTools(string output)
    {
        using JsonDocument body = JsonDocument.Parse(output);
        string summaryText = body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!;
        using JsonDocument summary = JsonDocument.Parse(summaryText);
        return
        [
            .. summary.RootElement.GetProperty("findings").EnumerateArray()
                .Select(finding => finding.GetProperty("scope").GetProperty("tool").GetString())
                .OfType<string>()
                .Order(StringComparer.Ordinal),
        ];
    }

    private static void AssertExport(string root, string toolName)
    {
        string report = ExportFiles.ReadReport(Path.Combine(root, "copilot-ledger-report.html"));
        XDocument page = ExportFiles.Parse(report);
        string[] titles =
        [
            .. page.Descendants()
                .Where(element => element.Name.LocalName == "title")
                .Select(element => element.Value),
        ];
        Assert.Contains($"14:00:10 · {toolName} · exit code 1", titles);
        Assert.Contains($"14:00:20 · {toolName} · exit code 1", titles);
        Assert.Contains($"14:00:30 · {toolName} · exit code 1", titles);
        Assert.Contains(
            page.Descendants().Where(element => element.Name.LocalName == "td"),
            cell => cell.Value.StartsWith(
                $"swigerb/copilot-ledger @ feature/ledger, (none), {toolName}; ",
                StringComparison.Ordinal));

        IReadOnlyList<(string Name, byte[] Bytes)> entries =
            ExportFiles.ReadZip(Path.Combine(root, "copilot-ledger-report.zip"));
        using JsonDocument toolCalls = JsonDocument.Parse(
            entries.Single(entry => entry.Name == "telemetry/tool-calls.json").Bytes);
        JsonElement[] hCalls =
        [
            .. toolCalls.RootElement.GetProperty("toolCalls").EnumerateArray()
                .Where(call => call.GetProperty("traceId").GetString() == H),
        ];
        Assert.Equal(3, hCalls.Length);
        Assert.All(hCalls, call => Assert.Equal(toolName, call.GetProperty("toolName").GetString()));

        using JsonDocument audit = JsonDocument.Parse(
            entries.Single(entry => entry.Name == "telemetry/audit-findings.json").Bytes);
        Assert.Equal(
            toolName,
            HFinding(audit.RootElement.GetProperty("findings")).GetProperty("scope").GetProperty("tool").GetString());
        Assert.DoesNotContain(ExecJs, report, StringComparison.Ordinal);
        Assert.All(
            entries,
            entry => Assert.DoesNotContain(ExecJs, Encoding.UTF8.GetString(entry.Bytes), StringComparison.Ordinal));
    }

    private static LedgerMcpContext McpContext(string root) => new()
    {
        DatabasePath = Path.Combine(root, "data", "ledger.db"),
        WorkingDirectory = Path.Combine(root, "repo"),
        GetEnvironmentVariable = _ => null,
        GitResolver = new FileSystemGitContextResolver(ceilingDirectory: root),
        TimeProvider = Clock(),
        Version = "9.9.9",
    };

    private static string McpText(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}
