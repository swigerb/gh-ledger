using CopilotLedger.Core.Pricing;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Pricing;

public sealed class JsonRateCardCalculatorTests
{
    public static TheoryData<string, DateTimeOffset, long, long, long, long, string, decimal, decimal> Vectors => new()
    {
        { "claude-opus-5.5", Utc(2026, 11, 20, 12), 1_000_000, 0, 0, 1_000_000, "default", 24.00m, 2400m },
        { "claude-opus-5.5", Utc(2026, 11, 20, 12), 1_200_000, 800_000, 100_000, 50_000, "default", 2.86m, 286m },
        { "claude-sonnet-5.5", Utc(2026, 11, 20, 12), 10_000, 0, 0, 2_000, "default", 0.04m, 4m },
        { "gpt-6.1-sol", Utc(2026, 11, 20, 12), 272_000, 0, 0, 10_000, "default", 0.644m, 64.4m },
        { "gpt-6.1-sol", Utc(2026, 11, 20, 12), 272_001, 0, 0, 10_000, "long_context", 1.238004m, 123.8004m },
        { "gpt-6.1-sol", Utc(2026, 11, 20, 12), 400_000, 300_000, 50_000, 20_000, "long_context", 0.81m, 81m },
        { "gpt-6-astra", Utc(2026, 11, 20, 12), 100_000, 40_000, 10_000, 5_000, "default", 0.915m, 91.5m },
        { "mai-code-2", Utc(2026, 11, 20, 12), 1_000_000, 400_000, 0, 100_000, "default", 0.56m, 56m },
        { "mai-code-2", Utc(2027, 5, 16, 23, 59, 59), 1_000_000, 400_000, 0, 100_000, "default", 0.56m, 56m },
        { "mai-code-2", Utc(2027, 5, 17, 0), 1_000_000, 400_000, 0, 100_000, "default", 1.12m, 112m },
        { "mai-code-2", Utc(2026, 11, 20, 12), 1_000, 0, 1_000, 0, "default", 0.0005m, 0.05m },
        { "mai-code-2", Utc(2026, 11, 20, 12), 1, 1, 0, 0, "default", 0.000000025m, 0.0000025m },
    };

    [Fact]
    public void LoadEmbedded_Succeeds()
    {
        Assert.Equal("2026-11-23", JsonRateCardCalculator.LoadEmbedded().RatesVersion);
    }

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Calculate_GitHubVector_ReturnsExactResult(
        string model,
        DateTimeOffset start,
        long input,
        long cacheRead,
        long cacheWrite,
        long output,
        string tier,
        decimal usdCost,
        decimal aiCredits)
    {
        CreditCalculationResult result = JsonRateCardCalculator.LoadEmbedded().Calculate(
            TestSpans.Chat(
                startTime: start,
                requestModel: model,
                inputTokens: input,
                outputTokens: output,
                cacheReadInputTokens: cacheRead,
                cacheWriteInputTokens: cacheWrite));

        Assert.Equal(BillingChannel.GitHubAiCredits, result.Channel);
        Assert.Equal(PricingStatus.Priced, result.Status);
        Assert.Equal(model, result.ModelId);
        Assert.Equal(model, result.RateCardModelId);
        Assert.Equal(tier, result.TierName);
        Assert.Equal(usdCost, result.UsdCost);
        Assert.Equal(aiCredits, result.AiCredits);
        Assert.Equal(0m, result.EstimatedCloudSavedUsd);
        if (input == 1 && model == "mai-code-2")
        {
            Assert.Equal(0.000000025m, result.Breakdown!.CacheReadUsd);
        }
    }

    [Fact]
    public void Calculate_V2_UsesExactCacheDiscountBreakdown()
    {
        JsonRateCardCalculator calculator = JsonRateCardCalculator.LoadEmbedded();
        var cached = TestSpans.Chat(
            requestModel: "claude-opus-5.5",
            inputTokens: 1_200_000,
            cacheReadInputTokens: 800_000,
            cacheWriteInputTokens: 100_000,
            outputTokens: 50_000);
        CreditCalculationResult cachedResult = calculator.Calculate(cached);
        CreditCalculationResult uncachedResult = calculator.Calculate(
            cached with { CacheReadInputTokens = 0, CacheWriteInputTokens = 0 });

        Assert.NotNull(cachedResult.Breakdown);
        Assert.Equal(300_000, cachedResult.UncachedInputTokens);
        Assert.Equal(1.20m, cachedResult.Breakdown.UncachedInputUsd);
        Assert.Equal(0.16m, cachedResult.Breakdown.CacheReadUsd);
        Assert.Equal(0.50m, cachedResult.Breakdown.CacheWriteUsd);
        Assert.Equal(1.00m, cachedResult.Breakdown.OutputUsd);
        Assert.Equal(5.80m, uncachedResult.UsdCost);
        Assert.Equal(2.94m, uncachedResult.UsdCost - cachedResult.UsdCost);
    }

    [Fact]
    public void Calculate_NullCacheWriteRate_UsesInputRate()
    {
        CreditCalculationResult result = JsonRateCardCalculator.LoadEmbedded().Calculate(
            TestSpans.Chat(
                requestModel: "mai-code-2",
                inputTokens: 1_000,
                cacheWriteInputTokens: 1_000));

        Assert.Equal(0.0005m, result.Breakdown!.CacheWriteUsd);
    }

    [Fact]
    public void Calculate_ResponseModelWinsAndNormalizes()
    {
        CreditCalculationResult result = JsonRateCardCalculator.LoadEmbedded().Calculate(
            TestSpans.Chat(
                requestModel: "auto",
                responseModel: "claude-sonnet-5.5",
                inputTokens: 10_000,
                outputTokens: 2_000));
        Assert.Equal(PricingStatus.Priced, result.Status);
        Assert.Equal("claude-sonnet-5.5", result.ModelId);
        Assert.Equal(0.04m, result.UsdCost);

        result = JsonRateCardCalculator.LoadEmbedded().Calculate(
            TestSpans.Chat(
                responseModel: "Claude Opus 5.5",
                inputTokens: 1_000_000,
                outputTokens: 1_000_000));
        Assert.Equal(PricingStatus.Priced, result.Status);
        Assert.Equal(24.00m, result.UsdCost);
        Assert.Equal("claude-opus-5.5", result.ModelId);
    }

    [Theory]
    [InlineData("invoke_agent")]
    [InlineData("execute_tool")]
    public void Calculate_NonInference_IsNotBillable(string operation)
    {
        CreditCalculationResult result = JsonRateCardCalculator.LoadEmbedded().Calculate(
            TestSpans.Chat(
                operationName: operation,
                requestModel: "claude-opus-5.5",
                inputTokens: 1_000_000,
                outputTokens: 1_000_000));

        Assert.Equal(BillingChannel.Unpriced, result.Channel);
        Assert.Equal(PricingStatus.NotBillableOperation, result.Status);
        Assert.Equal(0, result.UncachedInputTokens);
        AssertUnpricedAmounts(result);
    }

    [Theory]
    [InlineData(100, 80, 30, 0)]
    [InlineData(100, 0, 0, -1)]
    public void Calculate_InvalidUsage_IsReported(long input, long cacheRead, long cacheWrite, long output)
    {
        CreditCalculationResult result = JsonRateCardCalculator.LoadEmbedded().Calculate(
            TestSpans.Chat(
                requestModel: "claude-opus-5.5",
                inputTokens: input,
                outputTokens: output,
                cacheReadInputTokens: cacheRead,
                cacheWriteInputTokens: cacheWrite));

        Assert.Equal(BillingChannel.Unpriced, result.Channel);
        Assert.Equal(PricingStatus.InvalidUsage, result.Status);
        Assert.Equal(0, result.UncachedInputTokens);
        AssertUnpricedAmounts(result);
    }

    [Theory]
    [InlineData("gpt-unknown")]
    [InlineData(null)]
    public void Calculate_UnknownGitHubModel_IsReported(string? model)
    {
        CreditCalculationResult result = JsonRateCardCalculator.LoadEmbedded().Calculate(
            TestSpans.Chat(requestModel: model));

        Assert.Equal(BillingChannel.GitHubAiCredits, result.Channel);
        Assert.Equal(PricingStatus.UnknownModel, result.Status);
        AssertUnpricedAmounts(result);
    }

    [Theory]
    [InlineData("api.githubcopilot.com")]
    [InlineData("API.Enterprise.GitHubCopilot.com")]
    [InlineData("")]
    [InlineData(null)]
    public void Calculate_GitHubHosts_AreRecognized(string? host)
    {
        CreditCalculationResult result = JsonRateCardCalculator.LoadEmbedded().Calculate(
            TestSpans.Chat(
                serverAddress: host,
                requestModel: "claude-sonnet-5.5",
                inputTokens: 10_000,
                outputTokens: 2_000));

        Assert.Equal(BillingChannel.GitHubAiCredits, result.Channel);
        Assert.Equal(PricingStatus.Priced, result.Status);
        Assert.Equal(0.04m, result.UsdCost);
    }

    [Theory]
    [InlineData("api.example.com")]
    [InlineData("evilgithubcopilot.com")]
    [InlineData("evilopenai.azure.com")]
    public void Calculate_UnrecognizedHosts_AreNotPriced(string host)
    {
        CreditCalculationResult result = JsonRateCardCalculator.LoadEmbedded().Calculate(
            TestSpans.Chat(
                serverAddress: host,
                requestModel: "claude-sonnet-5.5",
                inputTokens: 10_000,
                outputTokens: 2_000));

        Assert.Equal(BillingChannel.Unpriced, result.Channel);
        Assert.Equal(PricingStatus.UnrecognizedEndpoint, result.Status);
        AssertUnpricedAmounts(result);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.0.0.2")]
    [InlineData("localhost")]
    [InlineData("LOCALHOST")]
    [InlineData("::1")]
    [InlineData("[::1]")]
    public void Calculate_Loopback_IsLocal(string host)
    {
        CreditCalculationResult result = CalculateLocal(host, "qwen3.8-27b-cuda-gpu:1");

        AssertLocal(result, "qwen3.8-27b", 0.23m);
    }

    [Theory]
    [InlineData("contoso.openai.azure.com")]
    [InlineData("contoso.services.ai.azure.com")]
    [InlineData("contoso.cognitiveservices.azure.com")]
    public void Calculate_ByokHostsWithoutRates_AreUnpriced(string host)
    {
        CreditCalculationResult result = JsonRateCardCalculator.LoadEmbedded().Calculate(
            TestSpans.Chat(
                serverAddress: host,
                requestModel: "gpt-6.1-sol",
                inputTokens: 100_000,
                outputTokens: 10_000));

        Assert.Equal(BillingChannel.FoundryByok, result.Channel);
        Assert.Equal(PricingStatus.ByokUnpriced, result.Status);
        AssertUnpricedAmounts(result);
    }

    [Fact]
    public void Calculate_ByokProvider_IsRecognized()
    {
        CreditCalculationResult result = JsonRateCardCalculator.LoadEmbedded().Calculate(
            TestSpans.Chat(
                providerName: "azure.ai.inference",
                requestModel: "gpt-6.1-sol",
                inputTokens: 100_000,
                outputTokens: 10_000));
        Assert.Equal(BillingChannel.FoundryByok, result.Channel);
        Assert.Equal(PricingStatus.ByokUnpriced, result.Status);
    }

    [Fact]
    public void Calculate_ByokFixture_PricesUsage()
    {
        CreditCalculationResult result = JsonRateCardCalculator.Parse(RateFixtures.ByokTestRatesJson).Calculate(
            TestSpans.Chat(
                serverAddress: "contoso.openai.azure.com",
                requestModel: "gpt-6.1-sol",
                inputTokens: 100_000,
                outputTokens: 10_000));

        Assert.Equal(BillingChannel.FoundryByok, result.Channel);
        Assert.Equal(PricingStatus.Priced, result.Status);
        Assert.Equal(0.315m, result.UsdCost);
        Assert.Equal(0m, result.AiCredits);
        Assert.Equal("default", result.TierName);
    }

    [Fact]
    public void Calculate_LocalUsesStandardBaselineWindow()
    {
        CreditCalculationResult result = CalculateLocal(
            "127.0.0.1",
            "qwen3.8-27b-cuda-gpu:1",
            Utc(2027, 5, 17, 0));
        AssertLocal(result, "qwen3.8-27b", 0.46m);
    }

    [Theory]
    [InlineData("qwen3.8:27b")]
    [InlineData("Qwen 3.8 27B")]
    [InlineData("qwen3.8:27b-q4_K_M")]
    public void Calculate_KnownLocalAliasesWithoutHost_AreCanonicalized(string model)
    {
        AssertLocal(CalculateLocal(null, model), "qwen3.8-27b", 0.23m);
    }

    [Fact]
    public void Calculate_UnlistedLoopbackModel_IsStillLocal()
    {
        AssertLocal(CalculateLocal("127.0.0.1", "phi-local-test"), "phi-local-test", 0.23m);
    }

    internal static CreditCalculationResult CalculateLocal(
        string? host,
        string model,
        DateTimeOffset? start = null)
    {
        return JsonRateCardCalculator.LoadEmbedded().Calculate(
            TestSpans.Chat(
                serverAddress: host,
                requestModel: model,
                startTime: start,
                inputTokens: 400_000,
                outputTokens: 12_000));
    }

    private static void AssertLocal(CreditCalculationResult result, string model, decimal saved)
    {
        Assert.Equal(BillingChannel.Local, result.Channel);
        Assert.Equal(PricingStatus.LocalOffload, result.Status);
        Assert.Equal(0m, result.UsdCost);
        Assert.Equal(0m, result.AiCredits);
        Assert.Equal(saved, result.EstimatedCloudSavedUsd);
        Assert.Equal("mai-code-2", result.RateCardModelId);
        Assert.Equal("default", result.TierName);
        Assert.Equal(model, result.ModelId);
    }

    private static void AssertUnpricedAmounts(CreditCalculationResult result)
    {
        Assert.Null(result.Breakdown);
        Assert.Null(result.TierName);
        Assert.Equal(0m, result.UsdCost);
        Assert.Equal(0m, result.AiCredits);
        Assert.Equal(0m, result.EstimatedCloudSavedUsd);
    }

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute = 0, int second = 0) =>
        new(year, month, day, hour, minute, second, TimeSpan.Zero);
}
