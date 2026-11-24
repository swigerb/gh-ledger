using CopilotLedger.Core.Import;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Storage;

// The live/import authority rules: every model call is charged once, and live spans win. SqliteLedgerStore runs them inside
// its BEGIN IMMEDIATE transactions, and LedgerSnapshot runs Decide on a read-only snapshot, so a dry run decides as the real
// run would. After every transaction:
//   I1 no canonical ID belongs to both an effective imported row and an effective live row;
//   I2 in a conversation with an effective live row, every effective imported row has a canonical ID;
//   I3 a conversation with an effective live row that has no canonical ID has no effective imported rows;
//   I4 every alias names a row, and a 'live' alias a live row; only imported rows are superseded, and nothing is deleted.
// Live rows are always effective (a CHECK constraint), so "live" below means ingest_source = 'otlp'.
internal static class UsageAuthority
{
    internal enum CanonicalOwner
    {
        None,
        Live,
        Import,
    }

    // What the ledger holds for one imported record. OwnerRowId is the effective row with its canonical ID, live first.
    internal readonly record struct ImportFacts(
        bool AliasExists,
        bool HasCanonicalId,
        CanonicalOwner Owner,
        long? OwnerRowId,
        bool ConversationHasLive,
        bool ConversationHasUnidentifiedLive);

    // The first rule that matches decides.
    internal static ImportResolution Decide(ImportFacts facts)
    {
        if (facts.AliasExists)
        {
            return ImportResolution.Duplicate;            // this record was resolved before
        }

        if (facts.HasCanonicalId)
        {
            return facts.Owner switch
            {
                CanonicalOwner.Live => ImportResolution.OverlapSkipped,    // I1: the live row has the charge
                CanonicalOwner.Import => ImportResolution.Duplicate,       // another copy of the call, such as a fork's
                _ => facts.ConversationHasUnidentifiedLive
                    ? ImportResolution.AmbiguousOverlap                    // I3
                    : ImportResolution.Recorded,
            };
        }

        return facts.ConversationHasLive
            ? ImportResolution.AmbiguousOverlap                            // I2
            : ImportResolution.Recorded;
    }

    internal static async Task<ImportFacts> GatherAsync(
        SqliteTransaction transaction,
        string source,
        string recordKeySha256,
        string? canonicalUsageId,
        string conversationId,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            WITH owner AS (
                SELECT id, ingest_source = 'otlp' AS live
                  FROM spans
                 WHERE canonical_usage_id = @canonical AND accounting_state = 'effective'
                 ORDER BY live DESC, id
                 LIMIT 1)
            SELECT EXISTS (SELECT 1 FROM import_record_aliases WHERE source = @source AND record_key_sha256 = @key),
                   (SELECT id FROM owner),
                   (SELECT live FROM owner),
                   EXISTS (SELECT 1 FROM spans WHERE conversation_id = @conversation AND ingest_source = 'otlp'),
                   EXISTS (SELECT 1 FROM spans
                            WHERE conversation_id = @conversation AND ingest_source = 'otlp' AND canonical_usage_id IS NULL);
            """;
        command.Parameters.AddWithValue("@source", source);
        command.Parameters.AddWithValue("@key", recordKeySha256);
        command.Parameters.AddWithValue("@canonical", (object?)canonicalUsageId ?? DBNull.Value);
        command.Parameters.AddWithValue("@conversation", conversationId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        long? ownerRowId = reader.IsDBNull(1) ? null : reader.GetInt64(1);
        return new ImportFacts(
            reader.GetInt64(0) == 1,
            canonicalUsageId is not null,
            ownerRowId is null ? CanonicalOwner.None : reader.GetInt64(2) == 1 ? CanonicalOwner.Live : CanonicalOwner.Import,
            ownerRowId,
            reader.GetInt64(3) == 1,
            reader.GetInt64(4) == 1);
    }

    // Supersedes the effective imported row with canonicalUsageId, wherever it is (I1).
    internal static async Task<int> SupersedeCanonicalAsync(
        SqliteTransaction transaction,
        string canonicalUsageId,
        long supersededBy,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE spans
               SET accounting_state = 'superseded', superseded_by = @superseded_by
             WHERE canonical_usage_id = @canonical AND ingest_source <> 'otlp' AND accounting_state = 'effective';
            """;
        command.Parameters.AddWithValue("@canonical", canonicalUsageId);
        command.Parameters.AddWithValue("@superseded_by", supersededBy);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // Supersedes the conversation's effective imported rows that its live rows can't be matched to: those without a canonical
    // ID (I2), or all of them when a live row has none (I3). supersededBy is the live row that arrived; when it's null, the
    // conversation's first live row, and nothing happens without one.
    internal static async Task<int> ReconcileConversationAsync(
        SqliteTransaction transaction,
        string conversationId,
        long? supersededBy,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE spans
               SET accounting_state = 'superseded',
                   superseded_by = COALESCE(@superseded_by,
                       (SELECT MIN(id) FROM spans WHERE conversation_id = @conversation AND ingest_source = 'otlp'))
             WHERE conversation_id = @conversation
               AND ingest_source <> 'otlp'
               AND accounting_state = 'effective'
               AND EXISTS (SELECT 1 FROM spans WHERE conversation_id = @conversation AND ingest_source = 'otlp')
               AND (canonical_usage_id IS NULL
                    OR EXISTS (SELECT 1 FROM spans
                                WHERE conversation_id = @conversation AND ingest_source = 'otlp' AND canonical_usage_id IS NULL));
            """;
        command.Parameters.AddWithValue("@conversation", conversationId);
        command.Parameters.AddWithValue("@superseded_by", supersededBy is long id ? id : DBNull.Value);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // resolution: recorded (the record's own row), live (the live row with its identity), or duplicate (an earlier row).
    internal static async Task AddAliasAsync(
        SqliteTransaction transaction,
        string source,
        string recordKeySha256,
        long spanRowId,
        string resolution,
        long recordedAtUnixMs,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO import_record_aliases (source, record_key_sha256, span_row_id, resolution, recorded_at_unix_ms)
            VALUES (@source, @key, @row, @resolution, @now);
            """;
        command.Parameters.AddWithValue("@source", source);
        command.Parameters.AddWithValue("@key", recordKeySha256);
        command.Parameters.AddWithValue("@row", spanRowId);
        command.Parameters.AddWithValue("@resolution", resolution);
        command.Parameters.AddWithValue("@now", recordedAtUnixMs);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static async Task UpsertCheckpointAsync(
        SqliteTransaction transaction,
        ImportSessionCheckpoint checkpoint,
        long importedAtUnixMs,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO import_sessions (
                source, log_id, log_size_bytes, log_last_write_unix_ms, log_sha256, complete, records_recorded,
                records_duplicate, records_overlap, records_ambiguous, imported_at_unix_ms
            ) VALUES (
                @source, @log, @size, @time, @sha256, @complete, @recorded, @duplicate, @overlap, @ambiguous, @now
            ) ON CONFLICT (source, log_id) DO UPDATE SET
                log_size_bytes = excluded.log_size_bytes,
                log_last_write_unix_ms = excluded.log_last_write_unix_ms,
                log_sha256 = excluded.log_sha256,
                complete = excluded.complete,
                records_recorded = excluded.records_recorded,
                records_duplicate = excluded.records_duplicate,
                records_overlap = excluded.records_overlap,
                records_ambiguous = excluded.records_ambiguous,
                imported_at_unix_ms = excluded.imported_at_unix_ms;
            """;
        command.Parameters.AddWithValue("@source", ImportIds.SourceName(checkpoint.Source));
        command.Parameters.AddWithValue("@log", checkpoint.LogId);
        command.Parameters.AddWithValue("@size", checkpoint.LogSizeBytes);
        command.Parameters.AddWithValue("@time", checkpoint.LogLastWriteTime.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("@sha256", checkpoint.LogSha256);
        command.Parameters.AddWithValue("@complete", checkpoint.Complete ? 1 : 0);
        command.Parameters.AddWithValue("@recorded", checkpoint.Recorded);
        command.Parameters.AddWithValue("@duplicate", checkpoint.Duplicates);
        command.Parameters.AddWithValue("@overlap", checkpoint.OverlapSkipped);
        command.Parameters.AddWithValue("@ambiguous", checkpoint.AmbiguousOverlap);
        command.Parameters.AddWithValue("@now", importedAtUnixMs);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // A log's checkpoint, or null. transaction may be null for a single read.
    internal static async Task<ImportSessionCheckpoint?> ReadCheckpointAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        ImportSourceKind source,
        string logId,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT log_size_bytes, log_last_write_unix_ms, complete, records_recorded, records_duplicate, records_overlap,
                   records_ambiguous, imported_at_unix_ms, log_sha256
            FROM import_sessions
            WHERE source = @source AND log_id = @log;
            """;
        command.Parameters.AddWithValue("@source", ImportIds.SourceName(source));
        command.Parameters.AddWithValue("@log", logId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ImportSessionCheckpoint
        {
            Source = source,
            LogId = logId,
            LogSizeBytes = reader.GetInt64(0),
            LogLastWriteTime = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1)),
            LogSha256 = reader.GetString(8),
            Complete = reader.GetInt64(2) == 1,
            Recorded = reader.GetInt64(3),
            Duplicates = reader.GetInt64(4),
            OverlapSkipped = reader.GetInt64(5),
            AmbiguousOverlap = reader.GetInt64(6),
            ImportedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(7)),
        };
    }
}
