using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace CopilotLedger.Storage;

// A report period and where it starts; From is null for all time.
public sealed record LedgerPeriod(string Label, DateTimeOffset? From)
{
    // value is today, all, Nd (1 to 3650 days, counting today), or a yyyy-MM-dd date, without regard to case or surrounding
    // white space. Periods start at local midnight in timeProvider's time zone. Returns false for anything else.
    public static bool TryParse(
        string? value,
        TimeProvider timeProvider,
        [NotNullWhen(true)] out LedgerPeriod? period)
    {
        string text = value?.Trim() ?? string.Empty;
        DateTimeOffset now = timeProvider.GetUtcNow();
        TimeZoneInfo zone = timeProvider.LocalTimeZone;
        DateOnly today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(now, zone).DateTime);

        if (text.Equals("today", StringComparison.OrdinalIgnoreCase))
        {
            period = new LedgerPeriod("today", Midnight(today, zone));
            return true;
        }

        if (text.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            period = new LedgerPeriod("all time", null);
            return true;
        }

        if (text.EndsWith("d", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(
                text[..^1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int days) &&
            days is >= 1 and <= 3650)
        {
            period = new LedgerPeriod(
                days == 1 ? "today" : $"last {days} days",
                Midnight(today.AddDays(-(days - 1)), zone));
            return true;
        }

        if (DateOnly.TryParseExact(
                text,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateOnly date))
        {
            period = new LedgerPeriod($"since {text}", Midnight(date, zone));
            return true;
        }

        period = null;
        return false;
    }

    private static DateTimeOffset Midnight(DateOnly date, TimeZoneInfo zone)
    {
        DateTime local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }
}
