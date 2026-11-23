using System.Globalization;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Pricing;

public sealed class RatesRefreshTests
{
    // model, start, input tokens, tier (null: no price for that date), then the expected
    // UncachedInputUsd, CacheReadUsd, CacheWriteUsd, OutputUsd, and AiCredits.
    // Every case also has 10,000 cache-read, 5,000 cache-write, and 2,000 output tokens.
    public static TheoryData<string, string, long, string?, decimal, decimal, decimal, decimal, decimal> PublishedPrices => new()
    {
        { "gpt-5.3-codex", "2026-11-20T12:00:00.0000000+00:00", 100000, "default", 0.14875m, 0.00175m, 0.00875m, 0.028m, 18.725m },
        { "gpt-5.4-nano", "2026-11-20T12:00:00.0000000+00:00", 100000, "default", 0.017m, 0.0002m, 0.001m, 0.0025m, 2.07m },
        { "gpt-5.6-luna", "2026-11-20T12:00:00.0000000+00:00", 200000, "default", 0.037m, 0.0002m, 0.00125m, 0.0024m, 4.085m },
        { "gpt-5.6-luna", "2026-11-20T12:00:00.0000000+00:00", 200001, "long_context", 0.0740004m, 0.0004m, 0.0025m, 0.0036m, 8.05004m },
        { "gpt-5.6-sol", "2026-11-20T12:00:00.0000000+00:00", 272000, "default", 1.028m, 0.004m, 0.025m, 0.04m, 109.7m },
        { "gpt-5.6-sol", "2026-11-20T12:00:00.0000000+00:00", 272001, "long_context", 2.056008m, 0.008m, 0.05m, 0.06m, 217.4008m },
        { "gpt-6-luna", "2026-11-20T12:00:00.0000000+00:00", 272000, "default", 0.0257m, 0.0001m, 0.000625m, 0.001m, 2.7425m },
        { "gpt-6-luna", "2026-11-20T12:00:00.0000000+00:00", 272001, "long_context", 0.0514002m, 0.0002m, 0.00125m, 0.0015m, 5.43502m },
        { "gpt-6-sol", "2026-11-20T12:00:00.0000000+00:00", 272000, "default", 0.514m, 0.002m, 0.0125m, 0.02m, 54.85m },
        { "gpt-6-sol", "2026-11-20T12:00:00.0000000+00:00", 272001, "long_context", 1.028004m, 0.004m, 0.025m, 0.03m, 108.7004m },
        { "gpt-6.1-terra", "2026-11-20T12:00:00.0000000+00:00", 272000, "default", 0.257m, 0.0005m, 0.00625m, 0.01m, 27.375m },
        { "gpt-6.1-terra", "2026-11-20T12:00:00.0000000+00:00", 272001, "long_context", 0.514002m, 0.001m, 0.0125m, 0.015m, 54.2502m },
        { "gpt-6.2-astra", "2026-11-20T12:00:00.0000000+00:00", 272000, "default", 2.056m, 0.004m, 0.05m, 0.08m, 219.0m },
        { "gpt-6.2-astra", "2026-11-20T12:00:00.0000000+00:00", 272001, "long_context", 4.112016m, 0.008m, 0.10m, 0.12m, 434.0016m },
        { "gpt-6.2-sol", "2026-11-20T12:00:00.0000000+00:00", 272000, "default", 0.514m, 0.001m, 0.0125m, 0.02m, 54.75m },
        { "gpt-6.2-sol", "2026-11-20T12:00:00.0000000+00:00", 272001, "long_context", 1.028004m, 0.002m, 0.025m, 0.03m, 108.5004m },
        { "claude-haiku-4.5", "2026-11-20T12:00:00.0000000+00:00", 100000, "default", 0.085m, 0.001m, 0.00625m, 0.01m, 10.225m },
        { "claude-haiku-5.5", "2026-11-20T12:00:00.0000000+00:00", 100000, "default", 0.0085m, 0.0001m, 0.000625m, 0.001m, 1.0225m },
        { "claude-haiku-5.5", "2026-11-20T12:00:00.0000000+00:00", 100001, "long_context", 0.0425005m, 0.0005m, 0.003125m, 0.005m, 5.11255m },
        { "claude-sonnet-4", "2026-11-20T12:00:00.0000000+00:00", 100000, "default", 0.255m, 0.003m, 0.01875m, 0.03m, 30.675m },
        { "claude-sonnet-4.6", "2026-11-20T12:00:00.0000000+00:00", 100000, "default", 0.255m, 0.003m, 0.01875m, 0.03m, 30.675m },
        { "claude-opus-4.8", "2026-11-20T12:00:00.0000000+00:00", 100000, "default", 0.425m, 0.005m, 0.03125m, 0.05m, 51.125m },
        { "claude-sonnet-5", "2026-11-20T12:00:00.0000000+00:00", 100000, "default", 0.17m, 0.002m, 0.0125m, 0.02m, 20.45m },
        { "claude-fable-5", "2026-11-20T12:00:00.0000000+00:00", 100000, "default", 0.85m, 0.01m, 0.0625m, 0.10m, 102.25m },
        { "claude-fable-5.1", "2026-11-20T12:00:00.0000000+00:00", 100000, "default", 0.85m, 0.0025m, 0.0625m, 0.10m, 101.50m },
        { "claude-fable-5.2", "2026-11-20T12:00:00.0000000+00:00", 100000, "default", 0.68m, 0.002m, 0.05m, 0.08m, 81.2m },
        { "gemini-3.8-flash", "2026-11-20T12:00:00.0000000+00:00", 100000, "default", 0.06375m, 0.00075m, 0.00375m, 0.0075m, 7.575m },
        { "gemini-3.8-flash", "2026-12-31T23:59:59.9999999+00:00", 100000, "default", 0.06375m, 0.00075m, 0.00375m, 0.0075m, 7.575m },
        { "gemini-3.8-flash", "2027-01-01T00:00:00.0000000+00:00", 100000, null, 0m, 0m, 0m, 0m, 0m },
        { "gemini-4-argon", "2026-11-20T12:00:00.0000000+00:00", 100000, "default", 0.17m, 0.001m, 0.01m, 0.02m, 20.1m },
        { "gemini-4-argon", "2026-11-30T23:59:59.9999999+00:00", 100000, "default", 0.17m, 0.001m, 0.01m, 0.02m, 20.1m },
        { "gemini-4-argon", "2026-12-01T00:00:00.0000000+00:00", 100000, null, 0m, 0m, 0m, 0m, 0m },
        { "gemini-4-carbon", "2026-11-20T12:00:00.0000000+00:00", 100000, "default", 0.17m, 0.001m, 0.01m, 0.02m, 20.1m },
        { "gemini-4-carbon", "2026-11-30T23:59:59.9999999+00:00", 100000, "default", 0.17m, 0.001m, 0.01m, 0.02m, 20.1m },
        { "gemini-4-carbon", "2026-12-01T00:00:00.0000000+00:00", 100000, "default", 0.34m, 0.002m, 0.02m, 0.04m, 40.2m },
        { "gemini-4-hydrogen", "2026-11-20T12:00:00.0000000+00:00", 100000, "default", 0.1275m, 0.00075m, 0.0075m, 0.015m, 15.075m },
        { "gemini-4-neon", "2026-11-20T12:00:00.0000000+00:00", 100000, "default", 0.255m, 0.0015m, 0.015m, 0.03m, 30.15m },
        { "mai-code-1.1-flash", "2026-11-20T12:00:00.0000000+00:00", 100000, "default", 0.017m, 0.0002m, 0.001m, 0.0024m, 2.06m },
        { "mai-code-2-flash", "2026-11-20T12:00:00.0000000+00:00", 100000, "default", 0.00425m, 0.00005m, 0.00025m, 0.0005m, 0.505m },
        { "mai-code-2-flash", "2027-05-16T23:59:59.9999999+00:00", 100000, "default", 0.00425m, 0.00005m, 0.00025m, 0.0005m, 0.505m },
        { "mai-code-2-flash", "2027-05-17T00:00:00.0000000+00:00", 100000, "default", 0.0085m, 0.0001m, 0.0005m, 0.001m, 1.01m },
        { "grok-4.5", "2026-11-20T12:00:00.0000000+00:00", 200000, "default", 0.37m, 0.005m, 0.01m, 0.012m, 39.7m },
        { "grok-4.5", "2026-11-20T12:00:00.0000000+00:00", 200001, "long_context", 0.740004m, 0.01m, 0.02m, 0.024m, 79.4004m },
        { "grok-4.6", "2026-11-20T12:00:00.0000000+00:00", 200000, "default", 0.37m, 0.005m, 0.01m, 0.012m, 39.7m },
        { "grok-4.6", "2026-11-20T12:00:00.0000000+00:00", 200001, "long_context", 0.740004m, 0.01m, 0.02m, 0.024m, 79.4004m },
        { "grok-4.7", "2026-11-20T12:00:00.0000000+00:00", 200000, "default", 0.37m, 0.005m, 0.01m, 0.012m, 39.7m },
        { "grok-4.7", "2026-11-20T12:00:00.0000000+00:00", 200001, "long_context", 0.740004m, 0.01m, 0.02m, 0.024m, 79.4004m },
        { "kimi-k3", "2026-11-20T12:00:00.0000000+00:00", 100000, "default", 0.255m, 0.003m, 0.015m, 0.03m, 30.3m },
    };

    [Theory]
    [MemberData(nameof(PublishedPrices))]
    public void RR1_PublishedPrices_MatchGitHubTable(
        string model,
        string start,
        long input,
        string? tier,
        decimal uncachedInputUsd,
        decimal cacheReadUsd,
        decimal cacheWriteUsd,
        decimal outputUsd,
        decimal aiCredits)
    {
        NormalizedGenAiSpan span = TestSpans.Chat(
            startTime: DateTimeOffset.Parse(start, CultureInfo.InvariantCulture),
            requestModel: model,
            inputTokens: input,
            cacheReadInputTokens: 10_000,
            cacheWriteInputTokens: 5_000,
            outputTokens: 2_000);

        CreditCalculationResult result =
            JsonRateCardCalculator.LoadEmbedded().Calculate(span);
        Assert.Equal(BillingChannel.GitHubAiCredits, result.Channel);
        Assert.Equal(model, result.ModelId);
        if (tier is null)
        {
            Assert.Equal(PricingStatus.NoRateForDate, result.Status);
            Assert.Null(result.RateCardModelId);
            Assert.Null(result.TierName);
            Assert.Null(result.Breakdown);
            Assert.Equal(0m, result.UsdCost);
            Assert.Equal(0m, result.AiCredits);
            return;
        }

        Assert.Equal(PricingStatus.Priced, result.Status);
        Assert.Equal(model, result.RateCardModelId);
        Assert.Equal(tier, result.TierName);
        Assert.NotNull(result.Breakdown);
        Assert.Equal(uncachedInputUsd, result.Breakdown.UncachedInputUsd);
        Assert.Equal(cacheReadUsd, result.Breakdown.CacheReadUsd);
        Assert.Equal(cacheWriteUsd, result.Breakdown.CacheWriteUsd);
        Assert.Equal(outputUsd, result.Breakdown.OutputUsd);
        Assert.Equal(aiCredits, result.AiCredits);
        Assert.Equal(
            uncachedInputUsd + cacheReadUsd + cacheWriteUsd + outputUsd,
            result.UsdCost);
    }

    [Theory]
    [InlineData(3665, 0, 3662, 43, "0.00047955", "0.047955")]
    [InlineData(3764, 3662, 99, 5, "0.000051795", "0.0051795")]
    public void RR2_LiveGpt6LunaPrices_ReconcileWithCopilot(
        long input,
        long cacheRead,
        long cacheWrite,
        long output,
        string expectedUsd,
        string expectedCredits)
    {
        NormalizedGenAiSpan span = TestSpans.Chat(
            requestModel: "gpt-6-luna",
            inputTokens: input,
            cacheReadInputTokens: cacheRead,
            cacheWriteInputTokens: cacheWrite,
            outputTokens: output);
        CreditCalculationResult result =
            JsonRateCardCalculator.LoadEmbedded().Calculate(span);

        Assert.Equal(PricingStatus.Priced, result.Status);
        Assert.Equal(BillingChannel.GitHubAiCredits, result.Channel);
        Assert.Equal("default", result.TierName);
        Assert.Equal(decimal.Parse(expectedUsd, CultureInfo.InvariantCulture), result.UsdCost);
        Assert.Equal(decimal.Parse(expectedCredits, CultureInfo.InvariantCulture), result.AiCredits);
    }

    [Theory]
    [InlineData("ollama")]
    [InlineData("lm_studio")]
    [InlineData("foundry_local")]
    [InlineData("llama_cpp")]
    public void RR3_LocalProviderNames_AreRecognized(string provider)
    {
        NormalizedGenAiSpan span = TestSpans.Chat(
            providerName: provider,
            requestModel: "llama3.3:70b",
            inputTokens: 400_000,
            outputTokens: 12_000);
        CreditCalculationResult result =
            JsonRateCardCalculator.LoadEmbedded().Calculate(span);

        Assert.Equal(BillingChannel.Local, result.Channel);
        Assert.Equal(PricingStatus.LocalOffload, result.Status);
        Assert.Equal(0.23m, result.EstimatedCloudSavedUsd);
        Assert.Equal("llama3.3:70b", result.ModelId);
        Assert.Equal("mai-code-2", result.RateCardModelId);
    }

    [Fact]
    public void RR4_RatesVersion_IsUnchanged() =>
        Assert.Equal(
            "2026-11-23",
            JsonRateCardCalculator.LoadEmbedded().RatesVersion);
}
