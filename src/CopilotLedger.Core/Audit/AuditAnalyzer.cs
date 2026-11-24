using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Core.Audit;

// Runs the audit rules over one AuditInput. Pure: no clock, no I/O, decimal and long arithmetic only.
public static class AuditAnalyzer
{
    // The implemented rules, in AuditRuleIds.All order.
    public static IReadOnlyList<IAuditRule> Rules { get; } =
    [
        new ContextBloatRule(),
        new ContextGrowthRule(),
        new CacheMissStreakRule(),
    ];

    // Rules whose ID input.Rules names; premium_model_trivial_turn only with input.Pricing. AuditRuleIds.All order.
    public static IReadOnlyList<string> EvaluatedRules(AuditInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return
        [
            .. AuditRuleIds.All.Where(id =>
                input.Rules.Contains(id, StringComparer.Ordinal) &&
                (id != AuditRuleIds.PremiumModelTrivialTurn || input.Pricing is not null)),
        ];
    }

    // Every finding of EvaluatedRules(input), in Order. Pure.
    public static IReadOnlyList<AuditFinding> Run(AuditInput input)
    {
        IReadOnlyList<string> evaluated = EvaluatedRules(input);
        return Order(Rules
            .Where(rule => evaluated.Contains(rule.Id, StringComparer.Ordinal))
            .SelectMany(rule => rule.Evaluate(input)));
    }

    public static AuditSummary Summarize(AuditInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        IReadOnlyList<AuditSpan> spans = input.Spans;
        return new AuditSummary
        {
            Spans = spans.Count,
            SpansBySource =
            [
                .. spans
                    .GroupBy(span => span.IngestSource, StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => new AuditSourceCount(group.Key, group.LongCount())),
            ],
            ToolCalls = input.ToolCalls.Count,
            UnpricedSpans = spans.LongCount(span => !span.BilledKnown),
            CacheReportedSpans = spans.LongCount(IsCacheReported),
            CacheAbsentSpans = spans.LongCount(span => !IsCacheReported(span) && !IsCacheLegacy(span)),
            CacheLegacyUnknownSpans = spans.LongCount(span => !IsCacheReported(span) && IsCacheLegacy(span)),
            SpansWithoutAgentIdentity = spans.LongCount(span => span.AgentInvocationId is null),
            ToolCallsWithoutAgentIdentity = input.ToolCalls.LongCount(call => call.AgentInvocationId is null),
            SpansWithUnknownToolCount = spans.LongCount(span => span.ToolCallCount is null),
            Coverage = input.Coverage,
        };
    }

    // Severity (High first), EstimatedImpactUsd (largest first, null last), WindowStart, RuleId, then Scope.Repository,
    // Branch, PullRequestNumber (none first), TraceId, Agent, Model, Tool (ordinal, null first), and WindowEnd. Stable.
    public static IReadOnlyList<AuditFinding> Order(IEnumerable<AuditFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);
        return
        [
            .. findings
                .OrderByDescending(finding => finding.Severity)
                .ThenBy(finding => finding.EstimatedImpactUsd is null)
                .ThenByDescending(finding => finding.EstimatedImpactUsd ?? 0m)
                .ThenBy(finding => finding.WindowStart)
                .ThenBy(finding => finding.RuleId, StringComparer.Ordinal)
                .ThenBy(finding => finding.Scope.Repository, StringComparer.Ordinal)
                .ThenBy(finding => finding.Scope.Branch, StringComparer.Ordinal)
                .ThenBy(finding => finding.Scope.PullRequestNumber ?? 0)
                .ThenBy(finding => finding.Scope.TraceId, StringComparer.Ordinal)
                .ThenBy(finding => finding.Scope.Agent, StringComparer.Ordinal)
                .ThenBy(finding => finding.Scope.Model, StringComparer.Ordinal)
                .ThenBy(finding => finding.Scope.Tool, StringComparer.Ordinal)
                .ThenBy(finding => finding.WindowEnd),
        ];
    }

    // Runs input and describes the run.
    public static AuditReport Report(
        AuditInput input,
        DateTimeOffset generatedAt,
        AuditReportFilter filter,
        AuditSeverity minSeverity,
        int? limit)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(filter);
        return new AuditReport
        {
            GeneratedAt = generatedAt,
            Filter = filter,
            RatesVersion = input.Pricing?.RatesVersion,
            EconomyModel = input.Pricing is null ? null : input.EconomyModel,
            Thresholds = input.Thresholds,
            Breaker = input.Breaker,
            Rules = EvaluatedRules(input),
            Summary = Summarize(input),
            Findings = Run(input),
            MinSeverity = minSeverity,
            Limit = limit,
        };
    }

    private static bool IsCacheReported(AuditSpan span) =>
        CacheMetrics.IsReported(span.CacheReadState, span.CacheWriteState);

    private static bool IsCacheLegacy(AuditSpan span) =>
        span.CacheReadState == CacheCounterState.LegacyUnknown || span.CacheWriteState == CacheCounterState.LegacyUnknown;
}
