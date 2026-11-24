using System.Globalization;
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Core.Audit;

namespace CopilotLedger.Cli.Audit;

// gh ledger audit's text output: a title, the findings table, what the rules could and couldn't see, and the recommendation
// of each kind of finding shown. Strings from the ledger, the options, or the rates reach the console only through
// AdvisoryText.Line, without escape sequences or controls. Public for tests.
public static class AuditTable
{
    private static readonly TableColumn[] Columns =
    [
        new("Severity", false),
        new("Rule", false),
        new("Scope", false),
        new("Window", false),
        new("Key metrics", false),
        new("Est. impact", true),
    ];

    public static void Write(TextWriter output, AuditReport report, TimeZoneInfo zone)
    {
        AuditReportFilter filter = report.Filter;
        string scope = AdvisoryText.Line(filter.Repository ?? "all repositories") +
            (filter.Branch is null ? "" : $", branch {AdvisoryText.Line(filter.Branch)}") +
            (filter.PullRequestNumber is int pr ? $", pull request #{pr}" : "");
        output.WriteLine($"Audit of {scope} ({filter.Period})");
        IReadOnlyList<AuditFinding> shown = report.Shown;
        IReadOnlyList<AuditFinding> all = report.Findings;
        if (all.Count == 0)
        {
            output.WriteLine("No findings.");
        }
        else
        {
            output.WriteLine(
                $"{AuditText.Plural(all.Count, "finding", "findings")}: " +
                $"{all.Count(finding => finding.Severity == AuditSeverity.High)} high, " +
                $"{all.Count(finding => finding.Severity == AuditSeverity.Warning)} warning, " +
                $"{all.Count(finding => finding.Severity == AuditSeverity.Info)} info." +
                (shown.Count < all.Count ? $" Showing {shown.Count}." : ""));
            output.WriteLine(
                "By rule: " +
                string.Join(", ", AuditRuleIds.All
                    .Select(id => (Id: id, Count: all.Count(finding => finding.RuleId == id)))
                    .Where(rule => rule.Count > 0)
                    .Select(rule => $"{rule.Id} {rule.Count.ToString(CultureInfo.InvariantCulture)}")) +
                ".");
        }

        if (shown.Count > 0)
        {
            output.WriteLine();
            TableWriter.Write(
                output,
                Columns,
                [
                    .. shown.Select(finding => (IReadOnlyList<string>)
                    [
                        AuditText.Severity(finding.Severity),
                        finding.RuleId,
                        AdvisoryText.Line(AuditText.Scope(finding.Scope)),
                        AuditText.Window(finding.WindowStart, finding.WindowEnd, zone),
                        AuditText.KeyMetrics(finding, report.Thresholds),
                        AuditText.Impact(finding.EstimatedImpactUsd),
                    ]),
                ]);
        }

        output.WriteLine();
        foreach (string line in CoverageLines(report, zone))
        {
            output.WriteLine(line);
        }

        string[] keys = [.. shown.Select(finding => finding.RecommendationKey).Distinct(StringComparer.Ordinal)];
        if (keys.Length > 0)
        {
            output.WriteLine();
            output.WriteLine("Recommendations:");
            foreach (string key in keys)
            {
                output.WriteLine($"  {key}: {AuditRecommendations.Text(key)}");
            }
        }
    }

    // What the rules read, what they couldn't see, and how to read the impacts. Lines about gaps appear only when there are any.
    public static IReadOnlyList<string> CoverageLines(AuditReport report, TimeZoneInfo zone)
    {
        AuditSummary summary = report.Summary;
        AuditCoverage coverage = summary.Coverage;
        List<string> lines = [];
        string sources = string.Join(", ", summary.SpansBySource
            .OrderBy(source => SourceOrder(source.Source))
            .ThenBy(source => source.Source, StringComparer.Ordinal)
            .Select(source => $"{LedgerFormat.Count(source.Spans)} {SourceName(source.Source)}"));
        lines.Add($"Requests: {LedgerFormat.Count(summary.Spans)} analyzed{(summary.Spans > 0 ? $" ({sources})" : "")}.");
        lines.Add($"Tool calls: {LedgerFormat.Count(summary.ToolCalls)} analyzed.");
        lines.Add(
            "Impacts: billed USD (GitHub AI credits and Foundry BYOK)" +
            (report.RatesVersion is null ? "" : $" at rates {AdvisoryText.Line(report.RatesVersion)}") +
            (report.EconomyModel is null ? "" : $", economy model {AdvisoryText.Line(report.EconomyModel)}") +
            ". Findings can overlap, so don't add their impacts up.");
        long unknownCache = summary.CacheAbsentSpans + summary.CacheLegacyUnknownSpans;
        if (unknownCache > 0)
        {
            lines.Add(
                $"Cache counters: unknown for {AuditText.Plural(unknownCache, "request", "requests")} " +
                $"({LedgerFormat.Count(summary.CacheAbsentSpans)} absent, {LedgerFormat.Count(summary.CacheLegacyUnknownSpans)} recorded " +
                $"before gh-ledger kept counter states); cache_miss_streak skipped {Them(unknownCache)}.");
        }

        long withoutIdentity = summary.SpansWithoutAgentIdentity + summary.ToolCallsWithoutAgentIdentity;
        if (withoutIdentity > 0)
        {
            lines.Add(
                $"Agent invocations: unknown for {AuditText.Plural(summary.SpansWithoutAgentIdentity, "request", "requests")} and " +
                $"{AuditText.Plural(summary.ToolCallsWithoutAgentIdentity, "tool call", "tool calls")}; the sequence rules skipped " +
                $"{Them(withoutIdentity)}.");
        }

        if (summary.SpansWithUnknownToolCount > 0)
        {
            lines.Add(
                $"Tool counts: unknown for {AuditText.Plural(summary.SpansWithUnknownToolCount, "request", "requests")}; " +
                $"premium_model_trivial_turn left {Them(summary.SpansWithUnknownToolCount)} out.");
        }

        // Tool history as export words it: from the retention before the audit, from the earliest span link the ledger keeps when
        // that's later, or none when the ledger keeps no link.
        string from = LedgerFormat.LocalTime(coverage.ToolHistoryFrom, zone);
        string kept = coverage.ToolHistoryStart switch
        {
            AuditToolHistoryStart.NoLinks => "the ledger keeps no tool history",
            AuditToolHistoryStart.EarliestLink => $"the ledger's tool history starts at {from}",
            _ => $"tool calls are kept for {coverage.ToolHistoryRetentionDays} days, since {from}",
        };
        if (coverage.InsufficientToolHistory)
        {
            List<string> gaps = [];
            long before = coverage.SpansBeforeToolHistory;
            if (before > 0)
            {
                gaps.Add(coverage.ToolHistoryStart == AuditToolHistoryStart.NoLinks
                    ? $"{AuditText.Plural(before, "request", "requests")} may be missing {(before == 1 ? "its" : "their")} tool calls"
                    : $"{AuditText.Plural(before, "request", "requests")} started earlier");
            }

            if (coverage.ImportedToolCallsWithoutDetail > 0)
            {
                gaps.Add(
                    $"{AuditText.Plural(coverage.ImportedToolCallsWithoutDetail, "imported tool call has", "imported tool calls have")} no detail");
            }

            long withoutToolData = coverage.ImportedRequestsWithoutToolData;
            if (withoutToolData > 0)
            {
                gaps.Add(withoutToolData == 1
                    ? "1 imported request has no tool-call count in its session log"
                    : $"{LedgerFormat.Count(withoutToolData)} imported requests have no tool-call count in their session logs");
            }

            string listed = gaps.Count == 1 ? gaps[0] : string.Join(", ", gaps.SkipLast(1)) + (gaps.Count > 2 ? "," : "") + " and " + gaps[^1];
            lines.Add($"Tool history: insufficient: {kept}; {listed}, so the tool rules can't see those calls.");
        }
        else
        {
            lines.Add($"Tool history: complete for these requests ({kept}).");
        }

        if (coverage.SupersededUnmatchedImportSpans > 0)
        {
            lines.Add(coverage.SupersededUnmatchedImportSpans == 1
                ? "Imports: 1 imported request was left out because live spans of its session replaced it without matching it; " +
                  "they may not cover all of its work."
                : $"Imports: {LedgerFormat.Count(coverage.SupersededUnmatchedImportSpans)} imported requests were left out because live " +
                  "spans of their sessions replaced them without matching them; they may not cover all of their work.");
        }

        if (coverage.LedgerAmbiguousImportRecords > 0)
        {
            lines.Add(
                $"Imports: across the whole ledger, gh ledger import deferred {AuditText.Plural(coverage.LedgerAmbiguousImportRecords, "request", "requests")} " +
                $"whose {(coverage.LedgerAmbiguousImportRecords == 1 ? "session has" : "sessions have")} live spans it couldn't match.");
        }

        if (coverage.LedgerIncompleteImportLogs > 0)
        {
            lines.Add(
                $"Imports: across the whole ledger, {AuditText.Plural(coverage.LedgerIncompleteImportLogs, "session log wasn't", "session logs weren't")} " +
                "imported completely.");
        }

        if (summary.UnpricedSpans > 0)
        {
            lines.Add(
                $"Unpriced: {AuditText.Plural(summary.UnpricedSpans, "request has", "requests have")} no billed price, so impacts that " +
                $"include {Them(summary.UnpricedSpans)} are unknown.");
        }

        lines.Add(
            $"Rules: {(report.Rules.Count == AuditRuleIds.All.Count ? $"all {AuditRuleIds.All.Count}" : string.Join(", ", report.Rules))}.");
        return lines;
    }

    private static string Them(long count) => count == 1 ? "it" : "them";

    private static int SourceOrder(string source) => source switch
    {
        "otlp" => 0,
        "import:copilot" => 1,
        "import:codex" => 2,
        _ => 3,
    };

    private static string SourceName(string source) => source switch
    {
        "otlp" => "live",
        "import:copilot" => "from Copilot session logs",
        "import:codex" => "from Codex rollouts",
        _ => $"from {AdvisoryText.Line(source)}",
    };
}
