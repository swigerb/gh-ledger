namespace CopilotLedger.Core.Audit;

// failed_tool_retry: one agent invocation retrying a failing tool call, below the circuit breaker's churn threshold. The
// failure test is the breaker's (AuditToolOutcomes.Classify); a call of another tool, a success, or an unknown outcome ends
// the streak.
internal sealed class FailedToolRetryRule : IAuditRule
{
    public string Id => AuditRuleIds.FailedToolRetry;

    public IEnumerable<AuditFinding> Evaluate(AuditInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        int trigger = input.Thresholds.FailedToolRetryTrigger(input.Breaker);
        int high = input.Breaker.ChurnFailures - 1;
        foreach (List<AuditToolCall> sequence in ToolSequences.Of(input.ToolCalls))
        {
            foreach (List<AuditToolCall> streak in ToolSequences.Runs(
                sequence,
                (_, call) => call.Outcome == AuditToolOutcome.Failure))
            {
                if (streak.Count < trigger)
                {
                    continue;
                }

                List<AuditSpan> requests = ToolSequences.Between(input.Spans, streak);
                yield return new AuditFinding
                {
                    RuleId = Id,
                    Severity = streak.Count >= high ? AuditSeverity.High : AuditSeverity.Warning,
                    Scope = ToolSequences.Scope(streak),
                    WindowStart = streak[0].StartTime,
                    WindowEnd = streak[^1].StartTime,
                    Metrics =
                    [
                        new AuditMetric("failures", streak.Count, AuditUnits.Calls),
                        new AuditMetric("requests", requests.Count, AuditUnits.Requests),
                        new AuditMetric("breakerChurnFailures", input.Breaker.ChurnFailures, AuditUnits.Calls),
                    ],
                    EstimatedImpactUsd = AuditMath.SumBilled(requests),
                    RecommendationKey = Id,
                };
            }
        }
    }
}
