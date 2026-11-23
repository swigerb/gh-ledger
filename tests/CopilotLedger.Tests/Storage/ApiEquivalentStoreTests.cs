using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Storage;

public sealed class ApiEquivalentStoreTests
{
    private const string T1 = "a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1";
    private const string T2 = "a2a2a2a2a2a2a2a2a2a2a2a2a2a2a2a2";
    private const string T3 = "a3a3a3a3a3a3a3a3a3a3a3a3a3a3a3a3";
    private static readonly JsonRateCardCalculator Retail =
        JsonRateCardCalculator.Parse(RateFixtures.RetailTestRatesJson);

    // Every outcome of the API-equivalent lens, as stored: the rate card's version and hash on every row, and NULL money for
    // the spans it couldn't price.
    [Fact]
    public async Task AS1_RecordAsync_StoresEachApiEquivalentOutcome()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);

        Assert.Equal(6, await store.RecordAsync(Entries()));

        await using SqliteConnection connection = await OpenAsync(temp);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT span_id, billing_channel, pricing_status, api_equivalent_usd_pico, api_equivalent_status,
                   api_rate_card_model_id, api_rates_version, api_rate_card_sha256
            FROM spans
            ORDER BY span_id;
            """;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        object?[][] expected =
        [
            ["a000000000000001", "github_ai_credits", "priced", 1_560_000_000_000L, "priced", "test-retail"],
            ["a000000000000002", "github_ai_credits", "unknown_model", null, "unpriced", null],
            ["a000000000000003", "unpriced", "invalid_usage", null, "not_applicable", null],
            ["a000000000000004", "local", "local_offload", 11_200_000_000L, "priced", "test-baseline"],
            ["a000000000000005", "foundry_byok", "priced", 300_000_000_000L, "priced", "gpt-6.1-sol"],
            ["a000000000000006", "unpriced", "unrecognized_endpoint", 4_500_000_000L, "priced", "test-retail"],
        ];
        foreach (object?[] row in expected)
        {
            Assert.True(await reader.ReadAsync());
            Assert.Equal(row[0], reader.GetString(0));
            Assert.Equal(row[1], reader.GetString(1));
            Assert.Equal(row[2], reader.GetString(2));
            Assert.Equal(row[3], reader.IsDBNull(3) ? null : reader.GetInt64(3));
            Assert.Equal(row[4], reader.GetString(4));
            Assert.Equal(row[5], reader.IsDBNull(5) ? null : reader.GetString(5));
            Assert.Equal("retail-fixture", reader.GetString(6));
            Assert.Equal(Retail.ApiRateCardSha256, reader.GetString(7));
        }

        Assert.False(await reader.ReadAsync());
        Assert.Equal(
            2L,
            await ScalarAsync(connection, "SELECT COUNT(*) FROM spans WHERE api_equivalent_usd_pico IS NULL;"));
        Assert.Equal(
            0L,
            await ScalarAsync(connection, "SELECT COUNT(*) FROM spans WHERE api_equivalent_usd_pico = 0;"));
    }

    [Fact]
    public async Task AS2_Rollups_SumPricedSpansAndCountTheRest()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        await store.RecordAsync(Entries());

        Assert.Collection(
            await store.GetRollupAsync(LedgerGrouping.Model, LedgerFilter.All),
            row => AssertRetail(row.Model, row, "gpt-6.1-sol", 0.3m, 0, 1),
            row => AssertRetail(row.Model, row, "test-retail", 1.5645m, 1, 1),
            row => AssertRetail(row.Model, row, "qwen-local", 0.0112m, 0, 1),
            row => AssertRetail(row.Model, row, "test-unknown", null, 1, 0));
        LedgerRollupRow total = Assert.Single(
            await store.GetRollupAsync(LedgerGrouping.Repository, LedgerFilter.All));
        AssertRetail(total.Repository, total, "(none)", 1.8757m, 2, 3);
        Assert.Equal(0.2725m, total.AiCreditsUsd);
        Assert.Equal(27.25m, total.AiCredits);
        Assert.Equal(0.315m, total.FoundryByokUsd);
        Assert.Equal(0.0075m, total.LocalSavedUsd);
    }

    [Fact]
    public async Task AS3_PromptChains_CarryApiEquivalentTotals()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        await store.RecordAsync(Entries());

        Assert.Collection(
            await store.GetTopPromptChainsAsync(LedgerFilter.All, 10),
            row => AssertRetail(row.TraceId, row, T3, 0.3045m, 0, 2),
            row => AssertRetail(row.TraceId, row, T1, 1.56m, 1, 0),
            row => AssertRetail(row.TraceId, row, T2, 0.0112m, 1, 1));
    }

    // A version 4 ledger keeps every row and value; its rows have no API-equivalent price until a backfill.
    [Fact]
    public async Task AS4_Migration5_KeepsEveryRowAndLeavesLegacyRowsUnpriced()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore first = await StoreAsync(temp);
        await first.RecordAsync(Entries());
        await using (SqliteConnection connection = await OpenAsync(temp))
        {
            await ExecuteAsync(connection, """
                ALTER TABLE spans DROP COLUMN api_equivalent_usd_pico;
                ALTER TABLE spans DROP COLUMN api_equivalent_status;
                ALTER TABLE spans DROP COLUMN api_rate_card_model_id;
                ALTER TABLE spans DROP COLUMN api_rates_version;
                ALTER TABLE spans DROP COLUMN api_rate_card_sha256;
                PRAGMA user_version = 4;
                """);
        }

        string[] before;
        await using (SqliteConnection connection = await OpenAsync(temp))
        {
            before = await RowsAsync(connection, "SELECT * FROM spans ORDER BY id;");
        }

        // A new process starts with no pooled connections; see SL8.
        SqliteConnection.ClearAllPools();
        SqliteLedgerStore store = new(DatabasePath(temp));
        await store.InitializeAsync();

        await using SqliteConnection check = await OpenAsync(temp);
        Assert.Equal(5L, await ScalarAsync(check, "PRAGMA user_version;"));
        Assert.Equal(6, before.Length);
        Assert.Equal(
            before,
            await RowsAsync(
                check,
                """
                SELECT * FROM (
                    SELECT id, trace_id, span_id, parent_span_id, operation_name, conversation_id, agent_name, provider_name,
                           request_model, response_model, model_id, rate_card_model_id, tier_name, server_address, server_port,
                           start_time_unix_ms, end_time_unix_ms, input_tokens, output_tokens, cache_read_input_tokens,
                           cache_write_input_tokens, billing_channel, pricing_status, rates_version, cost_usd_pico,
                           saved_usd_pico, repo_root, repo_slug, branch_name, head_commit_sha, recorded_at_unix_ms,
                           cache_read_state, cache_write_state, pull_request_number, agent_span_id
                    FROM spans)
                ORDER BY id;
                """));
        Assert.Equal(
            6L,
            await ScalarAsync(
                check,
                """
                SELECT COUNT(*) FROM spans
                WHERE api_equivalent_usd_pico IS NULL AND api_equivalent_status IS NULL AND api_rate_card_model_id IS NULL
                  AND api_rates_version IS NULL AND api_rate_card_sha256 IS NULL;
                """));
        LedgerRollupRow total = Assert.Single(
            await store.GetRollupAsync(LedgerGrouping.Repository, LedgerFilter.All));
        AssertRetail(total.Repository, total, "(none)", null, 6, 0);
        Assert.Equal(0.2725m, total.AiCreditsUsd);
        Assert.Equal(0.315m, total.FoundryByokUsd);
        Assert.Equal(0.0075m, total.LocalSavedUsd);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task AS5_RecordAsync_RejectsInconsistentApiEquivalentCosts(int caseNumber)
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        LedgerEntry[] entries = Entries();
        LedgerEntry priced = entries[0];
        LedgerEntry unpriced = entries[1];
        ApiEquivalentCost cost = priced.Calculation.ApiEquivalent;
        ApiEquivalentCost invalid = caseNumber switch
        {
            0 => cost with { Breakdown = null },
            1 => cost with { RateCardModelId = null },
            2 => unpriced.Calculation.ApiEquivalent with { Breakdown = cost.Breakdown },
            3 => cost with { Status = (ApiEquivalentStatus)7 },
            _ => null!,
        };
        LedgerEntry entry = priced with { Calculation = priced.Calculation with { ApiEquivalent = invalid } };

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => store.RecordAsync([unpriced, entry]));

        Assert.Equal(
            "An API-equivalent cost needs its rates, and a model and breakdown exactly when it's Priced. (Parameter 'entries')",
            exception.Message);
        Assert.Empty(await store.GetRollupAsync(LedgerGrouping.Repository, LedgerFilter.All));
    }

    // T1: a GitHub request priced in both lenses, and an unknown model. T2: inconsistent token counts, and a local request
    // priced as the card's local baseline. T3: a Foundry BYOK request, and an unrecognized endpoint whose model has a list
    // price. Only the first request reported cache counters.
    private static LedgerEntry[] Entries() =>
    [
        Entry(T1, "a000000000000001", TestSpans.Chat(
            requestModel: "test-retail",
            inputTokens: 300_000,
            cacheReadInputTokens: 100_000,
            cacheWriteInputTokens: 50_000,
            outputTokens: 10_000)),
        Entry(T1, "a000000000000002", TestSpans.Chat(
            requestModel: "test-unknown",
            inputTokens: 1_000,
            outputTokens: 100)),
        Entry(T2, "a000000000000003", TestSpans.Chat(
            requestModel: "test-retail",
            inputTokens: 1_000,
            cacheReadInputTokens: 2_000,
            outputTokens: 100)),
        Entry(T2, "a000000000000004", TestSpans.Chat(
            providerName: "ollama",
            requestModel: "qwen-local",
            inputTokens: 10_000,
            outputTokens: 1_000)),
        Entry(T3, "a000000000000005", TestSpans.Chat(
            serverAddress: "contoso.openai.azure.com",
            requestModel: "contoso-gpt61-prod",
            inputTokens: 100_000,
            outputTokens: 10_000)),
        Entry(T3, "a000000000000006", TestSpans.Chat(
            serverAddress: "llm.example.com",
            requestModel: "test-retail",
            inputTokens: 1_000,
            outputTokens: 100)),
    ];

    private static LedgerEntry Entry(string trace, string span, NormalizedGenAiSpan template)
    {
        NormalizedGenAiSpan value = template with { TraceId = trace, SpanId = span };
        return new LedgerEntry(value, Retail.Calculate(value), null);
    }

    private static void AssertRetail(
        string? key,
        LedgerRollupRow row,
        string expectedKey,
        decimal? usd,
        long unpriced,
        long estimated)
    {
        Assert.Equal(expectedKey, key);
        Assert.Equal(usd, row.ApiEquivalentUsd);
        Assert.Equal(unpriced, row.ApiEquivalentUnpricedSpanCount);
        Assert.Equal(estimated, row.ApiEquivalentEstimatedSpanCount);
    }

    private static void AssertRetail(
        string key,
        PromptChainRow row,
        string expectedKey,
        decimal? usd,
        long unpriced,
        long estimated)
    {
        Assert.Equal(expectedKey, key);
        Assert.Equal(usd, row.ApiEquivalentUsd);
        Assert.Equal(unpriced, row.ApiEquivalentUnpricedSpanCount);
        Assert.Equal(estimated, row.ApiEquivalentEstimatedSpanCount);
    }

    private static async Task<SqliteLedgerStore> StoreAsync(TempDirectory temp)
    {
        SqliteLedgerStore store = new(DatabasePath(temp));
        await store.InitializeAsync();
        return store;
    }

    private static string DatabasePath(TempDirectory temp) => Path.Combine(temp.Path, "ledger.db");

    private static async Task<SqliteConnection> OpenAsync(TempDirectory temp)
    {
        SqliteConnection connection = new($"Data Source={DatabasePath(temp)}");
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
