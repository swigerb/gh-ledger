namespace CopilotLedger.Core.Pricing;

internal sealed class RateCardDocument
{
    public int? SchemaVersion { get; init; }

    public string? RatesVersion { get; init; }

    public RateCardSource? Source { get; init; }

    public IReadOnlyList<string>? Notes { get; init; }

    public PricedRateSection? GithubAiCredits { get; init; }

    public FoundryRateSection? FoundryByok { get; init; }

    public LocalRateSection? LocalOffload { get; init; }

    public ApiEquivalentSection? ApiEquivalent { get; init; }   // schemaVersion 2 only
}

internal sealed class RateCardSource
{
    public string? Url { get; init; }

    public string? RetrievedOn { get; init; }
}

internal sealed class PricedRateSection
{
    public IReadOnlyList<string>? HostSuffixes { get; init; }

    public IReadOnlyList<PricedModelDocument>? Models { get; init; }
}

internal sealed class FoundryRateSection
{
    public IReadOnlyList<string>? HostSuffixes { get; init; }

    public IReadOnlyList<string>? ProviderNames { get; init; }

    public IReadOnlyList<PricedModelDocument>? Models { get; init; }
}

internal sealed class LocalRateSection
{
    public string? BaselineModel { get; init; }

    public IReadOnlyList<LocalModelDocument>? Models { get; init; }

    public IReadOnlyList<string>? AdditionalHosts { get; init; }

    public IReadOnlyList<string>? ProviderNames { get; init; }
}

internal sealed class ApiEquivalentSection
{
    public string? Description { get; init; }

    public string? LocalBaselineModel { get; init; }

    public IReadOnlyList<PricedModelDocument>? Models { get; init; }
}

internal sealed class PricedModelDocument
{
    public required string Id { get; init; }

    public string? Vendor { get; init; }      // apiEquivalent models only

    public string? SourceUrl { get; init; }   // apiEquivalent models only

    public IReadOnlyList<string> Aliases { get; init; } = [];

    public required IReadOnlyList<RateWindowDocument> Windows { get; init; }
}

internal sealed class RateWindowDocument
{
    public DateTimeOffset? EffectiveFrom { get; init; }

    public DateTimeOffset? EffectiveUntil { get; init; }

    public required IReadOnlyList<RateTierDocument> Tiers { get; init; }
}

internal sealed class RateTierDocument
{
    public required string Name { get; init; }

    public long? MaxInputTokens { get; init; }

    public required decimal InputPer1M { get; init; }

    public decimal? CachedInputPer1M { get; init; }

    public decimal? CacheWritePer1M { get; init; }

    public required decimal OutputPer1M { get; init; }
}

internal sealed class LocalModelDocument
{
    public required string Id { get; init; }

    public IReadOnlyList<string> Aliases { get; init; } = [];
}
