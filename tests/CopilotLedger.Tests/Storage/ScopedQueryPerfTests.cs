using System.Diagnostics;
using CopilotLedger.Core.Git;
using CopilotLedger.Mcp;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using ModelContextProtocol.Protocol;
using Xunit.Abstractions;

namespace CopilotLedger.Tests.Storage;

public sealed class ScopedQueryPerfTests(ITestOutputHelper output)
{
    [Fact]
    public async Task SQ1_LargeLedger_ScopedReadsTakeAtMostTwiceTheUnscoped()
    {
        if (Environment.GetEnvironmentVariable("GH_LEDGER_PERF") != "1")
        {
            return;
        }

        using TempDirectory temp = new();
        GitFixture.Create(temp.Path);
        string data = Path.Combine(temp.Path, "data");
        Directory.CreateDirectory(data);
        await LargeLedger.CreateAsync(Path.Combine(data, "ledger.db"));

        ManualTimeProvider clock = new(LargeLedger.Now, TimeZoneInfo.Utc);
        await using CliHarness cli = new(temp.Path, new FakeCliPlatform(), clock);

        long auditUnscoped = await MeasureAsync(
            "audit unscoped",
            () => cli.RunAsync("audit", "--since", "all", "--json"));
        long auditRepo = await MeasureAsync(
            "audit repo",
            () => cli.RunAsync(
                "audit",
                "--since",
                "all",
                "--json",
                "--repo",
                LargeLedger.GhLedgerRepository));
        long exportUnscoped = await MeasureAsync(
            "export unscoped",
            () => cli.RunAsync(
                "export",
                "--all-repos",
                "--since",
                "all",
                "--bundle",
                "--out",
                "all.html"));
        long exportRepo = await MeasureAsync(
            "export repo",
            () => cli.RunAsync(
                "export",
                "--repo",
                LargeLedger.GhLedgerRepository,
                "--since",
                "all",
                "--bundle",
                "--out",
                "repo.html"));
        long exportConversation = await MeasureAsync(
            "export conversation",
            () => cli.RunAsync(
                "export",
                "--all-repos",
                "--conversation",
                LargeLedger.Conversation,
                "--since",
                "all",
                "--bundle",
                "--out",
                "conv.html"));

        await using McpTestHarness mcp = await McpTestHarness.StartAsync(new LedgerMcpContext
        {
            DatabasePath = Path.Combine(data, "ledger.db"),
            WorkingDirectory = Path.Combine(temp.Path, "repo"),
            GetEnvironmentVariable = _ => null,
            GitResolver = new FileSystemGitContextResolver(ceilingDirectory: temp.Path),
            TimeProvider = clock,
            Version = "9.9.9",
        });
        long mcpDefault = await MeasureMcpAsync(mcp);

        Assert.True(auditRepo <= 2 * auditUnscoped + 1000);
        Assert.True(exportRepo <= 2 * exportUnscoped + 1000);
        Assert.True(exportConversation <= 2 * exportUnscoped + 1000);
        Assert.True(mcpDefault < 5000);
    }

    [Fact]
    public async Task SQ2_ScopedToolCallReads_AreExactAndBounded()
    {
        using TempDirectory temp = new();
        string databasePath = Path.Combine(temp.Path, "ledger.db");
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
                1000000000, 0, $r, 'main', $st, 'reported', 'absent', 'otlp', NULL)
            RETURNING id;
            """;
        span.Parameters.Add("$t", SqliteType.Text);
        span.Parameters.Add("$s", SqliteType.Text);
        span.Parameters.Add("$c", SqliteType.Text);
        span.Parameters.Add("$st", SqliteType.Integer);
        span.Parameters.Add("$r", SqliteType.Text);
        span.Prepare();

        await using SqliteCommand link = connection.CreateCommand();
        link.Transaction = transaction;
        link.CommandText =
            """
            INSERT INTO span_links (trace_id, span_id, operation_name, conversation_id, start_time_unix_ms, recorded_at_unix_ms, tool_name, tool_exit_code, status_code, owner_span_row_id)
            VALUES ($t, $s, 'execute_tool', $c, $st, $st, 'view', 0, 0, NULL);
            """;
        link.Parameters.Add("$t", SqliteType.Text);
        link.Parameters.Add("$s", SqliteType.Text);
        link.Parameters.Add("$c", SqliteType.Text);
        link.Parameters.Add("$st", SqliteType.Integer);
        link.Prepare();

        long @base = new DateTimeOffset(
            2026,
            11,
            1,
            0,
            0,
            0,
            TimeSpan.Zero).ToUnixTimeMilliseconds();
        (int Trace, int Spans, int Calls, string Conversation, string Repository)[] traces =
        [
            (0, 8000, 8000, "conv-big", "contoso/big"),
            (1, 10, 15, "conv-small", "swigerb/gh-ledger"),
        ];

        foreach ((int t, int spans, int calls, string conversation, string repository) in traces)
        {
            string trace = t.ToString("x32");
            long start = @base + t * 3_600_000L;
            span.Parameters["$t"].Value = trace;
            span.Parameters["$c"].Value = conversation;
            span.Parameters["$r"].Value = repository;
            for (int i = 0; i < spans; i++)
            {
                span.Parameters["$s"].Value = (i + 1).ToString("x16");
                span.Parameters["$st"].Value = start + i * 100L;
                await span.ExecuteNonQueryAsync();
            }

            link.Parameters["$t"].Value = trace;
            link.Parameters["$c"].Value = conversation;
            for (int j = 0; j < calls; j++)
            {
                link.Parameters["$s"].Value = "a" + j.ToString("x15");
                link.Parameters["$st"].Value = start + j * 60L + 30;
                await link.ExecuteNonQueryAsync();
            }
        }

        await transaction.CommitAsync();
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();
        DateTimeOffset generatedAt = new(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
        await AssertReadAsync(
            snapshot,
            new LedgerFilter { ConversationId = "conv-small" },
            15,
            10,
            generatedAt,
            bounded: true);
        await AssertReadAsync(
            snapshot,
            new LedgerFilter { Repository = "swigerb/gh-ledger" },
            15,
            10,
            generatedAt,
            bounded: true);
        await AssertReadAsync(
            snapshot,
            new LedgerFilter(),
            8015,
            8010,
            generatedAt,
            bounded: false);
    }

    private static async Task AssertReadAsync(
        LedgerExportSnapshot snapshot,
        LedgerFilter filter,
        long expectedCalls,
        int expectedSpans,
        DateTimeOffset generatedAt,
        bool bounded)
    {
        Stopwatch countTimer = Stopwatch.StartNew();
        long calls = await snapshot.CountToolCallsAsync(filter);
        countTimer.Stop();
        Assert.Equal(expectedCalls, calls);
        if (bounded)
        {
            Assert.True(countTimer.Elapsed < TimeSpan.FromSeconds(3));
        }

        Stopwatch auditTimer = Stopwatch.StartNew();
        CopilotLedger.Core.Audit.AuditInput input =
            await snapshot.GetAuditInputAsync(filter, generatedAt);
        auditTimer.Stop();
        Assert.Equal(expectedSpans, input.Spans.Count);
        Assert.Equal(expectedCalls, input.ToolCalls.Count);
        if (bounded)
        {
            Assert.True(auditTimer.Elapsed < TimeSpan.FromSeconds(3));
        }
    }

    private async Task<long> MeasureAsync(string label, Func<Task<CliRun>> run)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        CliRun result = await run();
        stopwatch.Stop();
        output.WriteLine($"{label,-28} {result.ExitCode} {stopwatch.ElapsedMilliseconds} ms");
        Assert.Equal(0, result.ExitCode);
        return stopwatch.ElapsedMilliseconds;
    }

    private async Task<long> MeasureMcpAsync(McpTestHarness mcp)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        CallToolResult result = await mcp.Client.CallToolAsync(
            "get_audit_findings",
            new Dictionary<string, object?>());
        stopwatch.Stop();
        int exitCode = result.IsError == true ? 1 : 0;
        output.WriteLine($"{"mcp get_audit_findings default",-28} {exitCode} {stopwatch.ElapsedMilliseconds} ms");
        Assert.NotEqual(true, result.IsError);
        return stopwatch.ElapsedMilliseconds;
    }
}
