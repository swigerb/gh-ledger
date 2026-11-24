using System.Globalization;
using CopilotLedger.Core.Audit;
using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Tests.TestSupport;

// Synthetic audit inputs for the rules' tests. A request defaults to claude-opus-5.5 on GitHub AI credits in contoso/webapp
// on main, in trace TraceId, conversation conv-1, and invocation Invocation (the default agent), priced at $0.01, with
// reported cache counters and a known zero tool count, from Start + offset to 2 seconds later. A tool call defaults to a
// view call with status 1 (OK) in the same trace and invocation. Inputs use the embedded rates, economy model mai-code-2,
// the default breaker settings, and UTC-5.
public static class AuditFixtures
{
    public const string TraceId = "a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0";
    public const string Invocation = "a0000000000000a0";
    public const string Repository = "contoso/webapp";
    public const string Conversation = "conv-1";

    public static readonly DateTimeOffset Start = new(2026, 11, 21, 14, 0, 0, TimeSpan.Zero);

    public static JsonRateCardCalculator Pricing { get; } = JsonRateCardCalculator.LoadEmbedded();

    public static AuditCoverage Coverage { get; } = new()
    {
        ToolHistoryFrom = Start - TimeSpan.FromDays(7),
        ToolHistoryStart = AuditToolHistoryStart.Retention,
        ToolHistoryRetentionDays = 7,
        SpansBeforeToolHistory = 0,
        ImportedToolCallsWithoutDetail = 0,
        ImportedRequestsWithoutToolData = 0,
        SupersededMatchedImportSpans = 0,
        SupersededUnmatchedImportSpans = 0,
        LedgerAmbiguousImportRecords = 0,
        LedgerIncompleteImportLogs = 0,
    };

    // Request number (span ID number in 16 hex digits).
    public static AuditSpan Span(int number, TimeSpan offset = default) => new()
    {
        TraceId = TraceId,
        SpanId = number.ToString("x16", CultureInfo.InvariantCulture),
        ConversationId = Conversation,
        StartTime = Start + offset,
        EndTime = Start + offset + TimeSpan.FromSeconds(2),
        AgentInvocationId = Invocation,
        Model = "claude-opus-5.5",
        InputTokens = 1_000,
        CacheReadInputTokens = 0,
        CacheWriteInputTokens = 0,
        OutputTokens = 500,
        CacheReadState = CacheCounterState.Reported,
        CacheWriteState = CacheCounterState.Reported,
        Channel = BillingChannel.GitHubAiCredits,
        PricingStatus = PricingStatus.Priced,
        CostUsd = 0.01m,
        AiCredits = 1m,
        ToolCallCount = 0,
        Repository = Repository,
        Branch = "main",
        IngestSource = "otlp",
    };

    // Tool call number (span ID c + number in 15 hex digits).
    public static AuditToolCall Tool(
        int number,
        TimeSpan offset,
        string? name = "view",
        long? exitCode = null,
        int statusCode = 1) => new()
    {
        TraceId = TraceId,
        SpanId = "c" + number.ToString("x15", CultureInfo.InvariantCulture),
        ConversationId = Conversation,
        AgentInvocationId = Invocation,
        StartTime = Start + offset,
        ToolName = name,
        ExitCode = exitCode,
        StatusCode = statusCode,
        Imported = false,
        Repository = Repository,
        Branch = "main",
    };

    public static AuditInput Input(IEnumerable<AuditSpan> spans, IEnumerable<AuditToolCall>? calls = null) => new()
    {
        Spans = [.. spans],
        ToolCalls = [.. calls ?? []],
        Coverage = Coverage,
        Breaker = BreakerSettings.Default,
        Pricing = Pricing,
        EconomyModel = "mai-code-2",
        TimeZone = ManualTimeProvider.UtcMinus5,
    };

    // The findings of one rule.
    public static IReadOnlyList<AuditFinding> Run(string ruleId, AuditInput input) =>
        AuditAnalyzer.Run(input with { Rules = [ruleId] });

    // name=value unit for each metric, in order, values in invariant form without trailing zeros.
    public static string Metrics(AuditFinding finding) =>
        string.Join(", ", finding.Metrics.Select(metric => $"{metric.Name}={Number(metric.Value)} {metric.Unit}"));

    public static string Number(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);

    // "none", or each finding as "{severity} [{metrics}] {impact or null} {recommendation key}", joined by " | ".
    public static string Describe(IReadOnlyList<AuditFinding> findings) =>
        findings.Count == 0
            ? "none"
            : string.Join(" | ", findings.Select(finding =>
                $"{AuditSeverities.ToName(finding.Severity)} [{Metrics(finding)}] " +
                $"{(finding.EstimatedImpactUsd is decimal impact ? Number(impact) : "null")} {finding.RecommendationKey}"));
}
