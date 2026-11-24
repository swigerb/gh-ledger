using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Storage;

public sealed class SchemaVersion6Tests
{
    private const string ServiceRequestId = "1f0e9c2a-7b3d-4e5f-8a9b-0c1d2e3f4a5b";
    private const string Canonical = "e60cf189bf4c90ec992e7763f23f3af37a01a61852a1e3ec40acaf1618ab6ae6";

    // Every spans column of a version 5 ledger.
    private const string Version5Columns = """
        id, trace_id, span_id, parent_span_id, operation_name, conversation_id, agent_name, provider_name, request_model,
        response_model, model_id, rate_card_model_id, tier_name, server_address, server_port, start_time_unix_ms,
        end_time_unix_ms, input_tokens, output_tokens, cache_read_input_tokens, cache_write_input_tokens, billing_channel,
        pricing_status, rates_version, cost_usd_pico, saved_usd_pico, repo_root, repo_slug, branch_name, head_commit_sha,
        recorded_at_unix_ms, cache_read_state, cache_write_state, pull_request_number, agent_span_id,
        api_equivalent_usd_pico, api_equivalent_status, api_rate_card_model_id, api_rates_version, api_rate_card_sha256
        """;

    private const string InsertRowSql = """
        INSERT INTO spans (trace_id, span_id, operation_name, start_time_unix_ms, end_time_unix_ms, input_tokens,
                           output_tokens, cache_read_input_tokens, cache_write_input_tokens, billing_channel, pricing_status,
                           rates_version, cost_usd_pico, saved_usd_pico, recorded_at_unix_ms, ingest_source, canonical_usage_id)
        VALUES (@p0, @p1, 'chat', 0, 0, 0, 0, 0, 0, 'github_ai_credits', 'priced', 'test', 0, 0, 0, @p2, @p3);
        """;

    private static readonly JsonRateCardCalculator Calculator = JsonRateCardCalculator.LoadEmbedded();

    public static TheoryData<string?, string?, string?, string?> LiveIdentities => new()
    {
        { "github", ServiceRequestId, null, Canonical },
        { "github", null, null, null },
        { "openai", ServiceRequestId, null, null },
        { "github", ServiceRequestId, "contoso.openai.azure.com", null },
    };

    [Fact]
    public async Task SV1_Migration6_KeepsEveryRowAsAnEffectiveUnidentifiedLiveRow()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore first = new(path);
        await first.InitializeAsync();
        await first.RecordAsync([Entry("github", ServiceRequestId, null), Entry("openai", null, null)]);
        await first.RecordSpanLinksAsync(
        [
            new GenAiSpanLink
            {
                TraceId = new string('6', 32),
                SpanId = new string('6', 16),
                OperationName = "invoke_agent",
                StartTime = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero),
            },
        ]);
        await LedgerSql.ExecuteAsync(path, """
            DROP TABLE import_record_aliases;
            DROP TABLE import_sessions;
            DROP INDEX ux_spans_effective_import_usage;
            DROP INDEX ix_spans_canonical_usage;
            ALTER TABLE span_links DROP COLUMN owner_span_row_id;
            ALTER TABLE spans DROP COLUMN superseded_by;
            ALTER TABLE spans DROP COLUMN imported_tool_call_count;
            ALTER TABLE spans DROP COLUMN accounting_state;
            ALTER TABLE spans DROP COLUMN canonical_usage_id;
            ALTER TABLE spans DROP COLUMN ingest_source;
            PRAGMA user_version = 5;
            """);
        IReadOnlyList<string> before = await LedgerSql.RowsAsync(path, $"SELECT {Version5Columns} FROM spans ORDER BY id;");

        // A new process starts with no pooled connections; see SL8.
        SqliteConnection.ClearAllPools();
        await new SqliteLedgerStore(path).InitializeAsync();

        Assert.Equal(6L, await LedgerSql.ScalarAsync(path, "PRAGMA user_version;"));
        Assert.Equal(2, before.Count);
        Assert.Equal(before, await LedgerSql.RowsAsync(path, $"SELECT {Version5Columns} FROM spans ORDER BY id;"));
        Assert.Equal(
            ["otlp|effective|null|null|null", "otlp|effective|null|null|null"],
            await LedgerSql.RowsAsync(
                path,
                """
                SELECT ingest_source, accounting_state, canonical_usage_id, superseded_by, imported_tool_call_count
                FROM spans ORDER BY id;
                """));
        Assert.Equal(["null"], await LedgerSql.RowsAsync(path, "SELECT owner_span_row_id FROM span_links;"));
        Assert.Equal(0L, await LedgerSql.ScalarAsync(path, "SELECT COUNT(*) FROM import_sessions;"));
        Assert.Equal(0L, await LedgerSql.ScalarAsync(path, "SELECT COUNT(*) FROM import_record_aliases;"));
    }

    [Theory]
    [MemberData(nameof(LiveIdentities))]
    public async Task SV2_OnlyGitHubBilledLiveSpansWithAServiceRequestIdAreIdentified(
        string? provider,
        string? serviceRequestId,
        string? host,
        string? expected)
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path);
        await store.InitializeAsync();

        Assert.Equal(1, await store.RecordAsync([Entry(provider, serviceRequestId, host)]));

        Assert.Equal(
            [$"otlp|{expected ?? "null"}|effective"],
            await LedgerSql.RowsAsync(path, "SELECT ingest_source, canonical_usage_id, accounting_state FROM spans;"));
    }

    [Fact]
    public async Task SV3_ChecksRejectAnUnknownSourceAndASupersededLiveRow()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path);
        await store.InitializeAsync();
        await store.RecordAsync([Entry("github", ServiceRequestId, null)]);

        SqliteException source = await Assert.ThrowsAsync<SqliteException>(() => LedgerSql.ExecuteAsync(
            path,
            InsertRowSql,
            new string('a', 32),
            new string('a', 16),
            "import:other",
            null));
        SqliteException superseded = await Assert.ThrowsAsync<SqliteException>(() => LedgerSql.ExecuteAsync(
            path,
            "UPDATE spans SET accounting_state = 'superseded', superseded_by = id WHERE ingest_source = 'otlp';"));

        Assert.Equal(19, source.SqliteErrorCode);
        Assert.Equal(19, superseded.SqliteErrorCode);
        Assert.Equal(
            ["otlp|effective|null"],
            await LedgerSql.RowsAsync(path, "SELECT ingest_source, accounting_state, superseded_by FROM spans;"));
    }

    [Fact]
    public async Task SV4_OnlyEffectiveImportRowsMustHaveDistinctCanonicalIds()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        await new SqliteLedgerStore(path).InitializeAsync();
        await LedgerSql.ExecuteAsync(path, InsertRowSql, new string('1', 32), new string('1', 16), "import:copilot", Canonical);

        SqliteException duplicate = await Assert.ThrowsAsync<SqliteException>(() => LedgerSql.ExecuteAsync(
            path,
            InsertRowSql,
            new string('2', 32),
            new string('2', 16),
            "import:codex",
            Canonical));
        await LedgerSql.ExecuteAsync(
            path,
            "UPDATE spans SET accounting_state = 'superseded', superseded_by = 99 WHERE trace_id = @p0;",
            new string('1', 32));
        await LedgerSql.ExecuteAsync(path, InsertRowSql, new string('2', 32), new string('2', 16), "import:codex", Canonical);
        await LedgerSql.ExecuteAsync(path, InsertRowSql, new string('3', 32), new string('3', 16), "otlp", Canonical);
        await LedgerSql.ExecuteAsync(path, InsertRowSql, new string('4', 32), new string('4', 16), "otlp", Canonical);

        Assert.Equal(19, duplicate.SqliteErrorCode);
        Assert.Equal(2067, duplicate.SqliteExtendedErrorCode);
        Assert.Equal(
            ["import:copilot|superseded", "import:codex|effective", "otlp|effective", "otlp|effective"],
            await LedgerSql.RowsAsync(
                path,
                "SELECT ingest_source, accounting_state FROM spans WHERE canonical_usage_id = @p0 ORDER BY id;",
                Canonical));
    }

    private static LedgerEntry Entry(string? provider, string? serviceRequestId, string? host)
    {
        NormalizedGenAiSpan span = TestSpans.Chat(
            providerName: provider,
            serverAddress: host,
            requestModel: "mai-code-2",
            inputTokens: 1_000,
            outputTokens: 10) with
        {
            ServiceRequestId = serviceRequestId,
        };
        return new LedgerEntry(span, Calculator.Calculate(span), null);
    }
}
