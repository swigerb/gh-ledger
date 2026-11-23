using System.Globalization;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Pricing;

public sealed class BillingSourceTests
{
    private static readonly JsonRateCardCalculator Retail =
        JsonRateCardCalculator.Parse(RateFixtures.RetailTestRatesJson);

    public static TheoryData<string, PricingStatus, decimal> FoundryModels => new()
    {
        { "gpt-unknown", PricingStatus.ByokUnpriced, 0m },
        { "contoso-gpt61-prod", PricingStatus.Priced, 0.315m },
    };

    [Theory]
    [InlineData("ollama")]
    [InlineData("Ollama")]
    public void BI1_AListedLocalProviderIsLocal(string provider)
    {
        CreditCalculationResult result = Retail.Calculate(Span(provider, "qwen-local", 400_000, 12_000));

        Assert.Equal(BillingChannel.Local, result.Channel);
        Assert.Equal(PricingStatus.LocalOffload, result.Status);
        Assert.Equal(0m, result.UsdCost);
        Assert.Equal(0.23m, result.EstimatedCloudSavedUsd);
        Assert.Equal(0.3584m, result.ApiEquivalent.UsdCost);
    }

    [Theory]
    [MemberData(nameof(FoundryModels))]
    public void BI2_AListedFoundryProviderIsFoundryByok(string model, PricingStatus status, decimal usd)
    {
        CreditCalculationResult result = Retail.Calculate(Span("azure.ai.inference", model, 100_000, 10_000));

        Assert.Equal(BillingChannel.FoundryByok, result.Channel);
        Assert.Equal(status, result.Status);
        Assert.Equal(usd, result.UsdCost);
        Assert.Equal(0m, result.AiCredits);
    }

    [Theory]
    [InlineData("openai")]
    [InlineData("anthropic")]
    [InlineData("github")]
    [InlineData(null)]
    public void BI3_AnyOtherProviderIsUnpricedButKeepsItsApiEquivalentPrice(string? provider)
    {
        CreditCalculationResult result = Retail.Calculate(Span(provider, "test-retail", 100_000, 10_000));

        Assert.Equal(BillingChannel.Unpriced, result.Channel);
        Assert.Equal(PricingStatus.OtherProvider, result.Status);
        Assert.Equal("test-retail", result.ModelId);
        Assert.Null(result.RateCardModelId);
        Assert.Null(result.Breakdown);
        Assert.Equal(0m, result.UsdCost);
        Assert.Equal(0m, result.AiCredits);
        Assert.Equal(0m, result.EstimatedCloudSavedUsd);
        Assert.Equal(100_000, result.UncachedInputTokens);
        Assert.Equal(ApiEquivalentStatus.Priced, result.ApiEquivalent.Status);
        Assert.Equal(0.45m, result.ApiEquivalent.UsdCost);
    }

    [Fact]
    public void BI4_AnOtherProviderRequestKeepsItsNormalizedModel()
    {
        CreditCalculationResult result = Retail.Calculate(Span("openai", " GPT Unknown ", 100_000, 10_000));

        Assert.Equal(PricingStatus.OtherProvider, result.Status);
        Assert.Equal("gpt-unknown", result.ModelId);
        Assert.Equal(ApiEquivalentStatus.Unpriced, result.ApiEquivalent.Status);
        Assert.Null(result.ApiEquivalent.UsdCost);
    }

    [Fact]
    public void BI5_OnlyTheEndpointSourceMakesAHostlessRequestGitHubs()
    {
        NormalizedGenAiSpan endpoint = Span("github", "test-retail", 100_000, 10_000) with
        {
            BillingSource = BillingSource.Endpoint,
        };

        CreditCalculationResult github = Retail.Calculate(endpoint);
        CreditCalculationResult provider = Retail.Calculate(endpoint with { BillingSource = BillingSource.Provider });

        Assert.Equal(BillingChannel.GitHubAiCredits, github.Channel);
        Assert.Equal(PricingStatus.Priced, github.Status);
        Assert.Equal(0.15m, github.UsdCost);
        Assert.Equal(15m, github.AiCredits);
        Assert.Equal(BillingChannel.Unpriced, provider.Channel);
        Assert.Equal(PricingStatus.OtherProvider, provider.Status);
        Assert.Equal(0m, provider.UsdCost);
        Assert.Equal(github.ApiEquivalent.UsdCost, provider.ApiEquivalent.UsdCost);
    }

    [Theory]
    [InlineData("invoke_agent", 100_000, 0, PricingStatus.NotBillableOperation)]
    [InlineData("chat", 100, 200, PricingStatus.InvalidUsage)]
    public void BI6_OperationAndUsageChecksStillComeFirst(
        string operation,
        long input,
        long cacheRead,
        PricingStatus status)
    {
        CreditCalculationResult result = Retail.Calculate(TestSpans.Chat(
            operationName: operation,
            providerName: "openai",
            requestModel: "test-retail",
            inputTokens: input,
            cacheReadInputTokens: cacheRead) with
        {
            BillingSource = BillingSource.Provider,
        });

        Assert.Equal(BillingChannel.Unpriced, result.Channel);
        Assert.Equal(status, result.Status);
        Assert.Equal(ApiEquivalentStatus.NotApplicable, result.ApiEquivalent.Status);
    }

    // A response model that the card doesn't have, such as a dated id, never hides a request model that it has: the billing
    // source changes only which channel prices the request.
    [Fact]
    public void BI7_ProviderBilledRequestsFallBackToTheirRequestModelAsLiveOnesDo()
    {
        CreditCalculationResult foundry = Retail.Calculate(
            Span("azure.ai.inference", "contoso-gpt61-prod", 100_000, 10_000) with { ResponseModel = "gpt-6.1-sol-2026-10-01" });
        CreditCalculationResult other = Retail.Calculate(
            Span("openai", "test-retail", 100_000, 10_000) with { ResponseModel = "test-retail-2026-10-01" });

        Assert.Equal(
            (BillingChannel.FoundryByok, PricingStatus.Priced, "gpt-6.1-sol", 0.315m),
            (foundry.Channel, foundry.Status, foundry.ModelId, foundry.UsdCost));
        Assert.Equal(
            (BillingChannel.Unpriced, PricingStatus.OtherProvider, "test-retail-2026-10-01", 0m),
            (other.Channel, other.Status, other.ModelId, other.UsdCost));
        Assert.Equal(ApiEquivalentStatus.Priced, other.ApiEquivalent.Status);
        Assert.Equal(0.45m, other.ApiEquivalent.UsdCost);
    }

    private static NormalizedGenAiSpan Span(string? provider, string model, long input, long output) =>
        TestSpans.Chat(
            startTime: DateTimeOffset.Parse("2026-11-20T12:00:00Z", CultureInfo.InvariantCulture),
            providerName: provider,
            requestModel: model,
            inputTokens: input,
            outputTokens: output) with
        {
            BillingSource = BillingSource.Provider,
        };
}
