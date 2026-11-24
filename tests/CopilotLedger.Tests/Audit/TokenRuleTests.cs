using CopilotLedger.Core.Audit;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Audit;

public sealed class TokenRuleTests
{
    public static TheoryData<string, string> BloatCases => new()
    {
        { "below ratio", "none" },
        { "below input", "none" },
        { "at", "info [inputTokens=50000 tokens, outputTokens=250 tokens, inputOutputRatio=200 ratio] null context_bloat" },
        { "exact", "info [inputTokens=260000 tokens, outputTokens=1000 tokens, inputOutputRatio=260 ratio] null context_bloat" },
        {
            "repeating ratio",
            "info [inputTokens=100000 tokens, outputTokens=300 tokens, inputOutputRatio=333.33333333333333333333333333 ratio] null context_bloat"
        },
        { "zero output", "info [inputTokens=80000 tokens, outputTokens=0 tokens, zeroOutput=1 flag] null context_bloat_zero_output" },
    };

    [Theory]
    [MemberData(nameof(BloatCases))]
    public void AR1_ContextBloat_BelowAtAndExact(string name, string expected)
    {
        (long input, long output) = name switch
        {
            "below ratio" => (50_000L, 251L),
            "below input" => (49_999L, 0L),
            "at" => (50_000L, 250L),
            "exact" => (260_000L, 1_000L),
            "repeating ratio" => (100_000L, 300L),
            "zero output" => (80_000L, 0L),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };
        AuditInput audit = AuditFixtures.Input([AuditFixtures.Span(1) with { InputTokens = input, OutputTokens = output }]);

        Assert.Equal(expected, AuditFixtures.Describe(AuditFixtures.Run(AuditRuleIds.ContextBloat, audit)));
    }

    public static TheoryData<string, string> GrowthCases => new()
    {
        { "below count", "none" },
        { "below ratio", "none" },
        {
            "at",
            "info [requests=10 requests, firstInputTokens=10000 tokens, lastInputTokens=40000 tokens, peakInputTokens=40000 tokens, growthRatio=4 ratio] null context_growth"
        },
        {
            "exact",
            "info [requests=12 requests, firstInputTokens=20000 tokens, lastInputTokens=92000 tokens, peakInputTokens=95000 tokens, growthRatio=4.6 ratio] null context_growth"
        },
        { "models alternate", "none" },
        { "invocations alternate", "none" },
    };

    [Theory]
    [MemberData(nameof(GrowthCases))]
    public void AR2_ContextGrowth_BelowAtAndExact(string name, string expected)
    {
        AuditSpan[] spans = name switch
        {
            "below count" => Growth([.. Enumerable.Range(0, 9).Select(k => 10_000L + 3_750L * k)]),
            "below ratio" => Growth([.. Enumerable.Range(0, 9).Select(k => 10_000L + 3_333L * k), 39_999L]),
            "at" => Growth([.. Enumerable.Range(0, 9).Select(k => 10_000L + 3_333L * k), 40_000L]),
            "exact" => Growth([.. Enumerable.Range(0, 11).Select(k => 20_000L + 7_500L * k), 92_000L]),
            "models alternate" =>
            [
                .. Growth([.. Enumerable.Range(0, 9).Select(k => 10_000L + 3_333L * k), 40_000L])
                    .Select((span, index) => index % 2 == 0 ? span : span with { Model = "claude-sonnet-5.5" }),
            ],
            "invocations alternate" =>
            [
                .. Growth([.. Enumerable.Range(0, 9).Select(k => 10_000L + 3_333L * k), 40_000L])
                    .Select((span, index) => index % 2 == 0 ? span : span with { AgentInvocationId = "b0000000000000b0" }),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

        Assert.Equal(expected, AuditFixtures.Describe(AuditFixtures.Run(AuditRuleIds.ContextGrowth, AuditFixtures.Input(spans))));
    }

    public static TheoryData<string, string, int, int> CacheCases => new()
    {
        { "below", "none", 0, 0 },
        {
            "at",
            "warning [requests=5 requests, inputTokens=200000 tokens, cacheReadInputTokens=0 tokens, uncachedInputTokens=200000 tokens, cacheHitRatio=0 ratio] 0.76 cache_miss_streak",
            0, 0
        },
        {
            "exact",
            "warning [requests=5 requests, inputTokens=200000 tokens, cacheReadInputTokens=15000 tokens, uncachedInputTokens=175000 tokens, cacheHitRatio=0.075 ratio] 0.665 cache_miss_streak",
            0, 0
        },
        { "hit boundary", "none", 0, 0 },
        { "small request", "none", 0, 0 },
        { "absent counters break", "none", 1, 0 },
        { "legacy counters break", "none", 0, 1 },
        {
            "high",
            "high [requests=10 requests, inputTokens=400000 tokens, cacheReadInputTokens=0 tokens, uncachedInputTokens=400000 tokens, cacheHitRatio=0 ratio] 1.52 cache_miss_streak",
            0, 0
        },
        {
            "local",
            "warning [requests=5 requests, inputTokens=200000 tokens, cacheReadInputTokens=0 tokens, uncachedInputTokens=200000 tokens, cacheHitRatio=0 ratio] 0 cache_miss_streak",
            0, 0
        },
        {
            "unpriced",
            "warning [requests=5 requests, inputTokens=200000 tokens, cacheReadInputTokens=0 tokens, uncachedInputTokens=200000 tokens, cacheHitRatio=0 ratio] null cache_miss_streak",
            0, 0
        },
        {
            "unpriced known model",
            "warning [requests=5 requests, inputTokens=200000 tokens, cacheReadInputTokens=0 tokens, uncachedInputTokens=200000 tokens, cacheHitRatio=0 ratio] null cache_miss_streak",
            0, 0
        },
        {
            "no rate for the date",
            "warning [requests=5 requests, inputTokens=200000 tokens, cacheReadInputTokens=0 tokens, uncachedInputTokens=200000 tokens, cacheHitRatio=0 ratio] null cache_miss_streak",
            0, 0
        },
        {
            "no cached rate",
            "warning [requests=5 requests, inputTokens=200000 tokens, cacheReadInputTokens=0 tokens, uncachedInputTokens=200000 tokens, cacheHitRatio=0 ratio] 0 cache_miss_streak",
            0, 0
        },
    };

    [Theory]
    [MemberData(nameof(CacheCases))]
    public void AR3_CacheMissStreak_BelowAtAndExact(string name, string expected, int absent, int legacy)
    {
        AuditInput audit = AuditFixtures.Input(name switch
        {
            "below" => Misses(4),
            "at" => Misses(5),
            "exact" => [.. Misses(5).Select(span => span with { CacheReadInputTokens = 3_000, CacheWriteInputTokens = 2_000 })],
            "hit boundary" => [.. Misses(5).Select(span => span.SpanId.EndsWith('3') ? span with { CacheReadInputTokens = 4_000 } : span)],
            "small request" => [.. Misses(5).Select(span => span.SpanId.EndsWith('3') ? span with { InputTokens = 19_999 } : span)],
            "absent counters break" => [.. Misses(7).Select(span => span.SpanId.EndsWith('4')
                ? span with { CacheReadState = CacheCounterState.Absent, CacheWriteState = CacheCounterState.Absent }
                : span)],
            "legacy counters break" => [.. Misses(7).Select(span => span.SpanId.EndsWith('4')
                ? span with { CacheReadState = CacheCounterState.LegacyUnknown, CacheWriteState = CacheCounterState.LegacyUnknown }
                : span)],
            "high" => Misses(10),
            "local" => [.. Misses(5).Select(span => span with
            {
                Channel = BillingChannel.Local,
                PricingStatus = PricingStatus.LocalOffload,
                CostUsd = 0m,
                AiCredits = 0m,
            })],
            "unpriced" => [.. Misses(5).Select(span => span with
            {
                Model = "gpt-unknown",
                PricingStatus = PricingStatus.UnknownModel,
                CostUsd = 0m,
                AiCredits = 0m,
            })],
            "unpriced known model" => [.. Misses(5).Select(span => span with
            {
                PricingStatus = PricingStatus.UnknownModel,
                CostUsd = 0m,
                AiCredits = 0m,
            })],
            "no rate for the date" => [.. Misses(5).Select(span => span.SpanId.EndsWith('3')
                ? span with { PricingStatus = PricingStatus.NoRateForDate, CostUsd = 0m, AiCredits = 0m }
                : span)],
            "no cached rate" => [.. Misses(5).Select(span => span with { Model = "claude-sonnet-5.5" })],
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        });
        if (name == "no cached rate")
        {
            audit = audit with
            {
                Pricing = JsonRateCardCalculator.Parse(
                    RateFixtures.ByokTestRatesJson.Replace("\"cachedInputPer1M\": 0.10", "\"cachedInputPer1M\": null", StringComparison.Ordinal)),
            };
        }

        Assert.Equal(expected, AuditFixtures.Describe(AuditFixtures.Run(AuditRuleIds.CacheMissStreak, audit)));
        AuditSummary summary = AuditAnalyzer.Summarize(audit);
        Assert.Equal(absent, summary.CacheAbsentSpans);
        Assert.Equal(legacy, summary.CacheLegacyUnknownSpans);
    }

    [Fact]
    public void AR4_TokenRuleFindings_CarryTheirScopeAndWindow()
    {
        AuditSpan bloated = AuditFixtures.Span(1) with
        {
            InputTokens = 60_000,
            OutputTokens = 100,
            AgentName = "explore",
            PullRequestNumber = 12,
            Branch = "feature/search",
        };
        AuditSpan[] growth = Growth([.. Enumerable.Range(0, 9).Select(k => 10_000L + 3_333L * k), 40_000L]);
        AuditSpan[] misses = Misses(5);
        AuditInput audit = AuditFixtures.Input([bloated, .. growth, .. misses]);

        AuditFinding bloat = Assert.Single(AuditFixtures.Run(AuditRuleIds.ContextBloat, audit with { Spans = [bloated] }));
        AuditFinding grown = Assert.Single(AuditFixtures.Run(AuditRuleIds.ContextGrowth, audit with { Spans = growth }));
        AuditFinding streak = Assert.Single(AuditFixtures.Run(AuditRuleIds.CacheMissStreak, audit with { Spans = misses }));

        Assert.Equal(
            new AuditScope
            {
                Repository = "contoso/webapp",
                Branch = "feature/search",
                PullRequestNumber = 12,
                Agent = "explore",
                Model = "claude-opus-5.5",
                TraceId = AuditFixtures.TraceId,
                ConversationId = AuditFixtures.Conversation,
            },
            bloat.Scope);
        Assert.Equal((bloated.StartTime, bloated.EndTime), (bloat.WindowStart, bloat.WindowEnd));
        Assert.Equal("(none)", grown.Scope.Agent);
        Assert.Equal("claude-opus-5.5", grown.Scope.Model);
        Assert.Null(grown.Scope.Tool);
        Assert.Equal((growth[0].StartTime, growth[^1].EndTime), (grown.WindowStart, grown.WindowEnd));
        Assert.Equal((misses[0].StartTime, misses[^1].EndTime), (streak.WindowStart, streak.WindowEnd));
        Assert.Equal(AuditFixtures.TraceId, streak.Scope.TraceId);
    }

    // Requests of the default invocation, 30 seconds apart, with these inputs and no cache reads.
    private static AuditSpan[] Growth(long[] inputs) =>
    [
        .. inputs.Select((tokens, index) =>
            AuditFixtures.Span(index + 1, TimeSpan.FromSeconds(30 * index)) with { InputTokens = tokens, OutputTokens = 1_000 }),
    ];

    // count cache misses of claude-opus-5.5, a minute apart: 40,000 input tokens, none read from the cache.
    private static AuditSpan[] Misses(int count) =>
    [
        .. Enumerable.Range(1, count).Select(number =>
            AuditFixtures.Span(number, TimeSpan.FromMinutes(number)) with { InputTokens = 40_000, OutputTokens = 1_000 }),
    ];
}
