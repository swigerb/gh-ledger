using System.Globalization;
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Core.Audit;

namespace CopilotLedger.Cli.Audit;

// The fixed texts of audit findings that the table and the export report show. Public for tests.
public static class AuditText
{
    public static string Severity(AuditSeverity severity) => severity switch
    {
        AuditSeverity.High => "High",
        AuditSeverity.Warning => "Warning",
        _ => "Info",
    };

    public static string RuleTitle(string ruleId) => ruleId switch
    {
        AuditRuleIds.TurnVelocity => "Turn velocity",
        AuditRuleIds.ToolLoop => "Tool loop",
        AuditRuleIds.FailedToolRetry => "Failed tool retries",
        AuditRuleIds.ContextBloat => "Context bloat",
        AuditRuleIds.ContextGrowth => "Context growth",
        AuditRuleIds.CacheMissStreak => "Cache-miss streak",
        AuditRuleIds.PremiumModelTrivialTurn => "Premium model on trivial turns",
        AuditRuleIds.VelocityNearBreaker => "Near the circuit breaker",
        _ => ruleId,
    };

    // The finding's key metrics in a few words.
    public static string KeyMetrics(AuditFinding finding, AuditThresholds thresholds)
    {
        string Count(string name) => LedgerFormat.Count((long)Metric(finding, name));
        return finding.RuleId switch
        {
            AuditRuleIds.TurnVelocity => $"{Count("requests")} requests in {Count("windowMinutes")} min",
            AuditRuleIds.ToolLoop => $"{Count("calls")} calls in a row, {Outcome(finding.Scope.ToolOutcome)}",
            AuditRuleIds.FailedToolRetry => $"{Count("failures")} failures in a row",
            AuditRuleIds.ContextBloat => ContextBloat(finding),
            AuditRuleIds.ContextGrowth =>
                $"{Count("firstInputTokens")} to {Count("lastInputTokens")} input in {Count("requests")} requests " +
                $"({decimal.Round(Metric(finding, "growthRatio"), 1, MidpointRounding.AwayFromZero).ToString("0.0", CultureInfo.InvariantCulture)}x)",
            AuditRuleIds.CacheMissStreak =>
                $"{Count("requests")} requests, {LedgerFormat.Percent(Metric(finding, "cacheHitRatio"))} cache hits, " +
                $"{Count("uncachedInputTokens")} uncached",
            AuditRuleIds.PremiumModelTrivialTurn =>
                $"{Count("requests")} tool-free requests of {LedgerFormat.Count(thresholds.TrivialOutputTokens)} or fewer output tokens",
            AuditRuleIds.VelocityNearBreaker =>
                $"{LedgerFormat.Credits(Metric(finding, "windowAiCredits"))} of {LedgerFormat.Credits(Metric(finding, "breakerVelocityAiCredits"))} " +
                $"AI credits in {Count("windowMinutes")} min ({LedgerFormat.Percent(Metric(finding, "breakerFraction"))})",
            _ => string.Join(", ", finding.Metrics.Select(metric => $"{metric.Name} {AuditJson.Number(metric.Value)}")),
        };
    }

    // repository @ branch, then the agent, model, and tool when the finding has them.
    public static string Scope(AuditScope scope) =>
        string.Join(", ", new[] { $"{scope.Repository} @ {scope.Branch}", scope.Agent, scope.Model, scope.Tool }.OfType<string>());

    // The window's start in zone, then its end's time on the same day or its date and time on another; one minute when they
    // fall in the same minute.
    public static string Window(DateTimeOffset start, DateTimeOffset end, TimeZoneInfo zone)
    {
        DateTime from = TimeZoneInfo.ConvertTime(start, zone).DateTime;
        DateTime to = TimeZoneInfo.ConvertTime(end, zone).DateTime;
        string text = from.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        if (to.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) == text)
        {
            return text;
        }

        return text + "-" + to.ToString(to.Date == from.Date ? "HH:mm" : "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    public static string Impact(decimal? usd) => usd is decimal value ? LedgerFormat.Usd(value) : "-";

    // count and the singular or plural noun: 1 request, 2 requests.
    public static string Plural(long count, string singular, string plural) =>
        $"{LedgerFormat.Count(count)} {(count == 1 ? singular : plural)}";

    private static decimal Metric(AuditFinding finding, string name) =>
        finding.Metrics.First(metric => metric.Name == name).Value;

    private static string ContextBloat(AuditFinding finding)
    {
        AuditMetric? requests = finding.Metrics.FirstOrDefault(metric => metric.Name == "requests");
        string prefix = requests is not null && requests.Value > 1m
            ? $"{LedgerFormat.Count((long)requests.Value)} requests, "
            : "";
        string body = finding.Metrics.Any(metric => metric.Name == "zeroOutput")
            ? $"{LedgerFormat.Count((long)Metric(finding, "inputTokens"))} in, no output"
            : $"{LedgerFormat.Count((long)Metric(finding, "inputTokens"))} in, " +
              $"{LedgerFormat.Count((long)Metric(finding, "outputTokens"))} out " +
              $"({decimal.Round(Metric(finding, "inputOutputRatio"), 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture)}:1)";
        return prefix + body;
    }

    private static string Outcome(AuditToolOutcome? outcome) => outcome switch
    {
        AuditToolOutcome.Success => "succeeded",
        AuditToolOutcome.Failure => "failed",
        _ => "outcome unknown",
    };
}
