using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Storage;

public sealed class SchemaVersion7Tests
{
    private static readonly JsonRateCardCalculator Calculator = JsonRateCardCalculator.LoadEmbedded();

    [Fact]
    public async Task SV7_1_Migration7_AddsColumnsAndIndexWithoutChangingFormat6Rows()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        await CreateFormat6Async(path);
        await InsertFormat6SpanAsync(path, 473_886_000);
        await InsertFormat6CheckpointAsync(path);

        await new SqliteLedgerStore(path).InitializeAsync();

        Assert.Equal(7L, await LedgerSql.ScalarAsync(path, "PRAGMA user_version;"));
        Assert.Equal(
            ["1|rate_card|473886000"],
            await LedgerSql.RowsAsync(
                path,
                "SELECT list_cost_usd_pico IS NULL, charge_source, cost_usd_pico FROM spans;"));
        Assert.Equal(1L, await LedgerSql.ScalarAsync(
            path,
            "SELECT complete FROM import_sessions WHERE source = 'copilot' AND log_id = 'session-a';"));
        Assert.Equal(0L, await LedgerSql.ScalarAsync(
            path,
            "SELECT import_revision FROM import_sessions WHERE source = 'copilot' AND log_id = 'session-a';"));
        Assert.Equal(
            "ix_span_links_import_span",
            await LedgerSql.ScalarAsync(
                path,
                "SELECT name FROM sqlite_master WHERE type = 'index' AND name = 'ix_span_links_import_span';"));
    }

    [Fact]
    public async Task SV7_2_StoreWritesChargedAndListAmountsAndChargeSource()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path);
        await store.InitializeAsync();
        NormalizedGenAiSpan span = TestSpans.Chat(requestModel: "claude-haiku-4.5", inputTokens: 1_000, outputTokens: 100);
        CreditCalculationResult calculation = Calculator.Calculate(span) with
        {
            UsdCost = 0.000473886m,
            ListUsdCost = 0.000527m,
            ChargeSource = ChargeSource.Reported,
            Channel = BillingChannel.GitHubAiCredits,
            Status = PricingStatus.Priced,
        };

        Assert.Equal(1, await store.RecordAsync([new LedgerEntry(span, calculation, null)]));

        Assert.Equal(
            ["473886000|527000000|reported"],
            await LedgerSql.RowsAsync(path, "SELECT cost_usd_pico, list_cost_usd_pico, charge_source FROM spans;"));
    }

    [Fact]
    public async Task SV7_3_DatabaseRejectsAReportedChargeForALocalSpan()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        await new SqliteLedgerStore(path).InitializeAsync();

        SqliteException exception = await Assert.ThrowsAsync<SqliteException>(() => LedgerSql.ExecuteAsync(
            path,
            """
            INSERT INTO spans (
                trace_id, span_id, operation_name, start_time_unix_ms, end_time_unix_ms,
                input_tokens, output_tokens, cache_read_input_tokens, cache_write_input_tokens,
                billing_channel, pricing_status, rates_version, cost_usd_pico, saved_usd_pico,
                recorded_at_unix_ms, charge_source
            ) VALUES (
                'trace', 'span', 'chat', 0, 0, 0, 0, 0, 0,
                'local', 'priced', 'test', 0, 0, 0, 'reported'
            );
            """));

        Assert.Equal(19, exception.SqliteErrorCode);
    }

    [Fact]
    public async Task SV7_4_StoreRejectsAReportedChargeForALocalSpan()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path);
        await store.InitializeAsync();
        NormalizedGenAiSpan span = TestSpans.Chat(requestModel: "claude-haiku-4.5");
        CreditCalculationResult calculation = Calculator.Calculate(span) with
        {
            Channel = BillingChannel.Local,
            Status = PricingStatus.LocalOffload,
            ChargeSource = ChargeSource.Reported,
        };

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => store.RecordAsync([new LedgerEntry(span, calculation, null)]));

        Assert.Equal(
            "A reported charge applies only to priced GitHub AI-credit requests. (Parameter 'entries')",
            exception.Message);
    }

    [Fact]
    public async Task SV7_5_CheckpointRoundTripsItsImportRevision()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path);
        await store.InitializeAsync();
        ImportSessionCheckpoint checkpoint = Checkpoint(importRevision: 1);

        await store.RecordImportedAsync([], checkpoint);

        Assert.Equal(1, (await store.GetImportSessionAsync(checkpoint.Source, checkpoint.LogId))!.ImportRevision);
    }

    [Fact]
    public async Task SV7_6_SnapshotReadsAFormat6CheckpointAsRevisionZeroWithoutMigrating()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        await CreateFormat6Async(path);
        await InsertFormat6CheckpointAsync(path);
        SqliteConnection.ClearAllPools();

        await using LedgerSnapshot snapshot = await LedgerSnapshot.OpenAsync(path);

        ImportSessionCheckpoint? checkpoint = await snapshot.GetImportSessionAsync(ImportSourceKind.Copilot, "session-a");
        Assert.Equal(0, checkpoint!.ImportRevision);
        Assert.Equal(6L, await LedgerSql.ScalarAsync(path, "PRAGMA user_version;"));
    }

    [Fact]
    public async Task SV7_7_AFormat6DaemonInsertUsesTheMigrationDefaults()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        await new SqliteLedgerStore(path).InitializeAsync();

        await InsertFormat6SpanAsync(path, 100);

        Assert.Equal(
            ["1|rate_card"],
            await LedgerSql.RowsAsync(path, "SELECT list_cost_usd_pico IS NULL, charge_source FROM spans;"));
    }

    private static ImportSessionCheckpoint Checkpoint(int importRevision) => new()
    {
        Source = ImportSourceKind.Copilot,
        LogId = "session-a",
        LogSizeBytes = 10,
        LogLastWriteTime = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero),
        LogSha256 = new string('a', 64),
        Complete = true,
        Recorded = 0,
        Duplicates = 0,
        OverlapSkipped = 0,
        AmbiguousOverlap = 0,
        ImportRevision = importRevision,
    };

    private static async Task CreateFormat6Async(string path)
    {
        await new SqliteLedgerStore(path).InitializeAsync();
        await LedgerSql.ExecuteAsync(path, """
            DROP INDEX IF EXISTS ix_spans_conversation_live;
            DROP INDEX ix_span_links_import_span;
            ALTER TABLE spans DROP COLUMN list_cost_usd_pico;
            ALTER TABLE spans DROP COLUMN charge_source;
            ALTER TABLE import_sessions DROP COLUMN import_revision;
            PRAGMA user_version = 6;
            """);
        SqliteConnection.ClearAllPools();
    }

    private static Task<int> InsertFormat6SpanAsync(string path, long costUsdPico) =>
        LedgerSql.ExecuteAsync(
            path,
            """
            INSERT INTO spans (
                trace_id, span_id, operation_name, start_time_unix_ms, end_time_unix_ms,
                input_tokens, output_tokens, cache_read_input_tokens, cache_write_input_tokens,
                billing_channel, pricing_status, rates_version, cost_usd_pico, saved_usd_pico, recorded_at_unix_ms
            ) VALUES (
                'trace', 'span', 'chat', 0, 0, 0, 0, 0, 0,
                'github_ai_credits', 'priced', 'test', @p0, 0, 0
            );
            """,
            costUsdPico);

    private static Task<int> InsertFormat6CheckpointAsync(string path) =>
        LedgerSql.ExecuteAsync(
            path,
            """
            INSERT INTO import_sessions (
                source, log_id, log_size_bytes, log_last_write_unix_ms, log_sha256, complete,
                records_recorded, records_duplicate, records_overlap, records_ambiguous, imported_at_unix_ms
            ) VALUES (
                'copilot', 'session-a', 10, 0, 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
                1, 0, 0, 0, 0, 0
            );
            """);
}
