using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Pricing;

namespace CopilotLedger.Core.Audit;

// velocity_near_breaker: a prompt chain (trace) whose spend came close to the circuit breaker's credit velocity rule without
// tripping it. It uses the breaker's own window, ordering, and stored settings.
internal sealed class VelocityNearBreakerRule : IAuditRule
{
    public string Id => AuditRuleIds.VelocityNearBreaker;

    public IEnumerable<AuditFinding> Evaluate(AuditInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        BreakerSettings breaker = input.Breaker;
        decimal threshold = breaker.VelocityThresholdUsd;
        TimeSpan window = breaker.VelocityWindow;
        foreach (IGrouping<string, AuditSpan> trace in input.Spans
            .GroupBy(span => span.TraceId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            AuditSpan first = AuditMath.ByStart(trace)[0];
            List<AuditSpan> events =
            [
                .. trace
                    .Where(span => span.CostUsd > 0m)
                    .OrderBy(span => span.EndTime)
                    .ThenBy(span => span.SpanId, StringComparer.Ordinal),
            ];
            CostEvent[] costs = [.. events.Select(span => new CostEvent(span.SpanId, span.EndTime, span.CostUsd))];
            if (BreakerRules.FindVelocityTrip(costs, threshold, window) is not null)
            {
                continue;
            }

            // The breaker's window: the events ending after end_j - window. The peak is the first window with the most spend.
            decimal sum = 0m;
            decimal peak = 0m;
            int head = 0;
            int peakFirst = -1;
            int peakLast = -1;
            for (int index = 0; index < events.Count; index++)
            {
                sum += events[index].CostUsd;
                DateTimeOffset cutoff = events[index].EndTime - window;
                while (events[head].EndTime <= cutoff)
                {
                    sum -= events[head].CostUsd;
                    head++;
                }

                if (sum > peak)
                {
                    peak = sum;
                    peakFirst = head;
                    peakLast = index;
                }
            }

            if (peakLast < 0 || peak < input.Thresholds.NearBreakerFraction * threshold)
            {
                continue;
            }

            yield return new AuditFinding
            {
                RuleId = Id,
                Severity = AuditSeverity.Info,
                Scope = new AuditScope
                {
                    Repository = first.Repository,
                    Branch = first.Branch,
                    PullRequestNumber = first.PullRequestNumber,
                    TraceId = first.TraceId,
                    ConversationId = first.ConversationId,
                },
                WindowStart = events.GetRange(peakFirst, peakLast - peakFirst + 1).Min(span => span.StartTime),
                WindowEnd = events[peakLast].EndTime,
                Metrics =
                [
                    new AuditMetric("windowCostUsd", peak, AuditUnits.Usd),
                    new AuditMetric("windowAiCredits", AiCreditMath.UsdToAiCredits(peak), AuditUnits.AiCredits),
                    new AuditMetric("breakerVelocityAiCredits", breaker.VelocityCredits, AuditUnits.AiCredits),
                    new AuditMetric("breakerFraction", peak / threshold, AuditUnits.Ratio),
                    new AuditMetric("windowMinutes", breaker.VelocityWindowMinutes, AuditUnits.Minutes),
                ],
                EstimatedImpactUsd = null,
                RecommendationKey = Id,
            };
        }
    }
}
