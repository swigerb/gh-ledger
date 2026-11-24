using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Export;

// The report's SVG. Text goes through HtmlEncoder.Default, and numbers are invariant.
internal static class ExportSvg
{
    // The timeline's session labels, plot, axis, axis labels, and overview.
    public static void Timeline(StringBuilder html, ExportTimeline timeline, TimeZoneInfo zone)
    {
        int height = Math.Max(timeline.Rows.Count, 1) * TimelineLayout.RowHeight;
        long from = timeline.FromUnixMs;
        long to = timeline.ToUnixMs;
        Line(html, "<div class=\"timeline-grid\">");
        Line(
            html,
            $"<svg class=\"timeline-labels\" role=\"img\" aria-labelledby=\"timeline-labels-title timeline-labels-desc\" width=\"200\" height=\"{Int(height)}\" viewBox=\"0 0 200 {Int(height)}\">");
        Line(
            html,
            "<title id=\"timeline-labels-title\">Sessions</title><desc id=\"timeline-labels-desc\">One row per Copilot session, in the order they started.</desc>");
        foreach (ExportTimelineRow row in timeline.Rows)
        {
            Line(
                html,
                $"<text x=\"196\" y=\"{Int(row.Index * TimelineLayout.RowHeight + 11)}\" text-anchor=\"end\"><title>{Text(row.Title)}</title>{Text(row.Label)}</text>");
        }

        Line(html, "</svg>");
        Line(html, "<div class=\"timeline-viewport\" id=\"timeline-viewport\" tabindex=\"0\" aria-describedby=\"timeline-help\">");
        Line(
            html,
            $"<svg id=\"timeline-plot\" role=\"img\" aria-labelledby=\"timeline-plot-title timeline-plot-desc\" viewBox=\"0 0 1000 {Int(height)}\" preserveAspectRatio=\"none\" height=\"{Int(height)}\">");
        Line(html, "<title id=\"timeline-plot-title\">Requests and tool calls over time</title>");
        Line(
            html,
            $"<desc id=\"timeline-plot-desc\">{Text($"{ExportRecords.Plural(timeline.Spans.Count, "request", "requests")} and {ExportRecords.Plural(timeline.ToolCalls.Count, "tool call", "tool calls")} from {LedgerFormat.LocalTime(DateTimeOffset.FromUnixTimeMilliseconds(from), zone)} to {LedgerFormat.LocalTime(DateTimeOffset.FromUnixTimeMilliseconds(to), zone)} ({zone.Id}). Bars are model requests, colored by model; ticks under them are tool calls, red when they failed. Shaded bands are nights, 18:00 to 06:00.")}</desc>");
        Nights(html, timeline, height);
        Line(html, "<g class=\"spans\">");
        foreach (ExportTimelineSpan bar in timeline.Spans)
        {
            ExportSpanRecord span = bar.Span;
            Line(
                html,
                $"<rect class=\"span m{Int(bar.Color)}\" x=\"{ExportJson.Number(bar.X)}\" y=\"{Int(bar.Row * TimelineLayout.RowHeight + 2)}\" width=\"{ExportJson.Number(bar.Width)}\" height=\"8\"><title>{Text($"{Clock(span.StartTime, zone)} · {span.Model ?? "(none)"} · {span.Agent ?? "(none)"} · {Cost(span)}")}</title></rect>");
        }

        Line(html, "</g>");
        Line(html, "<g class=\"tools\">");
        foreach (ExportTimelineTool tick in timeline.ToolCalls)
        {
            ExportToolCallRecord call = tick.Call;
            string y = Int(tick.Row * TimelineLayout.RowHeight + 11);
            string y2 = Int(tick.Row * TimelineLayout.RowHeight + 15);
            string outcome = call.ExitCode is long code
                ? "exit code " + code.ToString(CultureInfo.InvariantCulture)
                : call.Status == "error" ? "error" : "ok";
            Line(
                html,
                $"<line class=\"{(call.Failed ? "tool fail" : "tool")}\" x1=\"{ExportJson.Number(tick.X)}\" y1=\"{y}\" x2=\"{ExportJson.Number(tick.X)}\" y2=\"{y2}\"><title>{Text($"{Clock(call.StartTime, zone)} · {call.ToolName ?? "(unknown tool)"} · {outcome}")}</title></line>");
        }

        Line(html, "</g>");
        Line(html, "</svg>");
        Line(html, "<svg id=\"timeline-axis\" class=\"timeline-axis\" aria-hidden=\"true\" viewBox=\"0 0 1000 6\" preserveAspectRatio=\"none\" height=\"6\">");
        foreach (ExportAxisTick tick in timeline.Ticks.Where(tick => tick.Level <= timeline.AxisLevel))
        {
            Line(html, $"<line class=\"tick\" x1=\"{ExportJson.Number(tick.X)}\" y1=\"0\" x2=\"{ExportJson.Number(tick.X)}\" y2=\"6\" />");
        }

        Line(html, "</svg>");
        html.Append("<div class=\"axis-labels\" id=\"timeline-axis-labels\">");
        foreach (ExportAxisTick tick in TimelineLayout.Labels(timeline.Ticks, 0m, TimelineLayout.Width))
        {
            html.Append(
                $"<span class=\"axis-label\" style=\"left: {(tick.X / 10m).ToString("0.####", CultureInfo.InvariantCulture)}%\">{Text(tick.Label)}</span>");
        }

        Line(html, "</div>");
        Line(
            html,
            "<svg id=\"timeline-overview\" role=\"img\" aria-labelledby=\"timeline-overview-title timeline-overview-desc\" viewBox=\"0 0 1000 32\" preserveAspectRatio=\"none\" height=\"32\">");
        Line(
            html,
            "<title id=\"timeline-overview-title\">Overview</title><desc id=\"timeline-overview-desc\">How many requests started in each part of the timeline.</desc>");
        Nights(html, timeline, 32);
        int[] bins = new int[200];
        foreach (ExportTimelineSpan bar in timeline.Spans)
        {
            long offset = bar.Span.StartTime.ToUnixTimeMilliseconds() - from;
            bins[(int)Math.Min(offset * 200 / (to - from), 199)]++;
        }

        int most = bins.Max();
        for (int bin = 0; bin < bins.Length; bin++)
        {
            if (bins[bin] > 0)
            {
                decimal barHeight = decimal.Round(bins[bin] * 28m / most, 2, MidpointRounding.AwayFromZero);
                Line(
                    html,
                    $"<rect class=\"density\" x=\"{Int(bin * 5)}\" y=\"{(30m - barHeight).ToString("0.##", CultureInfo.InvariantCulture)}\" width=\"5\" height=\"{barHeight.ToString("0.##", CultureInfo.InvariantCulture)}\" />");
            }
        }

        Line(html, "<rect id=\"timeline-window\" class=\"window\" x=\"0\" y=\"0\" width=\"1000\" height=\"32\" />");
        Line(html, "</svg>");
        Line(html, "</div></div>");
    }

    // A cost chart of one lens: billed (GitHub AI credits, then Foundry BYOK), api (API-equivalent), or local (savings). Each
    // chart scales to its own largest bar.
    public static void CostSplit(
        StringBuilder html,
        string id,
        string title,
        IReadOnlyList<LedgerRollupRow> rows,
        string lens,
        Func<LedgerRollupRow, string> name) =>
        Bars(
            html,
            id,
            title,
            lens switch
            {
                "billed" => "Billed USD: GitHub AI credits, then Foundry BYOK. A hatched mark means some requests have no price.",
                "api" => "USD at API list prices. A hatched mark means some requests have no API-equivalent price.",
                _ => "The cloud spend that local models avoided, in USD.",
            },
            [
                .. rows.Select(row => lens switch
                {
                    "billed" => row.UnpricedSpanCount == row.SpanCount
                        ? new Bar(name(row), [], "Unpriced", "unpriced", 12)
                        : new Bar(
                            name(row),
                            [("seg-github", row.AiCreditsUsd), ("seg-byok", row.FoundryByokUsd)],
                            LedgerFormat.Usd(row.AiCreditsUsd + row.FoundryByokUsd)
                                + (row.UnpricedSpanCount > 0 ? $" + {LedgerFormat.Count(row.UnpricedSpanCount)} unpriced" : string.Empty),
                            row.UnpricedSpanCount > 0 ? "unpriced" : null,
                            12),
                    "api" => new Bar(
                        name(row),
                        [("seg-api", row.ApiEquivalentUsd ?? 0m)],
                        LedgerFormat.ApiEquivalent(row),
                        row.ApiEquivalentUnpricedSpanCount > 0 ? "unpriced" : null,
                        12),
                    _ => new Bar(name(row), [("seg-local", row.LocalSavedUsd)], LedgerFormat.Usd(row.LocalSavedUsd), null, 0),
                }),
            ],
            null);

    // Cache hit ratios as bars of ratio × 440. A row that reported no cache counters gets a hatched mark and n/a, never 0%.
    public static void CacheBars(
        StringBuilder html,
        string id,
        string title,
        IReadOnlyList<CacheEfficiencyRow> rows,
        Func<CacheEfficiencyRow, string> name) =>
        Bars(
            html,
            id,
            title,
            "The share of input tokens read from the prompt cache, over the requests that reported cache counters.",
            [
                .. rows.Select(row => row.CacheHitRatio is decimal ratio
                    ? new Bar(
                        name(row),
                        [("seg-cache", ratio)],
                        $"{LedgerFormat.Percent(ratio)} · {LedgerFormat.Count(row.CacheReportedSpanCount)} of {LedgerFormat.Count(row.SpanCount)} reported",
                        null,
                        0)
                    : new Bar(name(row), [], row.CacheReportedSpanCount == 0 ? "n/a (no cache counters)" : "n/a", "na", 60)),
            ],
            1m);

    // A prompt chain's context growth: a caption (rank, local start, requests, peak input; the trace ID in its title), then
    // each request's input tokens in start order, a line per sequence (agent invocation and model), from x = 48 to 348 and
    // y = 130 (0) to 10 (the most input), then a key of the sequences when there's more than one.
    public static void Growth(StringBuilder html, ExportChainTurns chain, TimeZoneInfo zone)
    {
        string id = "growth-" + Int(chain.Rank);
        IReadOnlyList<ExportTurnPoint> turns = chain.Turns;
        long most = turns.Count == 0 ? 0 : turns.Max(turn => turn.InputTokens);
        int sequences = turns.Count == 0 ? 0 : turns.Max(turn => turn.Sequence) + 1;
        string X(int index) => ExportJson.Number(
            decimal.Round(48m + index * 300m / Math.Max(turns.Count - 1, 1), 2, MidpointRounding.AwayFromZero));
        string Y(long input) => ExportJson.Number(
            most == 0 ? 130m : decimal.Round(130m - input * 120m / most, 2, MidpointRounding.AwayFromZero));
        string description =
            $"{ExportRecords.Plural(chain.TotalTurns, "request", "requests")} of trace {chain.TraceId} in {ExportRecords.Plural(sequences, "sequence", "sequences")}, with at most {LedgerFormat.Count(most)} input tokens."
            + (chain.TotalTurns > turns.Count ? $" The chart draws the first {LedgerFormat.Count(turns.Count)}." : string.Empty);
        string caption =
            $"#{Int(chain.Rank)} · {TimeZoneInfo.ConvertTime(chain.StartTime, zone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} · {ExportRecords.Plural(chain.TotalTurns, "request", "requests")} · peak {LedgerFormat.Count(chain.PeakInputTokens)} input tokens";
        Line(html, "<div class=\"growth-chart\">");
        Line(html, $"<p class=\"growth-caption\" id=\"{id}-caption\" title=\"{Text(chain.TraceId)}\">{Text(caption)}</p>");
        Line(html, $"<svg id=\"{id}\" class=\"growth\" role=\"img\" aria-labelledby=\"{id}-title {id}-desc\" viewBox=\"0 0 360 160\">");
        Line(
            html,
            $"<title id=\"{id}-title\">Prompt chain {Int(chain.Rank)}: input tokens per request</title><desc id=\"{id}-desc\">{Text(description)}</desc>");
        Line(html, "<line class=\"axis\" x1=\"48\" y1=\"10\" x2=\"48\" y2=\"130\" /><line class=\"axis\" x1=\"48\" y1=\"130\" x2=\"348\" y2=\"130\" />");
        Line(
            html,
            $"<text x=\"44\" y=\"134\" text-anchor=\"end\">0</text><text x=\"44\" y=\"14\" text-anchor=\"end\">{LedgerFormat.Count(most)}</text><text x=\"48\" y=\"146\" text-anchor=\"middle\">1</text><text x=\"348\" y=\"146\" text-anchor=\"middle\">{LedgerFormat.Count(turns.Count)}</text>");
        List<string> names = [];
        for (int sequence = 0; sequence < sequences; sequence++)
        {
            ExportTurnPoint[] points = [.. turns.Where(turn => turn.Sequence == sequence)];
            string name = $"{points[0].Agent ?? "(none)"} · {points[0].Model}";
            names.Add(name);
            string label = $"{name}: {ExportRecords.Plural(points.Length, "request", "requests")}";
            html.Append($"<g class=\"sequence s{Int(sequence % 8)}\" tabindex=\"0\" role=\"img\" aria-label=\"{Text(label)}\">");
            html.Append($"<polyline points=\"{string.Join(' ', points.Select(point => $"{X(point.Index)},{Y(point.InputTokens)}"))}\" />");
            foreach (ExportTurnPoint point in points)
            {
                html.Append(
                    $"<circle cx=\"{X(point.Index)}\" cy=\"{Y(point.InputTokens)}\" r=\"2\"><title>{Text($"Request {Int(point.Index + 1)} · {Clock(point.StartTime, zone)} · {point.Agent ?? "(none)"} · {point.Model} · {LedgerFormat.Count(point.InputTokens)} input tokens")}</title></circle>");
            }

            Line(html, "</g>");
        }

        Line(html, "</svg>");
        if (names.Count > 1)
        {
            Line(
                html,
                "<ul class=\"legend\">"
                + string.Concat(names.Select((name, sequence) => $"<li><span class=\"swatch s{Int(sequence % 8)}\"></span>{Text(name)}</li>"))
                + "</ul>");
        }

        Line(html, "</div>");
    }

    // A key's swatch of the hatch pattern, drawn with the class of the hatched mark it explains.
    public static string HatchSwatch(string mark) =>
        $"<svg class=\"swatch\" width=\"10\" height=\"10\" aria-hidden=\"true\" focusable=\"false\"><rect class=\"{mark}\" width=\"10\" height=\"10\" fill=\"url(#hatch)\" /></svg>";

    // A horizontal bar chart of at most ExportLimits.ChartRows rows. Segments start at x = 200 and are scaled so that max
    // (by default, the longest bar) is 440 long; then come the hatched mark and the value text.
    private static void Bars(StringBuilder html, string id, string title, string description, IReadOnlyList<Bar> bars, decimal? max)
    {
        Bar[] shown = [.. bars.Take(ExportLimits.ChartRows)];
        decimal scale = max ?? (shown.Length == 0 ? 0m : shown.Max(bar => bar.Segments.Sum(segment => segment.Value)));
        string desc = bars.Count > shown.Length
            ? $"{description} Showing {LedgerFormat.Count(shown.Length)} of {LedgerFormat.Count(bars.Count)}; the table lists them all."
            : description;
        Line(
            html,
            $"<svg id=\"{id}\" class=\"bars\" role=\"img\" aria-labelledby=\"{id}-title {id}-desc\" viewBox=\"0 0 860 {Int(shown.Length * 22 + 8)}\">");
        Line(html, $"<title id=\"{id}-title\">{Text(title)}</title><desc id=\"{id}-desc\">{Text(desc)}</desc>");
        for (int row = 0; row < shown.Length; row++)
        {
            Bar bar = shown[row];
            string label = $"{bar.Name}: {bar.Value}";
            html.Append($"<g class=\"bar\" tabindex=\"0\" role=\"img\" aria-label=\"{Text(label)}\"><title>{Text(label)}</title>");
            html.Append(
                $"<text class=\"label\" x=\"192\" y=\"{Int(row * 22 + 19)}\" text-anchor=\"end\">{Text(ExportRecords.Shorten(bar.Name, 28))}</text>");
            decimal x = 200m;
            foreach ((string segment, decimal value) in bar.Segments)
            {
                decimal width = scale == 0m ? 0m : decimal.Round(value * 440m / scale, 2, MidpointRounding.AwayFromZero);
                if (width > 0m)
                {
                    html.Append(
                        $"<rect class=\"{segment}\" x=\"{ExportJson.Number(x)}\" y=\"{Int(row * 22 + 6)}\" width=\"{ExportJson.Number(width)}\" height=\"14\" />");
                    x += width;
                }
            }

            if (bar.Mark is not null)
            {
                html.Append(
                    $"<rect class=\"{bar.Mark}\" x=\"{ExportJson.Number(x)}\" y=\"{Int(row * 22 + 6)}\" width=\"{Int(bar.MarkWidth)}\" height=\"14\" fill=\"url(#hatch)\" />");
                x += bar.MarkWidth;
            }

            Line(html, $"<text class=\"value\" x=\"{ExportJson.Number(x + 6m)}\" y=\"{Int(row * 22 + 18)}\">{Text(bar.Value)}</text></g>");
        }

        Line(html, "</svg>");
    }

    // The night bands, clipped to the axis, as rects of the given height.
    private static void Nights(StringBuilder html, ExportTimeline timeline, int height)
    {
        Line(html, "<g class=\"nights\">");
        foreach (UtcBand band in timeline.NightBands)
        {
            long start = Math.Max(band.FromUtc.ToUnixTimeMilliseconds(), timeline.FromUnixMs);
            long end = Math.Min(band.ToUtc.ToUnixTimeMilliseconds(), timeline.ToUnixMs);
            decimal x = TimelineLayout.Position(start, timeline.FromUnixMs, timeline.ToUnixMs);
            decimal width = TimelineLayout.Position(end, timeline.FromUnixMs, timeline.ToUnixMs) - x;
            Line(
                html,
                $"<rect class=\"night\" x=\"{ExportJson.Number(x)}\" y=\"0\" width=\"{ExportJson.Number(width)}\" height=\"{Int(height)}\" />");
        }

        Line(html, "</g>");
    }

    // What a request cost, for its bar's title.
    private static string Cost(ExportSpanRecord span) =>
        span.PricingStatus is not ("priced" or "local_offload")
            ? $"Unpriced ({span.PricingStatus})"
            : span.BillingChannel switch
            {
                BillingChannel.GitHubAiCredits => $"{LedgerFormat.Credits(span.AiCredits)} AI credits",
                BillingChannel.FoundryByok => $"{LedgerFormat.Usd(span.CostUsd)} BYOK",
                BillingChannel.Local => $"local, {LedgerFormat.Usd(span.SavedUsd)} saved",
                _ => $"Unpriced ({span.PricingStatus})",
            };

    private static string Clock(DateTimeOffset value, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(value, zone).ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Text(string value) => HtmlEncoder.Default.Encode(value);

    private static void Line(StringBuilder html, string line) => html.Append(line).Append('\n');

    // One bar: its row's name, its segments (CSS class and value), its value text, and its hatched mark's class and width.
    private sealed record Bar(string Name, IReadOnlyList<(string Class, decimal Value)> Segments, string Value, string? Mark, int MarkWidth);
}
