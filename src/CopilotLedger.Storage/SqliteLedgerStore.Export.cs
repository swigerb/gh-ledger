using Microsoft.Data.Sqlite;

namespace CopilotLedger.Storage;

// The row queries of gh ledger export, which LedgerExportSnapshot runs in its read transaction. Spans, coverage counts,
// provenance, and roots read effective rows (FilterSql); tool calls, breaker trips, and budgets belong to them.
public sealed partial class SqliteLedgerStore
{
    // ExportSpanRow's columns, read by ordinal in ReadExportSpan.
    private const string ExportSpanColumnsSql = """
        trace_id, span_id, parent_span_id, operation_name, conversation_id, agent_name, provider_name, request_model,
               response_model, model_id, start_time_unix_ms, end_time_unix_ms, input_tokens, output_tokens,
               cache_read_input_tokens, cache_write_input_tokens, cache_read_state, cache_write_state, billing_channel,
               pricing_status, rates_version, cost_usd_pico, saved_usd_pico, api_equivalent_usd_pico, api_equivalent_status,
               api_rates_version, api_rate_card_sha256, repo_slug, repo_root, branch_name, head_commit_sha, pull_request_number,
               ingest_source
        """;

    // ExportToolCallRow's columns, read by ordinal in ReadExportToolCall. A live link has no owner row.
    private const string ExportToolCallColumnsSql = """
        trace_id, span_id, conversation_id, agent_name, tool_name, start_time_unix_ms, tool_exit_code, status_code,
               COALESCE((SELECT owner.ingest_source FROM spans AS owner WHERE owner.id = span_links.owner_span_row_id), 'otlp')
        """;

    // The execute_tool links that count, start in the filter's period, and belong to a prompt chain with a selected span. The
    // subquery is uncorrelated, so SQLite evaluates it once per statement and looks each link's trace up in its result; a
    // correlated EXISTS tested FilterSql on every span of every unselected trace, once per link (#83). Unqualified columns
    // are span_links' outside the subquery and spans' inside it.
    private const string ExportToolCallWhereSql = $"""
        operation_name = 'execute_tool'
          AND {EffectiveLinkSql}
          AND (@from IS NULL OR start_time_unix_ms >= @from)
          AND (@to IS NULL OR start_time_unix_ms < @to)
          AND span_links.trace_id IN (SELECT trace_id FROM spans WHERE {FilterSql})
        """;

    // Rows after the cursor in (start time, trace ID, span ID) order. The plain lower bound on start_time_unix_ms lets every
    // page seek ix_spans_start_time or ix_span_links_start instead of scanning and sorting the whole table; the first page's
    // cursor is the period's start, or the earliest time, with empty IDs, which sorts before every row (ReadExportRowsAsync).
    private const string AfterCursorSql = """
        start_time_unix_ms >= @after_start
          AND (start_time_unix_ms, trace_id, span_id) > (@after_start, @after_trace, @after_span)
        """;

    internal static Task<IReadOnlyList<ExportSpanRow>> ReadExportSpansAsync(
        SqliteTransaction snapshot,
        LedgerFilter filter,
        ExportCursor? after,
        int limit,
        CancellationToken cancellationToken) =>
        ReadExportRowsAsync(
            snapshot,
            $"""
            SELECT {ExportSpanColumnsSql}
            FROM spans
            WHERE {FilterSql}
              AND {AfterCursorSql}
            ORDER BY start_time_unix_ms, trace_id, span_id
            LIMIT @limit;
            """,
            filter,
            after,
            limit,
            ReadExportSpan,
            cancellationToken);

    internal static Task<IReadOnlyList<ExportSpanRow>> ReadLatestExportSpansAsync(
        SqliteTransaction snapshot,
        LedgerFilter filter,
        int limit,
        CancellationToken cancellationToken) =>
        ReadExportRowsAsync(
            snapshot,
            $"""
            SELECT {ExportSpanColumnsSql}
            FROM spans
            WHERE {FilterSql}
            ORDER BY start_time_unix_ms DESC, trace_id DESC, span_id DESC
            LIMIT @limit;
            """,
            filter,
            null,
            limit,
            ReadExportSpan,
            cancellationToken);

    internal static Task<IReadOnlyList<ExportToolCallRow>> ReadExportToolCallsAsync(
        SqliteTransaction snapshot,
        LedgerFilter filter,
        ExportCursor? after,
        int limit,
        CancellationToken cancellationToken) =>
        ReadExportRowsAsync(
            snapshot,
            $"""
            SELECT {ExportToolCallColumnsSql}
            FROM span_links
            WHERE {ExportToolCallWhereSql}
              AND {AfterCursorSql}
            ORDER BY start_time_unix_ms, trace_id, span_id
            LIMIT @limit;
            """,
            filter,
            after,
            limit,
            ReadExportToolCall,
            cancellationToken);

    internal static Task<IReadOnlyList<ExportToolCallRow>> ReadLatestExportToolCallsAsync(
        SqliteTransaction snapshot,
        LedgerFilter filter,
        int limit,
        CancellationToken cancellationToken) =>
        ReadExportRowsAsync(
            snapshot,
            $"""
            SELECT {ExportToolCallColumnsSql}
            FROM span_links
            WHERE {ExportToolCallWhereSql}
            ORDER BY start_time_unix_ms DESC, trace_id DESC, span_id DESC
            LIMIT @limit;
            """,
            filter,
            null,
            limit,
            ReadExportToolCall,
            cancellationToken);

    internal static async Task<long> CountExportToolCallsAsync(
        SqliteTransaction snapshot,
        LedgerFilter filter,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = snapshot.Connection!.CreateCommand();
        command.Transaction = snapshot;
        command.CommandText = $"SELECT COUNT(*) FROM span_links WHERE {ExportToolCallWhereSql};";
        AddFilterParameters(command, filter);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    // The start of the earliest span link in the whole ledger, of any operation or owner, or null when there's none.
    internal static async Task<DateTimeOffset?> ReadEarliestLinkStartAsync(
        SqliteTransaction snapshot,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = snapshot.Connection!.CreateCommand();
        command.Transaction = snapshot;
        command.CommandText = "SELECT MIN(start_time_unix_ms) FROM span_links;";
        return await command.ExecuteScalarAsync(cancellationToken) is long start
            ? DateTimeOffset.FromUnixTimeMilliseconds(start)
            : null;
    }

    // A superseded row that a live span of the same call replaced (the same canonical usage ID) counts when that live row is
    // selected: it's the same request, counted once. One that live spans of its session replaced without matching it may be a
    // call that no live span covers, so it counts when the filter selects it by its own columns, as gh ledger import reports
    // it. An imported request lacks the detail of each tool call it made beyond the execute_tool links it owns: import keeps
    // the links of only the tool calls that started within the retention, and that completed. Each owner's links are counted
    // once, in a grouped join, because owner_span_row_id has no index to look them up by. An imported request whose log gives
    // no tool-call count may be missing tool calls without a trace: every Codex request (a rollout records no tool calls), and
    // a Copilot usage record whose numToolCalls is missing or invalid. A known count, 0 included, is data; Copilot's reader
    // records 0 for a compaction, which makes no tool calls, so a compaction that an earlier build imported without a count
    // counts too. import_sessions has no repository, so its two counts are the whole ledger's.
    internal static async Task<ExportCoverageRow> ReadExportCoverageAsync(
        SqliteTransaction snapshot,
        LedgerFilter filter,
        DateTimeOffset toolHistoryFrom,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = snapshot.Connection!.CreateCommand();
        command.Transaction = snapshot;
        command.CommandText = $"""
            SELECT ingest_source, COUNT(*) FROM spans WHERE {FilterSql} GROUP BY ingest_source ORDER BY ingest_source;

            SELECT COUNT(*) FROM spans AS superseded JOIN spans AS live ON live.id = superseded.superseded_by
            WHERE superseded.accounting_state = 'superseded'
              AND live.canonical_usage_id = superseded.canonical_usage_id
              AND live.id IN (SELECT id FROM spans WHERE {FilterSql});

            SELECT COUNT(*) FROM spans
            WHERE {SpanFilterSql}
              AND accounting_state = 'superseded'
              AND NOT EXISTS (SELECT 1 FROM spans AS live
                              WHERE live.id = spans.superseded_by AND live.canonical_usage_id = spans.canonical_usage_id);

            SELECT COALESCE(SUM(CASE WHEN start_time_unix_ms < @tool_history_from THEN 1 ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN imported_tool_call_count > 0
                                     THEN MAX(imported_tool_call_count - COALESCE(owned.links, 0), 0)
                                     ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN ingest_source <> 'otlp' AND imported_tool_call_count IS NULL THEN 1 ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN {ReportedSql} THEN 1 ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN NOT {ReportedSql}
                                      AND cache_read_state <> 'legacy_unknown' AND cache_write_state <> 'legacy_unknown'
                                     THEN 1 ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN NOT {ReportedSql}
                                      AND (cache_read_state = 'legacy_unknown' OR cache_write_state = 'legacy_unknown')
                                     THEN 1 ELSE 0 END), 0)
            FROM spans
            LEFT JOIN (SELECT owner_span_row_id, COUNT(*) AS links FROM span_links
                       WHERE operation_name = 'execute_tool' AND owner_span_row_id IS NOT NULL
                       GROUP BY owner_span_row_id) AS owned ON owned.owner_span_row_id = spans.id
            WHERE {FilterSql};

            SELECT COALESCE(SUM(records_ambiguous), 0), COALESCE(SUM(CASE WHEN complete = 0 THEN 1 ELSE 0 END), 0)
            FROM import_sessions;
            """;
        AddFilterParameters(command, filter);
        command.Parameters.AddWithValue("@tool_history_from", toolHistoryFrom.ToUnixTimeMilliseconds());
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        List<ExportSourceCount> sources = [];
        while (await reader.ReadAsync(cancellationToken))
        {
            sources.Add(new ExportSourceCount(reader.GetString(0), reader.GetInt64(1)));
        }

        await reader.NextResultAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        long matched = reader.GetInt64(0);
        await reader.NextResultAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        long unmatched = reader.GetInt64(0);
        await reader.NextResultAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        (long before, long withoutDetail, long withoutToolData, long reported, long absent, long legacy) =
            (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3), reader.GetInt64(4), reader.GetInt64(5));
        await reader.NextResultAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        sources.Sort((left, right) => string.CompareOrdinal(left.Source, right.Source));
        return new ExportCoverageRow
        {
            SpansBySource = sources,
            SupersededMatchedImportSpans = matched,
            SupersededUnmatchedImportSpans = unmatched,
            LedgerAmbiguousImportRecords = reader.GetInt64(0),
            LedgerIncompleteImportLogs = reader.GetInt64(1),
            SpansBeforeToolHistory = before,
            ImportedToolCallsWithoutDetail = withoutDetail,
            ImportedRequestsWithoutToolData = withoutToolData,
            CacheReportedSpans = reported,
            CacheAbsentSpans = absent,
            CacheLegacyUnknownSpans = legacy,
        };
    }

    internal static async Task<ExportRatesProvenance> ReadExportRatesProvenanceAsync(
        SqliteTransaction snapshot,
        LedgerFilter filter,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = snapshot.Connection!.CreateCommand();
        command.Transaction = snapshot;
        command.CommandText = $"""
            SELECT DISTINCT rates_version FROM spans WHERE {FilterSql} ORDER BY rates_version;

            SELECT DISTINCT api_rates_version, api_rate_card_sha256 FROM spans
            WHERE {FilterSql} AND api_rates_version IS NOT NULL AND api_rate_card_sha256 IS NOT NULL
            ORDER BY api_rates_version, api_rate_card_sha256;
            """;
        AddFilterParameters(command, filter);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        List<string> versions = [];
        while (await reader.ReadAsync(cancellationToken))
        {
            versions.Add(reader.GetString(0));
        }

        await reader.NextResultAsync(cancellationToken);
        List<ExportApiRateCard> cards = [];
        while (await reader.ReadAsync(cancellationToken))
        {
            cards.Add(new ExportApiRateCard(reader.GetString(0), reader.GetString(1)));
        }

        versions.Sort(StringComparer.Ordinal);
        cards.Sort((left, right) =>
        {
            int order = string.CompareOrdinal(left.RatesVersion, right.RatesVersion);
            return order != 0 ? order : string.CompareOrdinal(left.Sha256, right.Sha256);
        });
        return new ExportRatesProvenance { RatesVersions = versions, ApiRateCards = cards };
    }

    // The trips of the selected prompt chains, newest first.
    internal static async Task<IReadOnlyList<BreakerTrip>> ReadExportBreakerTripsAsync(
        SqliteTransaction snapshot,
        LedgerFilter filter,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = snapshot.Connection!.CreateCommand();
        command.Transaction = snapshot;
        command.CommandText = $"""
            SELECT id, rule, trace_id, conversation_id, repository, branch, repo_root, tripped_at_unix_ms, detail,
                   window_cost_usd_pico, window_minutes, failure_count, recorded_at_unix_ms, notified_at_unix_ms, reset_at_unix_ms
            FROM breaker_trips
            WHERE trace_id IN (SELECT trace_id FROM spans WHERE {FilterSql})
            ORDER BY tripped_at_unix_ms DESC, id DESC;
            """;
        AddFilterParameters(command, filter);
        return await ReadTripsAsync(command, cancellationToken);
    }

    // The budgets of the selected spans' repository and branch keys, with their all-time spend as budget list shows it.
    internal static async Task<IReadOnlyList<BranchBudgetRow>> ReadExportBudgetsAsync(
        SqliteTransaction snapshot,
        LedgerFilter filter,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = snapshot.Connection!.CreateCommand();
        command.Transaction = snapshot;
        command.CommandText = $"""
            {BranchBudgetSelectSql}
            WHERE EXISTS (SELECT 1 FROM spans
                           WHERE {FilterSql}
                             AND COALESCE(repo_slug, repo_root, '(none)') = b.repository
                             AND COALESCE(branch_name, '(none)') = b.branch)
            ORDER BY b.repository, b.branch;
            """;
        AddFilterParameters(command, filter);
        return await ReadBranchBudgetsAsync(command, cancellationToken);
    }

    // The selected spans' repository keys that are local paths: the roots of repositories without a GitHub slug.
    internal static async Task<IReadOnlyList<string>> ReadExportRepositoryRootsAsync(
        SqliteTransaction snapshot,
        LedgerFilter filter,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = snapshot.Connection!.CreateCommand();
        command.Transaction = snapshot;
        command.CommandText = $"""
            SELECT DISTINCT repo_root FROM spans
            WHERE {FilterSql} AND repo_slug IS NULL AND repo_root IS NOT NULL
            ORDER BY repo_root;
            """;
        AddFilterParameters(command, filter);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        List<string> roots = [];
        while (await reader.ReadAsync(cancellationToken))
        {
            roots.Add(reader.GetString(0));
        }

        roots.Sort(StringComparer.Ordinal);
        return roots;
    }

    private static async Task<IReadOnlyList<T>> ReadExportRowsAsync<T>(
        SqliteTransaction snapshot,
        string sql,
        LedgerFilter filter,
        ExportCursor? after,
        int limit,
        Func<SqliteDataReader, T> read,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = snapshot.Connection!.CreateCommand();
        command.Transaction = snapshot;
        command.CommandText = sql;
        AddFilterParameters(command, filter);
        command.Parameters.AddWithValue("@after_start", after?.StartTimeUnixMs ?? filter.From?.ToUnixTimeMilliseconds() ?? long.MinValue);
        command.Parameters.AddWithValue("@after_trace", after?.TraceId ?? string.Empty);
        command.Parameters.AddWithValue("@after_span", after?.SpanId ?? string.Empty);
        command.Parameters.AddWithValue("@limit", limit);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        List<T> rows = [];
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(read(reader));
        }

        return rows;
    }

    private static ExportSpanRow ReadExportSpan(SqliteDataReader reader) => new()
    {
        TraceId = reader.GetString(0),
        SpanId = reader.GetString(1),
        ParentSpanId = TextOrNull(reader, 2),
        OperationName = reader.GetString(3),
        ConversationId = TextOrNull(reader, 4),
        AgentName = TextOrNull(reader, 5),
        ProviderName = TextOrNull(reader, 6),
        RequestModel = TextOrNull(reader, 7),
        ResponseModel = TextOrNull(reader, 8),
        ModelId = TextOrNull(reader, 9),
        StartTime = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(10)),
        EndTime = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(11)),
        InputTokens = reader.GetInt64(12),
        OutputTokens = reader.GetInt64(13),
        CacheReadInputTokens = reader.GetInt64(14),
        CacheWriteInputTokens = reader.GetInt64(15),
        CacheReadState = ToCacheCounterState(reader.GetString(16)),
        CacheWriteState = ToCacheCounterState(reader.GetString(17)),
        BillingChannel = ToBillingChannel(reader.GetString(18)),
        PricingStatus = reader.GetString(19),
        RatesVersion = reader.GetString(20),
        CostUsd = PicoUsd.ToUsd(reader.GetInt64(21)),
        SavedUsd = PicoUsd.ToUsd(reader.GetInt64(22)),
        ApiEquivalentUsd = reader.IsDBNull(23) ? null : PicoUsd.ToUsd(reader.GetInt64(23)),
        ApiEquivalentStatus = TextOrNull(reader, 24),
        ApiRatesVersion = TextOrNull(reader, 25),
        ApiRateCardSha256 = TextOrNull(reader, 26),
        RepositorySlug = TextOrNull(reader, 27),
        RepositoryRoot = TextOrNull(reader, 28),
        BranchName = TextOrNull(reader, 29),
        HeadCommitSha = TextOrNull(reader, 30),
        PullRequestNumber = reader.IsDBNull(31) ? null : reader.GetInt32(31),
        IngestSource = reader.GetString(32),
    };

    private static ExportToolCallRow ReadExportToolCall(SqliteDataReader reader) => new()
    {
        TraceId = reader.GetString(0),
        SpanId = reader.GetString(1),
        ConversationId = TextOrNull(reader, 2),
        AgentName = TextOrNull(reader, 3),
        ToolName = TextOrNull(reader, 4),
        StartTime = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(5)),
        ToolExitCode = reader.IsDBNull(6) ? null : reader.GetInt64(6),
        StatusCode = reader.GetInt32(7),
        IngestSource = reader.GetString(8),
    };

    private static string? TextOrNull(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
}
