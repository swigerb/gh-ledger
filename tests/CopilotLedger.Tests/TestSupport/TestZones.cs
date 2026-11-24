namespace CopilotLedger.Tests.TestSupport;

// Time zones with fixed daylight-saving rules, so tests don't depend on the operating system's time zone database.
public static class TestZones
{
    // UTC-05:00, and UTC-04:00 from the second Sunday of March at 02:00 to the first Sunday of November at 02:00, as New York
    // since 2007.
    public static readonly TimeZoneInfo NewYork = Create("Test/New_York", 2, 2);

    // UTC-05:00, and UTC-04:00 from the second Sunday of March at 18:00 to the first Sunday of November at 07:00: 18:00 to 18:59
    // doesn't exist on the first day, and 06:00 to 06:59 happens twice on the last.
    public static readonly TimeZoneInfo Gap = Create("Test/Gap", 18, 7);

    private static TimeZoneInfo Create(string id, int startHour, int endHour) =>
        TimeZoneInfo.CreateCustomTimeZone(
            id,
            TimeSpan.FromHours(-5),
            id,
            id,
            id + " daylight",
            [
                TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                    DateTime.MinValue.Date,
                    DateTime.MaxValue.Date,
                    TimeSpan.FromHours(1),
                    TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, startHour, 0, 0), 3, 2, DayOfWeek.Sunday),
                    TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, endHour, 0, 0), 11, 1, DayOfWeek.Sunday)),
            ]);
}
