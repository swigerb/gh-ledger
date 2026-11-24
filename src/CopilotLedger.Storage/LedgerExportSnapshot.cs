using CopilotLedger.Core.Audit;
using CopilotLedger.Core.Breaker;
using CopilotLedger.Storage.Import;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Storage;

// One read transaction over the ledger that every query of gh ledger export shares, so its summaries and its details see the
// same rows (SqliteLedgerStore.OpenExportSnapshotAsync). It never writes. Unlike LedgerSnapshot, import's dry run, it reads an
// initialized ledger at the current schema version. gh ledger audit and MCP's get_audit_findings read their own snapshots.
public sealed class LedgerExportSnapshot : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction _transaction;

    internal LedgerExportSnapshot(SqliteConnection connection, SqliteTransaction transaction)
    {
        _connection = connection;
        _transaction = transaction;
    }

    // As SqliteLedgerStore.GetRollupAsync with the recorded API-equivalent results.
    public Task<IReadOnlyList<LedgerRollupRow>> GetRollupAsync(
        LedgerGrouping grouping,
        LedgerFilter filter,
        CancellationToken cancellationToken = default) =>
        SqliteLedgerStore.ReadRollupAsync(_transaction, grouping, filter, null, cancellationToken);

    // As SqliteLedgerStore.GetCacheEfficiencyAsync.
    public Task<IReadOnlyList<CacheEfficiencyRow>> GetCacheEfficiencyAsync(
        LedgerGrouping grouping,
        LedgerFilter filter,
        CancellationToken cancellationToken = default) =>
        SqliteLedgerStore.ReadCacheEfficiencyAsync(_connection, _transaction, grouping, filter, cancellationToken);

    // As SqliteLedgerStore.GetTopPromptChainsAsync with the recorded API-equivalent results. Throws ArgumentOutOfRangeException
    // unless 1 <= limit <= 1000.
    public Task<IReadOnlyList<PromptChainRow>> GetTopPromptChainsAsync(
        LedgerFilter filter,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        return SqliteLedgerStore.ReadTopPromptChainsAsync(_transaction, filter, limit, null, cancellationToken);
    }

    // As SqliteLedgerStore.GetPromptCountAsync.
    public Task<long> GetPromptCountAsync(
        LedgerFilter filter,
        CancellationToken cancellationToken = default) =>
        SqliteLedgerStore.ReadPromptCountAsync(_connection, _transaction, filter, cancellationToken);

    // As SqliteLedgerStore.GetTurnBreakdownAsync. Throws ArgumentNullException for a null selector or filter.
    public Task<IReadOnlyList<TurnTokenRow>> GetTurnBreakdownAsync(
        TurnSelector selector,
        LedgerFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(filter);
        return SqliteLedgerStore.ReadTurnBreakdownAsync(_connection, _transaction, selector, filter, cancellationToken);
    }

    // The filter's spans, ascending by (start time, trace ID, span ID), after `after` when it isn't null. Throws
    // ArgumentOutOfRangeException unless 1 <= limit <= 10,000.
    public Task<IReadOnlyList<ExportSpanRow>> GetSpansAsync(
        LedgerFilter filter,
        ExportCursor? after,
        int limit,
        CancellationToken cancellationToken = default)
    {
        CheckPageLimit(limit);
        return SqliteLedgerStore.ReadExportSpansAsync(_transaction, filter, after, limit, cancellationToken);
    }

    // The filter's latest spans, descending by (start time, trace ID, span ID). Throws ArgumentOutOfRangeException unless
    // 0 <= limit <= 100,000.
    public Task<IReadOnlyList<ExportSpanRow>> GetLatestSpansAsync(
        LedgerFilter filter,
        int limit,
        CancellationToken cancellationToken = default)
    {
        CheckLatestLimit(limit);
        return SqliteLedgerStore.ReadLatestExportSpansAsync(_transaction, filter, limit, cancellationToken);
    }

    // The execute_tool links that count and start in the filter's period, of the prompt chains with a selected span, ascending
    // by (start time, trace ID, span ID), after `after` when it isn't null. Throws ArgumentOutOfRangeException unless
    // 1 <= limit <= 10,000.
    public Task<IReadOnlyList<ExportToolCallRow>> GetToolCallsAsync(
        LedgerFilter filter,
        ExportCursor? after,
        int limit,
        CancellationToken cancellationToken = default)
    {
        CheckPageLimit(limit);
        return SqliteLedgerStore.ReadExportToolCallsAsync(_transaction, filter, after, limit, cancellationToken);
    }

    // GetToolCallsAsync's latest rows, descending. Throws ArgumentOutOfRangeException unless 0 <= limit <= 100,000.
    public Task<IReadOnlyList<ExportToolCallRow>> GetLatestToolCallsAsync(
        LedgerFilter filter,
        int limit,
        CancellationToken cancellationToken = default)
    {
        CheckLatestLimit(limit);
        return SqliteLedgerStore.ReadLatestExportToolCallsAsync(_transaction, filter, limit, cancellationToken);
    }

    // The number of rows GetToolCallsAsync pages through.
    public Task<long> CountToolCallsAsync(
        LedgerFilter filter,
        CancellationToken cancellationToken = default) =>
        SqliteLedgerStore.CountExportToolCallsAsync(_transaction, filter, cancellationToken);

    // The start of the earliest span link the ledger keeps, a tool call's or an agent invocation's, of any filter, or null when
    // it keeps none. The daemon deletes older links of every kind, so no tool call before it is left.
    public Task<DateTimeOffset?> GetEarliestLinkStartAsync(CancellationToken cancellationToken = default) =>
        SqliteLedgerStore.ReadEarliestLinkStartAsync(_transaction, cancellationToken);

    // What the filter's spans cover. Spans that start before toolHistoryFrom may have lost their tool calls to retention.
    public Task<ExportCoverageRow> GetCoverageAsync(
        LedgerFilter filter,
        DateTimeOffset toolHistoryFrom,
        CancellationToken cancellationToken = default) =>
        SqliteLedgerStore.ReadExportCoverageAsync(_transaction, filter, toolHistoryFrom, cancellationToken);

    public Task<ExportRatesProvenance> GetRatesProvenanceAsync(
        LedgerFilter filter,
        CancellationToken cancellationToken = default) =>
        SqliteLedgerStore.ReadExportRatesProvenanceAsync(_transaction, filter, cancellationToken);

    // The breaker trips of the prompt chains with a selected span, newest first.
    public Task<IReadOnlyList<BreakerTrip>> GetBreakerTripsAsync(
        LedgerFilter filter,
        CancellationToken cancellationToken = default) =>
        SqliteLedgerStore.ReadExportBreakerTripsAsync(_transaction, filter, cancellationToken);

    // The budgets of the selected spans' repository and branch keys, as budget list shows them.
    public Task<IReadOnlyList<BranchBudgetRow>> GetBudgetsAsync(
        LedgerFilter filter,
        CancellationToken cancellationToken = default) =>
        SqliteLedgerStore.ReadExportBudgetsAsync(_transaction, filter, cancellationToken);

    // The selected spans' repository roots that have no GitHub slug, ordinal.
    public Task<IReadOnlyList<string>> GetRepositoryRootsAsync(
        LedgerFilter filter,
        CancellationToken cancellationToken = default) =>
        SqliteLedgerStore.ReadExportRepositoryRootsAsync(_transaction, filter, cancellationToken);

    // The filter's spans as audit requests, ascending by (start time, trace ID, span ID). Throws ArgumentNullException for a
    // null filter.
    public Task<IReadOnlyList<AuditSpan>> GetAuditSpansAsync(
        LedgerFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return SqliteLedgerStore.ReadAuditSpansAsync(_transaction, filter, cancellationToken);
    }

    // GetToolCallsAsync's rows as audit tool calls, with their agent invocations and their prompt chains' scope. Throws
    // ArgumentNullException for a null filter.
    public Task<IReadOnlyList<AuditToolCall>> GetAuditToolCallsAsync(
        LedgerFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return SqliteLedgerStore.ReadAuditToolCallsAsync(_transaction, filter, cancellationToken);
    }

    // As SqliteLedgerStore.GetBreakerSettingsAsync.
    public Task<BreakerSettings> GetBreakerSettingsAsync(CancellationToken cancellationToken = default) =>
        SqliteLedgerStore.ReadBreakerSettingsAsync(_connection, _transaction, cancellationToken);

    // What an audit of the filter reads: its spans and tool calls, the coverage of GetCoverageAsync with export's tool history,
    // and the breaker settings; AuditInput's defaults for the rest. Throws ArgumentNullException for a null filter.
    public async Task<AuditInput> GetAuditInputAsync(
        LedgerFilter filter,
        DateTimeOffset generatedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        IReadOnlyList<AuditSpan> spans = await GetAuditSpansAsync(filter, cancellationToken);
        IReadOnlyList<AuditToolCall> calls = await GetAuditToolCallsAsync(filter, cancellationToken);

        // As export's coverage: tool history is the retention before generatedAt, but starts no earlier than the earliest span
        // link the ledger keeps, because the daemon deletes links by its own clock; a ledger that keeps no link keeps no tool
        // history.
        DateTimeOffset retained = generatedAt - LedgerImporter.ToolLinkRetention;
        DateTimeOffset? earliestLink = await GetEarliestLinkStartAsync(cancellationToken);
        (DateTimeOffset toolHistoryFrom, AuditToolHistoryStart toolHistoryStart) = earliestLink switch
        {
            null => (generatedAt, AuditToolHistoryStart.NoLinks),
            DateTimeOffset earliest when earliest > retained => (earliest, AuditToolHistoryStart.EarliestLink),
            _ => (retained, AuditToolHistoryStart.Retention),
        };
        ExportCoverageRow coverage = await GetCoverageAsync(filter, toolHistoryFrom, cancellationToken);
        BreakerSettings breaker = await GetBreakerSettingsAsync(cancellationToken);
        return new AuditInput
        {
            Spans = spans,
            ToolCalls = calls,
            Coverage = new AuditCoverage
            {
                ToolHistoryFrom = toolHistoryFrom,
                ToolHistoryStart = toolHistoryStart,
                ToolHistoryRetentionDays = LedgerImporter.ToolLinkRetention.Days,
                SpansBeforeToolHistory = coverage.SpansBeforeToolHistory,
                ImportedToolCallsWithoutDetail = coverage.ImportedToolCallsWithoutDetail,
                ImportedRequestsWithoutToolData = coverage.ImportedRequestsWithoutToolData,
                SupersededMatchedImportSpans = coverage.SupersededMatchedImportSpans,
                SupersededUnmatchedImportSpans = coverage.SupersededUnmatchedImportSpans,
                LedgerAmbiguousImportRecords = coverage.LedgerAmbiguousImportRecords,
                LedgerIncompleteImportLogs = coverage.LedgerIncompleteImportLogs,
            },
            Breaker = breaker,
        };
    }

    // Ends the read transaction, which never wrote, and closes the connection.
    public async ValueTask DisposeAsync()
    {
        await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static void CheckPageLimit(int limit)
    {
        if (limit is < 1 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }
    }

    private static void CheckLatestLimit(int limit)
    {
        if (limit is < 0 or > 100_000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }
    }
}
