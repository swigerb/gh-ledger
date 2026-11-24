using System.Text.Json;
using System.Text.Json.Nodes;
using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Git;
using CopilotLedger.Mcp;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using ModelContextProtocol.Protocol;

namespace CopilotLedger.Tests.Mcp;

// The MCP tool get_audit_findings, on AuditSeeds.
public sealed class AuditMcpTests
{
    [Fact]
    public async Task AM1_TheResult_IsAuditJsonOfTheCurrentRepository_ThenTheBreakerAndWarnings()
    {
        using TempDirectory temp = new();
        GitFixture.Create(temp.Path);
        await AuditSeeds.CreateAsync(temp.Path);
        await using McpTestHarness harness = await McpTestHarness.StartAsync(Context(temp.Path));
        await using CliHarness cli = new(temp.Path, new FakeCliPlatform(), Clock());

        CallToolResult result = await CallAsync(harness, "{}");
        CliRun audit = await cli.RunAsync("audit", "--json", "--repo", "swigerb/copilot-ledger", "--limit", "10");

        Assert.NotEqual(true, result.IsError);
        JsonObject structured = JsonNode.Parse(result.StructuredContent!.Value.GetRawText())!.AsObject();
        Assert.Equal(
            ["circuitBreaker", "warnings"],
            structured.Select(property => property.Key).TakeLast(2));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""{ "state": "closed", "openTrips": [] }"""), structured["circuitBreaker"]));
        Assert.Empty(structured["warnings"]!.AsArray());
        structured.Remove("circuitBreaker");
        structured.Remove("warnings");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(audit.Out), structured));
        Assert.Equal(4, structured["findingsTotal"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(result.StructuredContent!.Value.GetRawText()),
            JsonNode.Parse(Text(result))));
    }

    [Fact]
    public async Task AM2_WithoutARepository_PassOne()
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        await using McpTestHarness harness = await McpTestHarness.StartAsync(Context(temp.Path) with { WorkingDirectory = temp.Path });

        CallToolResult missing = await CallAsync(harness, "{}");
        CallToolResult passed = await CallAsync(harness, """{"repository":"swigerb/copilot-ledger","branch":"main"}""");

        Assert.True(missing.IsError);
        Assert.Equal($"No Git repository at {temp.Path}. Pass repository.", Text(missing));
        using JsonDocument document = JsonDocument.Parse(Text(passed));
        Assert.Equal("main", document.RootElement.GetProperty("filter").GetProperty("branch").GetString());
        Assert.Equal(
            "premium_model_trivial_turn",
            Assert.Single(document.RootElement.GetProperty("findings").EnumerateArray()).GetProperty("ruleId").GetString());
    }

    [Fact]
    public async Task AM3_AnOpenTripAndAnExceededBudget_OnTheCurrentBranch_ComeFirstAsWarnings()
    {
        using TempDirectory temp = new();
        GitFixture.Create(temp.Path);
        SqliteLedgerStore store = await AuditSeeds.CreateAsync(temp.Path);
        await store.RecordBreakerTripAsync(new NewBreakerTrip
        {
            Rule = BreakerRule.CreditVelocity,
            TraceId = AuditSeeds.R,
            ConversationId = "conv-r",
            Repository = "swigerb/copilot-ledger",
            Branch = "feature/ledger",
            TrippedAt = AuditSeeds.At("15:01:00"),
            Detail = "180.00 AI credits in 5 minutes",
            WindowCostUsd = 1.8m,
            WindowMinutes = 5,
        });
        await store.SetBranchBudgetAsync("swigerb/copilot-ledger", "feature/ledger", 1m);
        await using McpTestHarness harness = await McpTestHarness.StartAsync(Context(temp.Path));

        CallToolResult result = await CallAsync(harness, "{}");

        string[] lines = Text(result).Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("WARNING: CopilotLedger circuit breaker is open for swigerb/copilot-ledger @ feature/ledger: ", lines[0]);
        Assert.StartsWith("WARNING: swigerb/copilot-ledger @ feature/ledger has spent ", lines[1]);
        using JsonDocument document = JsonDocument.Parse(lines[2]);
        Assert.Equal("open", document.RootElement.GetProperty("circuitBreaker").GetProperty("state").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("warnings").GetArrayLength());
    }

    [Fact]
    public async Task AM4_InvalidRates_AreAnErrorResult()
    {
        using TempDirectory temp = new();
        GitFixture.Create(temp.Path);
        await AuditSeeds.CreateAsync(temp.Path);
        await using McpTestHarness harness = await McpTestHarness.StartAsync(
            Context(temp.Path) with { LoadAuditPricing = () => throw new InvalidDataException("rates.json: schemaVersion: is required") });

        CallToolResult result = await CallAsync(harness, "{}");

        Assert.True(result.IsError);
        Assert.Equal("Invalid rates overlay: rates.json: schemaVersion: is required", Text(result));
        Assert.Null(result.StructuredContent);
    }

    [Fact]
    public async Task AM5_TheArguments_ShapeTheAudit()
    {
        using TempDirectory temp = new();
        GitFixture.Create(temp.Path);
        await AuditSeeds.CreateAsync(temp.Path);
        await using McpTestHarness harness = await McpTestHarness.StartAsync(Context(temp.Path));

        CallToolResult result = await CallAsync(
            harness,
            """{"since":"all","branch":"feature/ledger","pullRequest":42,"minSeverity":"warning","rules":["cache_miss_streak","context_bloat","cache_miss_streak"],"limit":1}""");

        using JsonDocument document = JsonDocument.Parse(Text(result));
        JsonElement root = document.RootElement;
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(
                """{"repository":"swigerb/copilot-ledger","branch":"feature/ledger","pullRequest":42,"conversation":null,"since":"all","period":"all time","fromUtc":null,"toUtc":"2026-11-21T21:30:00.0000000+00:00"}"""),
            JsonNode.Parse(root.GetProperty("filter").GetRawText())));
        Assert.Equal(["context_bloat", "cache_miss_streak"], root.GetProperty("rules").EnumerateArray().Select(rule => rule.GetString()));
        Assert.Equal(("warning", 1, 2), (root.GetProperty("minSeverity").GetString(), root.GetProperty("limit").GetInt32(), root.GetProperty("findingsTotal").GetInt32()));
        Assert.Equal("cache_miss_streak", Assert.Single(root.GetProperty("findings").EnumerateArray()).GetProperty("ruleId").GetString());
    }

    private static ManualTimeProvider Clock() => new(AuditSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5);

    private static LedgerMcpContext Context(string root) => new()
    {
        DatabasePath = Path.Combine(root, "data", "ledger.db"),
        WorkingDirectory = Path.Combine(root, "repo"),
        GetEnvironmentVariable = _ => null,
        GitResolver = new FileSystemGitContextResolver(ceilingDirectory: root),
        TimeProvider = Clock(),
        Version = "9.9.9",
    };

    private static async Task<CallToolResult> CallAsync(McpTestHarness harness, string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        Dictionary<string, object?> arguments = document.RootElement
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => (object?)property.Value.Clone());
        return await harness.Client.CallToolAsync("get_audit_findings", arguments);
    }

    private static string Text(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}
