using CopilotLedger.Core.Import;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Storage;

public sealed class UsageAuthorityTests
{
    private const string S1 = "11111111-1111-4111-8111-111111111111";
    private const string S2 = "22222222-2222-4222-8222-222222222222";
    private const string S3 = "33333333-3333-4333-8333-333333333333";
    private const string RowsSql = "SELECT id, ingest_source, accounting_state, superseded_by FROM spans ORDER BY id;";

    [Fact]
    public async Task UA1_LiveFirst_TheImportIsOverlapSkippedAndAliasedToTheLiveRow()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);
        await store.RecordAsync([ImportEntries.Live("session-a", S1)]);

        ImportRecordResult result = await store.RecordImportedAsync(
            [ImportEntries.Copilot("usage-1", "session-a", S1)],
            null);

        Assert.Equal([ImportResolution.OverlapSkipped], result.Resolutions);
        Assert.Equal(["1|otlp|effective|null"], await LedgerSql.RowsAsync(path, RowsSql));
        Assert.Equal(
            ["live|1"],
            await LedgerSql.RowsAsync(path, "SELECT resolution, span_row_id FROM import_record_aliases;"));
        await ImportEntries.AssertInvariantsAsync(path);
    }

    [Fact]
    public async Task UA2_ImportFirst_TheLiveRowSupersedesTheImportedRow()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);

        ImportRecordResult result = await store.RecordImportedAsync(
            [ImportEntries.Copilot("usage-1", "session-a", S1)],
            null);
        int inserted = await store.RecordAsync([ImportEntries.Live("session-a", S1)]);

        Assert.Equal([ImportResolution.Recorded], result.Resolutions);
        Assert.Equal(1, inserted);
        Assert.Equal(
            ["1|import:copilot|superseded|2", "2|otlp|effective|null"],
            await LedgerSql.RowsAsync(path, RowsSql));
        Assert.Equal(
            ["recorded|1"],
            await LedgerSql.RowsAsync(path, "SELECT resolution, span_row_id FROM import_record_aliases;"));
        await ImportEntries.AssertInvariantsAsync(path);
    }

    [Fact]
    public async Task UA3_ARepeatedImportIsADuplicate()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);
        ImportedLedgerEntry entry = ImportEntries.Copilot("usage-1", "session-a", S1);

        ImportRecordResult first = await store.RecordImportedAsync([entry], null);
        ImportRecordResult second = await store.RecordImportedAsync([entry, entry], null);

        Assert.Equal([ImportResolution.Recorded], first.Resolutions);
        Assert.Equal([ImportResolution.Duplicate, ImportResolution.Duplicate], second.Resolutions);
        Assert.Equal(["1|import:copilot|effective|null"], await LedgerSql.RowsAsync(path, RowsSql));
        Assert.Equal(1L, await LedgerSql.ScalarAsync(path, "SELECT COUNT(*) FROM import_record_aliases;"));
        await ImportEntries.AssertInvariantsAsync(path);
    }

    [Fact]
    public async Task UA4_ConcurrentLiveAndImportWritesChargeEachCallOnce()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore live, string path) = await StoreAsync(temp);
        SqliteLedgerStore import = new(path);
        await import.InitializeAsync();

        for (int round = 0; round < 10; round++)
        {
            string serviceRequestId = $"{round:D8}-0000-4000-8000-000000000001";
            string conversation = $"session-{round}";
            await Task.WhenAll(
                Task.Run(() => live.RecordAsync([ImportEntries.Live(conversation, serviceRequestId)])),
                Task.Run(() => import.RecordImportedAsync(
                    [ImportEntries.Copilot($"usage-{round}", conversation, serviceRequestId)],
                    null)));
        }

        Assert.Equal(
            ["10|10|10"],
            await LedgerSql.RowsAsync(
                path,
                """
                SELECT COUNT(*), COUNT(DISTINCT canonical_usage_id), SUM(ingest_source = 'otlp')
                FROM spans WHERE accounting_state = 'effective';
                """));
        await ImportEntries.AssertInvariantsAsync(path);
    }

    [Fact]
    public async Task UA5_IdenticalLookingCallsWithDifferentKeysAreAllCharged()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);

        ImportRecordResult result = await store.RecordImportedAsync(
            [
                ImportEntries.Copilot("usage-1", "session-a", S1),
                ImportEntries.Copilot("usage-2", "session-a", S2),
                ImportEntries.Copilot("usage-3", "session-a", null, byok: true),
                ImportEntries.Copilot("usage-4", "session-a", null, byok: true),
            ],
            null);

        Assert.Equal(Enumerable.Repeat(ImportResolution.Recorded, 4), result.Resolutions);
        Assert.Equal(
            ["4|1"],
            await LedgerSql.RowsAsync(
                path,
                """
                SELECT COUNT(*), COUNT(DISTINCT input_tokens || '/' || output_tokens || '/' || start_time_unix_ms)
                FROM spans WHERE accounting_state = 'effective';
                """));
        await ImportEntries.AssertInvariantsAsync(path);
    }

    [Fact]
    public async Task UA6_ALiveExportDaysLaterAddsNoCharge()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);
        ImportedLedgerEntry entry = ImportEntries.Copilot("usage-1", "session-a", S1);
        LedgerEntry late = ImportEntries.Live("session-a", S1, ImportEntries.Start.AddDays(3));

        await store.RecordImportedAsync([entry], null);
        int first = await store.RecordAsync([late]);
        int again = await store.RecordAsync([late]);
        ImportRecordResult reimport = await store.RecordImportedAsync([entry], null);

        Assert.Equal(1, first);
        Assert.Equal(0, again);
        Assert.Equal([ImportResolution.Duplicate], reimport.Resolutions);
        Assert.Equal(
            ["1|import:copilot|superseded|2", "2|otlp|effective|null"],
            await LedgerSql.RowsAsync(path, RowsSql));
        await ImportEntries.AssertInvariantsAsync(path);
    }

    [Fact]
    public async Task UA7_AnUnidentifiedLiveRowDefersItsConversation()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);
        await store.RecordAsync([ImportEntries.Live("session-a", null)]);

        ImportRecordResult result = await store.RecordImportedAsync(
            [
                ImportEntries.Copilot("usage-1", "session-a", S1),
                ImportEntries.Copilot("usage-2", "session-a", null, byok: true),
            ],
            null);

        Assert.Equal([ImportResolution.AmbiguousOverlap, ImportResolution.AmbiguousOverlap], result.Resolutions);
        Assert.Equal(["1|otlp|effective|null"], await LedgerSql.RowsAsync(path, RowsSql));
        Assert.Equal(0L, await LedgerSql.ScalarAsync(path, "SELECT COUNT(*) FROM import_record_aliases;"));
        await ImportEntries.AssertInvariantsAsync(path);
    }

    [Fact]
    public async Task UA8_ALaterUnidentifiedLiveRowSupersedesItsConversationsImports()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);
        ImportedLedgerEntry first = ImportEntries.Copilot("usage-1", "session-a", S1);
        ImportedLedgerEntry second = ImportEntries.Copilot("usage-2", "session-a", S2);
        await store.RecordImportedAsync([first, second, ImportEntries.Copilot("usage-3", "session-b", S3)], null);

        await store.RecordAsync([ImportEntries.Live("session-a", null)]);
        ImportRecordResult reimport = await store.RecordImportedAsync([first, second], null);

        Assert.Equal([ImportResolution.Duplicate, ImportResolution.Duplicate], reimport.Resolutions);
        Assert.Equal(
            [
                "1|import:copilot|superseded|4",
                "2|import:copilot|superseded|4",
                "3|import:copilot|effective|null",
                "4|otlp|effective|null",
            ],
            await LedgerSql.RowsAsync(path, RowsSql));
        await ImportEntries.AssertInvariantsAsync(path);
    }

    [Fact]
    public async Task UA9_AnIdentifiedLiveRowSupersedesOnlyUnidentifiedImports()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);
        await store.RecordImportedAsync(
            [
                ImportEntries.Copilot("usage-1", "session-a", S1),
                ImportEntries.Copilot("usage-2", "session-a", null, byok: true),
            ],
            null);

        await store.RecordAsync([ImportEntries.Live("session-a", S2)]);

        Assert.Equal(
            ["1|import:copilot|effective|null", "2|import:copilot|superseded|3", "3|otlp|effective|null"],
            await LedgerSql.RowsAsync(path, RowsSql));
        await ImportEntries.AssertInvariantsAsync(path);
    }

    [Fact]
    public async Task UA10_AnUnidentifiedRecordIsRecordedOnlyWithoutLiveRows()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);
        ImportRecordResult alone = await store.RecordImportedAsync(
            [ImportEntries.Copilot("usage-1", "session-a", null, byok: true)],
            null);
        await store.RecordAsync([ImportEntries.Live("session-b", S1)]);

        ImportRecordResult beside = await store.RecordImportedAsync(
            [ImportEntries.Copilot("usage-2", "session-b", null, byok: true)],
            null);

        Assert.Equal([ImportResolution.Recorded], alone.Resolutions);
        Assert.Equal([ImportResolution.AmbiguousOverlap], beside.Resolutions);
        Assert.Equal(["1|import:copilot|effective|null", "2|otlp|effective|null"], await LedgerSql.RowsAsync(path, RowsSql));
        await ImportEntries.AssertInvariantsAsync(path);
    }

    [Fact]
    public async Task UA11_TheSameRecordInAnotherConversationIsADuplicate()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);

        ImportRecordResult result = await store.RecordImportedAsync(
            [
                ImportEntries.Copilot("usage-1", "session-a", S1),
                ImportEntries.Copilot("usage-1", "session-fork", S1),
                ImportEntries.Copilot("usage-2", "session-fork", S1),
            ],
            null);

        Assert.Equal(
            [ImportResolution.Recorded, ImportResolution.Duplicate, ImportResolution.Duplicate],
            result.Resolutions);
        Assert.Equal(["1|import:copilot|effective|null"], await LedgerSql.RowsAsync(path, RowsSql));
        Assert.Equal(
            ["duplicate|1", "recorded|1"],
            await LedgerSql.RowsAsync(path, "SELECT resolution, span_row_id FROM import_record_aliases ORDER BY resolution;"));
        await ImportEntries.AssertInvariantsAsync(path);
    }

    [Fact]
    public async Task UA12_TheCheckpointIsWrittenWithTheBatch()
    {
        using TempDirectory temp = new();
        ManualTimeProvider time = new(new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.Zero));
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path, time);
        await store.InitializeAsync();
        ImportSessionCheckpoint checkpoint = new()
        {
            Source = ImportSourceKind.Copilot,
            LogId = "session-a",
            LogSizeBytes = 4_096,
            LogLastWriteTime = new DateTimeOffset(2026, 11, 20, 12, 34, 56, 789, TimeSpan.Zero),
            Complete = true,
            Recorded = 1,
            Duplicates = 2,
            OverlapSkipped = 3,
            AmbiguousOverlap = 4,
        };

        await store.RecordImportedAsync([ImportEntries.Copilot("usage-1", "session-a", S1)], checkpoint);
        ImportSessionCheckpoint? written = await store.GetImportSessionAsync(ImportSourceKind.Copilot, "session-a");
        time.Advance(TimeSpan.FromHours(1));
        await store.RecordImportedAsync([], checkpoint with { Complete = false, LogSizeBytes = 8_192 });

        Assert.Equal(checkpoint with { ImportedAt = new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.Zero) }, written);
        Assert.Equal(
            checkpoint with
            {
                Complete = false,
                LogSizeBytes = 8_192,
                ImportedAt = new DateTimeOffset(2026, 11, 23, 11, 0, 0, TimeSpan.Zero),
            },
            await store.GetImportSessionAsync(ImportSourceKind.Copilot, "session-a"));
        Assert.Null(await store.GetImportSessionAsync(ImportSourceKind.Codex, "session-a"));
    }

    [Fact]
    public async Task UA13_ACancelledCallWritesNothing()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.RecordImportedAsync(
            [ImportEntries.Copilot("usage-1", "session-a", S1)],
            new ImportSessionCheckpoint
            {
                Source = ImportSourceKind.Copilot,
                LogId = "session-a",
                LogSizeBytes = 1,
                LogLastWriteTime = ImportEntries.Start,
                Complete = true,
                Recorded = 1,
                Duplicates = 0,
                OverlapSkipped = 0,
                AmbiguousOverlap = 0,
            },
            cancelled.Token));

        Assert.Equal(
            ["0|0|0"],
            await LedgerSql.RowsAsync(
                path,
                """
                SELECT (SELECT COUNT(*) FROM spans), (SELECT COUNT(*) FROM import_record_aliases),
                       (SELECT COUNT(*) FROM import_sessions);
                """));
    }

    [Theory]
    [InlineData(0, "Imported entries must come from Copilot or Codex session logs. (Parameter 'entries')")]
    [InlineData(1, "Record keys must be 64 lowercase hex characters. (Parameter 'entries')")]
    [InlineData(2, "Imported entries need a conversation ID. (Parameter 'entries')")]
    public async Task UA14_InvalidEntriesThrowBeforeAnythingIsWritten(int caseNumber, string message)
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);
        ImportedLedgerEntry valid = ImportEntries.Copilot("usage-1", "session-a", S1);
        ImportedLedgerEntry invalid = caseNumber switch
        {
            0 => valid with { Source = ImportSourceKind.Claude },
            1 => valid with { RecordKeySha256 = valid.RecordKeySha256[..63] },
            _ => valid with { Span = valid.Span with { ConversationId = null } },
        };

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => store.RecordImportedAsync([valid, invalid], null));

        Assert.Equal(message, exception.Message);
        Assert.Equal(0L, await LedgerSql.ScalarAsync(path, "SELECT COUNT(*) FROM spans;"));
    }

    [Fact]
    public async Task UA15_ALiveRowWhoseConversationIsAssignedLaterFollowsTheRules()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);
        await store.RecordImportedAsync([ImportEntries.Copilot("usage-1", "session-a", null, byok: true)], null);
        LedgerEntry live = ImportEntries.Live(null, null);
        await store.RecordAsync([live]);
        string[] before = [.. await LedgerSql.RowsAsync(path, RowsSql)];

        await store.RecordSpanLinksAsync(
        [
            new GenAiSpanLink
            {
                TraceId = live.Span.TraceId,
                SpanId = "a000000000000001",
                OperationName = "invoke_agent",
                ConversationId = "session-a",
                StartTime = ImportEntries.Start,
            },
            new GenAiSpanLink
            {
                TraceId = live.Span.TraceId,
                SpanId = live.Span.SpanId,
                ParentSpanId = "a000000000000001",
                OperationName = "chat",
                StartTime = ImportEntries.Start,
            },
        ]);
        int changed = await store.BackfillFromAgentSpansAsync([live.Span.TraceId]);

        Assert.Equal(["1|import:copilot|effective|null", "2|otlp|effective|null"], before);
        Assert.Equal(1, changed);
        Assert.Equal(
            ["1|import:copilot|superseded|2", "2|otlp|effective|null"],
            await LedgerSql.RowsAsync(path, RowsSql));
        await ImportEntries.AssertInvariantsAsync(path);
    }

    [Fact]
    public async Task UA16_RecordedImportsOwnTheirToolLinks()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);
        ImportedLedgerEntry entry = ImportEntries.Copilot("usage-1", "session-a", S1);
        entry = entry with
        {
            ToolLinks = [ImportEntries.ToolLink(entry, "toolu-1", 0), ImportEntries.ToolLink(entry, "toolu-2", 1)],
        };

        ImportRecordResult first = await store.RecordImportedAsync([entry], null);
        ImportRecordResult second = await store.RecordImportedAsync([entry], null);

        Assert.Equal(2, first.ToolLinksRecorded);
        Assert.Equal(0, second.ToolLinksRecorded);
        Assert.Equal(
            ["execute_tool|bash|0|1", "execute_tool|bash|1|1"],
            await LedgerSql.RowsAsync(
                path,
                "SELECT operation_name, tool_name, tool_exit_code, owner_span_row_id FROM span_links ORDER BY tool_exit_code;"));
    }

    [Fact]
    public async Task UA17_ACompactionAndAUsageRecordOfOneCallAreChargedOnce()
    {
        using TempDirectory temp = new();
        (SqliteLedgerStore store, string path) = await StoreAsync(temp);
        await store.RecordAsync([ImportEntries.Live("session-b", S2)]);

        ImportRecordResult result = await store.RecordImportedAsync(
            [
                ImportEntries.Copilot("usage-1", "session-a", S1),
                ImportEntries.Copilot(S1, "session-a", S1, ImportedRecordKind.Compaction),
                ImportEntries.Copilot(S2, "session-b", S2, ImportedRecordKind.Compaction),
            ],
            null);

        Assert.Equal(
            [ImportResolution.Recorded, ImportResolution.Duplicate, ImportResolution.OverlapSkipped],
            result.Resolutions);
        Assert.Equal(
            ["1|otlp|effective|null", "2|import:copilot|effective|null"],
            await LedgerSql.RowsAsync(path, RowsSql));
        Assert.Equal(
            ["duplicate|2", "live|1", "recorded|2"],
            await LedgerSql.RowsAsync(path, "SELECT resolution, span_row_id FROM import_record_aliases ORDER BY resolution;"));
        await ImportEntries.AssertInvariantsAsync(path);
    }

    private static async Task<(SqliteLedgerStore Store, string Path)> StoreAsync(TempDirectory temp)
    {
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path);
        await store.InitializeAsync();
        return (store, path);
    }
}
