using System.Text.Json;
using System.Text.Json.Nodes;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Pricing;

public sealed class ApiEquivalentCardTests
{
    // The SHA-256 of RetailTestRatesJson's canonical API-equivalent card.
    private const string RetailFixtureHash = "0075a4b28916a99fc339c7c8acad551cd5828b35c094d60d3fb9e0b0d939977c";

    // The SHA-256 of a card without apiEquivalent models: {"localBaselineModel":null,"models":[]}.
    private const string EmptyCardHash = "49a4384900a30f23c85c0bb1f208f01164855e992f1da709cca54615f954746b";

    private const string AddedModel = """
        { "id": "test-new", "aliases": ["test-new-latest"], "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1.5, "outputPer1M": 6 } ] } ] }
        """;

    private const string ReplacedModel = """
        { "id": "Test Retail", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 2.5, "cachedInputPer1M": 0.25, "outputPer1M": 12.5 } ] } ] }
        """;

    // Each case replaces every occurrence of find in RetailTestRatesJson.
    public static TheoryData<string, string, string> InvalidCards => new()
    {
        { "\"schemaVersion\": 2,", "\"schemaVersion\": 1,", "apiEquivalent: is allowed only when schemaVersion is 2" },
        { "\"schemaVersion\": 2,", "\"schemaVersion\": 3,", "schemaVersion: must be 1 or 2" },
        { "\"inputPer1M\": 3.00,", "\"inputPer1M\": 3.0000001,", "apiEquivalent.models[0].windows[0].tiers[0].inputPer1M: must have no more than 6 decimal places" },
        { "\"effectiveFrom\": \"2026-12-01T00:00:00Z\",", "\"effectiveFrom\": \"2026-11-30T00:00:00Z\",", "apiEquivalent.models[0].windows[1].effectiveFrom: windows must be sorted and must not overlap" },
        { "\"aliases\": [\"test-retail-2026-11-01\"],", "\"aliases\": [\"Test Retail\"],", "apiEquivalent.models[0].aliases[0]: duplicate model identity: test-retail" },
        { "\"description\": \"Fictional retail prices for tests only.\",", "\"Description\": \"x\",", "apiEquivalent.Description: is not a known property; did you mean description?" },
        { "\"description\": \"Fictional retail prices for tests only.\",", "\"localBaselineModel\": \"test-missing\",", "apiEquivalent.localBaselineModel: does not resolve to an apiEquivalent model: test-missing" },
        { "\"description\": \"Fictional retail prices for tests only.\",", "\"localBaselineModel\": \" \",", "apiEquivalent.localBaselineModel: must not be blank" },
        { "{ \"id\": \"test-baseline\", \"aliases\": [],", "{ \"id\": \"test-baseline\", \"vendor\": \"x\", \"aliases\": [],", "githubAiCredits.models[1].vendor: is not a known property" },
        { "\"vendor\": \"test-vendor\",", "\"vendor\": 5,", "apiEquivalent.models[0].vendor: must be a string" },
    };

    public static TheoryData<string, string> InvalidOverlays => new()
    {
        { """{ "schemaVersion": 1, "apiEquivalent": { } }""", "overlay: apiEquivalent: is allowed only when schemaVersion is 2" },
        { """{ "schemaVersion": 2, "apiEquivalent": { "models": [ { "id": "test-new", "aliases": ["test-baseline"], "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1, "outputPer1M": 1 } ] } ] } ] } }""", "overlay: apiEquivalent.models[0].aliases[0]: duplicate model identity in the merged rates: test-baseline (built-in model test-baseline)" },
        { """{ "schemaVersion": 2, "apiEquivalent": { "localBaselineModel": "test-missing" } }""", "overlay: apiEquivalent.localBaselineModel: does not resolve to an apiEquivalent model in the merged rates: test-missing" },
        { """{ "schemaVersion": 2, "apiEquivalent": { "localBaselineModel": "" } }""", "overlay: apiEquivalent.localBaselineModel: must not be blank" },
        { """{ "schemaVersion": 2, "apiEquivalent": { "models": [ { "id": "test-new", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1, "outputPer1M": 1.0000001 } ] } ] } ] } }""", "overlay: apiEquivalent.models[0].windows[0].tiers[0].outputPer1M: must have no more than 6 decimal places" },
        { """{ "schemaVersion": 2, "apiEquivalent": { "models": [ { "id": "test-new", "windows": [ { "effectiveFrom": "2027-01-01T00:00:00Z", "tiers": [ { "name": "default", "inputPer1M": 1, "outputPer1M": 1 } ] }, { "effectiveFrom": "2026-12-01T00:00:00Z", "tiers": [ { "name": "default", "inputPer1M": 2, "outputPer1M": 2 } ] } ] } ] } }""", "overlay: apiEquivalent.models[0].windows[1].effectiveFrom: windows must be sorted and must not overlap" },
        { """{ "schemaVersion": 2, "githubAiCredits": { "models": [ { "id": "test-new", "vendor": "x", "windows": [] } ] } }""", "overlay: githubAiCredits.models[0].vendor: is not a known property" },
    };

    [Theory]
    [MemberData(nameof(InvalidCards))]
    public void AK1_InvalidCardsReportExactPaths(string find, string replacement, string expected)
    {
        Assert.Contains(find, RateFixtures.RetailTestRatesJson);
        string json = RateFixtures.RetailTestRatesJson.Replace(find, replacement, StringComparison.Ordinal);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => JsonRateCardCalculator.Parse(json));

        Assert.Equal(expected, exception.Message);
    }

    [Theory]
    [MemberData(nameof(InvalidOverlays))]
    public void AK2_InvalidOverlaysReportExactPaths(string overlay, string expected)
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => JsonRateCardCalculator.Parse(RateFixtures.RetailTestRatesJson, overlay));

        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public void AK3_OverlayReplacesAndAddsModelsById()
    {
        string overlay = $$"""{ "schemaVersion": 2, "ratesVersion": "contoso", "apiEquivalent": { "models": [ {{ReplacedModel}}, {{AddedModel}} ] } }""";
        JsonNode expected = JsonNode.Parse(RateFixtures.RetailTestRatesJson)!;
        JsonArray models = expected["apiEquivalent"]!["models"]!.AsArray();
        models[0] = JsonNode.Parse(ReplacedModel);
        models.Add(JsonNode.Parse(AddedModel));

        JsonRateCardCalculator merged = JsonRateCardCalculator.Parse(RateFixtures.RetailTestRatesJson, overlay);

        Assert.Equal("retail-fixture+contoso", merged.RatesVersion);
        Assert.Equal(2, merged.SchemaVersion);
        Assert.Equal(JsonRateCardCalculator.Parse(expected.ToJsonString()).ApiRateCardSha256, merged.ApiRateCardSha256);
        Assert.NotEqual(RetailFixtureHash, merged.ApiRateCardSha256);
    }

    [Fact]
    public void AK4_OverlayChoosesTheLocalBaseline()
    {
        JsonNode expected = JsonNode.Parse(RateFixtures.RetailTestRatesJson)!;
        expected["apiEquivalent"]!["localBaselineModel"] = "test-no-cache";

        JsonRateCardCalculator merged = JsonRateCardCalculator.Parse(
            RateFixtures.RetailTestRatesJson,
            """{ "schemaVersion": 2, "apiEquivalent": { "localBaselineModel": "Test No Cache" } }""");

        Assert.Equal(JsonRateCardCalculator.Parse(expected.ToJsonString()).ApiRateCardSha256, merged.ApiRateCardSha256);
        Assert.NotEqual(RetailFixtureHash, merged.ApiRateCardSha256);
    }

    [Fact]
    public void AK5_SchemaVersionsAndPinnedHashes()
    {
        JsonRateCardCalculator retail = JsonRateCardCalculator.Parse(RateFixtures.RetailTestRatesJson);
        JsonRateCardCalculator credits = JsonRateCardCalculator.Parse(RateFixtures.ByokTestRatesJson);
        JsonRateCardCalculator upgraded = JsonRateCardCalculator.Parse(
            RateFixtures.ByokTestRatesJson,
            $$"""{ "schemaVersion": 2, "apiEquivalent": { "models": [ {{AddedModel}} ] } }""");

        Assert.Equal(2, retail.SchemaVersion);
        Assert.Equal(RetailFixtureHash, retail.ApiRateCardSha256);
        Assert.Equal(1, credits.SchemaVersion);
        Assert.Equal(EmptyCardHash, credits.ApiRateCardSha256);
        Assert.Equal(2, upgraded.SchemaVersion);
        Assert.Equal("test-fixture+user", upgraded.RatesVersion);
        Assert.NotEqual(EmptyCardHash, upgraded.ApiRateCardSha256);
    }

    [Fact]
    public void AK6_TheHashIgnoresFormattingOrderAndDescriptions()
    {
        JsonNode card = JsonNode.Parse(RateFixtures.RetailTestRatesJson)!;
        JsonArray models = card["apiEquivalent"]!["models"]!.AsArray();
        JsonNode[] reversed = models.Reverse().Select(model => model!.DeepClone()).ToArray();
        models.Clear();
        foreach (JsonNode model in reversed)
        {
            model["vendor"] = "another-vendor";
            model["sourceUrl"] = "https://example.invalid/other";
            models.Add(model);
        }

        JsonNode retail = models.Single(model => model!["id"]!.GetValue<string>() == "test-retail")!;
        retail["id"] = "Test Retail";
        retail["aliases"] = new JsonArray("TEST-RETAIL-2026-11-01");
        retail["windows"]![0]!["tiers"]![0]!["inputPer1M"] = 3m;
        retail["windows"]![0]!["tiers"]![0]!["outputPer1M"] = 15.000000m;
        card["apiEquivalent"]!["description"] = "Another description.";
        card["notes"] = new JsonArray("Other notes.");
        string reformatted = card.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
            .ReplaceLineEndings("\r\n");

        Assert.Equal(RetailFixtureHash, JsonRateCardCalculator.Parse(reformatted).ApiRateCardSha256);
    }

    [Theory]
    [InlineData("\"outputPer1M\": 3.20", "\"outputPer1M\": 3.21")]
    [InlineData("\"maxInputTokens\": 200000, \"inputPer1M\": 4.00", "\"maxInputTokens\": 200001, \"inputPer1M\": 4.00")]
    [InlineData("\"effectiveFrom\": \"2026-06-01T00:00:00Z\"", "\"effectiveFrom\": \"2026-06-01T00:00:01Z\"")]
    [InlineData("\"aliases\": [\"test-retail-2026-11-01\"]", "\"aliases\": [\"test-retail-2026-11-01\", \"test-retail-latest\"]")]
    [InlineData("\"name\": \"long_context\", \"maxInputTokens\": null, \"inputPer1M\": 6.00", "\"name\": \"long\", \"maxInputTokens\": null, \"inputPer1M\": 6.00")]
    public void AK7_AnyPricingChangeChangesTheHash(string find, string replacement)
    {
        Assert.Contains(find, RateFixtures.RetailTestRatesJson);
        string changed = RateFixtures.RetailTestRatesJson.Replace(find, replacement, StringComparison.Ordinal);

        Assert.NotEqual(RetailFixtureHash, JsonRateCardCalculator.Parse(changed).ApiRateCardSha256);
    }
}
