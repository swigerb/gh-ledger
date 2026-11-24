using CopilotLedger.Core.Audit;
using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Audit;

public sealed class VelocityRuleTests
{
    public static TheoryData<string, string> VelocityCases => new()
    {
        { "below", "none" },
        {
            "at",
            "warning [requests=30 requests, windowMinutes=5 minutes, windowCostUsd=0.936 usd, conversationMedianCostUsd=0.01 usd] 0.926 turn_velocity"
        },
        { "window edge", "none" },
        {
            "only the burst",
            "warning [requests=30 requests, windowMinutes=5 minutes, windowCostUsd=0.936 usd, conversationMedianCostUsd=0.936 usd] 0 turn_velocity"
        },
        {
            "high",
            "high [requests=60 requests, windowMinutes=5 minutes, windowCostUsd=0.6 usd, conversationMedianCostUsd=0.6 usd] 0 turn_velocity"
        },
        { "unpriced", "warning [requests=30 requests, windowMinutes=5 minutes] null turn_velocity" },
    };

    [Theory]
    [MemberData(nameof(VelocityCases))]
    public void AR5_TurnVelocity_BelowAtAndExact(string name, string expected)
    {
        AuditSpan[] spans = name switch
        {
            "below" => Burst(29, 10_000, 0.0312m),
            "at" => [.. Burst(30, 10_000, 0.0312m), .. OtherTrace()],
            "window edge" => Burst(30, 10_350, 0.0312m),
            "only the burst" => Burst(30, 10_000, 0.0312m),
            "high" => Burst(60, 5_000, 0.01m),
            "unpriced" =>
            [
                .. Burst(30, 10_000, 0.0312m).Select(span => span.SpanId.EndsWith("0f", StringComparison.Ordinal)
                    ? span with { PricingStatus = PricingStatus.UnknownModel, CostUsd = 0m, AiCredits = 0m }
                    : span),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

        Assert.Equal(expected, AuditFixtures.Describe(AuditFixtures.Run(AuditRuleIds.TurnVelocity, AuditFixtures.Input(spans))));
    }

    public static TheoryData<string, string> NearBreakerCases => new()
    {
        { "below", "none" },
        {
            "at",
            "info [windowCostUsd=1.2 usd, windowAiCredits=120 ai_credits, breakerVelocityAiCredits=150 ai_credits, breakerFraction=0.8 ratio, windowMinutes=5 minutes] null velocity_near_breaker"
        },
        {
            "exact",
            "info [windowCostUsd=1.25 usd, windowAiCredits=125 ai_credits, breakerVelocityAiCredits=150 ai_credits, breakerFraction=0.8333333333333333333333333333 ratio, windowMinutes=5 minutes] null velocity_near_breaker"
        },
        { "trips", "none" },
        { "apart", "none" },
        {
            "custom settings",
            "info [windowCostUsd=0.85 usd, windowAiCredits=85 ai_credits, breakerVelocityAiCredits=100 ai_credits, breakerFraction=0.85 ratio, windowMinutes=10 minutes] null velocity_near_breaker"
        },
    };

    [Theory]
    [MemberData(nameof(NearBreakerCases))]
    public void AR6_VelocityNearBreaker_BelowAtAndExact(string name, string expected)
    {
        AuditInput input = name switch
        {
            "below" => AuditFixtures.Input(Spend(7, 30, 0.17m)),
            "at" => AuditFixtures.Input(Spend(4, 60, 0.30m)),
            "exact" => AuditFixtures.Input(Spend(5, 50, 0.25m)),
            "trips" => AuditFixtures.Input(Spend(4, 60, 0.3775m)),
            "apart" => AuditFixtures.Input(Spend(2, 360, 0.70m)),
            "custom settings" => AuditFixtures.Input(Spend(5, 120, 0.17m)) with
            {
                Breaker = new BreakerSettings { VelocityCredits = 100m, VelocityWindowMinutes = 10 },
            },
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

        Assert.Equal(expected, AuditFixtures.Describe(AuditFixtures.Run(AuditRuleIds.VelocityNearBreaker, input)));
    }

    public static TheoryData<string, string> PremiumCases => new()
    {
        { "below", "none" },
        {
            "at",
            "warning [requests=10 requests, outputTokens=1200 tokens, modelOutputRatePer1M=20 usd_per_1m_tokens, topQuartileOutputRatePer1M=15 usd_per_1m_tokens] 0.273 premium_model_trivial_turn"
        },
        {
            "exact",
            "warning [requests=12 requests, outputTokens=1440 tokens, modelOutputRatePer1M=20 usd_per_1m_tokens, topQuartileOutputRatePer1M=15 usd_per_1m_tokens] 0.3276 premium_model_trivial_turn"
        },
        { "output above the limit", "none" },
        { "tool count unknown", "none" },
        { "not premium", "none" },
        {
            "no economy model",
            "warning [requests=10 requests, outputTokens=1200 tokens, modelOutputRatePer1M=20 usd_per_1m_tokens, topQuartileOutputRatePer1M=15 usd_per_1m_tokens] null premium_model_trivial_turn"
        },
        {
            "unpriced known model",
            "warning [requests=10 requests, outputTokens=1200 tokens, modelOutputRatePer1M=20 usd_per_1m_tokens, topQuartileOutputRatePer1M=15 usd_per_1m_tokens] null premium_model_trivial_turn"
        },
        {
            "no rate for the date",
            "warning [requests=10 requests, outputTokens=1200 tokens, modelOutputRatePer1M=20 usd_per_1m_tokens, topQuartileOutputRatePer1M=15 usd_per_1m_tokens] null premium_model_trivial_turn"
        },
        { "day boundary", "none" },
        {
            "high",
            "high [requests=20 requests, outputTokens=2400 tokens, modelOutputRatePer1M=20 usd_per_1m_tokens, topQuartileOutputRatePer1M=15 usd_per_1m_tokens] 0.546 premium_model_trivial_turn"
        },
        { "without pricing", "none" },
    };

    [Theory]
    [MemberData(nameof(PremiumCases))]
    public void AR7_PremiumModelTrivialTurn_BelowAtAndExact(string name, string expected)
    {
        AuditInput input = name switch
        {
            "below" => AuditFixtures.Input(Trivial(9)),
            "at" => AuditFixtures.Input(Trivial(10)),
            "exact" => AuditFixtures.Input(Trivial(12)),
            "output above the limit" => AuditFixtures.Input(
                Trivial(10).Select(span => span.SpanId.EndsWith('5') ? span with { OutputTokens = 201 } : span)),
            "tool count unknown" => AuditFixtures.Input(
                Trivial(10).Select(span => span.SpanId.EndsWith('5') ? span with { ToolCallCount = null } : span)),
            "not premium" => AuditFixtures.Input(Trivial(10).Select(span => span with { Model = "claude-sonnet-5.5" })),
            "no economy model" => AuditFixtures.Input(Trivial(10)) with { EconomyModel = null },
            "unpriced known model" => AuditFixtures.Input(
                Trivial(10).Select(span => span with { PricingStatus = PricingStatus.UnknownModel, CostUsd = 0m, AiCredits = 0m })),
            "no rate for the date" => AuditFixtures.Input(
                Trivial(10).Select(span => span.SpanId.EndsWith('5')
                    ? span with { PricingStatus = PricingStatus.NoRateForDate, CostUsd = 0m, AiCredits = 0m }
                    : span)),
            "day boundary" => AuditFixtures.Input(Trivial(10, TimeSpan.FromMinutes(14 * 60 + 50))),
            "high" => AuditFixtures.Input(Trivial(20)),
            "without pricing" => AuditFixtures.Input(Trivial(10)) with { Pricing = null },
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

        Assert.Equal(expected, AuditFixtures.Describe(AuditFixtures.Run(AuditRuleIds.PremiumModelTrivialTurn, input)));
        Assert.Equal(input.Pricing is not null, AuditAnalyzer.EvaluatedRules(input).Contains(AuditRuleIds.PremiumModelTrivialTurn));
    }

    [Fact]
    public void AR8_VelocityAndPremiumFindings_CarryTheirScopeAndWindow()
    {
        AuditSpan[] burst = Burst(30, 10_000, 0.0312m);
        AuditSpan[] spend = Spend(4, 60, 0.30m);
        AuditSpan[] trivial = [.. Trivial(10).Select(span => span with { AgentName = "explore", PullRequestNumber = 7 })];

        AuditFinding velocity = Assert.Single(AuditFixtures.Run(AuditRuleIds.TurnVelocity, AuditFixtures.Input(burst)));
        AuditFinding near = Assert.Single(AuditFixtures.Run(AuditRuleIds.VelocityNearBreaker, AuditFixtures.Input(spend)));
        AuditFinding premium = Assert.Single(AuditFixtures.Run(AuditRuleIds.PremiumModelTrivialTurn, AuditFixtures.Input(trivial)));

        Assert.Equal(
            new AuditScope
            {
                Repository = "contoso/webapp",
                Branch = "main",
                Agent = "(none)",
                TraceId = AuditFixtures.TraceId,
                ConversationId = AuditFixtures.Conversation,
            },
            velocity.Scope);
        Assert.Equal((burst[0].StartTime, burst[^1].EndTime), (velocity.WindowStart, velocity.WindowEnd));
        Assert.Equal(
            new AuditScope
            {
                Repository = "contoso/webapp",
                Branch = "main",
                TraceId = AuditFixtures.TraceId,
                ConversationId = AuditFixtures.Conversation,
            },
            near.Scope);
        Assert.Equal((spend[0].StartTime, spend[^1].EndTime), (near.WindowStart, near.WindowEnd));
        Assert.Equal(
            new AuditScope
            {
                Repository = "contoso/webapp",
                Branch = "main",
                PullRequestNumber = 7,
                Agent = "explore",
                Model = "claude-opus-5.5",
            },
            premium.Scope);
        Assert.Equal((trivial[0].StartTime, trivial[^1].EndTime), (premium.WindowStart, premium.WindowEnd));
    }

    // count requests of the default invocation, everyMilliseconds apart from Start, each billed cost.
    private static AuditSpan[] Burst(int count, int everyMilliseconds, decimal cost) =>
    [
        .. Enumerable.Range(1, count).Select(number =>
            AuditFixtures.Span(number, TimeSpan.FromMilliseconds((long)everyMilliseconds * (number - 1))) with
            {
                CostUsd = cost,
                AiCredits = cost / 0.01m,
            }),
    ];

    // Four $0.01 requests of another trace and invocation in the same conversation, at 15:00, 15:10, 15:20, and 15:30Z.
    private static AuditSpan[] OtherTrace() =>
    [
        .. Enumerable.Range(0, 4).Select(index =>
            AuditFixtures.Span(100 + index, TimeSpan.FromMinutes(60 + 10 * index)) with
            {
                TraceId = "b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1",
                AgentInvocationId = "b100000000000001",
            }),
    ];

    // count requests of one trace ending everySeconds apart, each billed cost.
    private static AuditSpan[] Spend(int count, int everySeconds, decimal cost) => Burst(count, everySeconds * 1_000, cost);

    // count short, tool-free claude-opus-5.5 requests 2 minutes apart from Start + offset: 30,000 input tokens (24,000 read
    // from the cache) and 120 output tokens, billed $0.0312 each.
    private static AuditSpan[] Trivial(int count, TimeSpan offset = default) =>
    [
        .. Enumerable.Range(1, count).Select(number =>
            AuditFixtures.Span(number, offset + TimeSpan.FromMinutes(2 * (number - 1))) with
            {
                InputTokens = 30_000,
                CacheReadInputTokens = 24_000,
                OutputTokens = 120,
                CostUsd = 0.0312m,
                AiCredits = 3.12m,
            }),
    ];
}
