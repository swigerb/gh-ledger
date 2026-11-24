using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace CopilotLedger.Core.Audit;

// An audit report's JSON, the same bytes for gh ledger audit --json, MCP's get_audit_findings (before its circuitBreaker and
// warnings), and the export bundle's telemetry/audit-findings.json: indented with "\n", escaped only as JSON requires, times in
// UTC round-trip form, money with every digit it has, and every other decimal without trailing zeros.
public static class AuditJson
{
    public const int SchemaVersion = 1;

    public static readonly JsonWriterOptions Options = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // The report as one JSON object, then "\n": UTF-8 without a byte order mark.
    public static byte[] Serialize(AuditReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, Options))
        {
            writer.WriteStartObject();
            WriteProperties(writer, report);
            writer.WriteEndObject();
        }

        stream.WriteByte((byte)'\n');
        return stream.ToArray();
    }

    // The report's properties, in order, inside the object that writer has open. findings holds report.Shown; findingsTotal
    // counts every finding of the evaluated rules.
    public static void WriteProperties(Utf8JsonWriter writer, AuditReport report)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(report);
        writer.WriteNumber("schemaVersion", SchemaVersion);
        writer.WriteString("generatedAtUtc", Time(report.GeneratedAt));
        writer.WriteStartObject("filter");
        WriteString(writer, "repository", report.Filter.Repository);
        WriteString(writer, "branch", report.Filter.Branch);
        WriteNumber(writer, "pullRequest", report.Filter.PullRequestNumber);
        WriteString(writer, "conversation", report.Filter.Conversation);
        writer.WriteString("since", report.Filter.Since);
        writer.WriteString("period", report.Filter.Period);
        WriteString(writer, "fromUtc", report.Filter.FromUtc is DateTimeOffset from ? Time(from) : null);
        writer.WriteString("toUtc", Time(report.Filter.ToUtc));
        writer.WriteEndObject();
        writer.WriteString("impactLens", "billed");
        WriteString(writer, "ratesVersion", report.RatesVersion);
        WriteString(writer, "economyModel", report.EconomyModel);
        WriteThresholds(writer, report);
        writer.WriteStartArray("rules");
        foreach (string rule in report.Rules)
        {
            writer.WriteStringValue(rule);
        }

        writer.WriteEndArray();
        WriteCoverage(writer, report.Summary);
        writer.WriteString("minSeverity", AuditSeverities.ToName(report.MinSeverity));
        WriteNumber(writer, "limit", report.Limit);
        writer.WriteNumber("findingsTotal", report.Findings.Count);
        writer.WriteStartArray("findings");
        foreach (AuditFinding finding in report.Shown)
        {
            WriteFinding(writer, finding);
        }

        writer.WriteEndArray();
    }

    // USD with every digit it has: at most 12 decimal places, as every amount an audit computes has.
    public static string Money(decimal value) => value.ToString("0.############", CultureInfo.InvariantCulture);

    // A ratio, rate, or count with every digit it has and no trailing zeros.
    public static string Number(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);

    public static string Time(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static void WriteThresholds(Utf8JsonWriter writer, AuditReport report)
    {
        AuditThresholds thresholds = report.Thresholds;
        writer.WriteStartObject("thresholds");
        writer.WriteNumber("turnVelocityRequests", thresholds.TurnVelocityRequests);
        writer.WriteNumber("turnVelocityWindowMinutes", thresholds.TurnVelocityWindowMinutes);
        writer.WriteNumber("toolLoopCalls", thresholds.ToolLoopCalls);
        writer.WriteNumber("failedToolRetryCalls", thresholds.FailedToolRetryTrigger(report.Breaker));
        writer.WriteNumber("contextBloatInputTokens", thresholds.ContextBloatInputTokens);
        writer.WriteNumber("contextBloatRatio", thresholds.ContextBloatRatio);
        writer.WriteNumber("contextGrowthRequests", thresholds.ContextGrowthRequests);
        WriteRaw(writer, "contextGrowthRatio", Number(thresholds.ContextGrowthRatio));
        writer.WriteNumber("cacheMissStreakRequests", thresholds.CacheMissStreakRequests);
        writer.WriteNumber("cacheMissInputTokens", thresholds.CacheMissInputTokens);
        WriteRaw(writer, "cacheMissHitRatio", Number(thresholds.CacheMissHitRatio));
        writer.WriteNumber("trivialOutputTokens", thresholds.TrivialOutputTokens);
        writer.WriteNumber("trivialRequestsPerDay", thresholds.TrivialRequestsPerDay);
        WriteRaw(writer, "nearBreakerFraction", Number(thresholds.NearBreakerFraction));
        WriteRaw(writer, "breakerVelocityAiCredits", Money(report.Breaker.VelocityCredits));
        writer.WriteNumber("breakerVelocityWindowMinutes", report.Breaker.VelocityWindowMinutes);
        writer.WriteNumber("breakerChurnFailures", report.Breaker.ChurnFailures);
        writer.WriteEndObject();
    }

    private static void WriteCoverage(Utf8JsonWriter writer, AuditSummary summary)
    {
        AuditCoverage coverage = summary.Coverage;
        writer.WriteStartObject("coverage");
        writer.WriteNumber("spans", summary.Spans);
        writer.WriteStartArray("spansBySource");
        foreach (AuditSourceCount source in summary.SpansBySource)
        {
            writer.WriteStartObject();
            writer.WriteString("source", source.Source);
            writer.WriteNumber("spans", source.Spans);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteNumber("toolCalls", summary.ToolCalls);
        writer.WriteNumber("unpricedSpans", summary.UnpricedSpans);
        writer.WriteNumber("cacheReportedSpans", summary.CacheReportedSpans);
        writer.WriteNumber("cacheAbsentSpans", summary.CacheAbsentSpans);
        writer.WriteNumber("cacheLegacyUnknownSpans", summary.CacheLegacyUnknownSpans);
        writer.WriteNumber("spansWithoutAgentIdentity", summary.SpansWithoutAgentIdentity);
        writer.WriteNumber("toolCallsWithoutAgentIdentity", summary.ToolCallsWithoutAgentIdentity);
        writer.WriteNumber("spansWithUnknownToolCount", summary.SpansWithUnknownToolCount);
        writer.WriteBoolean("insufficientToolHistory", coverage.InsufficientToolHistory);
        writer.WriteNumber("toolHistoryRetentionDays", coverage.ToolHistoryRetentionDays);
        writer.WriteString("toolHistoryFromUtc", Time(coverage.ToolHistoryFrom));
        writer.WriteString("toolHistoryStart", AuditToolHistoryStarts.ToName(coverage.ToolHistoryStart));
        writer.WriteNumber("spansBeforeToolHistory", coverage.SpansBeforeToolHistory);
        writer.WriteNumber("importedToolCallsWithoutDetail", coverage.ImportedToolCallsWithoutDetail);
        writer.WriteNumber("importedRequestsWithoutToolData", coverage.ImportedRequestsWithoutToolData);
        writer.WriteNumber("supersededMatchedImportSpans", coverage.SupersededMatchedImportSpans);
        writer.WriteNumber("supersededUnmatchedImportSpans", coverage.SupersededUnmatchedImportSpans);
        writer.WriteNumber("ledgerAmbiguousImportRecords", coverage.LedgerAmbiguousImportRecords);
        writer.WriteNumber("ledgerIncompleteImportLogs", coverage.LedgerIncompleteImportLogs);
        writer.WriteEndObject();
    }

    private static void WriteFinding(Utf8JsonWriter writer, AuditFinding finding)
    {
        AuditScope scope = finding.Scope;
        writer.WriteStartObject();
        writer.WriteString("ruleId", finding.RuleId);
        writer.WriteString("severity", AuditSeverities.ToName(finding.Severity));
        writer.WriteStartObject("scope");
        writer.WriteString("repository", scope.Repository);
        writer.WriteString("branch", scope.Branch);
        WriteNumber(writer, "pullRequest", scope.PullRequestNumber);
        WriteString(writer, "agent", scope.Agent);
        WriteString(writer, "model", scope.Model);
        WriteString(writer, "traceId", scope.TraceId);
        WriteString(writer, "conversationId", scope.ConversationId);
        WriteString(writer, "tool", scope.Tool);
        WriteString(writer, "toolOutcome", scope.ToolOutcome is AuditToolOutcome outcome ? AuditToolOutcomes.ToName(outcome) : null);
        writer.WriteEndObject();
        writer.WriteString("windowStart", Time(finding.WindowStart));
        writer.WriteString("windowEnd", Time(finding.WindowEnd));
        writer.WriteStartArray("metrics");
        foreach (AuditMetric metric in finding.Metrics)
        {
            writer.WriteStartObject();
            writer.WriteString("name", metric.Name);
            WriteRaw(writer, "value", metric.Unit == AuditUnits.Usd ? Money(metric.Value) : Number(metric.Value));
            writer.WriteString("unit", metric.Unit);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        WriteRaw(writer, "estimatedImpactUsd", finding.EstimatedImpactUsd is decimal impact ? Money(impact) : null);
        writer.WriteString("recommendationKey", finding.RecommendationKey);
        writer.WriteEndObject();
    }

    private static void WriteString(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
        }
        else
        {
            writer.WriteString(name, value);
        }
    }

    private static void WriteNumber(Utf8JsonWriter writer, string name, int? value)
    {
        if (value is int number)
        {
            writer.WriteNumber(name, number);
        }
        else
        {
            writer.WriteNull(name);
        }
    }

    private static void WriteRaw(Utf8JsonWriter writer, string name, string? json)
    {
        if (json is null)
        {
            writer.WriteNull(name);
        }
        else
        {
            writer.WritePropertyName(name);
            writer.WriteRawValue(json);
        }
    }
}
