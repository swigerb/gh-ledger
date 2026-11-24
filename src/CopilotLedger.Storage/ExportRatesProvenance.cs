namespace CopilotLedger.Storage;

public sealed record ExportApiRateCard(string RatesVersion, string Sha256);

// The rate cards that priced an export's selection.
public sealed record ExportRatesProvenance
{
    public required IReadOnlyList<string> RatesVersions { get; init; }              // distinct rates_version, ordinal
    public required IReadOnlyList<ExportApiRateCard> ApiRateCards { get; init; }    // distinct (api_rates_version, api_rate_card_sha256), ordinal
}
