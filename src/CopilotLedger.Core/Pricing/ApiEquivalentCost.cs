namespace CopilotLedger.Core.Pricing;

// A request priced at API list prices. It's a comparison: it never changes the request's billed amounts.
public sealed record ApiEquivalentCost
{
    public required ApiEquivalentStatus Status { get; init; }
    public required string RatesVersion { get; init; }       // the rate card that judged the request
    public required string RateCardSha256 { get; init; }     // that card's JsonRateCardCalculator.ApiRateCardSha256
    public string? RateCardModelId { get; init; }            // the apiEquivalent model applied; null unless Priced
    public string? TierName { get; init; }                   // null unless Priced
    public TokenCostBreakdown? Breakdown { get; init; }      // null unless Priced
    public decimal? UsdCost => Breakdown?.TotalUsd;          // null unless Priced, never 0 for unknown
}
