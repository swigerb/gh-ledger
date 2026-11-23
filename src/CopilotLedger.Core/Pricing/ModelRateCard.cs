namespace CopilotLedger.Core.Pricing;

public sealed record RateTier(
    string Name,
    long? MaxInputTokens,              // inclusive bound on InputTokens; null = unbounded (last tier only)
    decimal InputPer1M,
    decimal? CachedInputPer1M,         // null bills cache reads at InputPer1M
    decimal? CacheWritePer1M,          // null bills cache writes at InputPer1M
    decimal OutputPer1M);

public sealed record RateWindow(
    DateTimeOffset? EffectiveFrom,     // inclusive; null = unbounded
    DateTimeOffset? EffectiveUntil,    // exclusive; null = unbounded
    IReadOnlyList<RateTier> Tiers);

public sealed record ModelRateCard(
    string ModelId,                    // canonical normalized id
    BillingChannel Channel,            // GitHubAiCredits or FoundryByok; Unpriced for an apiEquivalent (retail) model
    IReadOnlyList<string> Aliases,     // normalized
    IReadOnlyList<RateWindow> Windows);
