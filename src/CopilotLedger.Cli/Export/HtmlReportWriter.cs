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
        Line(
            html,
            "<svg class=\"defs\" width=\"0\" height=\"0\" aria-hidden=\"true\" focusable=\"false\"><defs><pattern id=\"hatch\" width=\"6\" height=\"6\" patternUnits=\"userSpaceOnUse\" patternTransform=\"rotate(45)\"><rect class=\"hatch-back\" width=\"6\" height=\"6\" /><line class=\"hatch-line\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"6\" /></pattern></defs></svg>");
        WriteHeader(html, model);
        Line(html, "<main>");
        WriteSummary(html, model);
        WriteCoverage(html, model.Coverage, header.TimeZone);
        WriteTimeline(html, model, header.TimeZone);
        WriteCostSplit(html, model);
        WriteCache(html, model, header.TimeZone);
        WritePromptChains(html, model, header.TimeZone);
        WriteRollups(html, model);
        WriteBreaker(html, model, header.TimeZone);
        WriteAuditFindings(html, model.AuditFindings);
        Line(html, "</main>");
        Line(
            html,
            $"<footer><p>Written by gh-ledger {Text(header.ToolVersion)}. This report holds no prompts, responses, tool arguments or results, or file contents{(header.IncludeLocalPaths ? string.Empty : ", and no local paths")}. Times are in {Text(header.TimeZone.Id)}.</p></footer>");
        html.Append("<script type=\"application/json\" id=\"ledger-data\">");
        html.Append(Encoding.UTF8.GetString(ExportJson.LedgerData(model)));
        Line(html, "</script>");
        Line(html, "<script>");
        Line(html, ExportAssets.Script.TrimEnd('\n'));
        Line(html, "</script>");
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
            string requests = ExportRecords.Plural(before, "request", "requests");
            string their = before == 1 ? "its" : "their";
            string from = LedgerFormat.LocalTime(coverage.ToolHistoryFromUtc, zone);
            yield return coverage.ToolHistoryStart switch
            {
                ExportToolHistoryStart.NoLinks => $"The ledger keeps no tool history, so the {requests} may be missing {their} tool calls.",
                ExportToolHistoryStart.EarliestLink => $"The ledger's tool history starts at {from}, so the {requests} before it may be missing {their} tool calls.",
                _ => $"Tool calls are kept for {coverage.ToolHistoryRetentionDays.ToString(CultureInfo.InvariantCulture)} days, so the {requests} before {from} may be missing {their} tool calls.",
            };
        }

        long withoutDetail = coverage.ImportedToolCallsWithoutDetail;
        if (withoutDetail > 0)
        {
            yield return withoutDetail == 1
                ? "1 tool call of an imported request is counted without detail."
                : $"{LedgerFormat.Count(withoutDetail)} tool calls of imported requests are counted without detail.";
        }

        long withoutToolData = coverage.ImportedRequestsWithoutToolData;
        if (withoutToolData > 0)
        {
            yield return withoutToolData == 1
                ? "1 imported request has no tool-call count in its session log, so it may be missing its tool calls."
                : $"{LedgerFormat.Count(withoutToolData)} imported requests have no tool-call count in their session logs, so they may be missing their tool calls.";
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

        if (coverage.TimelineTruncated)
        {
            yield return ExportRecords.TimelineTruncation(coverage);
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
        yield return ("timeline", "Timeline");
        yield return ("cost-split", "Cost by model and agent");
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

    private static void WriteTimeline(StringBuilder html, ExportReportModel model, TimeZoneInfo zone)
    {
        ExportTimeline timeline = model.Timeline;
        ExportCoverage coverage = model.Coverage;
        SectionStart(html, "timeline", "Timeline");
        string note = timeline.IsEmpty
            ? "No requests or tool calls to draw."
            : coverage.TimelineTruncated
                ? ExportRecords.TimelineTruncation(coverage)
                : $"All {ExportRecords.Plural(coverage.Spans, "request", "requests")} and {ExportRecords.Plural(coverage.ToolCalls, "tool call", "tool calls")}.";
        Line(html, $"<p class=\"note\" id=\"timeline-note\">{Text(note)}</p>");
        if (!timeline.IsEmpty)
        {
            Line(
                html,
                "<div class=\"timeline-controls\" id=\"timeline-controls\" hidden=\"hidden\"><button type=\"button\" id=\"timeline-reset\">Reset zoom</button>");
            Line(
                html,
                "<p id=\"timeline-help\">Hold Ctrl or Command and scroll, or pinch, to zoom; drag sideways to pan; or focus the timeline and use the arrow keys, + and -, and 0. Drag across the overview to choose a range.</p></div>");
            Line(html, "<figure class=\"chart\" id=\"timeline-figure\">");
            ExportSvg.Timeline(html, timeline, zone);
            html.Append("<figcaption>Requests by session. <ul class=\"legend\">");
            for (int color = 0; color < timeline.Models.Count; color++)
            {
                html.Append($"<li><span class=\"swatch m{color.ToString(CultureInfo.InvariantCulture)}\"></span>{Text(timeline.Models[color])}</li>");
            }

            if (timeline.OtherModels)
            {
                html.Append("<li><span class=\"swatch m7\"></span>Other models</li>");
            }

            Line(html, "</ul></figcaption>");
            Line(html, "<details class=\"chart-table\"><summary>Data table</summary>");
            Table(
                html,
                "table-timeline",
                "Sessions on the timeline",
                ["#", "Session", "First request", "Last request", "Requests", "Models", "Tool calls", "Failed tool calls"],
                [
                    .. timeline.Rows.Select(row => (IReadOnlyList<Cell>)
                    [
                        Count(row.Index + 1),
                        new Cell(Text(row.Title), row.Title),
                        row.First is DateTimeOffset first ? TimeCell(first, zone) : new Cell("-", null),
                        row.Last is DateTimeOffset last ? TimeCell(last, zone) : new Cell("-", null),
                        Count(row.Requests),
                        Name(row.Models.Count == 0 ? "-" : string.Join(", ", row.Models)),
                        Count(row.ToolCalls),
                        Count(row.FailedToolCalls),
                    ]),
                ]);
            Line(html, "</details>");
            Line(html, "</figure>");
        }

        Line(html, "</section>");
    }

    // The three lenses of cost. Each figure stands alone without the script; the script shows one at a time.
    private static void WriteCostSplit(StringBuilder html, ExportReportModel model)
    {
        SectionStart(html, "cost-split", "Cost by model and agent");
        Line(
            html,
            "<p>Billed is what GitHub (AI credits) and Microsoft Foundry (BYOK) charge. The API-equivalent and local-savings views compare the same requests with other prices; they're never added to billed spend.</p>");
        Line(html, "<fieldset class=\"lens-toggle\" id=\"lens-toggle\" hidden=\"hidden\"><legend>Show</legend>");
        Line(html, "<label><input type=\"radio\" name=\"lens\" value=\"billed\" checked=\"checked\" /> Billed</label>");
        Line(html, "<label><input type=\"radio\" name=\"lens\" value=\"api\" /> API equivalent</label>");
        Line(html, "<label><input type=\"radio\" name=\"lens\" value=\"local\" /> Local savings</label></fieldset>");
        Lens(
            html,
            "billed",
            "Billed USD: GitHub AI credits and Foundry BYOK",
            "Billed USD",
            model.ByModel,
            model.ByAgent,
            ["Requests", "Unpriced", "GitHub USD", "BYOK USD", "Billed USD"],
            row =>
            [
                Count(row.SpanCount),
                Count(row.UnpricedSpanCount),
                Money(LedgerFormat.Usd(row.AiCreditsUsd), row.AiCreditsUsd),
                Money(LedgerFormat.Usd(row.FoundryByokUsd), row.FoundryByokUsd),
                Money(LedgerFormat.Usd(row.AiCreditsUsd + row.FoundryByokUsd), row.AiCreditsUsd + row.FoundryByokUsd),
            ]);
        Lens(
            html,
            "api",
            "API-equivalent USD: the same requests at API list prices",
            "API-equivalent USD",
            model.ByModel,
            model.ByAgent,
            ["Requests", "API equiv USD", "No API price", "Estimated"],
            row =>
            [
                Count(row.SpanCount),
                Money(LedgerFormat.ApiEquivalent(row), row.ApiEquivalentUsd),
                Count(row.ApiEquivalentUnpricedSpanCount),
                Count(row.ApiEquivalentEstimatedSpanCount),
            ]);
        Lens(
            html,
            "local",
            "Local savings: the cloud spend that local models avoided",
            "Local savings",
            [.. model.ByChannelModel.Where(row => row.Channel == BillingChannel.Local)],
            [.. model.ByAgent.Where(row => row.LocalSavedUsd > 0m)],
            ["Requests", "Local saved"],
            row => [Count(row.SpanCount), Money(LedgerFormat.Usd(row.LocalSavedUsd), row.LocalSavedUsd)]);
        Line(html, "</section>");
    }

    // One lens's figure: its key, a chart by model, a chart by agent, and their tables. Local bars never carry a hatched mark,
    // so the local key has one entry.
    private static void Lens(
        StringBuilder html,
        string lens,
        string caption,
        string name,
        IReadOnlyList<LedgerRollupRow> byModel,
        IReadOnlyList<LedgerRollupRow> byAgent,
        IReadOnlyList<string> headers,
        Func<LedgerRollupRow, IReadOnlyList<Cell>> cells)
    {
        string key = lens switch
        {
            "billed" => Key("seg-github", "GitHub AI credits") + Key("seg-byok", "Foundry BYOK") + HatchKey("unpriced", "Unpriced requests"),
            "api" => Key("seg-api", "API-equivalent USD") + HatchKey("unpriced", "No API-equivalent price"),
            _ => Key("seg-local", "Cloud spend avoided"),
        };
        Line(
            html,
            $"<figure class=\"chart lens\" id=\"lens-{lens}\" data-lens=\"{lens}\"><figcaption>{caption}<ul class=\"legend\" id=\"legend-{lens}\">{key}</ul></figcaption>");
        Chart(byModel, "model", row => row.Model ?? "(none)");
        Chart(byAgent, "agent", row => row.Agent ?? "(none)");
        Line(html, "<details class=\"chart-table\"><summary>Data tables</summary>");
        Table(
            html,
            $"table-cost-{lens}-model",
            $"{name} by model",
            ["Model", .. headers],
            [.. byModel.Select(row => (IReadOnlyList<Cell>)[Name(row.Model), .. cells(row)])]);
        Table(
            html,
            $"table-cost-{lens}-agent",
            $"{name} by agent",
            ["Agent", .. headers],
            [.. byAgent.Select(row => (IReadOnlyList<Cell>)[Name(row.Agent), .. cells(row)])]);
        Line(html, "</details>");
        Line(html, "</figure>");

        void Chart(IReadOnlyList<LedgerRollupRow> rows, string by, Func<LedgerRollupRow, string> label)
        {
            Line(html, $"<h3 class=\"chart-heading\">By {by}</h3>");
            if (rows.Count == 0)
            {
                Line(html, $"<p class=\"empty\">{(lens == "local" ? "No local requests." : "No requests.")}</p>");
            }
            else
            {
                ExportSvg.CostSplit(html, $"cost-{lens}-{by}", $"{name} by {by}", rows, lens, label);
            }
        }
    }

    // A key's entry: a swatch of a CSS class, then what it means.
    private static string Key(string swatch, string meaning) => $"<li><span class=\"swatch {swatch}\"></span>{Text(meaning)}</li>";

    // A key's entry for a hatched mark of the given class.
    private static string HatchKey(string mark, string meaning) => $"<li>{ExportSvg.HatchSwatch(mark)}{Text(meaning)}</li>";

    private static void WriteCache(StringBuilder html, ExportReportModel model, TimeZoneInfo zone)
    {
        SectionStart(html, "cache", "Cache efficiency");
        Line(
            html,
            $"<figure class=\"chart\" id=\"cache-figure\"><figcaption>Prompt-cache hit ratio by model and agent<ul class=\"legend\" id=\"legend-cache\">{Key("seg-cache", "Cache hit ratio")}{HatchKey("na", "No hit ratio (n/a)")}</ul></figcaption>");
        Line(html, "<h3 class=\"chart-heading\">By model</h3>");
        ExportSvg.CacheBars(html, "cache-model", "Cache hit ratio by model", model.CacheByModel, row => row.Model ?? "(none)");
        Line(html, "<h3 class=\"chart-heading\">By agent</h3>");
        ExportSvg.CacheBars(html, "cache-agent", "Cache hit ratio by agent", model.CacheByAgent, row => row.Agent ?? "(none)");
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
        Line(html, "</figure>");
        Line(html, "<h3 id=\"growth-heading\">Context growth</h3>");
        if (model.ChainTurns.Count == 0)
        {
            Line(html, "<p class=\"empty\" id=\"growth-none\">No prompt chain has more than one request.</p>");
        }
        else
        {
            Line(
                html,
                "<figure class=\"chart\" id=\"growth-figure\"><figcaption>Input tokens per request in the costliest prompt chains that have more than one request</figcaption>");
            Line(html, "<div class=\"growth-grid\">");
            foreach (ExportChainTurns chain in model.ChainTurns)
            {
                ExportSvg.Growth(html, chain, zone);
            }

            Line(html, "</div>");
            Line(html, "<details class=\"chart-table\"><summary>Data table</summary>");
            Table(
                html,
                "table-growth",
                "Context growth of the costliest prompt chains",
                ["#", "Trace", "Requests", "First input", "Peak input", "Sequences"],
                [
                    .. model.ChainTurns.Select(chain => (IReadOnlyList<Cell>)
                    [
                        Count(chain.Rank),
                        Code(chain.TraceId),
                        Count(chain.TotalTurns),
                        Count(chain.Turns.Count == 0 ? 0 : chain.Turns[0].InputTokens),
                        Count(chain.PeakInputTokens),
                        Count(chain.Turns.Count == 0 ? 0 : chain.Turns.Max(turn => turn.Sequence) + 1),
                    ]),
                ]);
            Line(html, "</details>");
            Line(html, "</figure>");
        }

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
