using CopilotLedger.Core.Pricing;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Pricing;

public sealed class RateCardLoadingTests
{
    public static TheoryData<string, string, string> InvalidDocuments => new()
    {
        { "\"schemaVersion\": 1,", "\"schemaVersion\": 3,", "schemaVersion: must be 1 or 2" },
        { "\"schemaVersion\": 1,", "\"schemaVersion\": 2,", "apiEquivalent: is required" },
        { "\"inputPer1M\": 2.00,", "\"inputPer1M\": 2.0000001,", "githubAiCredits.models[0].windows[0].tiers[0].inputPer1M: must have no more than 6 decimal places" },
        { "\"baselineModel\": \"claude-sonnet-5.5\"", "\"baselineModel\": \"gpt-missing\"", "localOffload.baselineModel: does not resolve to a githubAiCredits model: gpt-missing" },
        { "\"schemaVersion\": 1,", "\"schemaVersion\": 1, \"currency\": \"USD\",", "currency: is not a known property" },
        { "\"maxInputTokens\": null, \"inputPer1M\": 2.00", "\"maxInputTokens\": 1000, \"inputPer1M\": 2.00", "githubAiCredits.models[0].windows[0].tiers[0].maxInputTokens: the last tier must be unbounded" },
        { "\"inputPer1M\": 2.00,", "\"inputPer1M\": -2.00,", "githubAiCredits.models[0].windows[0].tiers[0].inputPer1M: must be greater than or equal to 0" },
        { "\"ratesVersion\": \"test-fixture\",", string.Empty, "ratesVersion: is required" },
        { ", \"retrievedOn\": \"2026-11-20\" }", " }", "source.retrievedOn: is required" },
        { "\"inputPer1M\": 2.00,", "\"inputPer1M\": 1000000.000001,", "githubAiCredits.models[0].windows[0].tiers[0].inputPer1M: must be less than or equal to 1000000" },
    };

    [Fact]
    public void Parse_Fixture_Succeeds()
    {
        Assert.Equal(
            "test-fixture",
            JsonRateCardCalculator.Parse(RateFixtures.ByokTestRatesJson).RatesVersion);
    }

    [Theory]
    [MemberData(nameof(InvalidDocuments))]
    public void Parse_InvalidDocument_ReportsExactMessage(string find, string replacement, string expected)
    {
        string json = RateFixtures.ByokTestRatesJson;
        Assert.Contains(find, json);
        json = json.Replace(find, replacement, StringComparison.Ordinal);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => JsonRateCardCalculator.Parse(json));
        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public void Parse_MalformedJson_Throws()
    {
        string json = RateFixtures.ByokTestRatesJson[..^1];
        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => JsonRateCardCalculator.Parse(json));
        Assert.Equal("line 49, column 1: invalid JSON", exception.Message);
    }

    [Fact]
    public void Parse_NullCollectionElement_ThrowsWithExactPath()
    {
        string json = RateFixtures.ByokTestRatesJson.Replace(
            "\"hostSuffixes\": [\"githubcopilot.com\"]",
            "\"hostSuffixes\": [\"githubcopilot.com\", null]",
            StringComparison.Ordinal);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => JsonRateCardCalculator.Parse(json));

        Assert.Equal("githubAiCredits.hostSuffixes[1]: must be a string", exception.Message);
    }

    [Fact]
    public void Parse_InvalidBaseWithOverlay_DoesNotAddOverlayPrefix()
    {
        string json = RateFixtures.ByokTestRatesJson.Replace(
            "\"inputPer1M\": 2.00,",
            "\"inputPer1M\": -2.00,",
            StringComparison.Ordinal);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => JsonRateCardCalculator.Parse(json, """{ "schemaVersion": 1 }"""));

        Assert.Equal(
            "githubAiCredits.models[0].windows[0].tiers[0].inputPer1M: must be greater than or equal to 0",
            exception.Message);
    }

    [Fact]
    public void Parse_InvalidOverlay_NamesTheOverlay()
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => JsonRateCardCalculator.Parse(
                RateFixtures.ByokTestRatesJson,
                """{ "schemaVersion": 3 }"""));

        Assert.Equal("overlay: schemaVersion: must be 1 or 2", exception.Message);
    }

    [Fact]
    public void Parse_RateAtMaximum_Loads()
    {
        string json = RateFixtures.ByokTestRatesJson.Replace(
            "\"inputPer1M\": 2.00,",
            "\"inputPer1M\": 1000000,",
            StringComparison.Ordinal);
        JsonRateCardCalculator calculator = JsonRateCardCalculator.Parse(json);

        CreditCalculationResult result = calculator.Calculate(
            TestSpans.Chat(requestModel: "claude-sonnet-5.5", inputTokens: 1));

        Assert.Equal(PricingStatus.Priced, result.Status);
        Assert.Equal(1m, result.UsdCost);
    }
}
