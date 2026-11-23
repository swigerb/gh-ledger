namespace CopilotLedger.Core.Pricing;

// The stored facts that the API-equivalent lens prices: a recorded request, or one being recorded.
public sealed record ApiEquivalentRequest
{
    public required string OperationName { get; init; }
    public required BillingChannel Channel { get; init; }    // Local requests are priced as the card's local retail baseline
    public string? ModelId { get; init; }                    // CreditCalculationResult.ModelId; null when the request named no model
    public string? RequestModel { get; init; }               // as reported; priced when the card lacks ModelId
    public required DateTimeOffset StartTime { get; init; }
    public required long InputTokens { get; init; }
    public required long OutputTokens { get; init; }
    public required long CacheReadInputTokens { get; init; }
    public required long CacheWriteInputTokens { get; init; }
}
