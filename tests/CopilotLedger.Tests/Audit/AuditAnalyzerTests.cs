using CopilotLedger.Core.Audit;
using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Audit;

public sealed class AuditAnalyzerTests
{
    [Fact]
    public void AA1_Order_RanksBySeverityImpactWindowRuleAndScope_AndIsStable()
    {
        AuditFinding[] findings =
        [
            Finding("a", AuditSeverity.Info, null, 0),
            Finding("d", AuditSeverity.Warning, 0.2m, 5, AuditRuleIds.ToolLoop),
            Finding("j", AuditSeverity.Warning, 0.2m, 5),
            Finding("b", AuditSeverity.High, null, 0),
            Finding("h", AuditSeverity.Warning, 0.2m, 5, pullRequest: 7),
            Finding("e", AuditSeverity.Warning, 0.2m, 5),
            Finding("g", AuditSeverity.Warning, 0.5m, 9),
            Finding("i", AuditSeverity.Warning, 0.2m, 5, tool: "bash"),
            Finding("c", AuditSeverity.High, 0.5m, 5),
            Finding("f", AuditSeverity.Warning, 0.2m, 1),
        ];

        IReadOnlyList<AuditFinding> ordered = AuditAnalyzer.Order(findings);

        Assert.Equal("c b g f j e i h d a", string.Join(" ", ordered.Select(finding => finding.RecommendationKey)));
    }

    [Fact]
    public void AA2_Summarize_CountsSourcesCacheStatesIdentityToolCountsAndPricing()
    {
        AuditSpan[] spans =
        [
            AuditFixtures.Span(1),
            AuditFixtures.Span(2) with
            {
                CacheReadState = CacheCounterState.Absent,
                CacheWriteState = CacheCounterState.Absent,
                AgentInvocationId = null,
                ToolCallCount = null,
            },
            AuditFixtures.Span(3) with
            {
                IngestSource = "import:copilot",
                CacheReadState = CacheCounterState.LegacyUnknown,
                CacheWriteState = CacheCounterState.Absent,
                AgentInvocationId = "main",
                PricingStatus = PricingStatus.UnknownModel,
            },
            AuditFixtures.Span(4) with { IngestSource = "import:copilot", CacheWriteState = CacheCounterState.Absent },
            AuditFixtures.Span(5) with
            {
                IngestSource = "import:codex",
                CacheReadState = CacheCounterState.Absent,
                Channel = BillingChannel.Local,
                PricingStatus = PricingStatus.LocalOffload,
            },
        ];
        AuditToolCall[] calls =
        [
            AuditFixtures.Tool(1, TimeSpan.FromSeconds(5)),
            AuditFixtures.Tool(2, TimeSpan.FromSeconds(6)) with { AgentInvocationId = null },
            AuditFixtures.Tool(3, TimeSpan.FromSeconds(7)),
        ];
        AuditInput input = AuditFixtures.Input(spans, calls);

        AuditSummary summary = AuditAnalyzer.Summarize(input);

        Assert.Equal(5, summary.Spans);
        Assert.Equal(
            [new AuditSourceCount("import:codex", 1), new AuditSourceCount("import:copilot", 2), new AuditSourceCount("otlp", 2)],
            summary.SpansBySource);
        Assert.Equal(3, summary.ToolCalls);
        Assert.Equal(1, summary.UnpricedSpans);
        Assert.Equal(3, summary.CacheReportedSpans);
        Assert.Equal(1, summary.CacheAbsentSpans);
        Assert.Equal(1, summary.CacheLegacyUnknownSpans);
        Assert.Equal(1, summary.SpansWithoutAgentIdentity);
        Assert.Equal(1, summary.ToolCallsWithoutAgentIdentity);
        Assert.Equal(1, summary.SpansWithUnknownToolCount);
        Assert.Same(input.Coverage, summary.Coverage);
    }

    [Fact]
    public void AA3_Report_DescribesTheRun_AndPremiumNeedsPricing()
    {
        AuditInput input = AuditFixtures.Input([AuditFixtures.Span(1)]);
        AuditReportFilter filter = new() { Since = "7d", Period = "last 7 days", ToUtc = AuditFixtures.Start };

        AuditReport priced = AuditAnalyzer.Report(input, AuditFixtures.Start, filter, AuditSeverity.Warning, 5);
        AuditReport unpriced = AuditAnalyzer.Report(input with { Pricing = null }, AuditFixtures.Start, filter, AuditSeverity.Info, null);
        AuditReport subset = AuditAnalyzer.Report(
            input with { Rules = [AuditRuleIds.ToolLoop, "nope", AuditRuleIds.TurnVelocity, AuditRuleIds.ToolLoop] },
            AuditFixtures.Start,
            filter,
            AuditSeverity.Info,
            null);

        Assert.Equal("2026-11-23", priced.RatesVersion);
        Assert.Equal("mai-code-2", priced.EconomyModel);
        Assert.Equal(AuditRuleIds.All, priced.Rules);
        Assert.Equal(AuditSeverity.Warning, priced.MinSeverity);
        Assert.Equal(5, priced.Limit);
        Assert.Equal(1, priced.Summary.Spans);
        Assert.Same(filter, priced.Filter);
        Assert.Same(AuditThresholds.Default, priced.Thresholds);
        Assert.Null(unpriced.RatesVersion);
        Assert.Null(unpriced.EconomyModel);
        Assert.DoesNotContain(AuditRuleIds.PremiumModelTrivialTurn, unpriced.Rules);
        Assert.Equal(7, unpriced.Rules.Count);
        Assert.Equal([AuditRuleIds.TurnVelocity, AuditRuleIds.ToolLoop], subset.Rules);
    }

    [Fact]
    public void AA4_Shown_AppliesTheMinimumSeverityThenTheLimit()
    {
        AuditReport report = AuditAnalyzer.Report(
            AuditFixtures.Input([]),
            AuditFixtures.Start,
            new AuditReportFilter { Since = "7d", Period = "last 7 days", ToUtc = AuditFixtures.Start },
            AuditSeverity.Warning,
            2) with
        {
            Findings =
            [
                Finding("h", AuditSeverity.High, 1m, 0),
                Finding("i", AuditSeverity.Info, 0.9m, 0),
                Finding("w1", AuditSeverity.Warning, 0.5m, 0),
                Finding("w2", AuditSeverity.Warning, 0.4m, 0),
            ],
        };

        Assert.Equal(["h", "w1"], report.Shown.Select(finding => finding.RecommendationKey));
        Assert.Equal(4, (report with { MinSeverity = AuditSeverity.Info, Limit = null }).Shown.Count);
        Assert.Equal(["h"], (report with { MinSeverity = AuditSeverity.High, Limit = null }).Shown.Select(finding => finding.RecommendationKey));
    }

    public static TheoryData<long?, int, bool, AuditToolOutcome> Outcomes => new()
    {
        { null, 0, false, AuditToolOutcome.Unknown },
        { null, 1, false, AuditToolOutcome.Success },
        { null, 2, false, AuditToolOutcome.Failure },
        { 0, 0, false, AuditToolOutcome.Success },
        { 0, 2, false, AuditToolOutcome.Failure },
        { 1, 0, false, AuditToolOutcome.Failure },
        { 1, 1, false, AuditToolOutcome.Failure },
        { -1, 1, false, AuditToolOutcome.Failure },
        { null, 0, true, AuditToolOutcome.Success },
        { null, 2, true, AuditToolOutcome.Failure },
        { 0, 0, true, AuditToolOutcome.Success },
        { 2, 0, true, AuditToolOutcome.Failure },
    };

    [Theory]
    [MemberData(nameof(Outcomes))]
    public void AA5_Classify_RefinesTheBreakersOutcome(long? exitCode, int statusCode, bool imported, AuditToolOutcome expected)
    {
        Assert.Equal(expected, AuditToolOutcomes.Classify(exitCode, statusCode, imported));
        AuditToolCall call = AuditFixtures.Tool(1, TimeSpan.Zero, exitCode: exitCode, statusCode: statusCode) with { Imported = imported };
        Assert.Equal(expected, call.Outcome);
    }

    [Fact]
    public void AA6_SeverityAndOutcomeNames_AreExactAndOrdinal()
    {
        Assert.Equal("info", AuditSeverities.ToName(AuditSeverity.Info));
        Assert.Equal("warning", AuditSeverities.ToName(AuditSeverity.Warning));
        Assert.Equal("high", AuditSeverities.ToName(AuditSeverity.High));
        Assert.Throws<ArgumentOutOfRangeException>(() => AuditSeverities.ToName((AuditSeverity)7));
        Assert.True(AuditSeverities.TryParse("high", out AuditSeverity high));
        Assert.Equal(AuditSeverity.High, high);
        Assert.True(AuditSeverities.TryParse("warning", out AuditSeverity warning));
        Assert.Equal(AuditSeverity.Warning, warning);
        Assert.True(AuditSeverities.TryParse("info", out AuditSeverity info));
        Assert.Equal(AuditSeverity.Info, info);
        Assert.False(AuditSeverities.TryParse("High", out _));
        Assert.False(AuditSeverities.TryParse("critical", out _));
        Assert.False(AuditSeverities.TryParse(" info", out _));
        Assert.False(AuditSeverities.TryParse(null, out _));
        Assert.Equal("success", AuditToolOutcomes.ToName(AuditToolOutcome.Success));
        Assert.Equal("failure", AuditToolOutcomes.ToName(AuditToolOutcome.Failure));
        Assert.Equal("unknown", AuditToolOutcomes.ToName(AuditToolOutcome.Unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() => AuditToolOutcomes.ToName((AuditToolOutcome)7));
    }

    [Fact]
    public void AA7_RuleIds_AreTheIssuesEightInOrder()
    {
        Assert.Equal(
            [
                "turn_velocity", "tool_loop", "failed_tool_retry", "context_bloat", "context_growth", "cache_miss_streak",
                "premium_model_trivial_turn", "velocity_near_breaker",
            ],
            AuditRuleIds.All);
        Assert.True(AuditRuleIds.IsKnown("tool_loop"));
        Assert.False(AuditRuleIds.IsKnown("Tool_loop"));
        Assert.False(AuditRuleIds.IsKnown(""));
        Assert.False(AuditRuleIds.IsKnown(null));
    }

    [Fact]
    public void AA8_Recommendations_HaveOneFixedSentencePerKey()
    {
        Assert.Equal(
            [
                "turn_velocity", "tool_loop", "failed_tool_retry", "context_bloat", "context_bloat_zero_output", "context_growth",
                "cache_miss_streak", "premium_model_trivial_turn", "velocity_near_breaker",
            ],
            AuditRecommendations.Keys);
        string[] texts = [.. AuditRecommendations.Keys.Select(AuditRecommendations.Text)];
        Assert.All(texts, text => Assert.True(text.Length > 40 && text.EndsWith('.') && !text.Contains('\n'), text));
        Assert.Equal(texts.Length, texts.Distinct(StringComparer.Ordinal).Count());
        Assert.Throws<ArgumentOutOfRangeException>(() => AuditRecommendations.Text("nope"));
    }

    [Theory]
    [InlineData(5, 3)]
    [InlineData(4, 3)]
    [InlineData(3, 2)]
    [InlineData(2, 2)]
    [InlineData(100, 3)]
    public void AA9_FailedToolRetryTrigger_StaysBelowTheChurnThreshold(int churnFailures, int expected)
    {
        Assert.Equal(
            expected,
            AuditThresholds.Default.FailedToolRetryTrigger(new BreakerSettings { ChurnFailures = churnFailures }));
    }

    private static AuditFinding Finding(
        string label,
        AuditSeverity severity,
        decimal? impact,
        int startMinute,
        string rule = AuditRuleIds.ContextBloat,
        int? pullRequest = null,
        string? tool = null) => new()
    {
        RuleId = rule,
        Severity = severity,
        Scope = new AuditScope
        {
            Repository = AuditFixtures.Repository,
            Branch = "main",
            PullRequestNumber = pullRequest,
            Agent = "(none)",
            TraceId = AuditFixtures.TraceId,
            Tool = tool,
        },
        WindowStart = AuditFixtures.Start.AddMinutes(startMinute),
        WindowEnd = AuditFixtures.Start.AddMinutes(startMinute + 1),
        Metrics = [],
        EstimatedImpactUsd = impact,
        RecommendationKey = label,
    };
}
