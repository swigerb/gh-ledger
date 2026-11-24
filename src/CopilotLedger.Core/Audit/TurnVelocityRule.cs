namespace CopilotLedger.Core.Audit;

// turn_velocity: one agent invocation sending many requests within a few minutes.
internal sealed class TurnVelocityRule : IAuditRule
{
    public string Id => AuditRuleIds.TurnVelocity;

    public IEnumerable<AuditFinding> Evaluate(AuditInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        AuditThresholds thresholds = input.Thresholds;
        TimeSpan window = TimeSpan.FromMinutes(thresholds.TurnVelocityWindowMinutes);
        foreach (List<AuditSpan> sequence in AuditMath.Sequences(input.Spans, byModel: false))
        {
            // The window ending at request j holds requests i..j with start_j - start_i < window; the peak is the first
            // window with the most requests.
            int peak = 0;
            int peakFirst = 0;
            int first = 0;
            for (int index = 0; index < sequence.Count; index++)
            {
                while (sequence[index].StartTime - sequence[first].StartTime >= window)
                {
                    first++;
                }

                if (index - first + 1 > peak)
                {
                    peak = index - first + 1;
                    peakFirst = first;
                }
            }

            if (peak < thresholds.TurnVelocityRequests)
            {
                continue;
            }

            List<AuditSpan> burst = sequence.GetRange(peakFirst, peak);
            decimal? windowCost = AuditMath.SumBilled(burst);
            decimal? median = ConversationMedian(input.Spans, sequence[0], window);
            List<AuditMetric> metrics =
            [
                new AuditMetric("requests", peak, AuditUnits.Requests),
                new AuditMetric("windowMinutes", thresholds.TurnVelocityWindowMinutes, AuditUnits.Minutes),
            ];
            if (windowCost is decimal cost)
            {
                metrics.Add(new AuditMetric("windowCostUsd", cost, AuditUnits.Usd));
            }

            if (median is decimal typical)
            {
                metrics.Add(new AuditMetric("conversationMedianCostUsd", typical, AuditUnits.Usd));
            }

            yield return new AuditFinding
            {
                RuleId = Id,
                Severity = AuditMath.Severity(peak, thresholds.TurnVelocityRequests),
                Scope = AuditMath.Scope(sequence[0], withModel: false),
                WindowStart = burst[0].StartTime,
                WindowEnd = AuditMath.LatestEnd(burst),
                Metrics = metrics,
                EstimatedImpactUsd = windowCost is null || median is null ? null : Math.Max(0m, windowCost.Value - median.Value),
                RecommendationKey = Id,
            };
        }
    }

    // The lower median of the billed cost of the conversation's non-empty windows, aligned at its first request. The
    // conversation is every request with first's conversation ID, or first's trace without one. Null when a window's cost
    // is unknown.
    private static decimal? ConversationMedian(IReadOnlyList<AuditSpan> spans, AuditSpan first, TimeSpan window)
    {
        List<AuditSpan> conversation =
        [
            .. spans.Where(span => first.ConversationId is null
                ? string.Equals(span.TraceId, first.TraceId, StringComparison.Ordinal)
                : string.Equals(span.ConversationId, first.ConversationId, StringComparison.Ordinal)),
        ];
        DateTimeOffset start = conversation.Min(span => span.StartTime);
        List<decimal> costs = [];
        foreach (IGrouping<long, AuditSpan> bucket in conversation.GroupBy(span => (span.StartTime - start).Ticks / window.Ticks))
        {
            if (AuditMath.SumBilled(bucket) is not decimal cost)
            {
                return null;
            }

            costs.Add(cost);
        }

        costs.Sort();
        return costs[(costs.Count - 1) / 2];
    }
}
