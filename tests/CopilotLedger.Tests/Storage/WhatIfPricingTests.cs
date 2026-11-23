using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Storage;

public sealed class WhatIfPricingTests
{
    private const string Legacy =
        "UPDATE spans SET (api_equivalent_usd_pico, api_equivalent_status, api_rate_card_model_id, api_rates_version, api_rate_card_sha256) = (NULL, NULL, NULL, NULL, NULL);";

    private static readonly JsonRateCardCalculator Retail =
        JsonRateCardCalculator.Parse(RateFixtures.RetailTestRatesJson);

    // Pricing again with the card the spans were recorded with gives back exactly the recorded rollup.
    [Theory]
    [InlineData(LedgerGrouping.Repository)]
    [InlineData(LedgerGrouping.Branch)]
    [InlineData(LedgerGrouping.Model)]
    [InlineData(LedgerGrouping.Agent)]
    [InlineData(LedgerGrouping.ChannelModel)]
    public async Task WI1_TheRecordingCard_ReproducesTheRecordedRollup(LedgerGrouping grouping)
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);

        Assert.Equal(
            await store.GetRollupAsync(grouping, LedgerFilter.All),
            await store.GetRollupAsync(grouping, LedgerFilter.All, Retail));
    }

    // Another card changes only the API-equivalent fields.
    [Fact]
    public async Task WI2_AnotherCard_ChangesOnlyTheApiEquivalentFields()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        IReadOnlyList<LedgerRollupRow> recorded = await store.GetRollupAsync(LedgerGrouping.Model, LedgerFilter.All);

        IReadOnlyList<LedgerRollupRow> whatIf = await store.GetRollupAsync(
            LedgerGrouping.Model,
            LedgerFilter.All,
            JsonRateCardCalculator.LoadEmbedded());

        Assert.Equal(
            [
                recorded[0] with { ApiEquivalentUsd = 0.3m, ApiEquivalentUnpricedSpanCount = 0, ApiEquivalentEstimatedSpanCount = 1 },
                recorded[1] with { ApiEquivalentUsd = null, ApiEquivalentUnpricedSpanCount = 3, ApiEquivalentEstimatedSpanCount = 0 },
                recorded[2] with { ApiEquivalentUsd = null, ApiEquivalentUnpricedSpanCount = 1, ApiEquivalentEstimatedSpanCount = 0 },
                recorded[3] with { ApiEquivalentUsd = null, ApiEquivalentUnpricedSpanCount = 1, ApiEquivalentEstimatedSpanCount = 0 },
            ],
            whatIf);
        Assert.Equal(["gpt-6.1-sol", "test-retail", "qwen-local", "test-unknown"], whatIf.Select(row => row.Model));
    }

    // Spans recorded without a result are priced too, and nothing is written.
    [Fact]
    public async Task WI3_LegacySpans_ArePricedWithoutWriting()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        await RowsAsync(temp, Legacy);
        string[] before = await RowsAsync(temp, "SELECT * FROM spans ORDER BY id;");

        LedgerRollupRow recorded = Assert.Single(
            await store.GetRollupAsync(LedgerGrouping.Repository, LedgerFilter.All));
        LedgerRollupRow whatIf = Assert.Single(
            await store.GetRollupAsync(LedgerGrouping.Repository, LedgerFilter.All, Retail));
        IReadOnlyList<PromptChainRow> chains =
            await store.GetTopPromptChainsAsync(LedgerFilter.All, 10, Retail);

        Assert.Null(recorded.ApiEquivalentUsd);
        Assert.Equal(6, recorded.ApiEquivalentUnpricedSpanCount);
        Assert.Equal(
            recorded with { ApiEquivalentUsd = 1.8757m, ApiEquivalentUnpricedSpanCount = 2, ApiEquivalentEstimatedSpanCount = 3 },
            whatIf);
        Assert.Collection(
            chains,
            row => AssertChain(row, "a3a3a3a3a3a3a3a3a3a3a3a3a3a3a3a3", 0.3045m, 0, 2),
            row => AssertChain(row, "a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1", 1.56m, 1, 0),
            row => AssertChain(row, "a2a2a2a2a2a2a2a2a2a2a2a2a2a2a2a2", 0.0112m, 1, 1));
        Assert.Equal(before, await RowsAsync(temp, "SELECT * FROM spans ORDER BY id;"));
    }

    // The chains, their order, and every recorded amount don't depend on the card.
    [Fact]
    public async Task WI4_PromptChains_KeepTheirOrderAndRecordedAmounts()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        LedgerFilter filter = new() { Repository = "(none)" };

        IReadOnlyList<PromptChainRow> recorded = await store.GetTopPromptChainsAsync(filter, 2);
        IReadOnlyList<PromptChainRow> whatIf =
            await store.GetTopPromptChainsAsync(filter, 2, JsonRateCardCalculator.LoadEmbedded());

        Assert.Equal(recorded.Select(Recorded), whatIf.Select(Recorded));
        Assert.Collection(
            recorded,
            row => AssertChain(row, "a3a3a3a3a3a3a3a3a3a3a3a3a3a3a3a3", 0.3045m, 0, 2),
            row => AssertChain(row, "a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1", 1.56m, 1, 0));
        Assert.Collection(
            whatIf,
            row => AssertChain(row, "a3a3a3a3a3a3a3a3a3a3a3a3a3a3a3a3", 0.3m, 1, 1),
            row => AssertChain(row, "a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1", null, 2, 0));
    }

    // What-if pricing falls back to the request model as ingestion does, so the recording card still gives back the
    // recorded rollup.
    [Fact]
    public async Task WI5_TheRequestModelFallback_MatchesIngestion()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        NormalizedGenAiSpan span = TestSpans.Chat(
            serverAddress: "llm.example.com",
            requestModel: "test-retail",
            responseModel: "test-retail-preview",
            inputTokens: 1_000,
            outputTokens: 100);
        await store.RecordAsync([new LedgerEntry(span, Retail.Calculate(span), null)]);

        IReadOnlyList<LedgerRollupRow> recorded = await store.GetRollupAsync(LedgerGrouping.Model, LedgerFilter.All);
        IReadOnlyList<LedgerRollupRow> whatIf =
            await store.GetRollupAsync(LedgerGrouping.Model, LedgerFilter.All, Retail);

        Assert.Equal(recorded, whatIf);
        LedgerRollupRow preview = Assert.Single(whatIf, row => row.Model == "test-retail-preview");
        Assert.Equal(0.0045m, preview.ApiEquivalentUsd);
        Assert.Equal(0, preview.ApiEquivalentUnpricedSpanCount);
    }

    private static string Recorded(PromptChainRow row) =>
        string.Join(
            '|',
            row.TraceId,
            string.Join(',', row.Agents),
            string.Join(',', row.Models),
            row.SpanCount,
            row.UnpricedSpanCount,
            row.InputTokens,
            row.OutputTokens,
            row.AiCredits,
            row.AiCreditsUsd,
            row.FoundryByokUsd,
            row.LocalSavedUsd);

    private static void AssertChain(
        PromptChainRow row,
        string traceId,
        decimal? usd,
        long unpriced,
        long estimated)
    {
        Assert.Equal(traceId, row.TraceId);
        Assert.Equal(usd, row.ApiEquivalentUsd);
        Assert.Equal(unpriced, row.ApiEquivalentUnpricedSpanCount);
        Assert.Equal(estimated, row.ApiEquivalentEstimatedSpanCount);
    }

    private static async Task<SqliteLedgerStore> StoreAsync(TempDirectory temp)
    {
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "ledger.db"));
        await store.InitializeAsync();
        await store.RecordAsync(ApiEquivalentStoreTests.Entries());
        return store;
    }

    // Each row as its values, NULL as "null", separated by '|'.
    private static async Task<string[]> RowsAsync(TempDirectory temp, string sql)
    {
        await using SqliteConnection connection = new($"Data Source={Path.Combine(temp.Path, "ledger.db")}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        List<string> rows = [];
        while (await reader.ReadAsync())
        {
            rows.Add(string.Join(
                '|',
                Enumerable.Range(0, reader.FieldCount)
                    .Select(index => reader.IsDBNull(index)
                        ? "null"
                        : Convert.ToString(reader.GetValue(index), System.Globalization.CultureInfo.InvariantCulture))));
        }

        return [.. rows];
    }
}
