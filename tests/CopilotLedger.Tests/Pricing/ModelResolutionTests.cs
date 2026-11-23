using System.Text.Json.Nodes;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Pricing;

// Issue #67: which model prices a request that reports a response model and a request model. See "Which model prices a
// request" in docs/configuration.md.
public sealed class ModelResolutionTests
{
    private static readonly JsonRateCardCalculator Embedded = JsonRateCardCalculator.LoadEmbedded();

    private static readonly JsonRateCardCalculator Retail =
        JsonRateCardCalculator.Parse(RateFixtures.RetailTestRatesJson);

    // response model, request model, the model that prices the request, its USD cost, and its AI credits. Each request
    // is on the Copilot host, with 120,000 input tokens (100,000 of them cache reads) and 2,000 output tokens. Claude
    // Haiku 4.5 costs $1.00 input, $0.10 cached input, and $5.00 output per 1M tokens: $0.02 + $0.01 + $0.01 = $0.04.
    // Claude Sonnet 5.5 costs $2.00, $0.10, and $10.00: $0.04 + $0.01 + $0.02 = $0.07. Their API list prices are the
    // same.
    public static TheoryData<string?, string?, string, decimal, decimal> Resolved => new()
    {
        { "claude-haiku-4-5-20251001", "claude-haiku-4.5", "claude-haiku-4.5", 0.04m, 4m },
        { "claude-haiku-4-5-20251001", null, "claude-haiku-4.5", 0.04m, 4m },
        { "claude-haiku-4-5-20991231", "claude-haiku-4.5", "claude-haiku-4.5", 0.04m, 4m },
        { null, "claude-haiku-4.5", "claude-haiku-4.5", 0.04m, 4m },
        { "Claude Haiku 4.5", "auto", "claude-haiku-4.5", 0.04m, 4m },
        { "claude-sonnet-5.5", "claude-haiku-4.5", "claude-sonnet-5.5", 0.07m, 7m },
    };

    [Theory]
    [MemberData(nameof(Resolved))]
    public void MR1_GitHubRequests_ArePricedAsTheFirstModelTheCardHas(
        string? responseModel,
        string? requestModel,
        string model,
        decimal usd,
        decimal aiCredits)
    {
        CreditCalculationResult result = Embedded.Calculate(TestSpans.Chat(
            serverAddress: "api.githubcopilot.com",
            requestModel: requestModel,
            responseModel: responseModel,
            inputTokens: 120_000,
            cacheReadInputTokens: 100_000,
            outputTokens: 2_000));

        Assert.Equal(BillingChannel.GitHubAiCredits, result.Channel);
        Assert.Equal(PricingStatus.Priced, result.Status);
        Assert.Equal(model, result.ModelId);
        Assert.Equal(model, result.RateCardModelId);
        Assert.Equal("default", result.TierName);
        Assert.Equal(usd, result.UsdCost);
        Assert.Equal(aiCredits, result.AiCredits);
        Assert.Equal(ApiEquivalentStatus.Priced, result.ApiEquivalent.Status);
        Assert.Equal(model, result.ApiEquivalent.RateCardModelId);
        Assert.Equal(usd, result.ApiEquivalent.UsdCost);
    }

    // When the card has neither model, the request is unknown_model, recorded as its response model, or else its
    // request model, and nothing is billed.
    [Theory]
    [InlineData("claude-haiku-4-5-20991231", "claude-unknown", "claude-haiku-4-5-20991231")]
    [InlineData("claude-haiku-4-5-20991231", null, "claude-haiku-4-5-20991231")]
    [InlineData(null, "claude-unknown", "claude-unknown")]
    [InlineData(null, null, null)]
    public void MR2_NeitherModelKnown_IsUnknownModel(string? responseModel, string? requestModel, string? model)
    {
        CreditCalculationResult result = Embedded.Calculate(TestSpans.Chat(
            serverAddress: "api.githubcopilot.com",
            requestModel: requestModel,
            responseModel: responseModel,
            inputTokens: 120_000,
            cacheReadInputTokens: 100_000,
            outputTokens: 2_000));

        Assert.Equal(BillingChannel.GitHubAiCredits, result.Channel);
        Assert.Equal(PricingStatus.UnknownModel, result.Status);
        Assert.Equal(model, result.ModelId);
        Assert.Null(result.RateCardModelId);
        Assert.Null(result.Breakdown);
        Assert.Equal(0m, result.UsdCost);
        Assert.Equal(0m, result.AiCredits);
        Assert.Equal(ApiEquivalentStatus.Unpriced, result.ApiEquivalent.Status);
        Assert.Null(result.ApiEquivalent.UsdCost);
    }

    // Foundry BYOK tries the request model too: Foundry requests often report a deployment name as the request model
    // and a dated id as the response model. 100,000 input and 10,000 output tokens cost $0.175 + $0.14 at the fixture's
    // BYOK prices, and $0.20 + $0.10 at its API list prices.
    [Theory]
    [InlineData("gpt-6.1-sol-2026-10-01", "contoso-gpt61-prod", "gpt-6.1-sol")]
    [InlineData("contoso-gpt61-prod", "gpt-6.1-sol-2026-10-01", "gpt-6.1-sol")]
    [InlineData("gpt-6.1-sol-2026-10-01", "contoso-unknown", null)]
    public void MR3_FoundryByokRequests_TryTheRequestModelToo(string responseModel, string requestModel, string? model)
    {
        CreditCalculationResult result = Retail.Calculate(TestSpans.Chat(
            serverAddress: "contoso.openai.azure.com",
            requestModel: requestModel,
            responseModel: responseModel,
            inputTokens: 100_000,
            outputTokens: 10_000));

        Assert.Equal(BillingChannel.FoundryByok, result.Channel);
        Assert.Equal(0m, result.AiCredits);
        if (model is null)
        {
            Assert.Equal(PricingStatus.ByokUnpriced, result.Status);
            Assert.Equal(responseModel, result.ModelId);
            Assert.Equal(0m, result.UsdCost);
            Assert.Equal(ApiEquivalentStatus.Unpriced, result.ApiEquivalent.Status);
            return;
        }

        Assert.Equal(PricingStatus.Priced, result.Status);
        Assert.Equal(model, result.ModelId);
        Assert.Equal(0.315m, result.UsdCost);
        Assert.Equal(model, result.ApiEquivalent.RateCardModelId);
        Assert.Equal(0.3m, result.ApiEquivalent.UsdCost);
    }

    // A local request matches local models by its response model, or by its request model when there's no response
    // model. Its savings don't depend on the model: 400,000 input and 12,000 output tokens at MAI Code 2's GitHub rates.
    [Theory]
    [InlineData(null, "qwen3.8-27b")]
    [InlineData("qwen-unknown", "qwen-unknown")]
    public void MR4_LocalRequests_DontTryTheRequestModel(string? responseModel, string model)
    {
        CreditCalculationResult result = Embedded.Calculate(TestSpans.Chat(
            providerName: "ollama",
            requestModel: "qwen3.8:27b",
            responseModel: responseModel,
            inputTokens: 400_000,
            outputTokens: 12_000));

        Assert.Equal(BillingChannel.Local, result.Channel);
        Assert.Equal(PricingStatus.LocalOffload, result.Status);
        Assert.Equal(model, result.ModelId);
        Assert.Equal("mai-code-2", result.RateCardModelId);
        Assert.Equal(0.23m, result.EstimatedCloudSavedUsd);
    }

    // The rates.json that releases publish for gh-ledger 0.4.1 and earlier keeps the GitHub aliases, so after a rates
    // update those versions price a dated response model too, although they never try the request model.
    [Fact]
    public void MR5_SchemaVersion1Rates_KeepTheVendorIds()
    {
        CreditCalculationResult result = JsonRateCardCalculator.Parse(PublishedRates.SchemaVersion1("2026-11-23"))
            .Calculate(TestSpans.Chat(
                serverAddress: "api.githubcopilot.com",
                responseModel: "claude-haiku-4-5-20251001",
                inputTokens: 120_000,
                cacheReadInputTokens: 100_000,
                outputTokens: 2_000));

        Assert.Equal(PricingStatus.Priced, result.Status);
        Assert.Equal("claude-haiku-4.5", result.ModelId);
        Assert.Equal(4m, result.AiCredits);
    }

    // The GitHub models have the aliases of their API list prices: the vendors' API ids and dated snapshot ids.
    [Fact]
    public void MR6_GitHubModelsHaveTheVendorIdsOfTheirListPrices()
    {
        JsonNode card = JsonNode.Parse(PublishedRates.Embedded())!;
        Dictionary<string, string[]> github = Aliases(card["githubAiCredits"]!);
        Dictionary<string, string[]> retail = Aliases(card["apiEquivalent"]!);

        Assert.All(retail, model => Assert.Equal(model.Value, github[model.Key]));
        Assert.All(github.Keys.Except(retail.Keys), id => Assert.Empty(github[id]));
        Assert.Equal(11, github.Values.Sum(aliases => aliases.Length));
    }

    // An overlay that added a vendor id as a GitHub model, as a workaround for #67, now repeats a built-in alias.
    [Fact]
    public void MR7_AnOverlayModelNamedAsABuiltInAlias_IsInvalid()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => JsonRateCardCalculator.Parse(
            PublishedRates.Embedded(),
            """
            { "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "claude-haiku-4-5-20251001", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1, "outputPer1M": 5 } ] } ] } ] } }
            """));

        Assert.Equal(
            "overlay: githubAiCredits.models[0].id: duplicate model identity in the merged rates: claude-haiku-4-5-20251001 (built-in model claude-haiku-4.5)",
            error.Message);
    }

    private static Dictionary<string, string[]> Aliases(JsonNode section) =>
        section["models"]!.AsArray().ToDictionary(
            model => model!["id"]!.GetValue<string>(),
            model => model!["aliases"]!.AsArray().Select(alias => alias!.GetValue<string>()).ToArray(),
            StringComparer.Ordinal);
}
