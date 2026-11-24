using CopilotLedger.Storage;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.TestSupport;

public static class LargeLedger
{
    public const string BigRepository = "contoso/big";
    public const string GhLedgerRepository = "swigerb/copilot-ledger";
    public const string Conversation = "conv-0003";
    public static readonly DateTimeOffset Base = new(2026, 11, 20, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    public static async Task<SqliteLedgerStore> CreateAsync(string databasePath)
    {
        SqliteLedgerStore store = new(databasePath);
        await store.InitializeAsync();

        await using SqliteConnection connection = new($"Data Source={databasePath}");
        await connection.OpenAsync();
        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync();

        await using SqliteCommand span = connection.CreateCommand();
        span.Transaction = transaction;
        span.CommandText =
            """
            INSERT INTO spans (trace_id, span_id, operation_name, conversation_id, request_model, model_id, start_time_unix_ms, end_time_unix_ms,
                input_tokens, output_tokens, cache_read_input_tokens, cache_write_input_tokens, billing_channel, pricing_status, rates_version,
                cost_usd_pico, saved_usd_pico, repo_slug, branch_name, recorded_at_unix_ms, cache_read_state, cache_write_state,
                ingest_source, imported_tool_call_count)
            VALUES ($t, $s, 'chat', $c, 'claude-sonnet-4.6', 'claude-sonnet-4.6', $st, $st + 50, 60000, 200, 57000, 0, 'github_ai_credits', 'priced', '2026-11-23',
                1000000000, 0, $r, 'main', $st, 'reported', 'absent', $src, $n)
            RETURNING id;
            """;
        span.Parameters.Add("$t", SqliteType.Text);
        span.Parameters.Add("$s", SqliteType.Text);
        span.Parameters.Add("$c", SqliteType.Text);
        span.Parameters.Add("$st", SqliteType.Integer);
        span.Parameters.Add("$r", SqliteType.Text);
        span.Parameters.Add("$src", SqliteType.Text);
        span.Parameters.Add("$n", SqliteType.Integer);
        span.Prepare();

        await using SqliteCommand link = connection.CreateCommand();
        link.Transaction = transaction;
        link.CommandText =
            """
            INSERT INTO span_links (trace_id, span_id, operation_name, conversation_id, start_time_unix_ms, recorded_at_unix_ms, tool_name, tool_exit_code, status_code, owner_span_row_id)
            VALUES ($t, $s, 'execute_tool', $c, $st, $st, 'view', 0, 0, $o);
            """;
        link.Parameters.Add("$t", SqliteType.Text);
        link.Parameters.Add("$s", SqliteType.Text);
        link.Parameters.Add("$c", SqliteType.Text);
        link.Parameters.Add("$st", SqliteType.Integer);
        link.Parameters.Add("$o", SqliteType.Integer);
        link.Prepare();

        for (int t = 0; t < 192; t++)
        {
            int spans = t < 2 ? 6000 : 200;
            int calls = t < 2 ? 9000 : 300;
            string trace = t.ToString("x32");
            string conversation = $"conv-{t:D4}";
            string repository = t % 2 == 0 ? BigRepository : GhLedgerRepository;
            bool imported = t % 2 == 0;
            long start = Base.ToUnixTimeMilliseconds() + t * 1_800_000L;
            long[] ids = new long[spans];

            span.Parameters["$t"].Value = trace;
            span.Parameters["$c"].Value = conversation;
            span.Parameters["$r"].Value = repository;
            span.Parameters["$src"].Value = imported ? "import:copilot" : "otlp";

            for (int i = 0; i < spans; i++)
            {
                span.Parameters["$s"].Value = (i + 1).ToString("x16");
                span.Parameters["$st"].Value = start + i * 100L;
                span.Parameters["$n"].Value =
                    imported ? (i % 2 == 0 ? 1 : 2) : DBNull.Value;
                ids[i] = (long)(await span.ExecuteScalarAsync())!;
            }

            link.Parameters["$t"].Value = trace;
            link.Parameters["$c"].Value = conversation;
            for (int j = 0; j < calls; j++)
            {
                link.Parameters["$s"].Value = "a" + j.ToString("x15");
                link.Parameters["$st"].Value = start + j * 60L + 30;
                link.Parameters["$o"].Value =
                    imported ? ids[(int)((long)j * spans / calls)] : DBNull.Value;
                await link.ExecuteNonQueryAsync();
            }
        }

        await transaction.CommitAsync();
        return store;
    }
}
