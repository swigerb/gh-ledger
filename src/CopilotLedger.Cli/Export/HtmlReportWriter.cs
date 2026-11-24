using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Export;

// The report page: UTF-8 without a byte order mark, "\n" line breaks only, and well-formed XML apart from the contents of
// its style and script elements. Text from the ledger, the options, or a time zone goes through HtmlEncoder.Default.
internal static class HtmlReportWriter
{
    private const string Policy =
        "default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; img-src data:";

    public static byte[] Write(ExportReportModel model)
    {
        ExportHeader header = model.Header;
        StringBuilder html = new();
        Line(html, "<!DOCTYPE html>");
        Line(html, "<html lang=\"en\">");
        Line(html, "<head>");
        Line(html, "<meta charset=\"utf-8\" />");
        Line(html, $"<meta http-equiv=\"Content-Security-Policy\" content=\"{Policy}\" />");
        Line(html, "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        Line(html, $"<meta name=\"generator\" content=\"gh-ledger {Text(header.ToolVersion)}\" />");
        Line(html, $"<title>CopilotLedger report: {Text(header.Scope)} ({Text(header.Filter.Period)})</title>");
        Line(html, "<style>");
        Line(html, ExportAssets.Css.TrimEnd('\n'));
        Line(html, "</style>");
        Line(html, "</head>");
        Line(html, "<body>");
        WriteHeader(html, model);
        Line(html, "<main>");
        WriteSummary(html, model);
        WriteCoverage(html, model.Coverage, header.TimeZone);
        WriteCache(html, model);
        WritePromptChains(html, model, header.TimeZone);
        WriteRollups(html, model);
        WriteBreaker(html, model, header.TimeZone);
        WriteAuditFindings(html, model.AuditFindings);
        Line(html, "</main>");
        Line(
            html,
            $"<footer><p>Written by gh-ledger {Text(header.ToolVersion)}. This report holds no prompts, responses, tool arguments or results, or file contents{(header.IncludeLocalPaths ? string.Empty : ", and no local paths")}. Times are in {Text(header.TimeZone.Id)}.</p></footer>");
        Line(html, "</body>");
        Line(html, "</html>");
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(html.ToString());
    }

    // The coverage notes, in order, each only when it applies.
    internal static IEnumerable<string> CoverageNotes(ExportCoverage coverage, TimeZoneInfo zone)
    {
        yield return coverage.Spans == 0
            ? "No requests match this export."
            : $"{ExportRecords.Plural(coverage.Spans, "request", "requests")}: {string.Join(", ", Sources(coverage.SpansBySource))}.";
        long matched = coverage.SupersededMatchedImportSpans;
        if (matched > 0)
        {
            yield return matched == 1
                ? "1 imported request was left out because a live span recorded the same call."
                : $"{LedgerFormat.Count(matched)} imported requests were left out because live spans recorded the same calls.";
        }

        long unmatched = coverage.SupersededUnmatchedImportSpans;
        if (unmatched > 0)
        {
            yield return unmatched == 1
                ? "1 imported request was left out because live spans of its session replaced it without matching it; they may not cover it."
                : $"{LedgerFormat.Count(unmatched)} imported requests were left out because live spans of their sessions replaced them without matching them; they may not cover them all.";
        }

        long ambiguous = coverage.LedgerAmbiguousImportRecords;
        if (ambiguous > 0)
        {
            yield return ambiguous == 1
                ? "Across the whole ledger, gh ledger import deferred 1 request whose session has live spans it couldn't match; it may belong in this report."
                : $"Across the whole ledger, gh ledger import deferred {LedgerFormat.Count(ambiguous)} requests whose sessions have live spans it couldn't match; some may belong in this report.";
        }

        long incomplete = coverage.LedgerIncompleteImportLogs;
        if (incomplete > 0)
        {
            yield return incomplete == 1
                ? "Across the whole ledger, 1 session log wasn't imported completely."
                : $"Across the whole ledger, {LedgerFormat.Count(incomplete)} session logs weren't imported completely.";
        }

        long before = coverage.SpansBeforeToolHistory;
        if (before > 0)
        {
            yield return $"Tool calls are kept for {coverage.ToolHistoryRetentionDays.ToString(CultureInfo.InvariantCulture)} days, so the {ExportRecords.Plural(before, "request", "requests")} before {LedgerFormat.LocalTime(coverage.ToolHistoryFromUtc, zone)} may be missing {(before == 1 ? "its" : "their")} tool calls.";
        }

        long withoutDetail = coverage.ImportedToolCallsWithoutDetail;
        if (withoutDetail > 0)
        {
            yield return withoutDetail == 1
                ? "1 tool call of an imported request is counted without detail."
                : $"{LedgerFormat.Count(withoutDetail)} tool calls of imported requests are counted without detail.";
        }

        long unpriced = coverage.UnpricedSpans;
        if (unpriced > 0)
        {
            yield return unpriced == 1
                ? "1 request has no price, so billed totals leave it out."
                : $"{LedgerFormat.Count(unpriced)} requests have no price, so billed totals leave them out.";
        }

        if (coverage.ApiEquivalentUnpricedSpans > 0)
        {
            yield return $"{LedgerFormat.Count(coverage.ApiEquivalentUnpricedSpans)} of {ExportRecords.Plural(coverage.Spans, "span", "spans")} had no API-equivalent price. API-equivalent amounts leave them out; * marks a partial amount.";
        }

        long estimated = coverage.ApiEquivalentEstimatedSpans;
        if (estimated > 0)
        {
            yield return $"{ExportRecords.Plural(estimated, "priced span", "priced spans")} reported no cache counters, so all of {(estimated == 1 ? "its" : "their")} input is priced as uncached; ~ marks an estimate.";
        }

        long unreported = coverage.CacheAbsentSpans + coverage.CacheLegacyUnknownSpans;
        if (unreported > 0)
        {
            yield return unreported == 1
                ? "1 request reported no cache counters; cache hit ratios leave it out."
                : $"{LedgerFormat.Count(unreported)} requests reported no cache counters; cache hit ratios leave them out.";
        }
    }

    private static void WriteHeader(StringBuilder html, ExportReportModel model)
    {
        ExportHeader header = model.Header;
        ExportFilter filter = header.Filter;
        TimeZoneInfo zone = header.TimeZone;
        Line(html, "<header id=\"report-header\">");
        Line(html, "<h1>CopilotLedger report</h1>");
        Line(html, $"<p class=\"scope\">{Text(header.Scope)} · {Text(filter.Period)}</p>");
        Line(html, "<dl class=\"meta\">");
        Line(
            html,
            $"<dt>Window</dt><dd>{(filter.FromUtc is DateTimeOffset from ? Time(from, zone) : "All time")} to {Time(filter.ToUtc, zone)}</dd>");
        Line(html, $"<dt>Generated</dt><dd>{Time(header.GeneratedAt, zone)} by gh-ledger {Text(header.ToolVersion)}</dd>");
        Line(
            html,
            $"<dt>Time zone</dt><dd>{Text(zone.Id)}{(header.RequestedTimeZone is string requested && requested != zone.Id ? $" (requested {Text(requested)})" : string.Empty)}</dd>");
        if (header.IncludeLocalPaths && filter.Repository is not null)
        {
            Line(html, $"<dt>Repository</dt><dd>{Text(filter.Repository)}</dd>");
        }

        Line(
            html,
            $"<dt>Rates</dt><dd>{(header.RatesVersions.Count == 0 ? "None recorded" : Text(string.Join(", ", header.RatesVersions)))}</dd>");
        Line(
            html,
            "<dt>API list prices</dt><dd>" +
            (header.ApiRateCards.Count == 0
                ? "None recorded"
                : string.Join(
                    ", ",
                    header.ApiRateCards.Select(card =>
                        $"<span title=\"{Text(card.Sha256)}\">{Text(card.RatesVersion)} {Text(card.Sha256[..Math.Min(12, card.Sha256.Length)])}</span>"))) +
            "</dd>");
        Line(html, "</dl>");
        html.Append("<nav aria-label=\"Sections\"><ul>");
        foreach ((string id, string title) in Sections(model))
        {
            html.Append($"<li><a href=\"#{id}\">{title}</a></li>");
        }

        Line(html, "</ul></nav>");
        Line(html, "</header>");
    }

    // The sections present, in page order.
    private static IEnumerable<(string Id, string Title)> Sections(ExportReportModel model)
    {
        yield return ("summary", "Summary");
        yield return ("coverage", "Coverage");
        yield return ("cache", "Cache efficiency");
        yield return ("prompt-chains", "Prompt chains");
        yield return ("rollups", "Spend tables");
        yield return ("breaker", "Circuit breaker and budgets");
        if (model.AuditFindings.Count > 0)
        {
            yield return ("audit-findings", "Audit findings");
        }
    }

    private static void WriteSummary(StringBuilder html, ExportReportModel model)
    {
        LedgerRollupRow totals = model.Summary.Totals;
        SectionStart(html, "summary", "Summary");
        Line(html, "<ul class=\"cards\">");
        Card(
            html,
            "requests",
            "Requests",
            LedgerFormat.Count(totals.SpanCount),
            totals.SpanCount.ToString(CultureInfo.InvariantCulture),
            $"{LedgerFormat.Count(totals.UnpricedSpanCount)} unpriced");
        Card(
            html,
            "credits",
            "GitHub AI credits",
            LedgerFormat.Credits(totals.AiCredits),
            LedgerFormat.Exact(totals.AiCredits),
            LedgerFormat.Usd(totals.AiCreditsUsd));
        Card(
            html,
            "byok",
            "Foundry BYOK",
            LedgerFormat.Usd(totals.FoundryByokUsd),
            LedgerFormat.Exact(totals.FoundryByokUsd),
            "billed by Microsoft Foundry");
        Card(
            html,
            "api",
            "API equivalent",
            LedgerFormat.ApiEquivalent(totals),
            totals.ApiEquivalentUsd is decimal api ? LedgerFormat.Exact(api) : "Unpriced",
            "a comparison, not billed");
        Card(
            html,
            "local",
            "Local saved",
            LedgerFormat.Usd(totals.LocalSavedUsd),
            LedgerFormat.Exact(totals.LocalSavedUsd),
            "cloud spend avoided");
        Card(
            html,
            "cache",
            "Cache hit",
            LedgerFormat.Percent(totals.CacheHitRatio),
            totals.CacheHitRatio?.ToString(CultureInfo.InvariantCulture) ?? "n/a",
            $"{LedgerFormat.Count(totals.CacheReportedSpanCount)} of {LedgerFormat.Count(totals.SpanCount)} reported");
        Card(
            html,
            "chains",
            "Prompt chains",
            LedgerFormat.Count(model.Summary.PromptChains),
            model.Summary.PromptChains.ToString(CultureInfo.InvariantCulture),
            "prompts sent");
        int open = model.BreakerTrips.Count(trip => trip.ResetAt is null);
        Card(
            html,
            "breaker",
            "Circuit breaker",
            $"{LedgerFormat.Count(open)} open",
            open.ToString(CultureInfo.InvariantCulture),
            ExportRecords.Plural(model.BreakerTrips.Count, "trip", "trips"));
        int exceeded = model.Budgets.Count(budget => budget.Exceeded);
        Card(
            html,
            "budgets",
            "Budgets",
            model.Budgets.Count == 0
                ? "None"
                : $"{LedgerFormat.Count(exceeded)} of {LedgerFormat.Count(model.Budgets.Count)} exceeded",
            model.Budgets.Count == 0 ? "None" : exceeded.ToString(CultureInfo.InvariantCulture),
            "all-time spend");
        Line(html, "</ul>");
        Line(html, "</section>");
    }

    private static void WriteCoverage(StringBuilder html, ExportCoverage coverage, TimeZoneInfo zone)
    {
        SectionStart(html, "coverage", "Coverage");
        Line(
            html,
            $"<p id=\"coverage-requests\">{(coverage.RequestsComplete ? "Requests: complete." : "Requests: may be incomplete; see the notes.")}</p>");
        Line(
            html,
            $"<p id=\"coverage-tool-calls\">{(coverage.ToolHistoryComplete ? "Tool calls: complete for this window." : "Tool calls: partial; see the notes.")}</p>");
        Line(html, "<ul id=\"coverage-notes\">");
        foreach (string note in CoverageNotes(coverage, zone))
        {
            Line(html, $"<li>{Text(note)}</li>");
        }

        Line(html, "</ul>");
        Line(html, "</section>");
    }

    private static void WriteCache(StringBuilder html, ExportReportModel model)
    {
        SectionStart(html, "cache", "Cache efficiency");
        Table(
            html,
            "table-cache-model",
            "Prompt cache by model",
            ["Model", .. CacheHeaders],
            [.. model.CacheByModel.Select(row => (IReadOnlyList<Cell>)[Name(row.Model), .. CacheCells(row)])]);
        Table(
            html,
            "table-cache-agent",
            "Prompt cache by agent",
            ["Agent", .. CacheHeaders],
            [.. model.CacheByAgent.Select(row => (IReadOnlyList<Cell>)[Name(row.Agent), .. CacheCells(row)])]);
        Line(html, "</section>");
    }

    private static void WritePromptChains(StringBuilder html, ExportReportModel model, TimeZoneInfo zone)
    {
        SectionStart(html, "prompt-chains", "Prompt chains");
        Table(
            html,
            "table-prompt-chains",
            $"Top {LedgerFormat.Count(model.PromptChains.Count)} of {LedgerFormat.Count(model.Summary.PromptChains)} prompt chains by billed cost",
            [
                "#", "Started", "Repository", "Branch", "Agents", "Models", "Requests", "Input", "Output", "AI credits",
                "GitHub USD", "BYOK USD", "API equiv USD", "Local saved", "Trace",
            ],
            [
                .. model.PromptChains.Select((row, index) => (IReadOnlyList<Cell>)
                [
                    Count(index + 1),
                    TimeCell(row.StartTime, zone),
                    Name(row.Repository),
                    Name(row.Branch),
                    Name(row.Agents.Count == 0 ? "-" : string.Join(", ", row.Agents)),
                    Name(row.Models.Count == 0 ? "-" : string.Join(", ", row.Models)),
                    Count(row.SpanCount),
                    Count(row.InputTokens),
                    Count(row.OutputTokens),
                    Money(LedgerFormat.Credits(row.AiCredits), row.AiCredits),
                    Money(LedgerFormat.Usd(row.AiCreditsUsd), row.AiCreditsUsd),
                    Money(LedgerFormat.Usd(row.FoundryByokUsd), row.FoundryByokUsd),
                    Money(LedgerFormat.ApiEquivalent(row), row.ApiEquivalentUsd),
                    Money(LedgerFormat.Usd(row.LocalSavedUsd), row.LocalSavedUsd),
                    Code(row.TraceId),
                ]),
            ]);
        Line(html, "</section>");
    }

    private static void WriteRollups(StringBuilder html, ExportReportModel model)
    {
        SectionStart(html, "rollups", "Spend tables");
        Table(
            html,
            "table-by-model",
            "Spend by model",
            ["Model", .. RollupHeaders],
            [.. model.ByModel.Select(row => (IReadOnlyList<Cell>)[Name(row.Model), .. RollupCells(row)])]);
        Table(
            html,
            "table-by-agent",
            "Spend by agent",
            ["Agent", .. RollupHeaders],
            [.. model.ByAgent.Select(row => (IReadOnlyList<Cell>)[Name(row.Agent), .. RollupCells(row)])]);
        Table(
            html,
            "table-by-branch",
            "Spend by branch",
            ["Repository", "Branch", .. RollupHeaders],
            [.. model.ByBranch.Select(row => (IReadOnlyList<Cell>)[Name(row.Repository), Name(row.Branch), .. RollupCells(row)])]);
        Table(
            html,
            "table-by-channel-model",
            "Spend by billing channel and model",
            ["Channel", "Model", .. RollupHeaders],
            [.. model.ByChannelModel.Select(row => (IReadOnlyList<Cell>)[Name(ChannelName(row.Channel)), Name(row.Model), .. RollupCells(row)])]);
        Line(html, "</section>");
    }

    private static void WriteBreaker(StringBuilder html, ExportReportModel model, TimeZoneInfo zone)
    {
        SectionStart(html, "breaker", "Circuit breaker and budgets");
        Table(
            html,
            "table-breaker-trips",
            "Breaker trips of these prompt chains",
            ["Rule", "Tripped", "State", "Repository", "Branch", "Window cost", "Window", "Failures", "Trace"],
            [
                .. model.BreakerTrips.Select(trip => (IReadOnlyList<Cell>)
                [
                    Name(trip.Rule == BreakerRule.CreditVelocity ? "Credit velocity" : "Retry churn"),
                    TimeCell(trip.TrippedAt, zone),
                    trip.ResetAt is DateTimeOffset reset ? new Cell($"Reset {Time(reset, zone)}", reset) : new Cell("Open", null),
                    Name(trip.Repository),
                    Name(trip.Branch),
                    trip.WindowCostUsd is decimal cost ? Money(LedgerFormat.Usd(cost), cost) : new Cell("-", null),
                    trip.WindowMinutes is int minutes ? new Cell($"{LedgerFormat.Count(minutes)} min", (long)minutes) : new Cell("-", null),
                    trip.FailureCount is int failures ? Count(failures) : new Cell("-", null),
                    Code(trip.TraceId),
                ]),
            ]);
        Table(
            html,
            "table-budgets",
            "Budgets of these branches",
            ["Repository", "Branch", "Budget (AI credits)", "Spent, all time (AI credits)", "Status"],
            [
                .. model.Budgets.Select(budget => (IReadOnlyList<Cell>)
                [
                    Name(budget.Repository),
                    Name(budget.Branch),
                    Money(LedgerFormat.Credits(budget.BudgetAiCredits), budget.BudgetAiCredits),
                    Money(LedgerFormat.Credits(budget.SpentAiCredits), budget.SpentAiCredits),
                    Name(budget.Exceeded ? "Exceeded" : "Within budget"),
                ]),
            ]);
        Line(html, "</section>");
    }

    private static readonly string[] RollupHeaders =
        ["Requests", "Unpriced", "AI credits", "GitHub USD", "BYOK USD", "API equiv USD", "Local saved", "Cache hit"];

    private static readonly string[] CacheHeaders =
        ["Requests", "Cache reported", "Input", "Cache read", "Cache write", "Uncached", "Output", "Hit %"];

    private static IReadOnlyList<Cell> RollupCells(LedgerRollupRow row) =>
    [
        Count(row.SpanCount),
        Count(row.UnpricedSpanCount),
        Money(LedgerFormat.Credits(row.AiCredits), row.AiCredits),
        Money(LedgerFormat.Usd(row.AiCreditsUsd), row.AiCreditsUsd),
        Money(LedgerFormat.Usd(row.FoundryByokUsd), row.FoundryByokUsd),
        Money(LedgerFormat.ApiEquivalent(row), row.ApiEquivalentUsd),
        Money(LedgerFormat.Usd(row.LocalSavedUsd), row.LocalSavedUsd),
        Ratio(row.CacheHitRatio),
    ];

    // status --cache's cells: totals over the requests that reported cache usage are n/a when none did, and marked * when
    // only some did.
    private static IReadOnlyList<Cell> CacheCells(CacheEfficiencyRow row) =>
    [
        Count(row.SpanCount),
        Count(row.CacheReportedSpanCount),
        Count(row.InputTokens),
        Observed(row, row.ReportedCacheReadInputTokens),
        Observed(row, row.ReportedCacheWriteInputTokens),
        Observed(row, row.ReportedUncachedInputTokens),
        Count(row.OutputTokens),
        Ratio(row.CacheHitRatio),
    ];

    private static Cell Observed(CacheEfficiencyRow row, long value) =>
        row.CacheReportedSpanCount == 0
            ? new Cell("n/a", null)
            : new Cell(LedgerFormat.Count(value) + (row.CacheUnreportedSpanCount > 0 ? "*" : string.Empty), value);

    private static string ChannelName(BillingChannel? channel) => channel switch
    {
        BillingChannel.GitHubAiCredits => "GitHub AI credits",
        BillingChannel.FoundryByok => "Foundry BYOK",
        BillingChannel.Local => "Local",
        _ => "Unpriced",
    };

    // A sortable table. Each cell's data-sort is its row's 0-based position when the rows are ordered by that column: values
    // ascending with null first, strings ordinal, ties in table order. A table without rows has one "None." row instead.
    private static void Table(
        StringBuilder html,
        string id,
        string caption,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<Cell>> rows)
    {
        Line(html, $"<div class=\"table-wrap\"><table class=\"{(rows.Count == 0 ? "data" : "data sortable")}\" id=\"{id}\">");
        Line(html, $"<caption>{caption}</caption>");
        Line(html, "<thead><tr>" + string.Concat(headers.Select(header => $"<th scope=\"col\">{header}</th>")) + "</tr></thead>");
        Line(html, "<tbody>");
        if (rows.Count == 0)
        {
            Line(html, $"<tr><td colspan=\"{headers.Count.ToString(CultureInfo.InvariantCulture)}\">None.</td></tr>");
        }

        int[][] ranks = [.. Enumerable.Range(0, headers.Count).Select(column => Ranks(rows, column))];
        for (int row = 0; row < rows.Count; row++)
        {
            html.Append("<tr>");
            for (int column = 0; column < headers.Count; column++)
            {
                Cell cell = rows[row][column];
                html.Append($"<td data-sort=\"{ranks[column][row].ToString(CultureInfo.InvariantCulture)}\"");
                html.Append(cell.Title is null ? ">" : $" title=\"{Text(cell.Title)}\">");
                html.Append(cell.Html).Append("</td>");
            }

            Line(html, "</tr>");
        }

        Line(html, "</tbody>");
        Line(html, "</table></div>");
    }

    private static int[] Ranks(IReadOnlyList<IReadOnlyList<Cell>> rows, int column)
    {
        int[] order = [.. Enumerable.Range(0, rows.Count).Order(Comparer<int>.Create((left, right) =>
        {
            int compared = CompareKeys(rows[left][column].Key, rows[right][column].Key);
            return compared != 0 ? compared : left.CompareTo(right);
        }))];
        int[] ranks = new int[rows.Count];
        for (int position = 0; position < order.Length; position++)
        {
            ranks[order[position]] = position;
        }

        return ranks;
    }

    private static int CompareKeys(object? left, object? right) => (left, right) switch
    {
        (null, null) => 0,
        (null, _) => -1,
        (_, null) => 1,
        (string text, string other) => string.CompareOrdinal(text, other),
        (IComparable value, _) => value.CompareTo(right),
        _ => 0,
    };

    // A cell: its HTML, which the caller encoded; the value it sorts by; and the exact value for its title, if any.
    private sealed record Cell(string Html, object? Key, string? Title = null);

    private static Cell Name(string? value) => new(Text(value ?? "(none)"), value ?? "(none)");

    private static Cell Count(long value) => new(LedgerFormat.Count(value), value);

    // A money amount as shown, titled with its exact value, or Unpriced when there's none.
    private static Cell Money(string shown, decimal? exact) =>
        new(Text(shown), exact, exact is decimal value ? LedgerFormat.Exact(value) : "Unpriced");

    private static Cell Ratio(decimal? ratio) =>
        new(Text(LedgerFormat.Percent(ratio)), ratio, ratio?.ToString(CultureInfo.InvariantCulture) ?? "n/a");

    private static Cell TimeCell(DateTimeOffset value, TimeZoneInfo zone) => new(Time(value, zone), value);

    private static Cell Code(string value) => new($"<code>{Text(value)}</code>", value);

    private static void WriteAuditFindings(StringBuilder html, IReadOnlyList<ExportAuditFinding> findings)
    {
        if (findings.Count == 0)
        {
            return;
        }

        SectionStart(html, "audit-findings", "Audit findings");
        Line(html, "<table>");
        Line(html, "<caption>Audit findings</caption>");
        Line(html, "<thead><tr><th scope=\"col\">Severity</th><th scope=\"col\">Finding</th><th scope=\"col\">Detail</th></tr></thead>");
        Line(html, "<tbody>");
        foreach (ExportAuditFinding finding in findings)
        {
            Line(html, $"<tr><td>{Text(finding.Severity)}</td><td>{Text(finding.Title)}</td><td>{Text(finding.Detail)}</td></tr>");
        }

        Line(html, "</tbody>");
        Line(html, "</table>");
        Line(html, "</section>");
    }

    // Each source's requests: live first, then Copilot session logs, Codex rollouts, and any other source (ordinal).
    private static IEnumerable<string> Sources(IReadOnlyList<ExportSourceCount> sources) =>
        sources
            .OrderBy(source => source.Source switch
            {
                "otlp" => 0,
                "import:copilot" => 1,
                "import:codex" => 2,
                _ => 3,
            })
            .ThenBy(source => source.Source, StringComparer.Ordinal)
            .Select(source => LedgerFormat.Count(source.Spans) + source.Source switch
            {
                "otlp" => " recorded live",
                "import:copilot" => " imported from Copilot session logs",
                "import:codex" => " imported from Codex rollouts",
                _ => $" from {source.Source}",
            });

    private static void Card(StringBuilder html, string id, string label, string value, string exact, string note) =>
        Line(
            html,
            $"<li class=\"card\" id=\"card-{id}\"><span class=\"card-label\">{label}</span> <span class=\"card-value\" title=\"{Text(exact)}\">{Text(value)}</span> <span class=\"card-note\">{Text(note)}</span></li>");

    private static void SectionStart(StringBuilder html, string id, string title)
    {
        Line(html, $"<section id=\"{id}\" aria-labelledby=\"{id}-heading\">");
        Line(html, $"<h2 id=\"{id}-heading\">{title}</h2>");
    }

    // <time datetime="{UTC, round trip}">{yyyy-MM-dd HH:mm in zone}</time>
    private static string Time(DateTimeOffset value, TimeZoneInfo zone) =>
        $"<time datetime=\"{value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)}\">{LedgerFormat.LocalTime(value, zone)}</time>";

    private static string Text(string value) => HtmlEncoder.Default.Encode(value);

    private static void Line(StringBuilder html, string line) => html.Append(line).Append('\n');
}
