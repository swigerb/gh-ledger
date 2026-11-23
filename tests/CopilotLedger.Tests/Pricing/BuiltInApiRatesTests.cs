using System.Globalization;
using System.Text.Json.Nodes;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Pricing;

public sealed class BuiltInApiRatesTests
{
    private static readonly JsonRateCardCalculator Embedded = JsonRateCardCalculator.LoadEmbedded();

    // model, start, input tokens, tier (null: no API list price for that date), then the expected UncachedInputUsd,
    // CacheReadUsd, CacheWriteUsd, and OutputUsd. Every case also has 10,000 cache-read, 5,000 cache-write, and 2,000
    // output tokens.
    public static TheoryData<string, string, long, string?, decimal, decimal, decimal, decimal> ListPrices => new()
    {
        { "claude-opus-5.5", "2026-11-23T12:00:00Z", 100_000, "default", 0.34m, 0.002m, 0.025m, 0.04m },
        { "claude-sonnet-5.5", "2026-11-23T12:00:00Z", 100_000, "default", 0.17m, 0.001m, 0.0125m, 0.02m },
        { "gpt-6.1-sol", "2026-11-23T12:00:00Z", 272_000, "default", 0.514m, 0.001m, 0.0125m, 0.02m },
        { "gpt-6.1-sol", "2026-11-23T12:00:00Z", 272_001, "long_context", 1.028004m, 0.002m, 0.025m, 0.03m },
        { "gpt-6-astra", "2026-11-23T12:00:00Z", 272_000, "default", 2.57m, 0.01m, 0.0625m, 0.1m },
        { "gpt-6-astra", "2026-11-23T12:00:00Z", 272_001, "long_context", 5.14002m, 0.02m, 0.125m, 0.15m },
        { "gpt-5.3-codex", "2026-11-23T12:00:00Z", 100_000, "default", 0.14875m, 0.00175m, 0.00875m, 0.028m },
        { "gpt-5.3-codex", "2027-03-31T23:59:59Z", 100_000, "default", 0.14875m, 0.00175m, 0.00875m, 0.028m },
        { "gpt-5.3-codex", "2027-04-01T00:00:00Z", 100_000, null, 0m, 0m, 0m, 0m },
        { "gpt-5.4-nano", "2026-11-23T12:00:00Z", 100_000, "default", 0.017m, 0.0002m, 0.001m, 0.0025m },
        { "gpt-5.4-nano", "2027-04-01T00:00:00Z", 100_000, null, 0m, 0m, 0m, 0m },
        { "gpt-5.6-luna", "2026-07-29T23:59:59Z", 100_000, null, 0m, 0m, 0m, 0m },
        { "gpt-5.6-luna", "2026-07-30T00:00:00Z", 100_000, "default", 0.017m, 0.0002m, 0.00125m, 0.0024m },
        { "gpt-5.6-luna", "2026-11-23T12:00:00Z", 272_000, "default", 0.0514m, 0.0002m, 0.00125m, 0.0024m },
        { "gpt-5.6-luna", "2026-11-23T12:00:00Z", 272_001, "long_context", 0.1028004m, 0.0004m, 0.0025m, 0.0036m },
        { "gpt-5.6-sol", "2026-08-20T23:59:59Z", 100_000, null, 0m, 0m, 0m, 0m },
        { "gpt-5.6-sol", "2026-08-21T00:00:00Z", 272_000, "default", 1.028m, 0.004m, 0.025m, 0.04m },
        { "gpt-5.6-sol", "2026-11-23T12:00:00Z", 272_001, "long_context", 2.056008m, 0.008m, 0.05m, 0.06m },
        { "gpt-5.6-sol", "2026-11-30T23:59:59.9999999Z", 100_000, "default", 0.34m, 0.004m, 0.025m, 0.04m },
        { "gpt-5.6-sol", "2026-12-01T00:00:00Z", 100_000, null, 0m, 0m, 0m, 0m },
        { "gpt-6-luna", "2026-11-23T12:00:00Z", 272_000, "default", 0.0257m, 0.0001m, 0.000625m, 0.001m },
        { "gpt-6-luna", "2026-11-23T12:00:00Z", 272_001, "long_context", 0.0514002m, 0.0002m, 0.00125m, 0.0015m },
        { "gpt-6-sol", "2026-11-23T12:00:00Z", 272_000, "default", 0.514m, 0.002m, 0.0125m, 0.02m },
        { "gpt-6-sol", "2026-11-23T12:00:00Z", 272_001, "long_context", 1.028004m, 0.004m, 0.025m, 0.03m },
        { "gpt-6.1-terra", "2026-11-23T12:00:00Z", 272_000, "default", 0.257m, 0.0005m, 0.00625m, 0.01m },
        { "gpt-6.1-terra", "2026-11-23T12:00:00Z", 272_001, "long_context", 0.514002m, 0.001m, 0.0125m, 0.015m },
        { "gpt-6.2-astra", "2026-11-23T12:00:00Z", 272_000, "default", 2.056m, 0.004m, 0.05m, 0.08m },
        { "gpt-6.2-astra", "2026-11-23T12:00:00Z", 272_001, "long_context", 4.112016m, 0.008m, 0.1m, 0.12m },
        { "gpt-6.2-sol", "2026-11-23T12:00:00Z", 272_000, "default", 0.514m, 0.001m, 0.0125m, 0.02m },
        { "gpt-6.2-sol", "2026-11-23T12:00:00Z", 272_001, "long_context", 1.028004m, 0.002m, 0.025m, 0.03m },
        { "claude-haiku-4.5", "2026-11-23T12:00:00Z", 100_000, "default", 0.085m, 0.001m, 0.00625m, 0.01m },
        { "claude-haiku-5.5", "2026-11-23T12:00:00Z", 100_000, "default", 0.0085m, 0.0001m, 0.000625m, 0.001m },
        { "claude-haiku-5.5", "2026-11-23T12:00:00Z", 100_001, "long_context", 0.0425005m, 0.0005m, 0.003125m, 0.005m },
        { "claude-sonnet-4", "2026-06-14T23:59:59Z", 100_000, "default", 0.255m, 0.003m, 0.01875m, 0.03m },
        { "claude-sonnet-4", "2026-06-15T00:00:00Z", 100_000, null, 0m, 0m, 0m, 0m },
        { "claude-sonnet-4.6", "2026-11-23T12:00:00Z", 100_000, "default", 0.255m, 0.003m, 0.01875m, 0.03m },
        { "claude-opus-4.8", "2026-11-23T12:00:00Z", 100_000, "default", 0.425m, 0.005m, 0.03125m, 0.05m },
        { "claude-sonnet-5", "2026-11-23T12:00:00Z", 100_000, "default", 0.17m, 0.002m, 0.0125m, 0.02m },
        { "claude-fable-5", "2026-11-23T12:00:00Z", 100_000, "default", 0.85m, 0.01m, 0.0625m, 0.1m },
        { "claude-fable-5.1", "2026-11-23T12:00:00Z", 100_000, "default", 0.85m, 0.0025m, 0.0625m, 0.1m },
        { "claude-fable-5.2", "2026-11-23T12:00:00Z", 100_000, "default", 0.68m, 0.002m, 0.05m, 0.08m },
        { "gemini-3.8-flash", "2026-12-31T23:59:59.9999999Z", 100_000, "default", 0.06375m, 0.00075m, 0.00375m, 0.0075m },
        { "gemini-3.8-flash", "2027-01-01T00:00:00Z", 100_000, "default", 0.1275m, 0.0015m, 0.0075m, 0.015m },
        { "gemini-4-argon", "2026-11-30T23:59:59.9999999Z", 100_000, "default", 0.17m, 0.001m, 0.01m, 0.02m },
        { "gemini-4-argon", "2026-12-01T00:00:00Z", 100_000, "default", 0.34m, 0.002m, 0.02m, 0.04m },
        { "gemini-4-carbon", "2026-11-30T23:59:59.9999999Z", 100_000, "default", 0.17m, 0.001m, 0.01m, 0.02m },
        { "gemini-4-carbon", "2026-12-01T00:00:00Z", 100_000, "default", 0.34m, 0.002m, 0.02m, 0.04m },
        { "gemini-4-hydrogen", "2026-11-23T12:00:00Z", 100_000, "default", 0.1275m, 0.00075m, 0.0075m, 0.015m },
        { "gemini-4-neon", "2026-11-23T12:00:00Z", 100_000, "default", 0.255m, 0.0015m, 0.015m, 0.03m },
        { "grok-4.5", "2026-11-23T12:00:00Z", 199_999, "default", 0.369998m, 0.003m, 0.01m, 0.012m },
        { "grok-4.5", "2026-11-23T12:00:00Z", 200_000, "long_context", 0.74m, 0.006m, 0.02m, 0.024m },
        { "grok-4.6", "2026-11-23T12:00:00Z", 199_999, "default", 0.369998m, 0.005m, 0.01m, 0.012m },
        { "grok-4.6", "2026-11-23T12:00:00Z", 200_000, "long_context", 0.74m, 0.01m, 0.02m, 0.024m },
        { "grok-4.7", "2026-11-23T12:00:00Z", 199_999, "default", 0.369998m, 0.005m, 0.01m, 0.012m },
        { "grok-4.7", "2026-11-23T12:00:00Z", 200_000, "long_context", 0.74m, 0.01m, 0.02m, 0.024m },
        { "kimi-k3", "2026-11-23T12:00:00Z", 100_000, "default", 0.255m, 0.003m, 0.015m, 0.03m },
    };

    public static TheoryData<string, string, string> Aliases => new()
    {
        { "claude-opus-4-8", "claude-opus-4.8", "2026-11-23T12:00:00Z" },
        { "claude-opus-5-5", "claude-opus-5.5", "2026-11-23T12:00:00Z" },
        { "claude-haiku-4-5", "claude-haiku-4.5", "2026-11-23T12:00:00Z" },
        { "claude-haiku-4-5-20251001", "claude-haiku-4.5", "2026-11-23T12:00:00Z" },
        { "claude-sonnet-4-20250514", "claude-sonnet-4", "2026-06-01T00:00:00Z" },
        { "gpt-5.4-nano-2026-03-17", "gpt-5.4-nano", "2026-11-23T12:00:00Z" },
    };

    [Theory]
    [MemberData(nameof(ListPrices))]
    public void AR1_BuiltInListPricesMatchTheVendorsPages(
        string model,
        string start,
        long input,
        string? tier,
        decimal uncachedInputUsd,
        decimal cacheReadUsd,
        decimal cacheWriteUsd,
        decimal outputUsd)
    {
        ApiEquivalentCost cost = Embedded.Calculate(TestSpans.Chat(
            startTime: DateTimeOffset.Parse(start, CultureInfo.InvariantCulture),
            requestModel: model,
            inputTokens: input,
            cacheReadInputTokens: 10_000,
            cacheWriteInputTokens: 5_000,
            outputTokens: 2_000)).ApiEquivalent;

        if (tier is null)
        {
            Assert.Equal(ApiEquivalentStatus.Unpriced, cost.Status);
            Assert.Null(cost.UsdCost);
            return;
        }

        Assert.Equal(ApiEquivalentStatus.Priced, cost.Status);
        Assert.Equal(model, cost.RateCardModelId);
        Assert.Equal(tier, cost.TierName);
        Assert.NotNull(cost.Breakdown);
        Assert.Equal(uncachedInputUsd, cost.Breakdown.UncachedInputUsd);
        Assert.Equal(cacheReadUsd, cost.Breakdown.CacheReadUsd);
        Assert.Equal(cacheWriteUsd, cost.Breakdown.CacheWriteUsd);
        Assert.Equal(outputUsd, cost.Breakdown.OutputUsd);
    }

    [Theory]
    [InlineData("mai-code-2", null)]
    [InlineData("mai-code-2-flash", null)]
    [InlineData("mai-code-1.1-flash", null)]
    [InlineData("qwen3.8-27b-cuda-gpu:1", "127.0.0.1")]
    public void AR2_ModelsWithoutAListPriceAreUnpriced(string model, string? host)
    {
        CreditCalculationResult result = Embedded.Calculate(TestSpans.Chat(
            serverAddress: host,
            requestModel: model,
            inputTokens: 400_000,
            outputTokens: 12_000));

        Assert.Equal(host is null ? PricingStatus.Priced : PricingStatus.LocalOffload, result.Status);
        Assert.Equal(ApiEquivalentStatus.Unpriced, result.ApiEquivalent.Status);
        Assert.Null(result.ApiEquivalent.UsdCost);
        Assert.Equal(host is null ? 0m : 0.23m, result.EstimatedCloudSavedUsd);
    }

    [Fact]
    public void AR3_EveryGitHubModelWithAListPriceHasOneFromItsVendor()
    {
        JsonNode card = JsonNode.Parse(PublishedRates.Embedded())!;
        string[] github = card["githubAiCredits"]!["models"]!.AsArray()
            .Select(model => model!["id"]!.GetValue<string>())
            .ToArray();
        JsonNode[] retail = card["apiEquivalent"]!["models"]!.AsArray().Select(model => model!).ToArray();
        Dictionary<string, string> pages = new(StringComparer.Ordinal)
        {
            ["anthropic"] = "https://platform.claude.com/docs/en/about-claude/pricing",
            ["openai"] = "https://developers.openai.com/api/docs/pricing",
            ["google"] = "https://ai.google.dev/gemini-api/docs/pricing",
            ["xai"] = "https://docs.x.ai/developers/pricing",
            ["moonshot"] = "https://platform.kimi.ai/docs/pricing/chat",
        };

        Assert.Equal(2, card["schemaVersion"]!.GetValue<int>());
        Assert.Equal(31, retail.Length);
        Assert.Equal(
            github.Where(id => !id.StartsWith("mai-", StringComparison.Ordinal)),
            retail.Select(model => model["id"]!.GetValue<string>()));
        Assert.All(retail, model =>
            Assert.Equal(pages[model["vendor"]!.GetValue<string>()], model["sourceUrl"]!.GetValue<string>()));
        Assert.Equal(
            ["mai-code-2", "mai-code-1.1-flash", "mai-code-2-flash"],
            github.Where(id => id.StartsWith("mai-", StringComparison.Ordinal)));
    }

    [Theory]
    [MemberData(nameof(Aliases))]
    public void AR4_VendorIdsAndSnapshotsAreAliases(string alias, string model, string start)
    {
        CreditCalculationResult result = Embedded.Calculate(TestSpans.Chat(
            startTime: DateTimeOffset.Parse(start, CultureInfo.InvariantCulture),
            requestModel: alias,
            inputTokens: 1_000,
            outputTokens: 100));

        Assert.Equal(PricingStatus.UnknownModel, result.Status);
        Assert.Equal(ApiEquivalentStatus.Priced, result.ApiEquivalent.Status);
        Assert.Equal(model, result.ApiEquivalent.RateCardModelId);
    }

    [Fact]
    public void AR5_TheBuiltInCardIsPinned()
    {
        Assert.Equal("2026-11-23", Embedded.RatesVersion);
        Assert.Equal(2, Embedded.SchemaVersion);
        Assert.Equal("634d783f8123f6aa8555220e830f7016b883083af08a043f29ca975a6b725045", Embedded.ApiRateCardSha256);
    }

    // Where the vendor's list price differs from GitHub's rate: Grok 4.5's cached input, and GPT-5.6 Luna's long-context
    // threshold (OpenAI: above 272,000 input tokens; GitHub: above 200,000).
    [Theory]
    [InlineData("grok-4.5", 150_000, 100_000)]
    [InlineData("gpt-5.6-luna", 200_001, 0)]
    public void AR6_ListPricesCanDifferFromGitHubsRates(string model, long input, long cacheRead)
    {
        NormalizedGenAiSpan span = TestSpans.Chat(
            requestModel: model,
            inputTokens: input,
            cacheReadInputTokens: cacheRead,
            outputTokens: 1_000);

        CreditCalculationResult result = Embedded.Calculate(span);

        Assert.Equal(PricingStatus.Priced, result.Status);
        Assert.Equal(ApiEquivalentStatus.Priced, result.ApiEquivalent.Status);
        if (model == "grok-4.5")
        {
            Assert.Equal("default", result.TierName);
            Assert.Equal(0.05m, result.Breakdown!.CacheReadUsd);
            Assert.Equal(0.03m, result.ApiEquivalent.Breakdown!.CacheReadUsd);
            Assert.Equal(0.156m, result.UsdCost);
            Assert.Equal(0.136m, result.ApiEquivalent.UsdCost);
        }
        else
        {
            Assert.Equal("long_context", result.TierName);
            Assert.Equal("default", result.ApiEquivalent.TierName);
            Assert.Equal(0.0818004m, result.UsdCost);
            Assert.Equal(0.0412002m, result.ApiEquivalent.UsdCost);
        }
    }
}
