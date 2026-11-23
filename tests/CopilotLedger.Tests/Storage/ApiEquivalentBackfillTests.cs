using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Storage;

public sealed class ApiEquivalentBackfillTests
{
    private const string ApiColumns =
        "api_equivalent_usd_pico, api_equivalent_status, api_rate_card_model_id, api_rates_version, api_rate_card_sha256";

    // Every column except the five api_* columns.
    private const string RecordedColumns = """
        id, trace_id, span_id, parent_span_id, operation_name, conversation_id, agent_name, provider_name, request_model,
        response_model, model_id, rate_card_model_id, tier_name, server_address, server_port, start_time_unix_ms,
        end_time_unix_ms, input_tokens, output_tokens, cache_read_input_tokens, cache_write_input_tokens, billing_channel,
        pricing_status, rates_version, cost_usd_pico, saved_usd_pico, repo_root, repo_slug, branch_name, head_commit_sha,
        recorded_at_unix_ms, cache_read_state, cache_write_state, pull_request_number, agent_span_id
        """;

    private static readonly JsonRateCardCalculator Retail =
        JsonRateCardCalculator.Parse(RateFixtures.RetailTestRatesJson);

    // Legacy spans get the results ingestion gives new ones; a dry run reports them and writes nothing.
    [Fact]
    public async Task AB1_Backfill_GivesLegacySpansTheResultsIngestionWould()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        await store.RecordAsync(ApiEquivalentStoreTests.Entries());
        await using SqliteConnection connection = await OpenAsync(temp);
        string[] ingested = await RowsAsync(connection, $"SELECT {ApiColumns} FROM spans ORDER BY id;");
        await ExecuteAsync(connection, $"UPDATE spans SET ({ApiColumns}) = (NULL, NULL, NULL, NULL, NULL);");
        string[] legacy = await RowsAsync(connection, "SELECT * FROM spans ORDER BY id;");
        string[] recorded = await RowsAsync(connection, $"SELECT {RecordedColumns} FROM spans ORDER BY id;");

        ApiEquivalentBackfill dryRun = await store.BackfillApiEquivalentAsync(Retail, dryRun: true);

        AssertBackfill(dryRun, 6, 4, 1.8757m, 1, 1, 0, 0);
        Assert.Equal(legacy, await RowsAsync(connection, "SELECT * FROM spans ORDER BY id;"));

        ApiEquivalentBackfill backfill = await store.BackfillApiEquivalentAsync(Retail, dryRun: false);

        AssertBackfill(backfill, 6, 4, 1.8757m, 1, 1, 0, 6);
        Assert.Equal(ingested, await RowsAsync(connection, $"SELECT {ApiColumns} FROM spans ORDER BY id;"));
        Assert.Equal(recorded, await RowsAsync(connection, $"SELECT {RecordedColumns} FROM spans ORDER BY id;"));
        AssertBackfill(await store.BackfillApiEquivalentAsync(Retail, dryRun: false), 0, 0, 0m, 0, 0, 0, 0);
    }

    // Only rows without a result change, and only in the api_* columns: they record the backfill's rates while
    // rates_version and every billed amount keep what was recorded.
    [Fact]
    public async Task AB2_Backfill_FillsOnlyRowsWithoutAResult()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        await store.RecordAsync(ApiEquivalentStoreTests.Entries());
        await using SqliteConnection connection = await OpenAsync(temp);
        await ExecuteAsync(
            connection,
            $"UPDATE spans SET ({ApiColumns}) = (NULL, NULL, NULL, NULL, NULL) WHERE span_id IN ('a000000000000001', 'a000000000000005');");
        string[] before = await RowsAsync(connection, $"SELECT {RecordedColumns} FROM spans ORDER BY id;");
        string[] untouched = await RowsAsync(
            connection,
            $"SELECT {ApiColumns} FROM spans WHERE api_equivalent_status IS NOT NULL ORDER BY id;");
        JsonRateCardCalculator builtIn = JsonRateCardCalculator.LoadEmbedded();

        ApiEquivalentBackfill backfill = await store.BackfillApiEquivalentAsync(builtIn, dryRun: false);

        AssertBackfill(backfill, 2, 1, 0.3m, 1, 0, 0, 2);
        Assert.Equal(before, await RowsAsync(connection, $"SELECT {RecordedColumns} FROM spans ORDER BY id;"));
        Assert.Equal(
            untouched,
            await RowsAsync(
                connection,
                $"SELECT {ApiColumns} FROM spans WHERE span_id NOT IN ('a000000000000001', 'a000000000000005') ORDER BY id;"));
        Assert.Equal(
            [
                $"a000000000000001|retail-fixture|null|unpriced|null|2026-11-23|{builtIn.ApiRateCardSha256}",
                $"a000000000000005|retail-fixture|300000000000|priced|gpt-6.1-sol|2026-11-23|{builtIn.ApiRateCardSha256}",
            ],
            await RowsAsync(
                connection,
                $"SELECT span_id, rates_version, {ApiColumns} FROM spans WHERE span_id IN ('a000000000000001', 'a000000000000005') ORDER BY id;"));
    }

    [Fact]
    public async Task AB3_Backfill_LeavesPricesBeyondTheRangeWithoutAResult()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        await store.RecordAsync(ApiEquivalentStoreTests.Entries());
        await using SqliteConnection connection = await OpenAsync(temp);
        await ExecuteAsync(
            connection,
            $"""
            UPDATE spans SET ({ApiColumns}) = (NULL, NULL, NULL, NULL, NULL), input_tokens = 10000000
            WHERE span_id = 'a000000000000002';
            """);
        JsonRateCardCalculator huge = JsonRateCardCalculator.Parse(
            RateFixtures.RetailTestRatesJson,
            """
            {
              "schemaVersion": 2,
              "apiEquivalent": {
                "models": [
                  { "id": "test-unknown", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1000000, "outputPer1M": 1000000 } ] } ] }
                ]
              }
            }
            """);

        ApiEquivalentBackfill backfill = await store.BackfillApiEquivalentAsync(huge, dryRun: false);

        AssertBackfill(backfill, 1, 0, 0m, 0, 0, 1, 0);
        Assert.Equal(
            1L,
            await ScalarAsync(connection, "SELECT COUNT(*) FROM spans WHERE api_equivalent_status IS NULL;"));
    }

    private static void AssertBackfill(
        ApiEquivalentBackfill backfill,
        long spans,
        long priced,
        decimal pricedUsd,
        long unpriced,
        long notApplicable,
        long outOfRange,
        long updated)
    {
        Assert.Equal(spans, backfill.SpanCount);
        Assert.Equal(priced, backfill.PricedSpanCount);
        Assert.Equal(pricedUsd, backfill.PricedUsd);
        Assert.Equal(unpriced, backfill.UnpricedSpanCount);
        Assert.Equal(notApplicable, backfill.NotApplicableSpanCount);
        Assert.Equal(outOfRange, backfill.OutOfRangeSpanCount);
        Assert.Equal(updated, backfill.UpdatedSpanCount);
    }

    private static async Task<SqliteLedgerStore> StoreAsync(TempDirectory temp)
    {
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "ledger.db"));
        await store.InitializeAsync();
        return store;
    }

    private static async Task<SqliteConnection> OpenAsync(TempDirectory temp)
    {
        SqliteConnection connection = new($"Data Source={Path.Combine(temp.Path, "ledger.db")}");
        await connection.OpenAsync();
        return connection;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    // Each row as its values, NULL as "null", separated by '|'.
    private static async Task<string[]> RowsAsync(SqliteConnection connection, string sql)
    {
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
