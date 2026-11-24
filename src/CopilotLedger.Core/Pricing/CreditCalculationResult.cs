namespace CopilotLedger.Core.Pricing;

public sealed record TokenCostBreakdown(
    decimal UncachedInputUsd,
    decimal CacheReadUsd,
    decimal CacheWriteUsd,
    decimal OutputUsd)
{
    public decimal TotalUsd => UncachedInputUsd + CacheReadUsd + CacheWriteUsd + OutputUsd;
}

public sealed record CreditCalculationResult
{
    public required BillingChannel Channel { get; init; }
    public required PricingStatus Status { get; init; }
    public required string RatesVersion { get; init; }
    public string? ModelId { get; init; }                 // canonical id if a rate card matched, else the normalized effective model
    public string? RateCardModelId { get; init; }         // rate card applied (the baseline for Local); null if none
    public string? TierName { get; init; }                // set only when a tier was applied
    public long UncachedInputTokens { get; init; }
    public TokenCostBreakdown? Breakdown { get; init; }   // priced at RateCardModelId's rates; null if nothing was priced
    public decimal UsdCost { get; init; }                 // charged USD: the reported charge when ChargeSource is Reported, else the rate card's
    public decimal ListUsdCost { get; init; }             // the rate card's billed USD for this request, whatever was charged
    public ChargeSource ChargeSource { get; init; }       // where UsdCost came from
    public decimal AiCredits { get; init; }               // UsdCost / 0.01m when Channel is GitHubAiCredits; otherwise 0
    public decimal EstimatedCloudSavedUsd { get; init; }  // Local only: Breakdown.TotalUsd; otherwise 0
    public required ApiEquivalentCost ApiEquivalent { get; init; }   // the same request at API list prices
}
