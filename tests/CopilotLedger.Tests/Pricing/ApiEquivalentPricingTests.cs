using System.Globalization;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Pricing;

public sealed class ApiEquivalentPricingTests
{
    private static readonly JsonRateCardCalculator Retail =
        JsonRateCardCalculator.Parse(RateFixtures.RetailTestRatesJson);

    // start, input, cache read, cache write, and output tokens of test-retail, then the tier and the expected uncached
    // input, cache read, cache write, and output USD at RetailTestRatesJson's API-equivalent prices.
    public static TheoryData<string, long, long, long, long, string, decimal, decimal, decimal, decimal> Vectors => new()
    {
        { "2026-11-20T12:00:00Z", 100_000, 0, 0, 0, "default", 0.3m, 0m, 0m, 0m },
        { "2026-11-20T12:00:00Z", 100_000, 60_000, 0, 0, "default", 0.12m, 0.018m, 0m, 0m },
        { "2026-11-20T12:00:00Z", 100_000, 0, 20_000, 0, "default", 0.24m, 0m, 0.075m, 0m },
        { "2026-11-20T12:00:00Z", 0, 0, 0, 10_000, "default", 0m, 0m, 0m, 0.15m },
        { "2026-11-20T12:00:00Z", 200_000, 120_000, 30_000, 5_000, "default", 0.15m, 0.036m, 0.1125m, 0.075m },
        { "2026-11-20T12:00:00Z", 200_001, 120_000, 30_000, 5_000, "long_context", 0.300006m, 0.072m, 0.225m, 0.1125m },
        { "2026-11-30T23:59:59.9999999Z", 100_000, 60_000, 20_000, 10_000, "default", 0.06m, 0.018m, 0.075m, 0.15m },
        { "2026-12-01T00:00:00Z", 100_000, 60_000, 20_000, 10_000, "default", 0.08m, 0.024m, 0.1m, 0.2m },
        { "2026-12-15T00:00:00Z", 300_000, 0, 0, 1_000, "long_context", 2.4m, 0m, 0m, 0.03m },
        { "2026-11-20T12:00:00Z", 1, 1, 0, 0, "default", 0m, 0.0000003m, 0m, 0m },
    };

    public static TheoryData<string, string, decimal> Models => new()
    {
        { "Test Retail", "test-retail", 0.45m },
        { "test-retail-2026-11-01", "test-retail", 0.45m },
        { "test-no-cache", "test-no-cache", 0.28m },
    };

    [Theory]
    [MemberData(nameof(Vectors))]
    public void AP1_PricesEachKindOfTokenExactly(
        string start,
        long input,
        long cacheRead,
        long cacheWrite,
        long output,
        string tier,
        decimal uncachedUsd,
        decimal cacheReadUsd,
        decimal cacheWriteUsd,
        decimal outputUsd)
    {
        ApiEquivalentCost cost = Retail.Calculate(TestSpans.Chat(
            startTime: DateTimeOffset.Parse(start, CultureInfo.InvariantCulture),
            requestModel: "test-retail",
            inputTokens: input,
            outputTokens: output,
            cacheReadInputTokens: cacheRead,
            cacheWriteInputTokens: cacheWrite)).ApiEquivalent;

        Assert.Equal(ApiEquivalentStatus.Priced, cost.Status);
        Assert.Equal("test-retail", cost.RateCardModelId);
        Assert.Equal(tier, cost.TierName);
        Assert.NotNull(cost.Breakdown);
        Assert.Equal(uncachedUsd, cost.Breakdown.UncachedInputUsd);
        Assert.Equal(cacheReadUsd, cost.Breakdown.CacheReadUsd);
        Assert.Equal(cacheWriteUsd, cost.Breakdown.CacheWriteUsd);
        Assert.Equal(outputUsd, cost.Breakdown.OutputUsd);
        Assert.Equal(uncachedUsd + cacheReadUsd + cacheWriteUsd + outputUsd, cost.UsdCost);
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void AP10_ModelNamesAndMissingCacheRates(string model, string rateCardModelId, decimal usd)
    {
        ApiEquivalentCost cost = Retail.Calculate(Span(model)).ApiEquivalent;

        Assert.Equal(ApiEquivalentStatus.Priced, cost.Status);
        Assert.Equal(rateCardModelId, cost.RateCardModelId);
        Assert.Equal(usd, cost.UsdCost);
    }

    [Theory]
    [InlineData("gpt-unknown", null, "2026-11-20T12:00:00Z")]
    [InlineData(null, null, "2026-11-20T12:00:00Z")]
    [InlineData("gpt-6.1-sol", null, "2026-05-31T23:59:59Z")]
    [InlineData("gpt-unknown", "contoso.openai.azure.com", "2026-11-20T12:00:00Z")]
    public void AP2_AnUnknownModelOrDateIsUnpricedNeverZero(string? model, string? host, string start)
    {
        CreditCalculationResult result = Retail.Calculate(TestSpans.Chat(
            startTime: DateTimeOffset.Parse(start, CultureInfo.InvariantCulture),
            serverAddress: host,
            requestModel: model,
            inputTokens: 100_000,
            outputTokens: 10_000));

        Assert.Equal(ApiEquivalentStatus.Unpriced, result.ApiEquivalent.Status);
        Assert.Null(result.ApiEquivalent.UsdCost);
        Assert.Null(result.ApiEquivalent.Breakdown);
        Assert.Null(result.ApiEquivalent.RateCardModelId);
        Assert.Null(result.ApiEquivalent.TierName);
    }

    [Theory]
    [InlineData("invoke_agent", 100_000, 0, 0, 10_000)]
    [InlineData("execute_tool", 100_000, 0, 0, 10_000)]
    [InlineData("chat", 100, 80, 30, 0)]
    [InlineData("chat", 100, 0, 0, -1)]
    public void AP3_NonInferenceAndInvalidUsageAreNotApplicable(
        string operation,
        long input,
        long cacheRead,
        long cacheWrite,
        long output)
    {
        CreditCalculationResult result = Retail.Calculate(TestSpans.Chat(
            operationName: operation,
            requestModel: "test-retail",
            inputTokens: input,
            outputTokens: output,
            cacheReadInputTokens: cacheRead,
            cacheWriteInputTokens: cacheWrite));

        Assert.Equal(
            operation == "chat" ? PricingStatus.InvalidUsage : PricingStatus.NotBillableOperation,
            result.Status);
        Assert.Equal(ApiEquivalentStatus.NotApplicable, result.ApiEquivalent.Status);
        Assert.Null(result.ApiEquivalent.UsdCost);
        Assert.Null(result.ApiEquivalent.Breakdown);
    }

    [Fact]
    public async Task AP4_LocalRequestsUseTheRetailBaselineAndKeepTheirSavings()
    {
        using TempDirectory temp = new();
        string overlay = Path.Combine(temp.Path, "rates.json");
        await File.WriteAllTextAsync(
            overlay,
            """{ "schemaVersion": 2, "apiEquivalent": { "localBaselineModel": "test-retail" } }""");
        await FileRetry.ReadAllTextAsync(overlay);
        NormalizedGenAiSpan span = TestSpans.Chat(
            serverAddress: "127.0.0.1",
            requestModel: "qwen-local",
            inputTokens: 400_000,
            outputTokens: 12_000);

        CreditCalculationResult baseline = Retail.Calculate(span);
        CreditCalculationResult chosen =
            JsonRateCardCalculator.LoadWithOverlay(RateFixtures.RetailTestRatesJson, overlay).Calculate(span);
        CreditCalculationResult withoutRetail =
            JsonRateCardCalculator.Parse(RateFixtures.ByokTestRatesJson).Calculate(span);
        CreditCalculationResult embedded = JsonRateCardCalculator.LoadEmbedded().Calculate(span);

        Assert.Equal(PricingStatus.LocalOffload, baseline.Status);
        Assert.Equal(0.23m, baseline.EstimatedCloudSavedUsd);
        Assert.Equal(ApiEquivalentStatus.Priced, baseline.ApiEquivalent.Status);
        Assert.Equal("test-baseline", baseline.ApiEquivalent.RateCardModelId);
        Assert.Equal(0.3584m, baseline.ApiEquivalent.UsdCost);
        Assert.Equal(0.23m, chosen.EstimatedCloudSavedUsd);
        Assert.Equal("test-retail", chosen.ApiEquivalent.RateCardModelId);
        Assert.Equal("long_context", chosen.ApiEquivalent.TierName);
        Assert.Equal(2.67m, chosen.ApiEquivalent.UsdCost);
        Assert.Equal(0.92m, withoutRetail.EstimatedCloudSavedUsd);
        Assert.Equal(ApiEquivalentStatus.Unpriced, withoutRetail.ApiEquivalent.Status);
        Assert.Null(withoutRetail.ApiEquivalent.UsdCost);
        Assert.Equal(PricingStatus.LocalOffload, embedded.Status);
        Assert.Equal(0.23m, embedded.EstimatedCloudSavedUsd);
        Assert.Equal("mai-code-2", embedded.RateCardModelId);
    }

    [Fact]
    public void AP5_ByokRequestsUseTheirCanonicalModel()
    {
        CreditCalculationResult result = Retail.Calculate(TestSpans.Chat(
            serverAddress: "contoso.openai.azure.com",
            requestModel: "contoso-gpt61-prod",
            inputTokens: 100_000,
            outputTokens: 10_000));

        Assert.Equal(BillingChannel.FoundryByok, result.Channel);
        Assert.Equal(PricingStatus.Priced, result.Status);
        Assert.Equal("gpt-6.1-sol", result.ModelId);
        Assert.Equal(0.315m, result.UsdCost);
        Assert.Equal(ApiEquivalentStatus.Priced, result.ApiEquivalent.Status);
        Assert.Equal("gpt-6.1-sol", result.ApiEquivalent.RateCardModelId);
        Assert.Equal(0.3m, result.ApiEquivalent.UsdCost);
    }

    [Fact]
    public void AP6_AnUnrecognizedEndpointStillGetsARetailEstimate()
    {
        CreditCalculationResult result = Retail.Calculate(TestSpans.Chat(
            serverAddress: "llm.example.com",
            requestModel: "test-retail",
            inputTokens: 100_000,
            outputTokens: 10_000));

        Assert.Equal(BillingChannel.Unpriced, result.Channel);
        Assert.Equal(PricingStatus.UnrecognizedEndpoint, result.Status);
        Assert.Equal(0m, result.UsdCost);
        Assert.Equal(ApiEquivalentStatus.Priced, result.ApiEquivalent.Status);
        Assert.Equal(0.45m, result.ApiEquivalent.UsdCost);
    }

    [Fact]
    public void AP7_EveryResultRecordsTheCardThatJudgedIt()
    {
        CreditCalculationResult[] results =
        [
            Retail.Calculate(TestSpans.Chat(requestModel: "test-retail", inputTokens: 1_000)),
            Retail.Calculate(TestSpans.Chat(requestModel: "gpt-unknown", inputTokens: 1_000)),
            Retail.Calculate(TestSpans.Chat(operationName: "invoke_agent", requestModel: "test-retail")),
        ];

        Assert.Equal(
            [ApiEquivalentStatus.Priced, ApiEquivalentStatus.Unpriced, ApiEquivalentStatus.NotApplicable],
            results.Select(result => result.ApiEquivalent.Status));
        Assert.All(results, result =>
        {
            Assert.Equal("retail-fixture", result.ApiEquivalent.RatesVersion);
            Assert.Equal(Retail.ApiRateCardSha256, result.ApiEquivalent.RateCardSha256);
        });
    }

    public static TheoryData<BillingChannel, string?, ApiEquivalentStatus, string?, decimal?> Requests => new()
    {
        { BillingChannel.GitHubAiCredits, "test-retail", ApiEquivalentStatus.Priced, "test-retail", 0.45m },
        { BillingChannel.Unpriced, "Test Retail", ApiEquivalentStatus.Priced, "test-retail", 0.45m },
        { BillingChannel.FoundryByok, "test-baseline", ApiEquivalentStatus.Priced, "test-baseline", 0.112m },
        { BillingChannel.Local, "qwen-local", ApiEquivalentStatus.Priced, "test-baseline", 0.112m },
        { BillingChannel.Local, null, ApiEquivalentStatus.Priced, "test-baseline", 0.112m },
        { BillingChannel.GitHubAiCredits, null, ApiEquivalentStatus.Unpriced, null, null },
        { BillingChannel.GitHubAiCredits, "mai-code-2", ApiEquivalentStatus.Unpriced, null, null },
    };

    [Theory]
    [MemberData(nameof(Requests))]
    public void AP8_RecordedRequestsArePricedByChannelAndModel(
        BillingChannel channel,
        string? model,
        ApiEquivalentStatus status,
        string? rateCardModelId,
        decimal? usd)
    {
        ApiEquivalentCost cost = Retail.CalculateApiEquivalent(new ApiEquivalentRequest
        {
            OperationName = "chat",
            Channel = channel,
            ModelId = model,
            StartTime = DateTimeOffset.Parse("2026-11-20T12:00:00Z", CultureInfo.InvariantCulture),
            InputTokens = 100_000,
            OutputTokens = 10_000,
            CacheReadInputTokens = 0,
            CacheWriteInputTokens = 0,
        });

        Assert.Equal(status, cost.Status);
        Assert.Equal(rateCardModelId, cost.RateCardModelId);
        Assert.Equal(usd, cost.UsdCost);
    }

    [Fact]
    public async Task AP9_AnOverlayAddsAndReplacesApiEquivalentModelsById()
    {
        using TempDirectory temp = new();
        string overlay = Path.Combine(temp.Path, "rates.json");
        await File.WriteAllTextAsync(
            overlay,
            """
            {
              "schemaVersion": 2,
              "ratesVersion": "contoso-2026-12",
              "apiEquivalent": {
                "models": [
                  { "id": "test-retail", "windows": [ { "tiers": [ { "name": "negotiated", "inputPer1M": 1.50, "cachedInputPer1M": 0.15, "outputPer1M": 7.50 } ] } ] },
                  { "id": "test-added", "vendor": "contoso", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 0.25, "outputPer1M": 1.25 } ] } ] }
                ]
              }
            }
            """);
        await FileRetry.ReadAllTextAsync(overlay);
        JsonRateCardCalculator merged = JsonRateCardCalculator.LoadWithOverlay(RateFixtures.RetailTestRatesJson, overlay);

        CreditCalculationResult replaced = merged.Calculate(Span("test-retail"));
        CreditCalculationResult added = merged.Calculate(Span("test-added"));
        CreditCalculationResult kept = merged.Calculate(Span("test-no-cache"));

        Assert.Equal("retail-fixture+contoso-2026-12", merged.RatesVersion);
        Assert.Equal(PricingStatus.Priced, replaced.Status);
        Assert.Equal(0.15m, replaced.UsdCost);
        Assert.Equal("negotiated", replaced.ApiEquivalent.TierName);
        Assert.Equal(0.225m, replaced.ApiEquivalent.UsdCost);
        Assert.Equal(PricingStatus.UnknownModel, added.Status);
        Assert.Equal(0.0375m, added.ApiEquivalent.UsdCost);
        Assert.Equal(0.28m, kept.ApiEquivalent.UsdCost);
        Assert.Equal("retail-fixture+contoso-2026-12", replaced.ApiEquivalent.RatesVersion);
        Assert.Equal(merged.ApiRateCardSha256, replaced.ApiEquivalent.RateCardSha256);
    }

    private static NormalizedGenAiSpan Span(string model) => TestSpans.Chat(
        requestModel: model,
        inputTokens: 100_000,
        outputTokens: 10_000);
}
