using CopilotLedger.Core.Audit;
using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Audit;

public sealed class ToolRuleTests
{
    public static TheoryData<string, string> LoopCases => new()
    {
        { "below", "none" },
        { "at", "warning [calls=8 calls, requests=7 requests] 0.07 tool_loop" },
        { "outcomes change", "none" },
        { "another tool between", "none" },
        { "unknown outcomes", "warning [calls=8 calls, requests=0 requests] 0 tool_loop" },
        { "unnamed call between", "none" },
        { "unknown invocation between", "none" },
        { "unknown invocation of another agent between", "warning [calls=8 calls, requests=8 requests] 0.08 tool_loop" },
        { "parallel batch", "warning [calls=8 calls, requests=0 requests] 0 tool_loop" },
        { "unpriced request", "warning [calls=8 calls, requests=7 requests] null tool_loop" },
        { "high", "high [calls=16 calls, requests=15 requests] 0.15 tool_loop" },
        { "high local", "warning [calls=16 calls, requests=15 requests] 0 tool_loop" },
        { "high unpriced", "warning [calls=16 calls, requests=15 requests] null tool_loop" },
        { "high parallel batch", "warning [calls=16 calls, requests=0 requests] 0 tool_loop" },
    };

    [Theory]
    [MemberData(nameof(LoopCases))]
    public void AR9_ToolLoop_BelowAtAndExact(string name, string expected)
    {
        AuditInput input = name switch
        {
            "below" => Interleaved(7, _ => ("view", null, 1)),
            "at" => Interleaved(8, _ => ("view", null, 1)),
            "outcomes change" => Interleaved(8, k => k < 4 ? ("view", null, 1) : ("view", null, 2)),
            "another tool between" => Interleaved(9, k => k == 4 ? ("grep", null, 1) : ("view", null, 1)),
            "unknown outcomes" => AuditFixtures.Input([], Calls(8, _ => 5, _ => ("view", null, 0))),
            "unnamed call between" => Interleaved(9, k => k == 4 ? (null, null, 1) : ("view", null, 1)),
            "unknown invocation between" => MapCalls(
                Interleaved(9, _ => ("view", null, 1)),
                (call, k) => k == 4 ? call with { AgentInvocationId = null } : call),
            "unknown invocation of another agent between" => MapCalls(
                Interleaved(9, _ => ("view", null, 1)),
                (call, k) => k == 4 ? call with { AgentInvocationId = null, AgentName = "explore" } : call),
            "parallel batch" => AuditFixtures.Input([AuditFixtures.Span(1)], Calls(8, _ => 5, _ => ("view", null, 1))),
            "unpriced request" => MapSpans(
                Interleaved(8, _ => ("view", null, 1)),
                span => span.SpanId.EndsWith('4') ? span with { PricingStatus = PricingStatus.UnknownModel } : span),
            "high" => Interleaved(16, _ => ("view", null, 1)),
            "high local" => MapSpans(
                Interleaved(16, _ => ("view", null, 1)),
                span => span with
                {
                    Channel = BillingChannel.Local,
                    PricingStatus = PricingStatus.LocalOffload,
                    CostUsd = 0m,
                    AiCredits = 0m,
                }),
            "high unpriced" => MapSpans(
                Interleaved(16, _ => ("view", null, 1)),
                span => span.SpanId.EndsWith('4') ? span with { PricingStatus = PricingStatus.UnknownModel } : span),
            "high parallel batch" => AuditFixtures.Input(
                [AuditFixtures.Span(1)],
                Calls(16, _ => 5, _ => ("view", null, 1))),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

        Assert.Equal(expected, AuditFixtures.Describe(AuditFixtures.Run(AuditRuleIds.ToolLoop, input)));
    }

    public static TheoryData<string, string> RetryCases => new()
    {
        { "below", "none" },
        { "at", "warning [failures=3 calls, requests=2 requests, breakerChurnFailures=5 calls] 0.02 failed_tool_retry" },
        { "high", "high [failures=4 calls, requests=3 requests, breakerChurnFailures=5 calls] 0.03 failed_tool_retry" },
        { "another tool between", "none" },
        { "agents interleaved", "none" },
        {
            "another invocation's call between",
            "warning [failures=3 calls, requests=3 requests, breakerChurnFailures=5 calls] 0.03 failed_tool_retry"
        },
        { "unknown invocation between", "none" },
        {
            "unknown invocation of another agent between",
            "warning [failures=3 calls, requests=3 requests, breakerChurnFailures=5 calls] 0.03 failed_tool_retry"
        },
        { "unknown outcome between", "none" },
        { "success between", "none" },
        { "imported", "warning [failures=3 calls, requests=2 requests, breakerChurnFailures=5 calls] 0.02 failed_tool_retry" },
        { "unpriced request", "warning [failures=3 calls, requests=2 requests, breakerChurnFailures=5 calls] null failed_tool_retry" },
        { "churn 3", "high [failures=2 calls, requests=1 requests, breakerChurnFailures=3 calls] 0.01 failed_tool_retry" },
    };

    [Theory]
    [MemberData(nameof(RetryCases))]
    public void AR10_FailedToolRetry_BelowAtAndExact(string name, string expected)
    {
        AuditInput input = name switch
        {
            "below" => Interleaved(2, _ => ("bash", 1, 2)),
            "at" => Interleaved(3, _ => ("bash", 1, 2)),
            "high" => Interleaved(4, _ => ("bash", 1, 2)),
            "another tool between" => Interleaved(4, k => k == 1 ? ("grep", 1, 2) : ("bash", 1, 2)),
            "agents interleaved" => MapCalls(
                Interleaved(3, _ => ("bash", 1, 2)),
                (call, k) => k == 1 ? call with { AgentInvocationId = "b0000000000000b0" } : call),
            "another invocation's call between" => MapCalls(
                Interleaved(4, k => k == 1 ? ("view", 0, 1) : ("bash", 1, 2)),
                (call, k) => k == 1 ? call with { AgentInvocationId = "b0000000000000b0" } : call),
            "unknown invocation between" => MapCalls(
                Interleaved(4, _ => ("bash", 1, 2)),
                (call, k) => k == 1 ? call with { AgentInvocationId = null } : call),
            "unknown invocation of another agent between" => MapCalls(
                Interleaved(4, _ => ("bash", 1, 2)),
                (call, k) => k == 1 ? call with { AgentInvocationId = null, AgentName = "explore" } : call),
            "unknown outcome between" => Interleaved(4, k => k == 1 ? ("bash", null, 0) : ("bash", 1, 2)),
            "success between" => Interleaved(4, k => k == 1 ? ("bash", 0, 1) : ("bash", 1, 2)),
            "imported" => MapCalls(Interleaved(3, _ => ("bash", null, 2)), (call, _) => call with { Imported = true }),
            "unpriced request" => MapSpans(
                Interleaved(3, _ => ("bash", 1, 2)),
                span => span.SpanId.EndsWith('2') ? span with { PricingStatus = PricingStatus.UnknownModel } : span),
            "churn 3" => Interleaved(2, _ => ("bash", 1, 2)) with { Breaker = new BreakerSettings { ChurnFailures = 3 } },
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

        Assert.Equal(expected, AuditFixtures.Describe(AuditFixtures.Run(AuditRuleIds.FailedToolRetry, input)));
    }

    [Fact]
    public void AR11_TheAnalyzer_ImplementsEveryRuleInOrder()
    {
        AuditInput input = AuditFixtures.Input([]);

        Assert.Equal(AuditRuleIds.All, AuditAnalyzer.Rules.Select(rule => rule.Id));
        Assert.Equal(AuditRuleIds.All, AuditAnalyzer.EvaluatedRules(input));
        Assert.Equal(
            [AuditRuleIds.ToolLoop, AuditRuleIds.ContextBloat],
            AuditAnalyzer.EvaluatedRules(input with { Rules = [AuditRuleIds.ContextBloat, AuditRuleIds.ToolLoop] }));
        Assert.DoesNotContain(AuditRuleIds.PremiumModelTrivialTurn, AuditAnalyzer.EvaluatedRules(input with { Pricing = null }));
    }

    [Fact]
    public void AR12_ToolFindings_CarryTheirChainsScopeTheToolAndTheOutcome()
    {
        AuditInput input = MapCalls(
            Interleaved(8, _ => ("view", null, 1)),
            (call, _) => call with { AgentName = "explore", Branch = "feature/search", PullRequestNumber = 12 });

        AuditFinding finding = Assert.Single(AuditFixtures.Run(AuditRuleIds.ToolLoop, input));

        Assert.Equal(
            new AuditScope
            {
                Repository = "contoso/webapp",
                Branch = "feature/search",
                PullRequestNumber = 12,
                Agent = "explore",
                TraceId = AuditFixtures.TraceId,
                ConversationId = AuditFixtures.Conversation,
                Tool = "view",
                ToolOutcome = AuditToolOutcome.Success,
            },
            finding.Scope);
        Assert.Equal((input.ToolCalls[0].StartTime, input.ToolCalls[^1].StartTime), (finding.WindowStart, finding.WindowEnd));
    }

    // Request k at 10k seconds and call k (from shape(k): tool, exit code, status) at 10k + 5 seconds, for k < count, then a
    // final request at 10 count seconds; every request $0.01.
    private static AuditInput Interleaved(int count, Func<int, (string? Tool, long? ExitCode, int Status)> shape) =>
        AuditFixtures.Input(
            Enumerable.Range(0, count + 1).Select(k => AuditFixtures.Span(k + 1, TimeSpan.FromSeconds(10 * k))),
            Calls(count, k => 10 * k + 5, shape));

    private static IEnumerable<AuditToolCall> Calls(
        int count,
        Func<int, int> second,
        Func<int, (string? Tool, long? ExitCode, int Status)> shape) =>
        Enumerable.Range(0, count).Select(k =>
        {
            (string? tool, long? exitCode, int status) = shape(k);
            return AuditFixtures.Tool(k + 1, TimeSpan.FromSeconds(second(k)), tool, exitCode, status);
        });

    private static AuditInput MapSpans(AuditInput input, Func<AuditSpan, AuditSpan> map) =>
        input with { Spans = [.. input.Spans.Select(map)] };

    private static AuditInput MapCalls(AuditInput input, Func<AuditToolCall, int, AuditToolCall> map) =>
        input with { ToolCalls = [.. input.ToolCalls.Select(map)] };
}
