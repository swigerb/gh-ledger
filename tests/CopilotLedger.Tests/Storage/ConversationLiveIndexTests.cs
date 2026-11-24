using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Storage;

public sealed class ConversationLiveIndexTests
{
    private static readonly JsonRateCardCalculator Calculator = JsonRateCardCalculator.LoadEmbedded();
    private const string IndexRow = "ix_spans_conversation_live|0|c|1";

    [Fact]
    public async Task LX1_ANewLedger_HasTheIndex()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");

        await new SqliteLedgerStore(path).InitializeAsync();

        await AssertIndexAsync(path);
        Assert.Equal(7L, await LedgerSql.ScalarAsync(path, "PRAGMA user_version;"));
    }

    [Fact]
    public async Task LX2_AFormat6Ledger_UpgradesTo7AndGainsTheIndex()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path);
        await store.InitializeAsync();
        NormalizedGenAiSpan span = TestSpans.Chat(conversationId: "c-1");
        await store.RecordAsync([new LedgerEntry(span, Calculator.Calculate(span), null)]);
        await LedgerSql.ExecuteAsync(
            path,
            """
            DROP INDEX ix_spans_conversation_live;
            DROP INDEX ix_span_links_import_span;
            ALTER TABLE spans DROP COLUMN list_cost_usd_pico;
            ALTER TABLE spans DROP COLUMN charge_source;
            ALTER TABLE import_sessions DROP COLUMN import_revision;
            PRAGMA user_version = 6;
            """);
        SqliteConnection.ClearAllPools();

        await new SqliteLedgerStore(path).InitializeAsync();

        Assert.Equal(7L, await LedgerSql.ScalarAsync(path, "PRAGMA user_version;"));
        await AssertIndexRowAsync(path);
        Assert.Equal(1L, await LedgerSql.ScalarAsync(path, "SELECT COUNT(*) FROM spans;"));
        Assert.Equal(
            "rate_card",
            await LedgerSql.ScalarAsync(path, "SELECT charge_source FROM spans;"));
    }

    [Fact]
    public async Task LX3_ASprint12Format7Ledger_GainsTheIndex()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path);
        await store.InitializeAsync();
        NormalizedGenAiSpan span = TestSpans.Chat(conversationId: "c-1");
        await store.RecordAsync([new LedgerEntry(span, Calculator.Calculate(span), null)]);
        await LedgerSql.ExecuteAsync(path, "DROP INDEX ix_spans_conversation_live;");

        await new SqliteLedgerStore(path).InitializeAsync();

        Assert.Equal(7L, await LedgerSql.ScalarAsync(path, "PRAGMA user_version;"));
        await AssertIndexRowAsync(path);
        Assert.Equal(1L, await LedgerSql.ScalarAsync(path, "SELECT COUNT(*) FROM spans;"));
    }

    [Fact]
    public async Task LX4_OpeningIsIdempotentAndConcurrentOpensAgree()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");

        await Task.WhenAll(
            new SqliteLedgerStore(path).InitializeAsync(),
            new SqliteLedgerStore(path).InitializeAsync());
        await new SqliteLedgerStore(path).InitializeAsync();

        Assert.Equal(
            1L,
            await LedgerSql.ScalarAsync(
                path,
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'ix_spans_conversation_live';"));
        Assert.Equal(7L, await LedgerSql.ScalarAsync(path, "PRAGMA user_version;"));
    }

    [Fact]
    public async Task LX5_TheImportLookups_UseTheIndex()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        await new SqliteLedgerStore(path).InitializeAsync();

        IReadOnlyList<string> rows = await LedgerSql.RowsAsync(
            path,
            """
            EXPLAIN QUERY PLAN
            SELECT EXISTS (
                     SELECT 1 FROM spans
                      WHERE conversation_id = 'c' AND ingest_source = 'otlp'
                   ),
                   EXISTS (
                     SELECT 1 FROM spans
                      WHERE conversation_id = 'c' AND ingest_source = 'otlp' AND canonical_usage_id IS NULL
                   );
            """);
        string plan = string.Join('\n', rows.Select(row => row.Split('|')[3]));

        Assert.Contains(
            "USING COVERING INDEX ix_spans_conversation_live (conversation_id=?)",
            plan,
            StringComparison.Ordinal);
        Assert.Contains(
            "USING COVERING INDEX ix_spans_conversation_live (conversation_id=? AND canonical_usage_id=?)",
            plan,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task LX6_ADryRun_NeverCreatesTheIndex_AndDecidesTheSame()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path);
        new CopilotSessionLogs("s-a")
            .Usage("u-1", "r-1")
            .Usage("u-2", "r-2")
            .Write(harness.Context.CopilotHome);
        SqliteLedgerStore store = new(harness.Context.DatabasePath, harness.TimeProvider);
        await store.InitializeAsync();
        NormalizedGenAiSpan span = TestSpans.Chat(conversationId: "s-a");
        await store.RecordAsync([new LedgerEntry(span, Calculator.Calculate(span), null)]);
        await LedgerSql.ExecuteAsync(
            harness.Context.DatabasePath,
            "DROP INDEX ix_spans_conversation_live;");

        CliRun first = await harness.RunAsync("import", "--all", "--dry-run", "--json");

        Assert.Equal(
            0L,
            await LedgerSql.ScalarAsync(
                harness.Context.DatabasePath,
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'ix_spans_conversation_live';"));
        await new SqliteLedgerStore(harness.Context.DatabasePath).InitializeAsync();

        CliRun second = await harness.RunAsync("import", "--all", "--dry-run", "--json");

        Assert.Equal(first.ExitCode, second.ExitCode);
        Assert.Equal(first.Out, second.Out);
        Assert.Equal(
            1L,
            await LedgerSql.ScalarAsync(
                harness.Context.DatabasePath,
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'ix_spans_conversation_live';"));
    }

    private static async Task AssertIndexAsync(string path)
    {
        await AssertIndexRowAsync(path);
        Assert.Equal(
            ["conversation_id", "canonical_usage_id"],
            await LedgerSql.RowsAsync(
                path,
                "SELECT name FROM pragma_index_info('ix_spans_conversation_live') ORDER BY seqno;"));
        Assert.Equal(
            "CREATE INDEX ix_spans_conversation_live ON spans (conversation_id, canonical_usage_id) WHERE ingest_source = 'otlp'",
            await LedgerSql.ScalarAsync(
                path,
                "SELECT sql FROM sqlite_master WHERE name = 'ix_spans_conversation_live';"));
    }

    private static async Task AssertIndexRowAsync(string path) =>
        Assert.Equal(
            [IndexRow],
            await LedgerSql.RowsAsync(
                path,
                """
                SELECT name || '|' || "unique" || '|' || origin || '|' || partial
                  FROM pragma_index_list('spans')
                 WHERE name = 'ix_spans_conversation_live';
                """));
}
