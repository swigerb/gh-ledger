using System.Globalization;
using CopilotLedger.Core.Audit;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Audit;

public sealed class AuditPricingTests
{
    public static TheoryData<BillingChannel, string?, string?, string, long, bool, string?> Tiers => new()
    {
        { BillingChannel.GitHubAiCredits, "claude-opus-5.5", null, "2026-11-21T14:00:00Z", 1_000, false, "default 4/0.2/5/20" },
        { BillingChannel.GitHubAiCredits, "gpt-6.1-sol", null, "2026-11-21T14:00:00Z", 300_000, false, "long_context 4/0.2/5/15" },
        { BillingChannel.GitHubAiCredits, "Claude Opus 5-5", null, "2026-11-21T14:00:00Z", 1_000, false, "default 4/0.2/5/20" },
        { BillingChannel.GitHubAiCredits, "gpt-unknown", "claude-opus-5.5", "2026-11-21T14:00:00Z", 1_000, false, "default 4/0.2/5/20" },
        { BillingChannel.GitHubAiCredits, "mai-code-2", null, "2027-06-01T00:00:00Z", 1_000, false, "default 1/0.05/-/5" },
        { BillingChannel.GitHubAiCredits, "gemini-4-argon", null, "2026-12-15T00:00:00Z", 1_000, false, null },
        { BillingChannel.FoundryByok, "gpt-6.1-sol", null, "2026-11-21T14:00:00Z", 1_000, false, null },
        { BillingChannel.FoundryByok, "gpt-6.1-sol", null, "2026-11-21T14:00:00Z", 1_000, true, "default 1.75/0.175/-/14" },
        { BillingChannel.Local, "claude-opus-5.5", null, "2026-11-21T14:00:00Z", 1_000, false, null },
        { BillingChannel.Unpriced, "claude-opus-5.5", null, "2026-11-21T14:00:00Z", 1_000, false, null },
    };

    [Theory]
    [MemberData(nameof(Tiers))]
    public void AP1_FindBilledTier_SelectsTheTierCalculateBillsWith(
        BillingChannel channel,
        string? model,
        string? requestModel,
        string instant,
        long inputTokens,
        bool byokCard,
        string? expected)
    {
        IAuditPricing pricing = byokCard ? JsonRateCardCalculator.Parse(RateFixtures.ByokTestRatesJson) : AuditFixtures.Pricing;

        RateTier? tier = pricing.FindBilledTier(
            channel,
            model,
            requestModel,
            DateTimeOffset.Parse(instant, CultureInfo.InvariantCulture),
            inputTokens);

        Assert.Equal(expected, tier is null ? null : $"{tier.Name} {Rate(tier.InputPer1M)}/{Rate(tier.CachedInputPer1M)}/{Rate(tier.CacheWritePer1M)}/{Rate(tier.OutputPer1M)}");
    }

    public static TheoryData<string, int, decimal, string> Quartiles => new()
    {
        {
            "2026-11-21T14:00:00Z", 34, 15m,
            "claude-sonnet-4 claude-sonnet-4.6 gemini-4-neon kimi-k3 claude-opus-5.5 gpt-5.6-sol claude-opus-4.8 claude-fable-5.2 gpt-6.2-astra claude-fable-5 claude-fable-5.1 gpt-6-astra"
        },
        {
            "2026-12-15T00:00:00Z", 33, 20m,
            "claude-opus-5.5 gemini-4-carbon gpt-5.6-sol claude-opus-4.8 claude-fable-5.2 gpt-6.2-astra claude-fable-5 claude-fable-5.1 gpt-6-astra"
        },
    };

    [Theory]
    [MemberData(nameof(Quartiles))]
    public void AP2_GitHubOutputRates_GiveTheTopQuartileOfTheEmbeddedCard(string instant, int count, decimal p75, string premium)
    {
        DateTimeOffset at = DateTimeOffset.Parse(instant, CultureInfo.InvariantCulture);

        decimal[] rates = [.. AuditFixtures.Pricing.GitHubOutputRates(at).Order()];

        Assert.Equal(count, rates.Length);
        Assert.Equal(p75, rates[(3 * rates.Length + 3) / 4 - 1]);
        string[] models = premium.Split(' ');
        Assert.Equal(models.Length, rates.Count(rate => rate >= p75));
        Assert.All(models, model => Assert.True(
            AuditFixtures.Pricing.FindBilledTier(BillingChannel.GitHubAiCredits, model, null, at, 0)!.OutputPer1M >= p75,
            model));
        Assert.True(AuditFixtures.Pricing.FindBilledTier(BillingChannel.GitHubAiCredits, "claude-sonnet-5.5", null, at, 0)!.OutputPer1M < p75);
    }

    [Theory]
    [InlineData("mai-code-2", "mai-code-2")]
    [InlineData("MAI Code 2", "mai-code-2")]
    [InlineData("claude-opus-4-8", "claude-opus-4.8")]
    [InlineData("qwen3.8-27b", null)]
    [InlineData("nope", null)]
    public void AP3_FindGitHubModel_ResolvesIdsAndAliasesOfTheGitHubSection(string model, string? expected)
    {
        Assert.Equal(expected, AuditFixtures.Pricing.FindGitHubModel(model));
    }

    [Fact]
    public void AP4_BaselineModel_IsTheCardsLocalOffloadBaseline()
    {
        Assert.Equal("mai-code-2", AuditFixtures.Pricing.BaselineModel);
        Assert.Equal("claude-sonnet-5.5", JsonRateCardCalculator.Parse(RateFixtures.ByokTestRatesJson).BaselineModel);
        Assert.Equal("2026-11-23", ((IAuditPricing)AuditFixtures.Pricing).RatesVersion);
    }

    private static string Rate(decimal? rate) =>
        rate is null ? "-" : rate.Value.ToString("0.############", CultureInfo.InvariantCulture);
}
