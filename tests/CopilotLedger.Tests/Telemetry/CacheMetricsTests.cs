using System.Globalization;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Tests.Telemetry;

public sealed class CacheMetricsTests
{
    public static TheoryData<long, long, decimal> HitRatios => new()
    {
        { 1, 2, 0.5m },
        { 0, 100, 0m },
        { 100, 100, 1m },
        { 1_450_000, 2_600_000, 0.5576923076923076923076923077m },
        { 1, 3, 0.3333333333333333333333333333m },
        { 2, 3, 0.6666666666666666666666666667m },
        { 3_662, 7_429, 0.4929331000134607618791223583m },
        { 1_950_000, 4_000_000, 0.4875m },
    };

    [Theory]
    [MemberData(nameof(HitRatios))]
    public void CM1_HitRatio_IsTheExactDecimalQuotient(long cacheRead, long input, decimal expected)
    {
        decimal? ratio = CacheMetrics.HitRatio(cacheRead, input);

        Assert.Equal(expected, ratio);
        Assert.Equal(
            expected.ToString(CultureInfo.InvariantCulture),
            ratio!.Value.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void CM2_HitRatio_IsNullWithoutInput()
    {
        Assert.Null(CacheMetrics.HitRatio(0, 0));
        Assert.Null(CacheMetrics.HitRatio(5, 0));
    }

    [Theory]
    [InlineData(CacheCounterState.Reported, CacheCounterState.Reported, true)]
    [InlineData(CacheCounterState.Reported, CacheCounterState.Absent, true)]
    [InlineData(CacheCounterState.Reported, CacheCounterState.LegacyUnknown, true)]
    [InlineData(CacheCounterState.Absent, CacheCounterState.Reported, true)]
    [InlineData(CacheCounterState.LegacyUnknown, CacheCounterState.Reported, true)]
    [InlineData(CacheCounterState.Absent, CacheCounterState.Absent, false)]
    [InlineData(CacheCounterState.Absent, CacheCounterState.LegacyUnknown, false)]
    [InlineData(CacheCounterState.LegacyUnknown, CacheCounterState.Absent, false)]
    [InlineData(CacheCounterState.LegacyUnknown, CacheCounterState.LegacyUnknown, false)]
    public void CM3_IsReported_NeedsOneReportedCounter(
        CacheCounterState cacheRead,
        CacheCounterState cacheWrite,
        bool expected)
    {
        Assert.Equal(expected, CacheMetrics.IsReported(cacheRead, cacheWrite));
    }
}
