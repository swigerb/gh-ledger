using CopilotLedger.Core.Pricing;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Pricing;

// The API-equivalent lens prices a request as its recorded model, or as its request model when no section of the card has
// the recorded model, as #61 asks for Foundry BYOK requests. See "API-equivalent prices" in docs/configuration.md.
public sealed class RequestModelFallbackTests
{
    private static readonly JsonRateCardCalculator Embedded = JsonRateCardCalculator.LoadEmbedded();

    private static readonly JsonRateCardCalculator Retail =
        JsonRateCardCalculator.Parse(RateFixtures.RetailTestRatesJson);

    // host, response model, request model, then the billing result and the API-equivalent model and cost. The fixture's
    // GitHub models are test-retail and test-baseline, and its only Foundry BYOK model is gpt-6.1-sol, so its billing
    // channels have neither model. 1,000 input and 100 output tokens cost $0.003 + $0.0015 at test-retail's list prices and
    // $0.002 + $0.0008 at test-no-cache's.
    public static TheoryData<string, string, string, BillingChannel, PricingStatus, string, decimal> FallsBack => new()
    {
        { "llm.example.com", "test-retail-preview", "test-retail", BillingChannel.Unpriced, PricingStatus.UnrecognizedEndpoint, "test-retail", 0.0045m },
        { "api.githubcopilot.com", "test-no-cache-preview", "test-no-cache", BillingChannel.GitHubAiCredits, PricingStatus.UnknownModel, "test-no-cache", 0.0028m },
        { "contoso.openai.azure.com", "test-no-cache-preview", "test-no-cache", BillingChannel.FoundryByok, PricingStatus.ByokUnpriced, "test-no-cache", 0.0028m },
    };

    [Theory]
    [MemberData(nameof(FallsBack))]
    public void RF1_AnUnknownResponseModel_IsPricedAsTheRequestModel(
        string host,
        string responseModel,
        string requestModel,
        BillingChannel channel,
        PricingStatus status,
        string retailModel,
        decimal usd)
    {
        CreditCalculationResult result = Retail.Calculate(TestSpans.Chat(
            serverAddress: host,
            requestModel: requestModel,
            responseModel: responseModel,
            inputTokens: 1_000,
            outputTokens: 100));

        Assert.Equal(channel, result.Channel);
        Assert.Equal(status, result.Status);
        Assert.Equal(responseModel, result.ModelId);
        Assert.Equal(0m, result.UsdCost);
        Assert.Equal(0m, result.AiCredits);
        Assert.Equal(ApiEquivalentStatus.Priced, result.ApiEquivalent.Status);
        Assert.Equal(retailModel, result.ApiEquivalent.RateCardModelId);
        Assert.Equal(usd, result.ApiEquivalent.UsdCost);
    }

    // A response model that the card knows without a list price is never priced as the request model: MAI Code 2 is a
    // GitHub model, and qwen3.8:27b a local one.
    [Theory]
    [InlineData("llm.example.com", "mai-code-2", BillingChannel.Unpriced, PricingStatus.UnrecognizedEndpoint)]
    [InlineData("api.githubcopilot.com", "mai-code-2", BillingChannel.GitHubAiCredits, PricingStatus.Priced)]
    [InlineData("llm.example.com", "qwen3.8:27b", BillingChannel.Unpriced, PricingStatus.UnrecognizedEndpoint)]
    public void RF2_AKnownResponseModel_IsNeverReplaced(
        string host,
        string responseModel,
        BillingChannel channel,
        PricingStatus status)
    {
        CreditCalculationResult result = Embedded.Calculate(TestSpans.Chat(
            serverAddress: host,
            requestModel: "claude-haiku-4.5",
            responseModel: responseModel,
            inputTokens: 1_000,
            outputTokens: 100));

        Assert.Equal(channel, result.Channel);
        Assert.Equal(status, result.Status);
        Assert.Equal(responseModel, result.ModelId);
        Assert.Equal(ApiEquivalentStatus.Unpriced, result.ApiEquivalent.Status);
        Assert.Null(result.ApiEquivalent.RateCardModelId);
        Assert.Null(result.ApiEquivalent.UsdCost);
    }

    // The recorded facts that backfill and what-if pricing pass: channel, ModelId, RequestModel, then the API-equivalent
    // model and cost. test-baseline's list prices are $0.80 input and $3.20 output per 1M tokens: $0.0008 + $0.00032.
    public static TheoryData<BillingChannel, string?, string?, string?, decimal?> Recorded => new()
    {
        { BillingChannel.Unpriced, "test-retail-preview", "test-retail", "test-retail", 0.0045m },
        { BillingChannel.Unpriced, "test-retail-preview", null, null, null },
        { BillingChannel.Unpriced, null, "test-retail", "test-retail", 0.0045m },
        { BillingChannel.GitHubAiCredits, "test-baseline", "test-retail", "test-baseline", 0.00112m },
        { BillingChannel.Local, "test-retail-preview", "test-retail", "test-baseline", 0.00112m },
    };

    [Theory]
    [MemberData(nameof(Recorded))]
    public void RF3_RecordedRequests_FallBackTheSameWay(
        BillingChannel channel,
        string? modelId,
        string? requestModel,
        string? retailModel,
        decimal? usd)
    {
        ApiEquivalentCost cost = Retail.CalculateApiEquivalent(new ApiEquivalentRequest
        {
            OperationName = "chat",
            Channel = channel,
            ModelId = modelId,
            RequestModel = requestModel,
            StartTime = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero),
            InputTokens = 1_000,
            OutputTokens = 100,
            CacheReadInputTokens = 0,
            CacheWriteInputTokens = 0,
        });

        Assert.Equal(usd is null ? ApiEquivalentStatus.Unpriced : ApiEquivalentStatus.Priced, cost.Status);
        Assert.Equal(retailModel, cost.RateCardModelId);
        Assert.Equal(usd, cost.UsdCost);
    }
}
