using System.Security.Cryptography;
using CopilotLedger.Core.Import;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Storage;

public sealed class LedgerSnapshotTests
{
    private const string S1 = "11111111-1111-4111-8111-111111111111";
    private const string S2 = "22222222-2222-4222-8222-222222222222";

    [Fact]
    public async Task SN1_WithoutALedgerNothingIsCreated()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "data", "ledger.db");

        await using (LedgerSnapshot snapshot = await LedgerSnapshot.OpenAsync(path))
        {
            ImportRecordResult result = await snapshot.EvaluateAsync(
                [ImportEntries.Copilot("usage-1", "session-a", S1), ImportEntries.Copilot("usage-1", "session-a", S1)]);

            Assert.Equal(0, snapshot.SchemaVersion);
            Assert.Equal([ImportResolution.Recorded, ImportResolution.Duplicate], result.Resolutions);
            Assert.Null(await snapshot.GetImportSessionAsync(ImportSourceKind.Copilot, "session-a"));
        }

        Assert.False(Directory.Exists(Path.Combine(temp.Path, "data")));
    }

    [Fact]
    public async Task SN2_AnImmutableReadLeavesTheLedgerAndItsCompanionsAsTheyWere()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path);
        await store.InitializeAsync();
        await store.RecordAsync([ImportEntries.Live("session-a", S1)]);
        SqliteConnection.ClearAllPools();
        Assert.False(File.Exists(path + "-wal"));
        Assert.False(File.Exists(path + "-shm"));
        byte[] before = SHA256.HashData(await FileRetry.ReadAllBytesAsync(path));

        await using (LedgerSnapshot snapshot = await LedgerSnapshot.OpenAsync(path))
        {
            ImportRecordResult result = await snapshot.EvaluateAsync([ImportEntries.Copilot("usage-1", "session-a", S1)]);

            Assert.Equal(7, snapshot.SchemaVersion);
            Assert.Equal([ImportResolution.OverlapSkipped], result.Resolutions);
            Assert.False(File.Exists(path + "-wal"));
            Assert.False(File.Exists(path + "-shm"));
        }

        Assert.False(File.Exists(path + "-wal"));
        Assert.False(File.Exists(path + "-shm"));
        Assert.Equal(before, SHA256.HashData(await FileRetry.ReadAllBytesAsync(path)));
    }

    [Fact]
    public async Task SN3_AVersion5LedgerIsntMigratedAndDefersItsLiveConversations()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path);
        await store.InitializeAsync();
        await store.RecordAsync([ImportEntries.Live("session-a", S1)]);
        await LedgerSql.ExecuteAsync(path, """
            ALTER TABLE spans DROP COLUMN list_cost_usd_pico;
            ALTER TABLE spans DROP COLUMN charge_source;
            ALTER TABLE import_sessions DROP COLUMN import_revision;
            DROP INDEX ix_span_links_import_span;
            DROP TABLE import_record_aliases;
            DROP TABLE import_sessions;
            DROP INDEX ux_spans_effective_import_usage;
            DROP INDEX ix_spans_canonical_usage;
            ALTER TABLE span_links DROP COLUMN owner_span_row_id;
            ALTER TABLE spans DROP COLUMN superseded_by;
            ALTER TABLE spans DROP COLUMN imported_tool_call_count;
            ALTER TABLE spans DROP COLUMN accounting_state;
            ALTER TABLE spans DROP COLUMN canonical_usage_id;
            ALTER TABLE spans DROP COLUMN ingest_source;
            PRAGMA user_version = 5;
            """);
        SqliteConnection.ClearAllPools();

        await using (LedgerSnapshot snapshot = await LedgerSnapshot.OpenAsync(path))
        {
            ImportRecordResult result = await snapshot.EvaluateAsync(
                [ImportEntries.Copilot("usage-1", "session-a", S1), ImportEntries.Copilot("usage-2", "session-b", S2)]);

            Assert.Equal(5, snapshot.SchemaVersion);
            Assert.Equal([ImportResolution.AmbiguousOverlap, ImportResolution.Recorded], result.Resolutions);
            Assert.Null(await snapshot.GetImportSessionAsync(ImportSourceKind.Copilot, "session-a"));
        }

        Assert.Equal(5L, await LedgerSql.ScalarAsync(path, "PRAGMA user_version;"));
    }

    [Fact]
    public async Task SN4_ASnapshotDecidesAsTheImportWill()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path);
        await store.InitializeAsync();
        await store.RecordAsync([ImportEntries.Live("session-a", S1)]);
        await store.RecordImportedAsync([ImportEntries.Copilot("usage-0", "session-c", null, byok: true)], null);
        ImportedLedgerEntry[] entries =
        [
            ImportEntries.Copilot("usage-1", "session-a", S1),
            ImportEntries.Copilot("usage-2", "session-b", S2),
            ImportEntries.Copilot("usage-2", "session-b", S2),
            ImportEntries.Copilot(S2, "session-b", S2, ImportedRecordKind.Compaction),
            ImportEntries.Copilot("usage-3", "session-a", null, byok: true),
            ImportEntries.Copilot("usage-0", "session-c", null, byok: true),
        ];

        ImportRecordResult dry;
        await using (LedgerSnapshot snapshot = await LedgerSnapshot.OpenAsync(path))
        {
            dry = await snapshot.EvaluateAsync(entries);
        }

        ImportRecordResult real = await store.RecordImportedAsync(entries, null);

        Assert.Equal(
            [
                ImportResolution.OverlapSkipped,
                ImportResolution.Recorded,
                ImportResolution.Duplicate,
                ImportResolution.Duplicate,
                ImportResolution.AmbiguousOverlap,
                ImportResolution.Duplicate,
            ],
            real.Resolutions);
        Assert.Equal(real.Resolutions, dry.Resolutions);
    }

    [Fact]
    public async Task SN5_ANewerLedgerIsRejected()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        await new SqliteLedgerStore(path).InitializeAsync();
        await LedgerSql.ExecuteAsync(path, "PRAGMA user_version = 99;");
        SqliteConnection.ClearAllPools();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => LedgerSnapshot.OpenAsync(path));

        Assert.Equal("Database user_version 99 is newer than the supported version 7.", exception.Message);
    }

    [Fact]
    public async Task SN6_ASnapshotReadsCheckpoints()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path, new ManualTimeProvider(new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.Zero)));
        await store.InitializeAsync();
        ImportSessionCheckpoint checkpoint = new()
        {
            Source = ImportSourceKind.Codex,
            LogId = "archived/019a1b2c-3d4e-7f50-8a6b-7c8d9e0f1a2b",
            LogSizeBytes = 10,
            LogLastWriteTime = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero),
            LogSha256 = new string('c', 64),
            Complete = true,
            Recorded = 0,
            Duplicates = 0,
            OverlapSkipped = 0,
            AmbiguousOverlap = 0,
        };
        await store.RecordImportedAsync([], checkpoint);

        await using LedgerSnapshot snapshot = await LedgerSnapshot.OpenAsync(path);

        Assert.Equal(
            checkpoint with { ImportedAt = new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.Zero) },
            await snapshot.GetImportSessionAsync(ImportSourceKind.Codex, checkpoint.LogId));
    }

    // With only one of -wal and -shm, a read-only open could create the other, so the snapshot refuses before opening.
    [Theory]
    [InlineData("-wal", "-shm")]
    [InlineData("-shm", "-wal")]
    public async Task SN7_ALedgerWithOneCompanionFileIsRefusedUnchanged(string present, string missing)
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path);
        await store.InitializeAsync();
        await store.RecordAsync([ImportEntries.Live("session-a", S1)]);
        SqliteConnection.ClearAllPools();
        await File.WriteAllBytesAsync(path + present, []);
        DateTime written = File.GetLastWriteTimeUtc(path + present);
        byte[] before = SHA256.HashData(await FileRetry.ReadAllBytesAsync(path));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => LedgerSnapshot.OpenAsync(path));

        Assert.Equal(LedgerSnapshot.MissingCompanionMessage, exception.Message);
        Assert.False(File.Exists(path + missing));
        Assert.Equal(0, new FileInfo(path + present).Length);
        Assert.Equal(written, File.GetLastWriteTimeUtc(path + present));
        Assert.Equal(before, SHA256.HashData(await FileRetry.ReadAllBytesAsync(path)));
    }
}
