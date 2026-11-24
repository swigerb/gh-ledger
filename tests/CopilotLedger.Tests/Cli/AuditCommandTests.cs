using System.Text.Json;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

// gh ledger audit: options, the run, and exit codes.
public sealed class AuditCommandTests
{
    public static TheoryData<string[], string> InvalidOptions => new()
    {
        { ["--since", "0d"], "Invalid --since value 0d. Use today, all, <N>d (1-3650), or yyyy-MM-dd." },
        { ["--pr", "0"], "--pr must be a positive integer." },
        {
            ["--rule", "tool_loop", "--rule", "nope"],
            "--rule must be turn_velocity, tool_loop, failed_tool_retry, context_bloat, context_growth, cache_miss_streak, premium_model_trivial_turn, or velocity_near_breaker."
        },
        { ["--min-severity", "High"], "--min-severity must be info, warning, or high." },
        { ["--fail-on", "info"], "--fail-on must be warning or high." },
        { ["--limit", "0"], "--limit must be from 1 to 200." },
        { ["--limit", "201"], "--limit must be from 1 to 200." },
        { ["--economy-model", " "], "--economy-model can't be blank." },
    };

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public async Task AC1_InvalidOptions_ExitWithCode1BeforeReadingTheLedger(string[] options, string message)
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp.Path);

        CliRun run = await harness.RunAsync(["audit", .. options]);

        Assert.Equal(1, run.ExitCode);
        Assert.Contains(message, run.Error);
        Assert.False(Directory.Exists(harness.Context.DataDirectory));
    }

    [Fact]
    public async Task AC2_TheSeededLedger_GivesThePinnedJsonAndTable()
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        await using CliHarness harness = Harness(temp.Path);

        CliRun json = await harness.RunAsync("audit", "--json");
        CliRun table = await harness.RunAsync("audit");

        Assert.Equal((0, AuditOutputs.Json + "\n", ""), (json.ExitCode, json.Out, json.Error));
        Assert.Equal((0, AuditOutputs.Table + "\n", ""), (table.ExitCode, table.Out, table.Error));
    }

    public static TheoryData<string[], int> FailOnCases => new()
    {
        { ["--fail-on", "high"], 2 },
        { ["--fail-on", "high", "--rule", "cache_miss_streak"], 0 },
        { ["--fail-on", "warning", "--rule", "cache_miss_streak"], 2 },
        { ["--fail-on", "warning", "--rule", "context_bloat"], 0 },
        { ["--fail-on", "warning", "--min-severity", "high", "--limit", "1"], 2 },
        { ["--fail-on", "high", "--json"], 2 },
        { [], 0 },
    };

    [Theory]
    [MemberData(nameof(FailOnCases))]
    public async Task AC3_FailOn_ExitsWithCode2WhenAnEvaluatedFindingReachesIt(string[] options, int exitCode)
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        await using CliHarness harness = Harness(temp.Path);

        CliRun run = await harness.RunAsync(["audit", .. options]);

        Assert.Equal(exitCode, run.ExitCode);
        Assert.Equal("", run.Error);
        Assert.StartsWith(options.Contains("--json") ? "{" : "Audit of all repositories (last 7 days)", run.Out);
    }

    [Fact]
    public async Task AC4_TheEconomyModel_MustBeAGitHubModel_AndPricesThePremiumImpact()
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        await using CliHarness harness = Harness(temp.Path);

        CliRun unknown = await harness.RunAsync("audit", "--economy-model", "qwen3.8-27b");
        CliRun sonnet = await harness.RunAsync("audit", "--economy-model", "Claude Sonnet 5-5", "--rule", "premium_model_trivial_turn", "--json");

        Assert.Equal(1, unknown.ExitCode);
        Assert.Equal("--economy-model qwen3.8-27b isn't a GitHub AI credits model in the rates in use (2026-11-23).\n", unknown.Error);
        Assert.Equal("", unknown.Out);
        Assert.Equal(0, sonnet.ExitCode);
        using JsonDocument document = JsonDocument.Parse(sonnet.Out);
        Assert.Equal("claude-sonnet-5.5", document.RootElement.GetProperty("economyModel").GetString());
        Assert.Equal(0.1872m, document.RootElement.GetProperty("findings")[0].GetProperty("estimatedImpactUsd").GetDecimal());
    }

    [Fact]
    public async Task AC5_TheRatesOverlay_SetsTheDefaultEconomyModel_AndAnInvalidOneStopsTheAudit()
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        await using CliHarness harness = Harness(temp.Path);
        File.WriteAllText(
            harness.Context.RatesOverlayPath,
            """{ "schemaVersion": 1, "localOffload": { "baselineModel": "claude-sonnet-5.5" } }""");

        CliRun overlaid = await harness.RunAsync("audit", "--json", "--rule", "premium_model_trivial_turn");
        File.WriteAllText(harness.Context.RatesOverlayPath, "{ }");
        CliRun invalid = await harness.RunAsync("audit");

        using JsonDocument document = JsonDocument.Parse(overlaid.Out);
        Assert.Equal("claude-sonnet-5.5", document.RootElement.GetProperty("economyModel").GetString());
        Assert.Equal(0.1872m, document.RootElement.GetProperty("findings")[0].GetProperty("estimatedImpactUsd").GetDecimal());
        Assert.Equal(1, invalid.ExitCode);
        Assert.Equal($"Invalid rates overlay: {harness.Context.RatesOverlayPath}: schemaVersion: is required\n", invalid.Error);
        Assert.Equal("", invalid.Out);
    }

    [Fact]
    public async Task AC6_RulesLimitAndMinimumSeverity_ShapeTheJson()
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        await using CliHarness harness = Harness(temp.Path);

        CliRun run = await harness.RunAsync(
            "audit", "--json", "--rule", "context_bloat", "--rule", "cache_miss_streak", "--rule", "context_bloat",
            "--min-severity", "warning", "--limit", "1");

        using JsonDocument document = JsonDocument.Parse(run.Out);
        JsonElement root = document.RootElement;
        Assert.Equal(["context_bloat", "cache_miss_streak"], root.GetProperty("rules").EnumerateArray().Select(rule => rule.GetString()));
        Assert.Equal("warning", root.GetProperty("minSeverity").GetString());
        Assert.Equal(1, root.GetProperty("limit").GetInt32());
        Assert.Equal(2, root.GetProperty("findingsTotal").GetInt32());
        Assert.Equal("cache_miss_streak", Assert.Single(root.GetProperty("findings").EnumerateArray()).GetProperty("ruleId").GetString());
    }

    [Fact]
    public async Task AC7_AnEmptyLedger_HasNoFindings()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp.Path);

        CliRun run = await harness.RunAsync("audit", "--json", "--since", "all");

        Assert.Equal(0, run.ExitCode);
        using JsonDocument document = JsonDocument.Parse(run.Out);
        JsonElement root = document.RootElement;
        Assert.Equal(JsonValueKind.Null, root.GetProperty("filter").GetProperty("fromUtc").ValueKind);
        Assert.Equal("all time", root.GetProperty("filter").GetProperty("period").GetString());
        Assert.Equal(0, root.GetProperty("coverage").GetProperty("spans").GetInt32());
        Assert.Equal(0, root.GetProperty("findingsTotal").GetInt32());
        Assert.Empty(root.GetProperty("findings").EnumerateArray());
    }

    [Fact]
    public async Task AC8_RepositoryBranchAndPullRequest_NarrowTheAudit()
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        await using CliHarness harness = Harness(temp.Path);

        (int Spans, string Rules) main = await AuditAsync(harness, "--repo", "swigerb/copilot-ledger", "--branch", "main");
        (int Spans, string Rules) pullRequest = await AuditAsync(harness, "--pr", "42");
        (int Spans, string Rules) imported = await AuditAsync(harness, "--repo", "(none)");

        Assert.Equal((12, "premium_model_trivial_turn"), main);
        Assert.Equal((14, "failed_tool_retry cache_miss_streak context_bloat"), pullRequest);
        Assert.Equal((3, ""), imported);
    }

    // Names from the ledger reach the table without escape sequences or controls, which could rewrite the terminal; the JSON
    // keeps them, escaped.
    [Fact]
    public async Task AC9_EscapesInLedgerNames_NeverReachTheTable()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"), new ManualTimeProvider(AuditSeeds.GeneratedAt));
        await store.InitializeAsync();
        const string agentName = "explore\u001B]0;owned\u0007";
        const string toolName = "view\u001B[2J\u009B31m";
        AuditSeeds.LiveTrace trace = new("e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5", "conv-e", null);
        string agent = trace.Agent(0, AuditSeeds.At("19:00:00"), null, agentName);
        trace.Request(1, agent, AuditSeeds.At("19:00:00"), "claude-sonnet-5.5", 10_000, 8_000, 300, agentName);
        for (int k = 0; k < 8; k++)
        {
            trace.Tool(k, agent, AuditSeeds.At("19:00:00").AddSeconds(10 * (k + 1)), toolName, null, 1, agentName);
        }

        await trace.RecordAsync(store);
        await using CliHarness harness = Harness(temp.Path);

        CliRun table = await harness.RunAsync("audit");
        CliRun json = await harness.RunAsync("audit", "--json");

        Assert.Equal((0, ""), (table.ExitCode, table.Error));
        Assert.DoesNotContain(table.Out, character => character is '\u0007' or '\u001B' or (>= '\u0080' and <= '\u009F'));
        Assert.Contains("  (none) @ (none), explore, view  ", table.Out);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        JsonElement scope = Assert.Single(document.RootElement.GetProperty("findings").EnumerateArray()).GetProperty("scope");
        Assert.Equal((agentName, toolName), (scope.GetProperty("agent").GetString(), scope.GetProperty("tool").GetString()));
    }

    private static CliHarness Harness(string root) =>
        new(root, new FakeCliPlatform(), new ManualTimeProvider(AuditSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));

    private static async Task<(int Spans, string Rules)> AuditAsync(CliHarness harness, params string[] options)
    {
        CliRun run = await harness.RunAsync(["audit", "--json", .. options]);
        Assert.Equal(0, run.ExitCode);
        using JsonDocument document = JsonDocument.Parse(run.Out);
        return (
            document.RootElement.GetProperty("coverage").GetProperty("spans").GetInt32(),
            string.Join(" ", document.RootElement.GetProperty("findings").EnumerateArray().Select(finding => finding.GetProperty("ruleId").GetString())));
    }
}
