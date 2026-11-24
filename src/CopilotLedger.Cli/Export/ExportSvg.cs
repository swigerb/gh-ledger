using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Core.Pricing;

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
        foreach (ExportAxisTick tick in timeline.Ticks)
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
}
