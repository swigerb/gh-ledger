using System.Text;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Telemetry;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Storage;

// The ledger as one consistent, read-only snapshot, for a dry run: it decides each imported record as RecordImportedAsync
// would, and remembers its own decisions, without ever creating, migrating, or writing the ledger. With neither -wal nor -shm
// beside the ledger, it opens the file as immutable, so SQLite doesn't create them either.
public sealed class LedgerSnapshot : IAsyncDisposable
{
    private readonly SqliteConnection? _connection;
    private readonly SqliteTransaction? _transaction;
    private readonly HashSet<(string Source, string Key)> _aliases = [];
    private readonly HashSet<string> _recordedCanonicalIds = new(StringComparer.Ordinal);

    private LedgerSnapshot(SqliteConnection? connection, SqliteTransaction? transaction, int schemaVersion)
    {
        _connection = connection;
        _transaction = transaction;
        SchemaVersion = schemaVersion;
    }

    // 0 when there's no ledger.
    public int SchemaVersion { get; }

    // Throws InvalidOperationException for a ledger newer than this gh-ledger, as InitializeAsync does.
    public static async Task<LedgerSnapshot> OpenAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        string path = Path.GetFullPath(databasePath);
        if (!File.Exists(path))
        {
            return new LedgerSnapshot(null, null, 0);
        }

        bool companions = File.Exists(path + "-wal") || File.Exists(path + "-shm");
        SqliteConnection connection = new(new SqliteConnectionStringBuilder
        {
            DataSource = companions ? path : ImmutableUri(path),
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA busy_timeout = 5000;";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            int version;
            await using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA user_version;";
                version = Convert.ToInt32(
                    await command.ExecuteScalarAsync(cancellationToken),
                    System.Globalization.CultureInfo.InvariantCulture);
            }

            int latest = LedgerMigrations.Migrations[^1].Version;
            if (version > latest)
            {
                throw new InvalidOperationException(
                    $"Database user_version {version} is newer than the supported version {latest}.");
            }

            return new LedgerSnapshot(connection, connection.BeginTransaction(deferred: true), version);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    // What RecordImportedAsync would decide for these entries now, counting the decisions of earlier calls on this snapshot.
    public async Task<ImportRecordResult> EvaluateAsync(
        IReadOnlyList<ImportedLedgerEntry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        foreach (ImportedLedgerEntry entry in entries)
        {
            SqliteLedgerStore.ValidateImportedEntry(entry, nameof(entries));
        }

        List<ImportResolution> resolutions = new(entries.Count);
        int links = 0;
        foreach (ImportedLedgerEntry entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string source = ImportIds.SourceName(entry.Source);
            string? canonical = UsageIdentity.CanonicalUsageId(
                entry.Span.ProviderName,
                entry.Span.ServiceRequestId,
                entry.Calculation.Channel);
            UsageAuthority.ImportFacts facts = await FactsAsync(
                source,
                entry.RecordKeySha256,
                canonical,
                entry.Span.ConversationId!,
                cancellationToken);
            if (_aliases.Contains((source, entry.RecordKeySha256)))
            {
                facts = facts with { AliasExists = true };
            }

            if (canonical is not null &&
                facts.Owner == UsageAuthority.CanonicalOwner.None &&
                _recordedCanonicalIds.Contains(canonical))
            {
                facts = facts with { Owner = UsageAuthority.CanonicalOwner.Import };
            }

            ImportResolution resolution = UsageAuthority.Decide(facts);
            if (resolution != ImportResolution.AmbiguousOverlap)
            {
                _aliases.Add((source, entry.RecordKeySha256));
            }

            if (resolution == ImportResolution.Recorded)
            {
                links += entry.ToolLinks.Count;
                if (canonical is not null)
                {
                    _recordedCanonicalIds.Add(canonical);
                }
            }

            resolutions.Add(resolution);
        }

        return new ImportRecordResult { Resolutions = resolutions, ToolLinksRecorded = links };
    }

    // The checkpoint of a log, or null when none was written or the ledger has no import checkpoints yet.
    public async Task<ImportSessionCheckpoint?> GetImportSessionAsync(
        ImportSourceKind source,
        string logId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logId);
        return SchemaVersion < 6
            ? null
            : await UsageAuthority.ReadCheckpointAsync(_connection!, _transaction, source, logId, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }

    // Before schema version 6, the ledger has no aliases or usage identities, and every live row is unidentified.
    private async Task<UsageAuthority.ImportFacts> FactsAsync(
        string source,
        string recordKeySha256,
        string? canonicalUsageId,
        string conversationId,
        CancellationToken cancellationToken)
    {
        if (SchemaVersion >= 6)
        {
            return await UsageAuthority.GatherAsync(
                _transaction!,
                source,
                recordKeySha256,
                canonicalUsageId,
                conversationId,
                cancellationToken);
        }

        bool live = false;
        if (SchemaVersion > 0)
        {
            await using SqliteCommand command = _connection!.CreateCommand();
            command.Transaction = _transaction;
            command.CommandText = "SELECT EXISTS (SELECT 1 FROM spans WHERE conversation_id = @conversation);";
            command.Parameters.AddWithValue("@conversation", conversationId);
            live = (long)(await command.ExecuteScalarAsync(cancellationToken))! == 1;
        }

        return new UsageAuthority.ImportFacts(
            false,
            canonicalUsageId is not null,
            UsageAuthority.CanonicalOwner.None,
            null,
            live,
            live);
    }

    // file:///C:/…/ledger.db?immutable=1 (file:///home/… elsewhere), every byte but [A-Za-z0-9-._~/:] percent-encoded.
    private static string ImmutableUri(string path)
    {
        string full = path.Replace('\\', '/');
        StringBuilder encoded = new();
        foreach (byte value in Encoding.UTF8.GetBytes(full))
        {
            char character = (char)value;
            if (value < 0x80 && (char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '_' or '~' or '/' or ':'))
            {
                encoded.Append(character);
            }
            else
            {
                encoded.Append('%').Append(value.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return (full.StartsWith('/') ? "file://" : "file:///") + encoded + "?immutable=1";
    }
}
