namespace CopilotLedger.Storage;

// What SqliteLedgerStore.BackfillApiEquivalentAsync found: the spans recorded without an API-equivalent result, by the
// result the rate card gave them.
public sealed record ApiEquivalentBackfill
{
    public required long SpanCount { get; init; }               // spans without a result before the backfill
    public required long PricedSpanCount { get; init; }
    public required decimal PricedUsd { get; init; }            // the priced spans at API list prices
    public required long UnpricedSpanCount { get; init; }
    public required long NotApplicableSpanCount { get; init; }
    public required long OutOfRangeSpanCount { get; init; }     // priced beyond the pico-USD range; left without a result
    public required long UpdatedSpanCount { get; init; }        // rows written; 0 for a dry run
}
