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

    [Fact]
    public async Task SQ3_UnscopedToolCallPaging_IsExactAndLinear()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = await LargeLedger.CreateAsync(path);

        LedgerFilter[] filters =
        [
            LedgerFilter.All,
            new LedgerFilter { Repository = LargeLedger.GhLedgerRepository },
            new LedgerFilter { ConversationId = LargeLedger.Conversation },
        ];
        string[] scopes =
        [
            "1 = 1",
            $"repo_slug = '{LargeLedger.GhLedgerRepository}'",
            $"conversation_id = '{LargeLedger.Conversation}'",
        ];
        int[] expectedRows = [75_000, 37_500, 300];

        List<List<(string TraceId, string SpanId)>> references = new();
        await using (SqliteConnection connection = new($"Data Source={path}"))
        {
            await connection.OpenAsync();
            for (int i = 0; i < scopes.Length; i++)
            {
                await using SqliteCommand command = connection.CreateCommand();
                command.CommandText =
                    $"""
                    SELECT trace_id, span_id FROM span_links
                    WHERE operation_name = 'execute_tool'
                      AND trace_id IN (SELECT trace_id FROM spans WHERE accounting_state = 'effective' AND {scopes[i]})
                    ORDER BY start_time_unix_ms, trace_id, span_id;
                    """;
                await using SqliteDataReader reader = await command.ExecuteReaderAsync();
                List<(string TraceId, string SpanId)> rows = new();
                while (await reader.ReadAsync())
                {
                    rows.Add((reader.GetString(0), reader.GetString(1)));
                }

                references.Add(rows);
            }
        }

        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();
        int[] order = [0, 1, 2, 0];
        foreach (int i in order)
        {
            List<(string TraceId, string SpanId)> actual = await PageToolCallsAsync(snapshot, filters[i]);
            Assert.Equal(references[i], actual);
            Assert.Equal(references[i].Count, await snapshot.CountToolCallsAsync(filters[i]));
            Assert.Equal(expectedRows[i], actual.Count);
        }

        long firstSpans = 0;
        long firstTools = 0;
        long secondSpans = 0;
        long secondTools = 0;
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<(string TraceId, string SpanId)> spans = await PageSpansAsync(snapshot);
        firstSpans = stopwatch.ElapsedMilliseconds;
        stopwatch.Restart();
        List<(string TraceId, string SpanId)> tools = await PageToolCallsAsync(snapshot, LedgerFilter.All);
        firstTools = stopwatch.ElapsedMilliseconds;
        stopwatch.Restart();
        spans = await PageSpansAsync(snapshot);
        secondSpans = stopwatch.ElapsedMilliseconds;
        stopwatch.Restart();
        tools = await PageToolCallsAsync(snapshot, LedgerFilter.All);
        secondTools = stopwatch.ElapsedMilliseconds;
        long spansMs = Math.Min(firstSpans, secondSpans);
        long toolsMs = Math.Min(firstTools, secondTools);

        output.WriteLine($"{"page spans unscoped",-28} {spansMs} ms");
        output.WriteLine($"{"page tool calls unscoped",-28} {toolsMs} ms");
        Assert.Equal(50_000, spans.Count);
        Assert.Equal(75_000, tools.Count);
        Assert.True(toolsMs <= 2 * spansMs + 250);
    }

    private static async Task<List<(string TraceId, string SpanId)>> PageSpansAsync(
        LedgerExportSnapshot snapshot)
    {
        List<(string TraceId, string SpanId)> rows = new();
        ExportCursor? after = null;
        while (true)
        {
            IReadOnlyList<ExportSpanRow> page =
                await snapshot.GetSpansAsync(LedgerFilter.All, after, 1000);
            rows.AddRange(page.Select(row => (row.TraceId, row.SpanId)));
            if (page.Count < 1000)
            {
                return rows;
            }

            ExportSpanRow last = page[^1];
            after = new ExportCursor(last.StartTime.ToUnixTimeMilliseconds(), last.TraceId, last.SpanId);
        }
    }

    private static async Task<List<(string TraceId, string SpanId)>> PageToolCallsAsync(
        LedgerExportSnapshot snapshot,
        LedgerFilter filter)
    {
        List<(string TraceId, string SpanId)> rows = new();
        ExportCursor? after = null;
        while (true)
        {
            IReadOnlyList<ExportToolCallRow> page =
                await snapshot.GetToolCallsAsync(filter, after, 1000);
            rows.AddRange(page.Select(row => (row.TraceId, row.SpanId)));
            if (page.Count < 1000)
            {
                return rows;
            }

            ExportToolCallRow last = page[^1];
            after = new ExportCursor(last.StartTime.ToUnixTimeMilliseconds(), last.TraceId, last.SpanId);
        }
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
