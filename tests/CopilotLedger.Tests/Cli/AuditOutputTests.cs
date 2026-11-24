using System.Globalization;
using System.Text;
using System.Text.Json;
using CopilotLedger.Cli.Audit;
using CopilotLedger.Core.Audit;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

// The audit report's JSON (AuditJson) and text (AuditTable, AuditText).
public sealed class AuditOutputTests
{
    [Fact]
    public async Task AO1_TheSeededLedgersJson_IsPinned()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await AuditSeeds.CreateAsync(temp.Path);

        byte[] json = AuditJson.Serialize(await AuditOutputs.ReportAsync(store));

        Assert.Equal(AuditOutputs.Json + "\n", Encoding.UTF8.GetString(json));
        Assert.NotEqual(0xEF, json[0]);
    }

    [Fact]
    public void AO2_TheJson_WritesNullsMoneyRatiosAndTimesExactly()
    {
        AuditReport report = Report(
            [
                new AuditFinding
                {
                    RuleId = AuditRuleIds.TurnVelocity,
                    Severity = AuditSeverity.Warning,
                    Scope = new AuditScope { Repository = "C:\\repo \"x\" <y>", Branch = "é/ü" },
                    WindowStart = new DateTimeOffset(2026, 11, 21, 9, 0, 0, TimeSpan.FromHours(-5)),
                    WindowEnd = new DateTimeOffset(2026, 11, 21, 14, 0, 0, 123, TimeSpan.Zero),
                    Metrics =
                    [
                        new AuditMetric("windowCostUsd", 0.000000123456m, AuditUnits.Usd),
                        new AuditMetric("ratio", 1m / 3m, AuditUnits.Ratio),
                        new AuditMetric("requests", 30.00m, AuditUnits.Requests),
                    ],
                    EstimatedImpactUsd = null,
                    RecommendationKey = AuditRuleIds.TurnVelocity,
                },
            ],
            fromUtc: null);

        string json = Encoding.UTF8.GetString(AuditJson.Serialize(report));
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement finding = document.RootElement.GetProperty("findings")[0];

        Assert.DoesNotContain("\r", json);
        Assert.EndsWith("}\n", json);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("filter").GetProperty("fromUtc").ValueKind);
        Assert.Equal(JsonValueKind.Null, finding.GetProperty("estimatedImpactUsd").ValueKind);
        Assert.Contains("\"value\": 0.000000123456,", json);
        Assert.Contains("\"value\": 0.3333333333333333333333333333,", json);
        Assert.Contains("\"value\": 30,", json);
        Assert.Contains("\"repository\": \"C:\\\\repo \\\"x\\\" <y>\",", json);
        Assert.Contains("\"branch\": \"é/ü\",", json);
        Assert.Equal("2026-11-21T14:00:00.0000000+00:00", finding.GetProperty("windowStart").GetString());
        Assert.Equal("2026-11-21T14:00:00.1230000+00:00", finding.GetProperty("windowEnd").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("findingsTotal").GetInt32());
    }

    [Fact]
    public async Task AO3_TheSeededLedgersTable_IsPinned()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await AuditSeeds.CreateAsync(temp.Path);

        Assert.Equal(AuditOutputs.Table + "\n", Write(await AuditOutputs.ReportAsync(store)));
    }

    [Fact]
    public void AO4_TheTable_SaysWhenThereAreNoFindings()
    {
        AuditReport report = Report([], fromUtc: AuditOutputs.Window.From) with
        {
            Filter = new AuditReportFilter
            {
                Repository = "contoso/webapp",
                Branch = "main",
                PullRequestNumber = 7,
                Since = "today",
                Period = "today",
                ToUtc = AuditSeeds.GeneratedAt,
            },
        };

        Assert.Equal(
            """
            Audit of contoso/webapp, branch main, pull request #7 (today)
            No findings.

            Requests: 0 analyzed.
            Tool calls: 0 analyzed.
            Impacts: billed USD (GitHub AI credits and Foundry BYOK) at rates 2026-11-23, economy model mai-code-2. Findings can overlap, so don't add their impacts up.
            Tool history: complete for these requests (tool calls are kept for 7 days, since 2026-11-14 16:30).
            Rules: all 8.

            """.ReplaceLineEndings("\n"),
            Write(report));
    }

    [Fact]
    public async Task AO5_TheTable_ShowsTheMinimumSeverityAndTheLimit()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await AuditSeeds.CreateAsync(temp.Path);

        string table = Write(await AuditOutputs.ReportAsync(store, AuditSeverity.Warning, 1));

        Assert.StartsWith(
            """
            Audit of all repositories (last 7 days)
            4 findings: 1 high, 2 warning, 1 info. Showing 1.

            Severity  Rule               Scope                                                   Window                  Key metrics          Est. impact
            High      failed_tool_retry  swigerb/copilot-ledger @ feature/ledger, explore, bash  2026-11-21 10:00-10:01  4 failures in a row        $0.01

            Requests: 29 analyzed
            """.ReplaceLineEndings("\n"),
            table);
        Assert.EndsWith(
            """

            Recommendations:
              failed_tool_retry: Fix the cause of the failing command, such as a missing dependency or a wrong path, before the agent retries it, because every retry costs another model request.

            """.ReplaceLineEndings("\n"),
            table);
    }

    [Fact]
    public void AO6_CoverageLines_ExplainEveryGap()
    {
        AuditInput input = AuditFixtures.Input(
            [
                AuditFixtures.Span(1),
                AuditFixtures.Span(2) with { CacheReadState = CacheCounterState.Absent, CacheWriteState = CacheCounterState.Absent },
                AuditFixtures.Span(3) with
                {
                    IngestSource = "import:copilot",
                    CacheReadState = CacheCounterState.LegacyUnknown,
                    CacheWriteState = CacheCounterState.Absent,
                    AgentInvocationId = "main",
                },
                AuditFixtures.Span(4) with { IngestSource = "import:codex", AgentInvocationId = null, ToolCallCount = null },
                AuditFixtures.Span(5) with { IngestSource = "import:claude", PricingStatus = PricingStatus.UnknownModel },
            ],
            [AuditFixtures.Tool(1, TimeSpan.FromSeconds(5)) with { AgentInvocationId = null }]) with
        {
            Coverage = AuditFixtures.Coverage with
            {
                SpansBeforeToolHistory = 2,
                ImportedToolCallsWithoutDetail = 1,
                ImportedRequestsWithoutToolData = 1,
                SupersededUnmatchedImportSpans = 1,
                LedgerAmbiguousImportRecords = 2,
                LedgerIncompleteImportLogs = 1,
            },
            Rules = [AuditRuleIds.ContextBloat, AuditRuleIds.ToolLoop],
        };
        AuditReport report = AuditAnalyzer.Report(
            input,
            AuditSeeds.GeneratedAt,
            new AuditReportFilter { Since = "7d", Period = "last 7 days", ToUtc = AuditSeeds.GeneratedAt },
            AuditSeverity.Info,
            20);

        Assert.Equal(
            [
                "Requests: 5 analyzed (2 live, 1 from Copilot session logs, 1 from Codex rollouts, 1 from import:claude).",
                "Tool calls: 1 analyzed.",
                "Impacts: billed USD (GitHub AI credits and Foundry BYOK) at rates 2026-11-23, economy model mai-code-2. Findings can overlap, so don't add their impacts up.",
                "Cache counters: unknown for 2 requests (1 absent, 1 recorded before gh-ledger kept counter states); cache_miss_streak skipped them.",
                "Agent invocations: unknown for 1 request and 1 tool call; the sequence rules skipped them.",
                "Tool counts: unknown for 1 request; premium_model_trivial_turn left it out.",
                "Tool history: insufficient: tool calls are kept for 7 days, since 2026-11-14 09:00; 2 requests started earlier, 1 imported tool call has no detail, and 1 imported request has no tool-call count in its session log, so the tool rules can't see those calls.",
                "Imports: 1 imported request was left out because live spans of its session replaced it without matching it; they may not cover all of its work.",
                "Imports: across the whole ledger, gh ledger import deferred 2 requests whose sessions have live spans it couldn't match.",
                "Imports: across the whole ledger, 1 session log wasn't imported completely.",
                "Unpriced: 1 request has no billed price, so impacts that include it are unknown.",
                "Rules: tool_loop, context_bloat.",
            ],
            AuditTable.CoverageLines(report, ManualTimeProvider.UtcMinus5));
    }

    public static TheoryData<string, string> KeyMetricCases => new()
    {
        { "turn_velocity", "30 requests in 5 min" },
        { "tool_loop success", "8 calls in a row, succeeded" },
        { "tool_loop failure", "8 calls in a row, failed" },
        { "tool_loop unknown", "8 calls in a row, outcome unknown" },
        { "failed_tool_retry", "4 failures in a row" },
        { "context_bloat", "100,000 in, 300 out (333:1)" },
        { "context_bloat zero output", "80,000 in, no output" },
        { "context_growth", "20,000 to 92,000 input in 12 requests (4.6x)" },
        { "cache_miss_streak", "5 requests, 7.5% cache hits, 175,000 uncached" },
        { "premium_model_trivial_turn", "12 tool-free requests of 200 or fewer output tokens" },
        { "velocity_near_breaker", "125.00 of 150.00 AI credits in 5 min (83.3%)" },
    };

    [Theory]
    [MemberData(nameof(KeyMetricCases))]
    public void AO7_KeyMetrics_SummarizeEachRulesFinding(string name, string expected)
    {
        (string rule, AuditToolOutcome? outcome, (string Name, decimal Value)[] metrics) = name switch
        {
            "turn_velocity" => (AuditRuleIds.TurnVelocity, (AuditToolOutcome?)null, new[] { ("requests", 30m), ("windowMinutes", 5m) }),
            "tool_loop success" => (AuditRuleIds.ToolLoop, AuditToolOutcome.Success, [("calls", 8m), ("requests", 7m)]),
            "tool_loop failure" => (AuditRuleIds.ToolLoop, AuditToolOutcome.Failure, [("calls", 8m), ("requests", 7m)]),
            "tool_loop unknown" => (AuditRuleIds.ToolLoop, AuditToolOutcome.Unknown, [("calls", 8m), ("requests", 7m)]),
            "failed_tool_retry" => (AuditRuleIds.FailedToolRetry, AuditToolOutcome.Failure, [("failures", 4m), ("requests", 3m)]),
            "context_bloat" => (AuditRuleIds.ContextBloat, null, [("inputTokens", 100_000m), ("outputTokens", 300m), ("inputOutputRatio", 100_000m / 300m)]),
            "context_bloat zero output" => (AuditRuleIds.ContextBloat, null, [("inputTokens", 80_000m), ("outputTokens", 0m), ("zeroOutput", 1m)]),
            "context_growth" => (AuditRuleIds.ContextGrowth, null, [("requests", 12m), ("firstInputTokens", 20_000m), ("lastInputTokens", 92_000m), ("peakInputTokens", 95_000m), ("growthRatio", 4.6m)]),
            "cache_miss_streak" => (AuditRuleIds.CacheMissStreak, null, [("requests", 5m), ("inputTokens", 200_000m), ("cacheReadInputTokens", 15_000m), ("uncachedInputTokens", 175_000m), ("cacheHitRatio", 0.075m)]),
            "premium_model_trivial_turn" => (AuditRuleIds.PremiumModelTrivialTurn, null, [("requests", 12m), ("outputTokens", 1_440m)]),
            "velocity_near_breaker" => (AuditRuleIds.VelocityNearBreaker, null, [("windowCostUsd", 1.25m), ("windowAiCredits", 125m), ("breakerVelocityAiCredits", 150m), ("breakerFraction", 1.25m / 1.5m), ("windowMinutes", 5m)]),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };
        AuditFinding finding = new()
        {
            RuleId = rule,
            Severity = AuditSeverity.Warning,
            Scope = new AuditScope { Repository = "contoso/webapp", Branch = "main", ToolOutcome = outcome },
            WindowStart = AuditSeeds.GeneratedAt,
            WindowEnd = AuditSeeds.GeneratedAt,
            Metrics = [.. metrics.Select(metric => new AuditMetric(metric.Name, metric.Value, AuditUnits.Requests))],
            RecommendationKey = rule,
        };

        Assert.Equal(expected, AuditText.KeyMetrics(finding, AuditThresholds.Default));
    }

    [Fact]
    public void AO8_ScopeWindowImpactAndTitles_AreFixedTexts()
    {
        TimeZoneInfo zone = ManualTimeProvider.UtcMinus5;
        DateTimeOffset start = new(2026, 11, 21, 15, 0, 20, TimeSpan.Zero);

        Assert.Equal("contoso/webapp @ main", AuditText.Scope(new AuditScope { Repository = "contoso/webapp", Branch = "main" }));
        Assert.Equal(
            "contoso/webapp @ (none), explore, claude-opus-5.5, bash",
            AuditText.Scope(new AuditScope { Repository = "contoso/webapp", Branch = "(none)", Agent = "explore", Model = "claude-opus-5.5", Tool = "bash" }));
        Assert.Equal("2026-11-21 10:00", AuditText.Window(start, start.AddSeconds(30), zone));
        Assert.Equal("2026-11-21 10:00-10:01", AuditText.Window(start, start.AddSeconds(60), zone));
        Assert.Equal("2026-11-21 10:00-2026-11-22 00:00", AuditText.Window(start, start.AddHours(14), zone));
        Assert.Equal("$0.33", AuditText.Impact(0.3276m));
        Assert.Equal("$0.00", AuditText.Impact(0m));
        Assert.Equal("-", AuditText.Impact(null));
        Assert.Equal(
            [
                "Turn velocity", "Tool loop", "Failed tool retries", "Context bloat", "Context growth", "Cache-miss streak",
                "Premium model on trivial turns", "Near the circuit breaker",
            ],
            AuditRuleIds.All.Select(AuditText.RuleTitle));
        Assert.Equal(["High", "Warning", "Info"], new[] { AuditSeverity.High, AuditSeverity.Warning, AuditSeverity.Info }.Select(AuditText.Severity));
    }

    // Tool history is worded as export words its start: the retention, the earliest span link the ledger keeps when that's
    // later, or none when the ledger keeps no link.
    [Fact]
    public void AO9_ToolHistory_IsWordedAsExportWordsItsStart()
    {
        AuditReport report = Report([], fromUtc: AuditOutputs.Window.From);
        AuditCoverage earliest = report.Summary.Coverage with
        {
            ToolHistoryFrom = new DateTimeOffset(2026, 11, 20, 12, 0, 1, TimeSpan.Zero),
            ToolHistoryStart = AuditToolHistoryStart.EarliestLink,
        };
        AuditCoverage none = report.Summary.Coverage with
        {
            ToolHistoryFrom = AuditSeeds.GeneratedAt,
            ToolHistoryStart = AuditToolHistoryStart.NoLinks,
        };

        Assert.Equal(
            [
                "Tool history: complete for these requests (the ledger's tool history starts at 2026-11-20 07:00).",
                "Tool history: insufficient: the ledger's tool history starts at 2026-11-20 07:00; 3 requests started earlier, so the tool rules can't see those calls.",
                "Tool history: complete for these requests (the ledger keeps no tool history).",
                "Tool history: insufficient: the ledger keeps no tool history; 1 request may be missing its tool calls, so the tool rules can't see those calls.",
            ],
            new[] { earliest, earliest with { SpansBeforeToolHistory = 3 }, none, none with { SpansBeforeToolHistory = 1 } }.Select(coverage =>
                AuditTable.CoverageLines(report with { Summary = report.Summary with { Coverage = coverage } }, ManualTimeProvider.UtcMinus5)
                    .Single(line => line.StartsWith("Tool history:", StringComparison.Ordinal))));
    }

    private static AuditReport Report(IReadOnlyList<AuditFinding> findings, DateTimeOffset? fromUtc) =>
        AuditAnalyzer.Report(
            AuditFixtures.Input([]),
            AuditSeeds.GeneratedAt,
            new AuditReportFilter { Since = "all", Period = "all time", FromUtc = fromUtc, ToUtc = AuditSeeds.GeneratedAt },
            AuditSeverity.Info,
            20) with
        {
            Findings = findings,
            Summary = AuditAnalyzer.Summarize(AuditFixtures.Input([])) with
            {
                Coverage = AuditFixtures.Coverage with { ToolHistoryFrom = AuditSeeds.GeneratedAt.AddDays(-7) },
            },
        };

    private static string Write(AuditReport report)
    {
        StringWriter writer = new(CultureInfo.InvariantCulture) { NewLine = "\n" };
        AuditTable.Write(writer, report, ManualTimeProvider.UtcMinus5);
        return writer.ToString();
    }
}
