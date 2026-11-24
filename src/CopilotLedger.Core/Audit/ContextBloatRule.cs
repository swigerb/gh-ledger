namespace CopilotLedger.Core.Audit;

// context_bloat: an invocation/model whose requests have large inputs that dwarf output, or returned no output.
internal sealed class ContextBloatRule : IAuditRule
{
    public string Id => AuditRuleIds.ContextBloat;

    public IEnumerable<AuditFinding> Evaluate(AuditInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        AuditThresholds thresholds = input.Thresholds;
        var groups = AuditMath.ByStart(input.Spans)
            .Where(span =>
                span.InputTokens >= thresholds.ContextBloatInputTokens &&
                (span.OutputTokens == 0 ||
                 span.InputTokens >= thresholds.ContextBloatRatio * span.OutputTokens))
            .GroupBy(span => (
                span.Repository,
                span.Branch,
                span.PullRequestNumber,
                span.TraceId,
                span.ConversationId,
                span.AgentInvocationId,
                Agent: AuditMath.Agent(span.AgentName),
                span.Model,
                ZeroOutput: span.OutputTokens == 0));
        foreach (IGrouping<(string Repository, string Branch, int? PullRequestNumber, string TraceId,
                     string? ConversationId, string? AgentInvocationId, string Agent, string Model,
                     bool ZeroOutput), AuditSpan> group in groups)
        {
            AuditSpan first = group.First();
            long inputTokens = group.Sum(span => span.InputTokens);
            long outputTokens = group.Sum(span => span.OutputTokens);
            AuditMetric last = group.Key.ZeroOutput
                ? new AuditMetric("zeroOutput", 1m, AuditUnits.Flag)
                : new AuditMetric("inputOutputRatio", (decimal)inputTokens / outputTokens, AuditUnits.Ratio);
            yield return new AuditFinding
            {
                RuleId = Id,
                Severity = AuditSeverity.Info,
                Scope = AuditMath.Scope(first, withModel: true),
                WindowStart = first.StartTime,
                WindowEnd = AuditMath.LatestEnd(group),
                Metrics =
                [
                    new AuditMetric("requests", group.Count(), AuditUnits.Requests),
                    new AuditMetric("inputTokens", inputTokens, AuditUnits.Tokens),
                    new AuditMetric("outputTokens", outputTokens, AuditUnits.Tokens),
                    last,
                ],
                EstimatedImpactUsd = null,
                RecommendationKey = group.Key.ZeroOutput ? AuditRecommendations.ContextBloatZeroOutput : Id,
            };
        }
    }
}
