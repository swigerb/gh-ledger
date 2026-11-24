using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using CopilotLedger.Core.Audit;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

// gh ledger export's audit findings: the report's section and the bundle's telemetry/audit-findings.json.
public sealed class ExportAuditTests
{
    private const string Report = "copilot-ledger-report.html";
    private const string Bundle = "copilot-ledger-report.zip";

    [Fact]
    public async Task EF1_TheReport_ShowsTheFindingsWithFixedTexts()
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);

        CliRun run = await Harness(temp.Path).RunAsync("export", "--all-repos", "--since", "7d");

        Assert.Equal((0, ""), (run.ExitCode, run.Error));
        string html = ExportFiles.ReadReport(Path.Combine(temp.Path, Report));
        Assert.Contains(
            "<p id=\"audit-note\">Review signals from fixed rules over these requests and tool calls, not proof of waste. Estimated impacts are billed USD at rates 2026-11-23; findings can overlap, so their impacts aren't added together.</p>\n",
            html);
        Assert.Contains(
            string.Concat(
                Row("High", "Failed tool retries: 4 failures in a row", "swigerb/copilot-ledger @ feature/ledger, explore, bash; 2026-11-21 10:00-10:01; est. impact $0.01. " + AuditRecommendations.Text("failed_tool_retry")),
                Row("Warning", "Premium model on trivial turns: 12 tool-free requests of 200 or fewer output tokens", "swigerb/copilot-ledger @ main, (none), claude-opus-5.5; 2026-11-21 09:00-09:22; est. impact $0.33. " + AuditRecommendations.Text("premium_model_trivial_turn")),
                Row("Warning", "Cache-miss streak: 6 requests, 0.0% cache hits, 240,000 uncached", "swigerb/copilot-ledger @ feature/ledger, (none), mai-code-2; 2026-11-21 13:00-13:05; est. impact $0.11. " + AuditRecommendations.Text("cache_miss_streak")),
                Row("Info", "Context bloat: 260,000 in, 1,000 out (260:1)", "swigerb/copilot-ledger @ feature/ledger, (none), gpt-6.2-sol; 2026-11-21 12:00; est. impact -. " + AuditRecommendations.Text("context_bloat"))),
            html);
        Assert.Contains("<a href=\"#audit-findings\">Audit findings</a>", html);
    }

    [Fact]
    public async Task EF2_TheBundlesAuditFile_IsAuditJsonOfTheSameWindowWithEveryFinding()
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);

        CliRun export = await cli.RunAsync("export", "--all-repos", "--since", "7d", "--bundle");
        CliRun audit = await cli.RunAsync("audit", "--json", "--since", "7d");

        Assert.Equal((0, ""), (export.ExitCode, export.Error));
        JsonObject file = JsonNode.Parse(Entry(temp.Path, "telemetry/audit-findings.json"))!.AsObject();
        JsonObject expected = JsonNode.Parse(audit.Out)!.AsObject();
        Assert.Null(file["limit"]);
        Assert.Equal(20, expected["limit"]!.GetValue<int>());
        file.Remove("limit");
        expected.Remove("limit");
        Assert.True(JsonNode.DeepEquals(expected, file));
        Assert.EndsWith("}\n", System.Text.Encoding.UTF8.GetString(Entry(temp.Path, "telemetry/audit-findings.json")));
    }

    [Fact]
    public async Task EF3_LocalRepositoryRoots_AreFolderNamesUnlessLocalPathsAreIncluded()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await AuditSeeds.CreateAsync(temp.Path);
        string root = Path.Combine(temp.Path, "private-7c1d", "tools");
        AuditSeeds.LiveTrace local = new("10101010101010101010101010101010", "conv-local", new GitWorkspaceContext(root, Path.Combine(root, ".git"), "main", GitFixture.Sha2, null, null));
        local.Request(1, null, AuditSeeds.At("19:00:00"), "claude-opus-5.5", 60_000, 0, 100);
        await local.RecordAsync(store);
        CliHarness cli = Harness(temp.Path);

        await cli.RunAsync("export", "--all-repos", "--since", "7d", "--bundle");
        string hidden = System.Text.Encoding.UTF8.GetString(Entry(temp.Path, "telemetry/audit-findings.json"));
        string report = ExportFiles.ReadReport(Path.Combine(temp.Path, Report));
        CliRun included = await cli.RunAsync("export", "--all-repos", "--since", "7d", "--bundle", "--force", "--include-local-paths");
        string shown = System.Text.Encoding.UTF8.GetString(Entry(temp.Path, "telemetry/audit-findings.json"));

        Assert.Equal(0, included.ExitCode);
        Assert.DoesNotContain("private-7c1d", hidden);
        Assert.DoesNotContain("private-7c1d", report);
        Assert.Contains("\"repository\": \"tools\",", hidden);
        Assert.Contains("<td>tools @ main, (none), claude-opus-5.5; ", report);
        Assert.Contains($"\"repository\": {JsonSerializer.Serialize(root, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping })},", shown);
    }

    [Fact]
    public async Task EF4_AnInvalidOverlay_LeavesThePremiumRuleAndRepricedImpactsOut()
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        CliHarness cli = Harness(temp.Path);
        File.WriteAllText(cli.Context.RatesOverlayPath, "{ }");

        CliRun run = await cli.RunAsync("export", "--all-repos", "--since", "7d", "--bundle");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            $"Warning: invalid rates overlay, so the report's audit findings leave out premium_model_trivial_turn and repriced estimates: {cli.Context.RatesOverlayPath}: schemaVersion: is required\n",
            run.Error);
        using JsonDocument file = JsonDocument.Parse(Entry(temp.Path, "telemetry/audit-findings.json"));
        JsonElement root = file.RootElement;
        Assert.Equal(JsonValueKind.Null, root.GetProperty("ratesVersion").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("economyModel").ValueKind);
        Assert.DoesNotContain("premium_model_trivial_turn", root.GetProperty("rules").EnumerateArray().Select(rule => rule.GetString()));
        Assert.Equal(
            ["failed_tool_retry 0.0129", "cache_miss_streak null", "context_bloat null"],
            root.GetProperty("findings").EnumerateArray().Select(finding =>
                $"{finding.GetProperty("ruleId").GetString()} {finding.GetProperty("estimatedImpactUsd").GetRawText()}"));
        Assert.Contains(
            "findings can overlap, so their impacts aren't added together. The rates in use couldn't be loaded, so premium_model_trivial_turn wasn't evaluated and repriced estimates are unknown.</p>",
            ExportFiles.ReadReport(Path.Combine(temp.Path, Report)));
    }

    [Fact]
    public async Task EF5_TheReport_ShowsTheFirst100Findings_AndTheBundleHasThemAll()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await AuditSeeds.CreateAsync(temp.Path);
        AuditSeeds.LiveTrace bloat = new("b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2", "conv-bloat", null);
        for (int k = 0; k < 101; k++)
        {
            bloat.Request(1 + k, null, AuditSeeds.At("19:00:00").AddSeconds(20 * k), "claude-sonnet-5.5", 60_000, 50_000, 100);
        }

        await bloat.RecordAsync(store);

        await Harness(temp.Path).RunAsync("export", "--all-repos", "--since", "7d", "--bundle");

        string html = ExportFiles.ReadReport(Path.Combine(temp.Path, Report));
        Assert.Contains(
            " The table shows the first 100 of 105 findings; telemetry/audit-findings.json in the bundle and gh ledger audit --json list them all.</p>",
            html);
        Assert.Equal(100, ExportFiles.ById(ExportFiles.Parse(html), "audit-findings").Descendants("tbody").Single().Elements("tr").Count());
        using JsonDocument file = JsonDocument.Parse(Entry(temp.Path, "telemetry/audit-findings.json"));
        Assert.Equal(105, file.RootElement.GetProperty("findings").GetArrayLength());
        Assert.Equal(105, file.RootElement.GetProperty("findingsTotal").GetInt32());
    }

    // The audit's tool history is the export's in every case. With the links before 11-21 deleted and a tool call at 13:00
    // kept, both start at that earliest link and count the 2 requests of 11-20 before it; with every link deleted, neither has
    // tool history; and an imported request without a tool-call count, a Codex request or a Copilot one whose log gave none,
    // leaves both incomplete.
    [Theory]
    [InlineData("earliest link", "2026-11-21T13:00:00.0000000+00:00", "earliest_link", 2, 0)]
    [InlineData("no links", "2026-11-21T21:30:00.0000000+00:00", "no_links", 8, 0)]
    [InlineData("codex", "2026-11-20T12:00:00.0000000+00:00", "earliest_link", 0, 1)]
    [InlineData("copilot without a count", "2026-11-20T12:00:00.0000000+00:00", "earliest_link", 0, 1)]
    public async Task EF6_TheAuditFilesToolHistory_IsTheExportsCoverage(
        string name,
        string from,
        string start,
        long before,
        long withoutToolData)
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        if (name == "earliest link")
        {
            await store.RecordSpanLinksAsync(
            [
                new GenAiSpanLink
                {
                    TraceId = ExportSeeds.E,
                    SpanId = ExportSeeds.E[..2] + "00000000000099",
                    OperationName = GenAiOperationNames.ExecuteTool,
                    ConversationId = "conv-x",
                    StartTime = new DateTimeOffset(2026, 11, 21, 13, 0, 0, TimeSpan.Zero),
                    ToolName = "bash",
                    ToolExitCode = 0,
                },
            ]);
            await store.PruneSpanLinksAsync(new DateTimeOffset(2026, 11, 21, 0, 0, 0, TimeSpan.Zero));
        }
        else if (name == "no links")
        {
            await store.PruneSpanLinksAsync(new DateTimeOffset(2026, 11, 22, 0, 0, 0, TimeSpan.Zero));
        }
        else if (name == "codex")
        {
            await store.RecordImportedAsync([AuditSeeds.Codex("x9", "conv-codex")], null);
        }
        else
        {
            await store.RecordImportedAsync([ImportEntries.Copilot("k8", "conv-n", null)], null);
        }

        CliRun run = await Harness(temp.Path).RunAsync("export", "--all-repos", "--bundle");

        Assert.Equal((0, ""), (run.ExitCode, run.Error));
        using JsonDocument summary = JsonDocument.Parse(Entry(temp.Path, "telemetry/summary.json"));
        using JsonDocument audit = JsonDocument.Parse(Entry(temp.Path, "telemetry/audit-findings.json"));
        JsonElement history = summary.RootElement.GetProperty("coverage").GetProperty("toolHistory");
        JsonElement coverage = audit.RootElement.GetProperty("coverage");
        Assert.Equal(
            (from, start, before, withoutToolData, true),
            (
                coverage.GetProperty("toolHistoryFromUtc").GetString(),
                coverage.GetProperty("toolHistoryStart").GetString(),
                coverage.GetProperty("spansBeforeToolHistory").GetInt64(),
                coverage.GetProperty("importedRequestsWithoutToolData").GetInt64(),
                coverage.GetProperty("insufficientToolHistory").GetBoolean()));
        Assert.Equal(
            (
                history.GetProperty("retainedFromUtc").GetString(),
                history.GetProperty("spansBeforeRetention").GetInt64(),
                history.GetProperty("importedToolCallsWithoutDetail").GetInt64(),
                history.GetProperty("importedRequestsWithoutToolData").GetInt64(),
                !history.GetProperty("complete").GetBoolean()),
            (
                coverage.GetProperty("toolHistoryFromUtc").GetString(),
                coverage.GetProperty("spansBeforeToolHistory").GetInt64(),
                coverage.GetProperty("importedToolCallsWithoutDetail").GetInt64(),
                coverage.GetProperty("importedRequestsWithoutToolData").GetInt64(),
                coverage.GetProperty("insufficientToolHistory").GetBoolean()));
    }

    private static CliHarness Harness(string root) =>
        new(root, new FakeCliPlatform(), new ManualTimeProvider(AuditSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));

    private static byte[] Entry(string root, string name) =>
        ExportFiles.ReadZip(Path.Combine(root, Bundle)).Single(entry => entry.Name == name).Bytes;

    private static string Row(string severity, string title, string detail) =>
        $"<tr><td>{HtmlEncoder.Default.Encode(severity)}</td><td>{HtmlEncoder.Default.Encode(title)}</td><td>{HtmlEncoder.Default.Encode(detail)}</td></tr>\n";
}
