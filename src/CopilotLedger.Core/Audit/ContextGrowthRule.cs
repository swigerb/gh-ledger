namespace CopilotLedger.Core.Audit;

// context_growth: one agent invocation's requests to one model whose input kept growing, from the first request to the last.
internal sealed class ContextGrowthRule : IAuditRule
{
    public string Id => AuditRuleIds.ContextGrowth;

    public IEnumerable<AuditFinding> Evaluate(AuditInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        AuditThresholds thresholds = input.Thresholds;
        foreach (List<AuditSpan> sequence in AuditMath.Sequences(input.Spans, byModel: true))
        {
            AuditSpan first = sequence[0];
            AuditSpan last = sequence[^1];
            if (sequence.Count < thresholds.ContextGrowthRequests ||
                first.InputTokens <= 0 ||
                last.InputTokens < thresholds.ContextGrowthRatio * first.InputTokens)
            {
                continue;
            }

            yield return new AuditFinding
            {
                RuleId = Id,
                Severity = AuditSeverity.Info,
                Scope = AuditMath.Scope(first, withModel: true),
                WindowStart = first.StartTime,
                WindowEnd = AuditMath.LatestEnd(sequence),
                Metrics =
                [
                    new AuditMetric("requests", sequence.Count, AuditUnits.Requests),
                    new AuditMetric("firstInputTokens", first.InputTokens, AuditUnits.Tokens),
                    new AuditMetric("lastInputTokens", last.InputTokens, AuditUnits.Tokens),
                    new AuditMetric("peakInputTokens", sequence.Max(span => span.InputTokens), AuditUnits.Tokens),
                    new AuditMetric("growthRatio", (decimal)last.InputTokens / first.InputTokens, AuditUnits.Ratio),
                ],
                EstimatedImpactUsd = null,
                RecommendationKey = Id,
            };
        }
    }
}
