using System.Globalization;
using CopilotLedger.Cli.Reporting;

namespace CopilotLedger.Cli.Export;

// The timeline's geometry: rows, bars, ticks, night bands, and axis marks, with x from 0 to Width over the drawn range.
internal static class TimelineLayout
{
    public const int Width = 1000;
    public const int RowHeight = 16;
    public const int MaxRows = 200;
    private const long MinimumRangeMs = 3_600_000;
    private const long DayMs = 86_400_000;

    public static readonly ExportTimeline Empty = new()
    {
        FromUnixMs = 0,
        ToUnixMs = 0,
        Rows = [],
        Spans = [],
        ToolCalls = [],
        NightBands = [],
        Ticks = [],
        Models = [],
        OtherModels = false,
    };

    // spans and toolCalls are the drawn ones in start order; modelsByCost is the model rollup's order, which picks colors.
    public static ExportTimeline Build(
        IReadOnlyList<ExportSpanRecord> spans,
        IReadOnlyList<ExportToolCallRecord> toolCalls,
        IReadOnlyList<string> modelsByCost,
        TimeZoneInfo zone)
    {
        if (spans.Count == 0 && toolCalls.Count == 0)
        {
            return Empty;
        }

        long t0 = Math.Min(
            spans.Count == 0 ? long.MaxValue : spans.Min(span => span.StartTime.ToUnixTimeMilliseconds()),
            toolCalls.Count == 0 ? long.MaxValue : toolCalls.Min(call => call.StartTime.ToUnixTimeMilliseconds()));
        long t1 = Math.Max(
            spans.Count == 0 ? long.MinValue : spans.Max(span => span.EndTime.ToUnixTimeMilliseconds()),
            toolCalls.Count == 0 ? long.MinValue : toolCalls.Max(call => call.StartTime.ToUnixTimeMilliseconds()));
        if (t1 - t0 < MinimumRangeMs)
        {
            t0 -= (MinimumRangeMs - (t1 - t0)) / 2;
            t1 = t0 + MinimumRangeMs;
        }

        // A tool call's row is its prompt chain's: the conversation of the trace's first drawn request, else its own.
        Dictionary<string, string?> traces = new(StringComparer.Ordinal);
        foreach (ExportSpanRecord span in spans)
        {
            traces.TryAdd(span.TraceId, span.ConversationId);
        }

        string? ToolConversation(ExportToolCallRecord call) =>
            traces.TryGetValue(call.TraceId, out string? conversation) ? conversation : call.ConversationId;

        Dictionary<string, Group> conversations = new(StringComparer.Ordinal);
        Group? none = null;
        Group GroupOf(string? conversation)
        {
            if (conversation is null)
            {
                return none ??= new Group(null);
            }

            if (!conversations.TryGetValue(conversation, out Group? group))
            {
                group = new Group(conversation);
                conversations.Add(conversation, group);
            }

            return group;
        }

        foreach (ExportSpanRecord span in spans)
        {
            GroupOf(span.ConversationId).Add(span.StartTime.ToUnixTimeMilliseconds());
        }

        foreach (ExportToolCallRecord call in toolCalls)
        {
            GroupOf(ToolConversation(call)).Add(call.StartTime.ToUnixTimeMilliseconds());
        }

        // With more than MaxRows groups, the MaxRows - 1 with the most glyphs keep rows and the others share the last.
        List<Group> groups = [.. conversations.Values];
        if (none is not null)
        {
            groups.Add(none);
        }

        List<Group> kept = groups.Count <= MaxRows
            ? groups
            : [.. groups
                .OrderByDescending(group => group.Glyphs)
                .ThenBy(group => group.First)
                .ThenBy(group => group.Conversation is null ? 1 : 0)
                .ThenBy(group => group.Conversation, StringComparer.Ordinal)
                .Take(MaxRows - 1)];
        List<Group> ordered =
        [
            .. kept.Where(group => group.Conversation is not null)
                .OrderBy(group => group.First)
                .ThenBy(group => group.Conversation, StringComparer.Ordinal),
            .. kept.Where(group => group.Conversation is null),
        ];
        int others = groups.Count - kept.Count;
        for (int index = 0; index < ordered.Count; index++)
        {
            ordered[index].Row = index;
        }

        foreach (Group group in groups.Where(group => group.Row < 0))
        {
            group.Row = ordered.Count;
        }

        decimal X(long instant) => Position(instant, t0, t1);

        List<string> colors = [.. modelsByCost.Take(7)];
        ExportTimelineSpan[] bars =
        [
            .. spans.Select(span =>
            {
                decimal start = X(span.StartTime.ToUnixTimeMilliseconds());
                decimal end = X(span.EndTime.ToUnixTimeMilliseconds());
                int color = span.Model is null ? -1 : colors.IndexOf(span.Model);
                return new ExportTimelineSpan(span, GroupOf(span.ConversationId).Row, start, Math.Max(end - start, 0m), color < 0 ? 7 : color);
            }),
        ];
        ExportTimelineTool[] ticks =
        [
            .. toolCalls.Select(call =>
                new ExportTimelineTool(call, GroupOf(ToolConversation(call)).Row, X(call.StartTime.ToUnixTimeMilliseconds()))),
        ];
        ILookup<int, ExportTimelineSpan> barsByRow = bars.ToLookup(bar => bar.Row);
        ILookup<int, ExportTimelineTool> ticksByRow = ticks.ToLookup(tick => tick.Row);

        // A row's label is its number and the local time of its first request or tool call, which is unique and readable
        // even when session IDs share a long prefix; the title and the data table hold the whole ID.
        List<ExportTimelineRow> rows =
        [
            .. ordered.Select(group => Row(
                group.Row,
                group.Conversation is null
                    ? $"{Ordinal(group.Row)} · no session"
                    : $"{Ordinal(group.Row)} · {TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(group.First), zone).ToString("MM-dd HH:mm", CultureInfo.InvariantCulture)}",
                group.Conversation ?? "(no session)",
                barsByRow[group.Row],
                ticksByRow[group.Row])),
        ];
        if (others > 0)
        {
            rows.Add(Row(
                ordered.Count,
                $"{Ordinal(ordered.Count)} · {LedgerFormat.Count(others)} other sessions",
                $"({LedgerFormat.Count(others)} other sessions)",
                barsByRow[ordered.Count],
                ticksByRow[ordered.Count]));
        }

        return new ExportTimeline
        {
            FromUnixMs = t0,
            ToUnixMs = t1,
            Rows = rows,
            Spans = bars,
            ToolCalls = ticks,
            NightBands = ExportTime.NightBands(
                zone,
                DateTimeOffset.FromUnixTimeMilliseconds(t0),
                DateTimeOffset.FromUnixTimeMilliseconds(t1)),
            Ticks = Ticks(t0, t1, zone, X),
            Models = colors,
            OtherModels = bars.Any(bar => bar.Color == 7),
        };
    }

    // An instant's x on the axis from `from` (0) to `to` (Width), to three decimal places.
    public static decimal Position(long instant, long from, long to) =>
        decimal.Round((instant - from) * (decimal)Width / (to - from), 3, MidpointRounding.AwayFromZero);

    // A row's number as shown: its index from 1.
    private static string Ordinal(int row) => (row + 1).ToString(CultureInfo.InvariantCulture);

    // The axis labels to show over [from, to] of x: the finest level whose ticks there, counting coarser levels, number at
    // most 12; at level 0, every ceil(n / 12)-th day.
    public static IReadOnlyList<ExportAxisTick> Labels(IReadOnlyList<ExportAxisTick> ticks, decimal from, decimal to)
    {
        ExportAxisTick[] inView = [.. ticks.Where(tick => tick.X >= from && tick.X <= to)];
        for (int level = 3; level >= 0; level--)
        {
            ExportAxisTick[] shown = [.. inView.Where(tick => tick.Level <= level)];
            if (shown.Length <= 12)
            {
                return shown;
            }
        }

        ExportAxisTick[] days = [.. inView.Where(tick => tick.Level == 0)];
        int every = (days.Length + 11) / 12;
        return [.. days.Where((_, index) => index % every == 0)];
    }

    // Local boundaries from t0 to t1 as ticks: level 0 at midnight, 1 at 06:00, 12:00, and 18:00 within 62 days, 2 at other
    // whole hours within 7 days, and 3 at other 10-minute marks within a day. A boundary that a transition skips maps to the
    // instant of the previous one and is dropped.
    private static IReadOnlyList<ExportAxisTick> Ticks(long t0, long t1, TimeZoneInfo zone, Func<long, decimal> x)
    {
        long range = t1 - t0;
        TimeSpan step = range <= DayMs
            ? TimeSpan.FromMinutes(10)
            : range <= 7 * DayMs
                ? TimeSpan.FromHours(1)
                : range <= 62 * DayMs ? TimeSpan.FromHours(6) : TimeSpan.FromDays(1);
        DateTime end = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(t1), zone).DateTime;
        List<ExportAxisTick> ticks = [];
        long previous = long.MinValue;
        for (DateTime local = ExportTime.LocalDate(DateTimeOffset.FromUnixTimeMilliseconds(t0), zone).ToDateTime(TimeOnly.MinValue);
             local <= end;
             local += step)
        {
            long instant = ExportTime.ToUtc(local, zone).ToUnixTimeMilliseconds();
            if (instant < t0 || instant > t1 || instant == previous)
            {
                continue;
            }

            previous = instant;
            int level = local.TimeOfDay == TimeSpan.Zero ? 0 : local.Minute != 0 ? 3 : local.Hour % 6 == 0 ? 1 : 2;
            ticks.Add(new ExportAxisTick(
                x(instant),
                level,
                local.ToString(level == 0 ? "yyyy-MM-dd" : "HH:mm", CultureInfo.InvariantCulture)));
        }

        int days = ticks.Count(tick => tick.Level == 0);
        if (days > 2_000)
        {
            int every = (days + 1_999) / 2_000;
            int day = -1;
            ticks = [.. ticks.Where(tick => tick.Level != 0 || ++day % every == 0)];
        }

        return ticks;
    }

    private static ExportTimelineRow Row(
        int index,
        string label,
        string title,
        IEnumerable<ExportTimelineSpan> bars,
        IEnumerable<ExportTimelineTool> ticks)
    {
        ExportTimelineSpan[] requests = [.. bars];
        ExportTimelineTool[] calls = [.. ticks];
        return new ExportTimelineRow(
            index,
            label,
            title,
            requests.Length,
            calls.Length,
            calls.Count(tick => tick.Call.Failed),
            requests.Length == 0 ? null : requests[0].Span.StartTime,
            requests.Length == 0 ? null : requests[^1].Span.StartTime,
            [.. requests.Select(bar => bar.Span.Model ?? "(none)").Distinct().Order(StringComparer.Ordinal)]);
    }

    // One row's glyphs before rows are assigned.
    private sealed class Group(string? conversation)
    {
        public string? Conversation { get; } = conversation;
        public long Glyphs { get; private set; }
        public long First { get; private set; } = long.MaxValue;
        public int Row { get; set; } = -1;

        public void Add(long start)
        {
            Glyphs++;
            First = Math.Min(First, start);
        }
    }
}
