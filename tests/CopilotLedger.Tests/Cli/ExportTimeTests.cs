using System.Globalization;
using CopilotLedger.Cli;
using CopilotLedger.Cli.Export;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

// The export's time rules: time zones by IANA ID, nights across daylight-saving transitions, and --generated-at.
public sealed class ExportTimeTests
{
    [Fact]
    public void ET1_SystemTimeZonesResolveByIanaId()
    {
        SystemCliPlatform platform = new();
        TimeZoneInfo utc = platform.FindTimeZone("UTC")!;
        TimeZoneInfo newYork = platform.FindTimeZone("America/New_York")!;

        Assert.Equal(TimeSpan.Zero, utc.BaseUtcOffset);
        Assert.Equal(TimeSpan.FromHours(-5), newYork.GetUtcOffset(Utc("2026-01-15T12:00:00Z")));
        Assert.Equal(TimeSpan.FromHours(-4), newYork.GetUtcOffset(Utc("2026-07-01T12:00:00Z")));
        Assert.Null(platform.FindTimeZone("Invalid/Zone"));
        Assert.Null(platform.FindTimeZone(""));
        Assert.Null(platform.FindTimeZone(" UTC"));
    }

    // gh-ledger runs with invariant globalization, so on Windows an IANA ID reaches its time zone only through Windows' ICU.
    [Fact]
    public void ET2_WindowsMapsIanaIdsThroughItsIcu()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("Eastern Standard Time", SystemCliPlatform.WindowsTimeZoneId("America/New_York"));
            Assert.Equal("UTC", SystemCliPlatform.WindowsTimeZoneId("Etc/UTC"));
            Assert.Null(SystemCliPlatform.WindowsTimeZoneId("Invalid/Zone"));
            return;
        }

        TimeZoneInfo berlin = new SystemCliPlatform().FindTimeZone("Europe/Berlin")!;
        Assert.Equal(TimeSpan.FromHours(1), berlin.GetUtcOffset(Utc("2026-01-15T12:00:00Z")));
        Assert.Equal(TimeSpan.FromHours(2), berlin.GetUtcOffset(Utc("2026-07-01T12:00:00Z")));
    }

    [Fact]
    public void ET3_UtcNightsAreSixPmToSixAm()
    {
        Assert.Equal(
            [Band("2026-11-20T18:00:00Z", "2026-11-21T06:00:00Z")],
            ExportTime.NightBands(TimeZoneInfo.Utc, Utc("2026-11-20T12:00:00Z"), Utc("2026-11-21T12:00:00Z")));
    }

    [Fact]
    public void ET4_TheSpringForwardNightIsElevenHours()
    {
        IReadOnlyList<UtcBand> bands =
            ExportTime.NightBands(TestZones.NewYork, Utc("2026-03-07T12:00:00Z"), Utc("2026-03-09T12:00:00Z"));

        Assert.Equal(
            [Band("2026-03-07T23:00:00Z", "2026-03-08T10:00:00Z"), Band("2026-03-08T22:00:00Z", "2026-03-09T10:00:00Z")],
            bands);
        Assert.Equal(TimeSpan.FromHours(11), bands[0].ToUtc - bands[0].FromUtc);
    }

    [Fact]
    public void ET5_TheFallBackNightIsThirteenHours()
    {
        IReadOnlyList<UtcBand> bands =
            ExportTime.NightBands(TestZones.NewYork, Utc("2026-10-31T12:00:00Z"), Utc("2026-11-02T12:00:00Z"));

        Assert.Equal(
            [Band("2026-10-31T22:00:00Z", "2026-11-01T11:00:00Z"), Band("2026-11-01T23:00:00Z", "2026-11-02T11:00:00Z")],
            bands);
        Assert.Equal(TimeSpan.FromHours(13), bands[0].ToUtc - bands[0].FromUtc);
    }

    // On 2026-03-08, Test/Gap skips 18:00 to 18:59, so that night starts when the clocks jump, at 19:00 UTC-04:00.
    [Fact]
    public void ET6_ASkippedStartMovesToTheEndOfTheGap()
    {
        Assert.Equal(
            [Band("2026-03-08T23:00:00Z", "2026-03-09T10:00:00Z")],
            ExportTime.NightBands(TestZones.Gap, Utc("2026-03-08T12:00:00Z"), Utc("2026-03-09T12:00:00Z")));
        Assert.Equal(Utc("2026-03-08T23:00:00Z"), ExportTime.ToUtc(new DateTime(2026, 3, 8, 18, 30, 0), TestZones.Gap));
    }

    // On 2026-11-01, Test/Gap has 06:00 to 06:59 twice, so that night ends at the first 06:00, at UTC-04:00.
    [Fact]
    public void ET7_ARepeatedEndTakesItsFirstOccurrence()
    {
        Assert.Equal(
            [Band("2026-10-31T22:00:00Z", "2026-11-01T10:00:00Z")],
            ExportTime.NightBands(TestZones.Gap, Utc("2026-10-31T12:00:00Z"), Utc("2026-11-01T12:00:00Z")));
        Assert.Equal(Utc("2026-11-01T10:30:00Z"), ExportTime.ToUtc(new DateTime(2026, 11, 1, 6, 30, 0), TestZones.Gap));
    }

    [Theory]
    [InlineData("2026-11-21T21:30:00Z", "2026-11-21T21:30:00.0000000+00:00")]
    [InlineData("2026-11-21T21:30:00.1234567Z", "2026-11-21T21:30:00.1230000+00:00")]
    [InlineData("2026-11-21T21:30:00.0000000+00:00", "2026-11-21T21:30:00.0000000+00:00")]
    [InlineData("2026-11-21T21:30:00.5+00:00", "2026-11-21T21:30:00.5000000+00:00")]
    [InlineData("2026-11-21T21:30:00", null)]
    [InlineData("2026-11-21T21:30:00-05:00", null)]
    [InlineData("2026-11-21T21:30:00.Z", null)]
    [InlineData("2026-11-21T21:30:00.12345678Z", null)]
    [InlineData("2026-11-21 21:30:00Z", null)]
    [InlineData("2026-02-30T21:30:00Z", null)]
    [InlineData("", null)]
    [InlineData(" 2026-11-21T21:30:00Z", null)]
    public void ET8_GeneratedAtIsUtcToTheMillisecond(string value, string? expected)
    {
        bool parsed = ExportTime.TryParseGeneratedAt(value, out DateTimeOffset instant);

        Assert.Equal(expected is not null, parsed);
        if (expected is not null)
        {
            Assert.Equal(expected, instant.ToString("O", CultureInfo.InvariantCulture));
        }
    }

    private static UtcBand Band(string from, string to) => new(Utc(from), Utc(to));

    private static DateTimeOffset Utc(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
}
