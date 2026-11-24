using System.Text;
using CopilotLedger.Core.Pricing;

namespace CopilotLedger.Tests.Pricing;

public sealed class NanoAiuTests
{
    public static TheoryData<string, long> JsonNumberAccepts => new()
    {
        { "47388600", 47_388_600 },
        { "47388600.0", 47_388_600 },
        { "4.73886E7", 47_388_600 },
        { "4.73886e+7", 47_388_600 },
        { "0", 0 },
        { "-0", 0 },
        { "0.0e5", 0 },
        { "9007199254740992", 9_007_199_254_740_992 },
    };

    [Theory]
    [MemberData(nameof(JsonNumberAccepts))]
    public void NA1_TryFromJsonNumber_AcceptsExactIntegers(string input, long expected)
    {
        bool result = NanoAiu.TryFromJsonNumber(Encoding.UTF8.GetBytes(input), out long actual);

        Assert.True(result);
        Assert.Equal(expected, actual);
    }

    public static TheoryData<string> JsonNumberRejects => new()
    {
        "9007199254740993",
        "1e21",
        "0.5",
        "-1",
        "1E-400",
        "1e400",
        "79228162514264337593543950336",
        "\"47388600\"",
        "true",
        "null",
        "{}",
        "[]",
    };

    [Theory]
    [MemberData(nameof(JsonNumberRejects))]
    public void NA2_TryFromJsonNumber_RejectsUnusableValues(string input)
    {
        Assert.False(NanoAiu.TryFromJsonNumber(Encoding.UTF8.GetBytes(input), out _));
    }

    public static TheoryData<ulong, long> Ieee754BitsAccepts => new()
    {
        { 0x418698BDC0000000, 47_388_600 },
        { 0x4340000000000000, 9_007_199_254_740_992 },
        { 0x3FF0000000000000, 1 },
        { 0x41CDCD6500000000, 1_000_000_000 },
        { 0x0000000000000000, 0 },
        { 0x8000000000000000, 0 },
    };

    [Theory]
    [MemberData(nameof(Ieee754BitsAccepts))]
    public void NA3_TryFromIeee754Bits_AcceptsExactIntegers(ulong input, long expected)
    {
        bool result = NanoAiu.TryFromIeee754Bits(input, out long actual);

        Assert.True(result);
        Assert.Equal(expected, actual);
    }

    public static TheoryData<ulong> Ieee754BitsRejects => new()
    {
        0x4340000000000001,
        0x3FE0000000000000,
        0x7FF8000000000000,
        0x7FF0000000000000,
        0xFFF0000000000000,
        0xBFF0000000000000,
        0x0000000000000001,
        0x4440000000000000,
    };

    [Theory]
    [MemberData(nameof(Ieee754BitsRejects))]
    public void NA4_TryFromIeee754Bits_RejectsUnusableValues(ulong input)
    {
        Assert.False(NanoAiu.TryFromIeee754Bits(input, out _));
    }

    [Fact]
    public void NA5_Conversions_UseExactMoneyAndPicoUnits()
    {
        Assert.Equal(0.000473886m, NanoAiu.ToUsd(47_388_600));
        Assert.Equal(0.0473886m, NanoAiu.ToAiCredits(47_388_600));
        Assert.Equal(473_886_000, NanoAiu.ToPicoUsd(47_388_600));
    }

    public static TheoryData<long, decimal, decimal, long> ConversionBounds => new()
    {
        { 0, 0m, 0m, 0 },
        { 9_007_199_254_740_992, 90_071.99254740992m, 9_007_199.254740992m, 90_071_992_547_409_920 },
    };

    [Theory]
    [MemberData(nameof(ConversionBounds))]
    public void NA6_Conversions_AcceptBounds(
        long nanoAiu,
        decimal expectedUsd,
        decimal expectedAiCredits,
        long expectedPicoUsd)
    {
        Assert.Equal(expectedUsd, NanoAiu.ToUsd(nanoAiu));
        Assert.Equal(expectedAiCredits, NanoAiu.ToAiCredits(nanoAiu));
        Assert.Equal(expectedPicoUsd, NanoAiu.ToPicoUsd(nanoAiu));
    }

    public static TheoryData<long> OutOfRangeValues => new()
    {
        -1,
        9_007_199_254_740_993,
    };

    [Theory]
    [MemberData(nameof(OutOfRangeValues))]
    public void NA7_OutOfRangeValues_Throw(long nanoAiu)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NanoAiu.ToUsd(nanoAiu));
        Assert.Throws<ArgumentOutOfRangeException>(() => NanoAiu.ToAiCredits(nanoAiu));
        Assert.Throws<ArgumentOutOfRangeException>(() => NanoAiu.ToPicoUsd(nanoAiu));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReportedCharge.Valid(nanoAiu));
    }

    [Fact]
    public void NA8_DefaultReportedCharge_IsAbsent()
    {
        ReportedCharge charge = default;

        Assert.Equal(ReportedChargeState.Absent, charge.State);
        Assert.Equal(0, charge.NanoAiu);
        Assert.Equal(default, ReportedCharge.Absent);
    }

    [Fact]
    public void NA9_ProductFiles_UseNoBinaryFloatingPoint()
    {
        string root = RepositoryRoot();

        foreach (string path in new[]
                 {
                     Path.Combine(root, "src", "CopilotLedger.Core", "Pricing", "NanoAiu.cs"),
                     Path.Combine(root, "src", "CopilotLedger.Core", "Pricing", "ReportedCharge.cs"),
                 })
        {
            string text = File.ReadAllText(path);
            Assert.DoesNotContain("double", text);
            Assert.DoesNotContain("float", text);
            Assert.DoesNotContain("Double", text);
            Assert.DoesNotContain("Single", text);
            Assert.DoesNotContain("BitConverter", text);
        }
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CopilotLedger.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
