using System.Globalization;
using System.Text.RegularExpressions;

namespace CopilotLedger.Cli.Export;

// One night of the timeline's shading, from a local NightStart to the next day's NightEnd, in UTC.
public sealed record UtcBand(DateTimeOffset FromUtc, DateTimeOffset ToUtc);

// The time rules of gh ledger export.
public static partial class ExportTime
{
    public static readonly TimeOnly NightStart = new(18, 0);
    public static readonly TimeOnly NightEnd = new(6, 0);

    private static readonly string[] GeneratedAtFormats = ["yyyy-MM-dd'T'HH:mm:ssK", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"];

    // A local date and time in zone as a UTC instant, to the millisecond. A time that a spring-forward transition skips moves to
    // the end of the gap (the transition instant); a time that a fall-back transition repeats takes its first, earlier occurrence.
    public static DateTimeOffset ToUtc(DateTime local, TimeZoneInfo zone)
    {
        DateTime value = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        for (int step = 0; zone.IsInvalidTime(value); step++)
        {
            if (step == 1440)
            {
                throw new InvalidOperationException($"The time zone {zone.Id} skips more than a day.");
            }

            value = value.AddMinutes(1);
        }

        TimeSpan offset = zone.IsAmbiguousTime(value) ? zone.GetAmbiguousTimeOffsets(value).Max() : zone.GetUtcOffset(value);
        return TruncateToMilliseconds(new DateTimeOffset(value.Ticks - offset.Ticks, TimeSpan.Zero));
    }

    // The nights, from local NightStart to the next day's NightEnd in zone, that overlap [fromUtc, toUtc): unclipped, in order.
    public static IReadOnlyList<UtcBand> NightBands(TimeZoneInfo zone, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        List<UtcBand> bands = [];
        DateOnly last = LocalDate(toUtc, zone);
        for (DateOnly date = LocalDate(fromUtc, zone).AddDays(-1); date <= last; date = date.AddDays(1))
        {
            DateTimeOffset start = ToUtc(date.ToDateTime(NightStart), zone);
            DateTimeOffset end = ToUtc(date.AddDays(1).ToDateTime(NightEnd), zone);
            if (end > fromUtc && start < toUtc)
            {
                bands.Add(new UtcBand(start, end));
            }
        }

        return bands;
    }

    // value in UTC, truncated to whole milliseconds.
    public static DateTimeOffset TruncateToMilliseconds(DateTimeOffset value) =>
        new(value.UtcTicks - value.UtcTicks % TimeSpan.TicksPerMillisecond, TimeSpan.Zero);

    // --generated-at: yyyy-MM-ddTHH:mm:ss, an optional fraction of 1 to 7 digits, then Z or +00:00. The instant, truncated to
    // milliseconds; false for anything else. The shape is checked first because the K specifier also accepts a missing zone.
    public static bool TryParseGeneratedAt(string? value, out DateTimeOffset instant)
    {
        if (value is not null
            && GeneratedAtShape().IsMatch(value)
            && DateTimeOffset.TryParseExact(
                value,
                GeneratedAtFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTimeOffset parsed)
            && parsed.Offset == TimeSpan.Zero)
        {
            instant = TruncateToMilliseconds(parsed);
            return true;
        }

        instant = default;
        return false;
    }

    // The local date of an instant in zone.
    internal static DateOnly LocalDate(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

    [GeneratedRegex(@"\A[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,7})?(Z|\+00:00)\z")]
    private static partial Regex GeneratedAtShape();
}
