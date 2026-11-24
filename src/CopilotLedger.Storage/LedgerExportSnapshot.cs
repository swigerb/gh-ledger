using Microsoft.Data.Sqlite;

namespace CopilotLedger.Storage;

// One read transaction over the ledger that every query of gh ledger export shares, so its summaries and its details see the
// same rows (SqliteLedgerStore.OpenExportSnapshotAsync). It never writes. Unlike LedgerSnapshot, import's dry run, it reads an
// initialized ledger at the current schema version.
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

    // Ends the read transaction, which never wrote, and closes the connection.
    public async ValueTask DisposeAsync()
    {
        await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
