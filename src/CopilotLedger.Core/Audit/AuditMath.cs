using CopilotLedger.Core.Pricing;

namespace CopilotLedger.Core.Audit;

// Helpers the rules share. No rounding: comparisons multiply, and money is a sum of exact decimal amounts.
internal static class AuditMath
{
    public const string NoAgent = "(none)";

    // The agent label of a finding's scope: the agent's name, or (none) for the default agent.
    public static string Agent(string? agentName) => agentName ?? NoAgent;

    // Requests in (start, span ID ordinal) order.
    public static List<AuditSpan> ByStart(IEnumerable<AuditSpan> spans) =>
    [
        .. spans
            .OrderBy(span => span.StartTime)
            .ThenBy(span => span.SpanId, StringComparer.Ordinal),
    ];

    // Tool calls in (start, span ID ordinal) order.
    public static List<AuditToolCall> ByStart(IEnumerable<AuditToolCall> calls) =>
    [
        .. calls
            .OrderBy(call => call.StartTime)
            .ThenBy(call => call.SpanId, StringComparer.Ordinal),
    ];

    // The billed cost of spans: null when any of them has no known billed price; 0 for none.
    public static decimal? SumBilled(IEnumerable<AuditSpan> spans)
    {
        decimal sum = 0m;
        foreach (AuditSpan span in spans)
        {
            if (!span.BilledKnown)
            {
                return null;
            }

            sum += span.CostUsd;
        }

        return sum;
    }

    // The latest end of spans, which must not be empty.
    public static DateTimeOffset LatestEnd(IEnumerable<AuditSpan> spans) => spans.Max(span => span.EndTime);

    // What span's tokens cost at tier, as JsonRateCardCalculator prices them: uncached input, cache reads (at the input
    // rate without a cached rate), cache writes (likewise), and output.
    public static decimal Cost(RateTier tier, AuditSpan span)
    {
        long uncached = Math.Max(0, span.InputTokens - span.CacheReadInputTokens - span.CacheWriteInputTokens);
        return AiCreditMath.CostUsd(uncached, tier.InputPer1M) +
            AiCreditMath.CostUsd(span.CacheReadInputTokens, tier.CachedInputPer1M ?? tier.InputPer1M) +
            AiCreditMath.CostUsd(span.CacheWriteInputTokens, tier.CacheWritePer1M ?? tier.InputPer1M) +
            AiCreditMath.CostUsd(span.OutputTokens, tier.OutputPer1M);
    }

    // Warning at the trigger, High at twice it.
    public static AuditSeverity Severity(long count, long trigger) =>
        count >= 2 * trigger ? AuditSeverity.High : AuditSeverity.Warning;

    // The request sequences of the agent invocations: requests with an agent identity, partitioned by repository, branch,
    // pull request, trace, and invocation (and model when byModel), each in start order. Partitions in ordinal key order.
    public static IEnumerable<List<AuditSpan>> Sequences(IEnumerable<AuditSpan> spans, bool byModel) =>
        spans
            .Where(span => span.AgentInvocationId is not null)
            .GroupBy(span => (span.Repository, span.Branch, span.PullRequestNumber, span.TraceId, span.AgentInvocationId,
                Model: byModel ? span.Model : null))
            .OrderBy(group => group.Key.Repository, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Branch, StringComparer.Ordinal)
            .ThenBy(group => group.Key.PullRequestNumber ?? 0)
            .ThenBy(group => group.Key.TraceId, StringComparer.Ordinal)
            .ThenBy(group => group.Key.AgentInvocationId, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Model, StringComparer.Ordinal)
            .Select(group => ByStart(group));

    // The scope of a request sequence or a single request: its first request's repository, branch, pull request, agent,
    // trace, and conversation, with model when withModel.
    public static AuditScope Scope(AuditSpan first, bool withModel) => new()
    {
        Repository = first.Repository,
        Branch = first.Branch,
        PullRequestNumber = first.PullRequestNumber,
        Agent = Agent(first.AgentName),
        Model = withModel ? first.Model : null,
        TraceId = first.TraceId,
        ConversationId = first.ConversationId,
    };
}
