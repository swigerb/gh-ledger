using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Core.Audit;

// cache_miss_streak: consecutive large requests of one agent invocation to one model that the prompt cache barely served.
// Only requests whose cache usage was reported count: an unknown request ends a streak and is never a miss.
internal sealed class CacheMissStreakRule : IAuditRule
{
    public string Id => AuditRuleIds.CacheMissStreak;

    public IEnumerable<AuditFinding> Evaluate(AuditInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        AuditThresholds thresholds = input.Thresholds;
        foreach (List<AuditSpan> sequence in AuditMath.Sequences(input.Spans, byModel: true))
        {
            int index = 0;
            while (index < sequence.Count)
            {
                if (!IsMiss(sequence[index], thresholds))
                {
                    index++;
                    continue;
                }

                int end = index;
                while (end < sequence.Count && IsMiss(sequence[end], thresholds))
                {
                    end++;
                }

                List<AuditSpan> streak = sequence.GetRange(index, end - index);
                index = end;
                if (streak.Count >= thresholds.CacheMissStreakRequests)
                {
                    yield return Finding(streak, input);
                }
            }
        }
    }

    private AuditFinding Finding(List<AuditSpan> streak, AuditInput input)
    {
        long inputTokens = streak.Sum(span => span.InputTokens);
        long cacheRead = streak.Sum(span => span.CacheReadInputTokens);
        return new AuditFinding
        {
            RuleId = Id,
            Severity = AuditMath.Severity(streak.Count, input.Thresholds.CacheMissStreakRequests),
            Scope = AuditMath.Scope(streak[0], withModel: true),
            WindowStart = streak[0].StartTime,
            WindowEnd = AuditMath.LatestEnd(streak),
            Metrics =
            [
                new AuditMetric("requests", streak.Count, AuditUnits.Requests),
                new AuditMetric("inputTokens", inputTokens, AuditUnits.Tokens),
                new AuditMetric("cacheReadInputTokens", cacheRead, AuditUnits.Tokens),
                new AuditMetric("uncachedInputTokens", streak.Sum(Uncached), AuditUnits.Tokens),
                new AuditMetric("cacheHitRatio", (decimal)cacheRead / inputTokens, AuditUnits.Ratio),
            ],
            EstimatedImpactUsd = Impact(streak, input.Pricing),
            RecommendationKey = Id,
        };
    }

    private static bool IsMiss(AuditSpan span, AuditThresholds thresholds) =>
        CacheMetrics.IsReported(span.CacheReadState, span.CacheWriteState) &&
        span.InputTokens >= thresholds.CacheMissInputTokens &&
        span.CacheReadInputTokens < thresholds.CacheMissHitRatio * span.InputTokens;

    private static long Uncached(AuditSpan span) =>
        Math.Max(0, span.InputTokens - span.CacheReadInputTokens - span.CacheWriteInputTokens);

    // What the uncached input cost above the cached input rate, at each request's billed tier: 0 for a local request; null
    // when a request has no known billed price (BilledKnown), as a recorded sum would be, or no billed tier in the active
    // card.
    private static decimal? Impact(List<AuditSpan> streak, IAuditPricing? pricing)
    {
        decimal sum = 0m;
        foreach (AuditSpan span in streak)
        {
            if (!span.BilledKnown)
            {
                return null;
            }

            if (span.Channel == BillingChannel.Local)
            {
                continue;
            }

            RateTier? tier = span.Channel is BillingChannel.GitHubAiCredits or BillingChannel.FoundryByok
                ? pricing?.FindBilledTier(span.Channel, span.Model, span.RequestModel, span.StartTime, span.InputTokens)
                : null;
            if (tier is null)
            {
                return null;
            }

            decimal difference = Math.Max(0m, tier.InputPer1M - (tier.CachedInputPer1M ?? tier.InputPer1M));
            sum += AiCreditMath.CostUsd(Uncached(span), difference);
        }

        return sum;
    }
}
