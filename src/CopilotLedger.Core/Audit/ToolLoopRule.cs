namespace CopilotLedger.Core.Audit;

// tool_loop: one agent invocation calling the same tool many times in a row with the same outcome.
internal sealed class ToolLoopRule : IAuditRule
{
    public string Id => AuditRuleIds.ToolLoop;

    public IEnumerable<AuditFinding> Evaluate(AuditInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        int trigger = input.Thresholds.ToolLoopCalls;
        foreach (List<AuditToolCall> sequence in ToolSequences.Of(input.ToolCalls))
        {
            foreach (List<AuditToolCall> run in ToolSequences.Runs(sequence, (first, call) => call.Outcome == first.Outcome))
            {
                if (run.Count < trigger)
                {
                    continue;
                }

                List<AuditSpan> requests = ToolSequences.Between(input.Spans, run);
                decimal? impact = AuditMath.SumBilled(requests);
                // High only when the run is long and costs something known; a free or unpriced loop is routine exploration (#80).
                AuditSeverity severity = run.Count >= 2L * trigger && impact is > 0m ? AuditSeverity.High : AuditSeverity.Warning;
                yield return new AuditFinding
                {
                    RuleId = Id,
                    Severity = severity,
                    Scope = ToolSequences.Scope(run),
                    WindowStart = run[0].StartTime,
                    WindowEnd = run[^1].StartTime,
                    Metrics =
                    [
                        new AuditMetric("calls", run.Count, AuditUnits.Calls),
                        new AuditMetric("requests", requests.Count, AuditUnits.Requests),
                    ],
                    EstimatedImpactUsd = impact,
                    RecommendationKey = Id,
                };
            }
        }
    }
}
