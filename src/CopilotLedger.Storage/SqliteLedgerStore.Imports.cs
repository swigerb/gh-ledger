using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Storage;

// Session-log records that gh ledger import records, under the same authority rules as live spans (UsageAuthority).
public sealed partial class SqliteLedgerStore
{
    // Resolves each entry by the authority rules under one BEGIN IMMEDIATE transaction: inserts the Recorded ones with an
    // alias and the tool links they own, aliases the others that a row accounts for, adds the new tool links of a record read
    // again whose own row still counts, and writes checkpoint (when it isn't null) in the same transaction. Validates every
    // entry and the checkpoint first (ArgumentException, nothing written). An exception, cancellation included, rolls the
    // whole call back.
    public async Task<ImportRecordResult> RecordImportedAsync(
        IReadOnlyList<ImportedLedgerEntry> entries,
        ImportSessionCheckpoint? checkpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        EnsureInitialized();
        foreach (ImportedLedgerEntry entry in entries)
        {
            ValidateImportedEntry(entry, nameof(entries));
        }

        if (checkpoint is not null)
        {
            ValidateCheckpoint(checkpoint, nameof(checkpoint));
        }

        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        long now = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        List<ImportResolution> resolutions = new(entries.Count);
        int links = 0;
        int relinked = 0;
        int removed = 0;
        int repriced = 0;
        int liveRepriced = 0;
        foreach (ImportedLedgerEntry entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string source = ImportIds.SourceName(entry.Source);
            string? canonical = UsageIdentity.CanonicalUsageId(
                entry.Span.ProviderName,
                entry.Span.ServiceRequestId,
                entry.Calculation.Channel);
            UsageAuthority.ImportFacts facts = await UsageAuthority.GatherAsync(
                transaction,
                source,
                entry.RecordKeySha256,
                canonical,
                entry.Span.ConversationId!,
                cancellationToken);
            ImportResolution resolution = UsageAuthority.Decide(facts);
            long? ownRowId = null;
            if (resolution == ImportResolution.Recorded)
            {
                long? id = await InsertSpanAsync(
                    transaction,
                    entry.Span,
                    entry.Calculation,
                    ImportProvenance(entry, canonical),
                    now,
                    cancellationToken);
                if (id is long rowId)
                {
                    await UsageAuthority.AddAliasAsync(
                        transaction, source, entry.RecordKeySha256, rowId, "recorded", now, cancellationToken);
                    ownRowId = rowId;
                }
                else
                {
                    // Only colliding trace and span ID hashes get here; the row that has them accounts for the record.
                    await UsageAuthority.AddAliasAsync(
                        transaction,
                        source,
                        entry.RecordKeySha256,
                        await ExistingRowAsync(transaction, entry.Span, cancellationToken),
                        "duplicate",
                        now,
                        cancellationToken);
                    resolution = ImportResolution.Duplicate;
                }
            }
            else if (resolution == ImportResolution.OverlapSkipped ||
                     (resolution == ImportResolution.Duplicate && !facts.AliasExists))
            {
                await UsageAuthority.AddAliasAsync(
                    transaction,
                    source,
                    entry.RecordKeySha256,
                    facts.OwnerRowId!.Value,
                    resolution == ImportResolution.OverlapSkipped ? "live" : "duplicate",
                    now,
                    cancellationToken);
            }

            if (resolution == ImportResolution.Duplicate &&
                ownRowId is null)
            {
                ownRowId = await UsageAuthority.RecordedRowAsync(
                    transaction, source, entry, cancellationToken);
            }

            if (resolution == ImportResolution.Duplicate && ownRowId is long existingRowId)
            {
                UsageAuthority.StoredPricing stored =
                    await UsageAuthority.ReadPricingAsync(connection, transaction, existingRowId, 7, cancellationToken);
                UsageAuthority.StoredPricing wanted = UsageAuthority.PricingOf(entry.Calculation);
                if (stored != wanted)
                {
                    repriced += await UsageAuthority.RewritePricingAsync(
                        transaction, existingRowId, wanted, cancellationToken);
                }
            }

            if (ownRowId is long linkOwnerRowId)
            {
                foreach (GenAiSpanLink link in entry.ToolLinks)
                {
                    removed += await ExtraOwnedLinksAsync(transaction, link, cancellationToken);
                    switch (await UpsertOwnedLinkAsync(
                                transaction, link, linkOwnerRowId, now, cancellationToken))
                    {
                        case LinkWrite.Inserted:
                            links++;
                            break;
                        case LinkWrite.Relinked:
                            relinked++;
                            break;
                    }
                }
            }
            else if (resolution is ImportResolution.OverlapSkipped or ImportResolution.AmbiguousOverlap ||
                     resolution == ImportResolution.Duplicate)
            {
                removed += await RemoveOwnedLinksAsync(transaction, entry.ToolLinks, cancellationToken);
            }

            if (canonical is not null &&
                facts.Owner == UsageAuthority.CanonicalOwner.Live &&
                entry.Calculation.ChargeSource == ChargeSource.Reported)
            {
                liveRepriced += await UsageAuthority.ApplyReportedToLiveAsync(
                    transaction,
                    facts.OwnerRowId!.Value,
                    PicoUsd.FromUsd(entry.Calculation.UsdCost),
                    cancellationToken);
            }

            resolutions.Add(resolution);
        }

        if (checkpoint is not null)
        {
            await UsageAuthority.UpsertCheckpointAsync(transaction, checkpoint, now, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new ImportRecordResult
        {
            Resolutions = resolutions,
            ToolLinksRecorded = links,
            ToolLinksRelinked = relinked,
            ToolLinksRemoved = removed,
            Repriced = repriced,
            LiveRepriced = liveRepriced,
        };
    }

    // The checkpoint of a log, or null when none was written.
    public async Task<ImportSessionCheckpoint?> GetImportSessionAsync(
        ImportSourceKind source,
        string logId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logId);
        EnsureInitialized();
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        return await UsageAuthority.ReadCheckpointAsync(connection, null, source, logId, true, cancellationToken);
    }

    // The source's imported rows that started at or after since (all when it's null) and that live rows superseded without
    // sharing their canonical ID (UsageAuthority.ReadUnmatchedSupersessionsAsync).
    internal async Task<IReadOnlyList<UsageAuthority.UnmatchedSupersession>> GetUnmatchedSupersessionsAsync(
        ImportSourceKind source,
        DateTimeOffset? since,
        CancellationToken cancellationToken)
    {
        EnsureInitialized();
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        return await UsageAuthority.ReadUnmatchedSupersessionsAsync(connection, null, source, since, cancellationToken);
    }

    private static RowProvenance ImportProvenance(ImportedLedgerEntry entry, string? canonicalUsageId) =>
        new(
            "import:" + ImportIds.SourceName(entry.Source),
            canonicalUsageId,
            entry.ToolCallCount,
            entry.Git?.RepositoryRoot,
            entry.Git?.RepositorySlug,
            entry.Git?.Branch,
            entry.Git?.HeadCommitSha,
            null);

    private static async Task<long> ExistingRowAsync(
        SqliteTransaction transaction,
        NormalizedGenAiSpan span,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT id FROM spans WHERE trace_id = @trace AND span_id = @span;";
        command.Parameters.AddWithValue("@trace", span.TraceId);
        command.Parameters.AddWithValue("@span", span.SpanId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private enum LinkWrite { Unchanged, Inserted, Relinked }

    // The imported link for link.SpanId in link.ConversationId, made to belong to the requesting row in place.
    private static async Task<LinkWrite> UpsertOwnedLinkAsync(
        SqliteTransaction transaction,
        GenAiSpanLink link,
        long ownerRowId,
        long recordedAtUnixMs,
        CancellationToken cancellationToken)
    {
        (string TraceId, long OwnerRowId)? existing = null;
        await using (SqliteCommand read = transaction.Connection!.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = """
                SELECT trace_id, owner_span_row_id
                  FROM span_links
                 WHERE span_id = @span_id AND conversation_id = @conversation_id
                   AND owner_span_row_id IS NOT NULL
                 ORDER BY trace_id;
                """;
            read.Parameters.AddWithValue("@span_id", link.SpanId);
            read.Parameters.AddWithValue("@conversation_id", OrDbNull(link.ConversationId));
            await using SqliteDataReader reader = await read.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                existing = (reader.GetString(0), reader.GetInt64(1));
            }
        }

        if (existing is (string retainedTraceId, long _))
        {
            await using SqliteCommand remove = transaction.Connection!.CreateCommand();
            remove.Transaction = transaction;
            remove.CommandText = """
                DELETE FROM span_links
                 WHERE span_id = @span_id AND conversation_id = @conversation_id
                   AND owner_span_row_id IS NOT NULL AND trace_id <> @retained_trace;
                """;
            remove.Parameters.AddWithValue("@span_id", link.SpanId);
            remove.Parameters.AddWithValue("@conversation_id", OrDbNull(link.ConversationId));
            remove.Parameters.AddWithValue("@retained_trace", retainedTraceId);
            await remove.ExecuteNonQueryAsync(cancellationToken);
        }

        LinkWrite result;
        if (existing is (string traceId, long existingOwnerRowId) &&
            (traceId != link.TraceId || existingOwnerRowId != ownerRowId))
        {
            await using SqliteCommand update = transaction.Connection!.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE span_links
                   SET trace_id = @trace_id, owner_span_row_id = @owner
                 WHERE trace_id = @old_trace AND span_id = @span_id;
                """;
            update.Parameters.AddWithValue("@trace_id", link.TraceId);
            update.Parameters.AddWithValue("@owner", ownerRowId);
            update.Parameters.AddWithValue("@old_trace", traceId);
            update.Parameters.AddWithValue("@span_id", link.SpanId);
            await update.ExecuteNonQueryAsync(cancellationToken);
            result = LinkWrite.Relinked;
        }
        else if (existing is not null)
        {
            result = LinkWrite.Unchanged;
        }
        else
        {
            await using SqliteCommand insert = transaction.Connection!.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO span_links (
                    trace_id, span_id, parent_span_id, operation_name, agent_name, conversation_id, start_time_unix_ms,
                    tool_name, tool_exit_code, status_code, recorded_at_unix_ms, owner_span_row_id
                ) VALUES (
                    @trace_id, @span_id, @parent_span_id, @operation_name, @agent_name, @conversation_id, @start_time_unix_ms,
                    @tool_name, @tool_exit_code, @status_code, @recorded_at_unix_ms, @owner
                ) ON CONFLICT (trace_id, span_id) DO NOTHING;
                """;
            insert.Parameters.AddWithValue("@trace_id", link.TraceId);
            insert.Parameters.AddWithValue("@span_id", link.SpanId);
            insert.Parameters.AddWithValue("@parent_span_id", OrDbNull(link.ParentSpanId));
            insert.Parameters.AddWithValue("@operation_name", OrDbNull(link.OperationName));
            insert.Parameters.AddWithValue("@agent_name", OrDbNull(link.AgentName));
            insert.Parameters.AddWithValue("@conversation_id", OrDbNull(link.ConversationId));
            insert.Parameters.AddWithValue("@start_time_unix_ms", link.StartTime.ToUnixTimeMilliseconds());
            insert.Parameters.AddWithValue("@tool_name", OrDbNull(link.ToolName));
            insert.Parameters.AddWithValue("@tool_exit_code", OrDbNull(link.ToolExitCode));
            insert.Parameters.AddWithValue("@status_code", link.StatusCode);
            insert.Parameters.AddWithValue("@recorded_at_unix_ms", recordedAtUnixMs);
            insert.Parameters.AddWithValue("@owner", ownerRowId);
            result = await insert.ExecuteNonQueryAsync(cancellationToken) == 1
                ? LinkWrite.Inserted
                : LinkWrite.Unchanged;
        }

        return result;
    }

    private static async Task<int> ExtraOwnedLinksAsync(
        SqliteTransaction transaction,
        GenAiSpanLink link,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT MAX(COUNT(*) - 1, 0)
              FROM span_links
             WHERE span_id = @span_id AND conversation_id = @conversation_id
               AND owner_span_row_id IS NOT NULL;
            """;
        command.Parameters.AddWithValue("@span_id", link.SpanId);
        command.Parameters.AddWithValue("@conversation_id", OrDbNull(link.ConversationId));
        return Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<int> RemoveOwnedLinksAsync(
        SqliteTransaction transaction,
        IReadOnlyList<GenAiSpanLink> links,
        CancellationToken cancellationToken)
    {
        int removed = 0;
        foreach (GenAiSpanLink link in links)
        {
            await using SqliteCommand command = transaction.Connection!.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                DELETE FROM span_links
                 WHERE span_id = @span_id AND conversation_id = @conversation_id
                   AND owner_span_row_id IS NOT NULL;
                """;
            command.Parameters.AddWithValue("@span_id", link.SpanId);
            command.Parameters.AddWithValue("@conversation_id", OrDbNull(link.ConversationId));
            removed += await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return removed;
    }

    internal static void ValidateImportedEntry(ImportedLedgerEntry entry, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(entry, parameterName);
        if (entry.Source is not (ImportSourceKind.Copilot or ImportSourceKind.Codex))
        {
            throw new ArgumentException("Imported entries must come from Copilot or Codex session logs.", parameterName);
        }

        if (entry.RecordKeySha256 is not { Length: 64 } ||
            !entry.RecordKeySha256.All(character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'f'))
        {
            throw new ArgumentException("Record keys must be 64 lowercase hex characters.", parameterName);
        }

        if (string.IsNullOrEmpty(entry.Span.ConversationId))
        {
            throw new ArgumentException("Imported entries need a conversation ID.", parameterName);
        }

        ValidateEntry(new LedgerEntry(entry.Span, entry.Calculation, null), parameterName);
        if (entry.ToolCallCount is < 0 ||
            entry.ToolLinks.Any(link => link.OperationName != GenAiOperationNames.ExecuteTool))
        {
            throw new ArgumentException(
                "Tool call counts can't be negative, and imported links must be execute_tool links.",
                parameterName);
        }
    }

    private static void ValidateCheckpoint(ImportSessionCheckpoint checkpoint, string parameterName)
    {
        if (checkpoint.Source is not (ImportSourceKind.Copilot or ImportSourceKind.Codex) ||
            string.IsNullOrEmpty(checkpoint.LogId) ||
            checkpoint.LogSizeBytes < 0 ||
            checkpoint.LogSha256 is not { Length: 64 } ||
            !checkpoint.LogSha256.All(character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'f') ||
            checkpoint.ImportRevision < 0 ||
            checkpoint.Recorded < 0 ||
            checkpoint.Duplicates < 0 ||
            checkpoint.OverlapSkipped < 0 ||
            checkpoint.AmbiguousOverlap < 0)
        {
            throw new ArgumentException(
                "A checkpoint must name a Copilot or Codex log and its SHA-256, and its size and counts can't be negative.",
                parameterName);
        }
    }
}
