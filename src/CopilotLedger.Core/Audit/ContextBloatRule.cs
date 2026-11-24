namespace CopilotLedger.Core.Audit;

// context_bloat: a request whose input is large and dwarfs its output, or that returned no output at all.
internal sealed class ContextBloatRule : IAuditRule
{
    public string Id => AuditRuleIds.ContextBloat;

    public IEnumerable<AuditFinding> Evaluate(AuditInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        AuditThresholds thresholds = input.Thresholds;
        foreach (AuditSpan span in AuditMath.ByStart(input.Spans))
        {
            bool zeroOutput = span.OutputTokens == 0;
            if (span.InputTokens < thresholds.ContextBloatInputTokens ||
                (!zeroOutput && span.InputTokens < thresholds.ContextBloatRatio * span.OutputTokens))
            {
                continue;
            }

            AuditMetric last = zeroOutput
                ? new AuditMetric("zeroOutput", 1m, AuditUnits.Flag)
                : new AuditMetric("inputOutputRatio", (decimal)span.InputTokens / span.OutputTokens, AuditUnits.Ratio);
            yield return new AuditFinding
            {
                RuleId = Id,
                Severity = AuditSeverity.Info,
                Scope = AuditMath.Scope(span, withModel: true),
                WindowStart = span.StartTime,
                WindowEnd = span.EndTime,
                Metrics =
                [
                    new AuditMetric("inputTokens", span.InputTokens, AuditUnits.Tokens),
                    new AuditMetric("outputTokens", span.OutputTokens, AuditUnits.Tokens),
                    last,
                ],
                EstimatedImpactUsd = null,
                RecommendationKey = zeroOutput ? AuditRecommendations.ContextBloatZeroOutput : Id,
            };
        }
    }
}
