using System.Security.Cryptography;
using System.Text.Json.Nodes;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Storage.Import;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Import;

public sealed class ImportRepairTests
{
    private const string S1 = "11111111-1111-4111-8111-111111111111";
    private const string S2 = "22222222-2222-4222-8222-222222222222";
    private const string S3 = "33333333-3333-4333-8333-333333333333";
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonRateCardCalculator Embedded = JsonRateCardCalculator.LoadEmbedded();

    [Fact]
    public async Task RR1_AnOldCompleteCheckpointRepricesItsImportedRow()
    {
        using TempDirectory temp = new();
        await PrepareReportedImportAsync(temp, "s-a");

        ImportSourceReport report = Copilot(await RunAsync(temp));

        Assert.Equal((0, 1, 1), (report.SessionsSkippedUnchanged, report.Duplicates, report.Repriced));
        Assert.Equal(
            ["473886000|reported|1500000000"],
            await LedgerSql.RowsAsync(
                Database(temp), "SELECT cost_usd_pico, charge_source, list_cost_usd_pico FROM spans;"));
        Assert.Equal(
            1,
            (await (await StoreAsync(temp)).GetImportSessionAsync(ImportSourceKind.Copilot, "s-a"))!.ImportRevision);
    }

    [Fact]
    public async Task RR2_TheRepairedLogIsSkippedOnTheNextRun()
    {
        using TempDirectory temp = new();
        await PrepareReportedImportAsync(temp, "s-a");
        await RunAsync(temp);

        ImportSourceReport report = Copilot(await RunAsync(temp));

        Assert.Equal((1, 0), (report.SessionsSkippedUnchanged, report.Repriced));
    }

    [Fact]
    public async Task RR3_AReportedLogChargeRepairsItsOverlappingLiveRow()
    {
        using TempDirectory temp = new();
        await PrepareLiveAsync(temp, "s-a", nullListCost: true);
        WriteReported(temp, "s-a");

        ImportSourceReport report = Copilot(await RunAsync(temp));

        Assert.Equal((1, 1), (report.OverlapSkipped, report.LiveRepriced));
        Assert.Equal(
            ["473886000|750000000|reported"],
            await LedgerSql.RowsAsync(
                Database(temp), "SELECT cost_usd_pico, list_cost_usd_pico, charge_source FROM spans;"));
    }

    [Fact]
    public async Task RR4_AReportedLogChargeRepairsAnEarlierLiveAlias()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await PrepareLiveAsync(temp, "s-a", nullListCost: true);
        WriteReported(temp, "s-a");
        ImportedLedgerEntry entry = await ReadEntryAsync(temp, "s-a", Embedded);
        await store.RecordImportedAsync([AsRateCard(entry)], null);

        ImportSourceReport report = Copilot(await RunAsync(temp));

        Assert.Equal((1, 1), (report.Duplicates, report.LiveRepriced));
        Assert.Equal(
            ["473886000|750000000|reported"],
            await LedgerSql.RowsAsync(
                Database(temp), "SELECT cost_usd_pico, list_cost_usd_pico, charge_source FROM spans;"));
    }

    [Fact]
    public async Task RR5_AnAlreadyReportedLiveRowIsNotChanged()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        LedgerEntry live = ImportEntries.Live("s-a", S1);
        await store.RecordAsync([live with
        {
            Calculation = live.Calculation with
            {
                UsdCost = 0.0004m,
                ChargeSource = ChargeSource.Reported,
            },
        }]);
        WriteReported(temp, "s-a");

        ImportSourceReport report = Copilot(await RunAsync(temp));

        Assert.Equal(0, report.LiveRepriced);
        Assert.Equal(
            ["400000000|reported"],
            await LedgerSql.RowsAsync(Database(temp), "SELECT cost_usd_pico, charge_source FROM spans;"));
    }

    [Fact]
    public async Task RR6_AnUnidentifiedLiveRowMakesTheRecordAmbiguousAndUnchanged()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        await store.RecordAsync([ImportEntries.Live("s-a", null)]);
        WriteReported(temp, "s-a");

        ImportSourceReport report = Copilot(await RunAsync(temp));

        Assert.Equal((1, 0), (report.AmbiguousOverlap, report.LiveRepriced));
        Assert.Equal(
            ["750000000|rate_card"],
            await LedgerSql.RowsAsync(Database(temp), "SELECT cost_usd_pico, charge_source FROM spans;"));
    }

    [Fact]
    public async Task RR7_AnOverlayNowKnownAsLocalRepricesAnImportedByokRow()
    {
        using TempDirectory temp = new();
        await PrepareByokImportAsync(temp, "s-local", Embedded);
        JsonRateCardCalculator calculator = await LocalOverlayAsync(temp);

        ImportSourceReport report = Copilot(await RunAsync(temp, calculator));

        Assert.Equal(1, report.Repriced);
        Assert.Equal(
            ["local|local_offload|750000000"],
            await LedgerSql.RowsAsync(
                Database(temp), "SELECT billing_channel, pricing_status, saved_usd_pico FROM spans;"));
    }

    [Fact]
    public async Task RR8_AnIncompleteCompactionCheckpointIsReadAndCompleted()
    {
        using TempDirectory temp = new();
        string path = new CopilotSessionLogs("s-a")
            .Compaction(
                null,
                change: data =>
                {
                    JsonObject used = data["compactionTokensUsed"]!.AsObject();
                    used.Remove("provider");
                    used.Remove("copilotUsage");
                })
            .Write(CopilotHome(temp));
        SqliteLedgerStore store = await StoreAsync(temp);
        SessionLogLocation log = Assert.Single(new CopilotSessionLogSource().Discover(CopilotHome(temp)).Logs);
        SessionLogReadResult read = await new CopilotSessionLogSource().ReadAsync(log, CancellationToken.None);
        await store.RecordImportedAsync([], Checkpoint(log, read, complete: false, revision: 0));

        ImportSourceReport report = Copilot(await RunAsync(temp));

        Assert.Equal(1, report.Recorded);
        ImportSessionCheckpoint checkpoint =
            (await store.GetImportSessionAsync(ImportSourceKind.Copilot, "s-a"))!;
        Assert.True(checkpoint.Complete);
        Assert.Equal(1, checkpoint.ImportRevision);
        Assert.Equal(1L, await LedgerSql.ScalarAsync(Database(temp), "SELECT COUNT(*) FROM spans;"));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task RR9_DryRunReportsTheSameRepairsWithoutWriting()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await PrepareReportedImportAsync(temp, "s-repair", S2);
        await PrepareLiveAsync(temp, "s-live", store);
        WriteReported(temp, "s-live");
        await PrepareByokImportAsync(temp, "s-local", Embedded, store);
        JsonRateCardCalculator calculator = await LocalOverlayAsync(temp);
        SqliteConnection.ClearAllPools();
        byte[] before = SHA256.HashData(await FileRetry.ReadAllBytesAsync(Database(temp)));

        ImportSourceReport dry = Copilot(await RunAsync(temp, calculator, request => request with { DryRun = true }));

        Assert.Equal(before, SHA256.HashData(await FileRetry.ReadAllBytesAsync(Database(temp))));
        Assert.Equal((2, 1), (dry.Repriced, dry.LiveRepriced));
        ImportSourceReport real = Copilot(await RunAsync(temp, calculator));
        Assert.Equal((dry.Repriced, dry.LiveRepriced), (real.Repriced, real.LiveRepriced));
    }

    [Fact]
    public async Task RR10_DryRunRepairsAFormat6RowWithoutMigratingIt()
    {
        using TempDirectory temp = new();
        await PrepareReportedImportAsync(temp, "s-a");
        await LedgerSql.ExecuteAsync(Database(temp), """
            ALTER TABLE spans DROP COLUMN list_cost_usd_pico;
            ALTER TABLE spans DROP COLUMN charge_source;
            ALTER TABLE import_sessions DROP COLUMN import_revision;
            DROP INDEX ix_span_links_import_span;
            PRAGMA user_version = 6;
            """);
        SqliteConnection.ClearAllPools();
        byte[] before = SHA256.HashData(await FileRetry.ReadAllBytesAsync(Database(temp)));

        ImportSourceReport report = Copilot(await RunAsync(temp, request => request with { DryRun = true }));

        Assert.Equal(1, report.Repriced);
        Assert.Equal(before, SHA256.HashData(await FileRetry.ReadAllBytesAsync(Database(temp))));
        Assert.Equal(6L, await LedgerSql.ScalarAsync(Database(temp), "PRAGMA user_version;"));
    }

    [Fact]
    public async Task RR11_BudgetsSeeTheReportedLiveChargeAfterRepair()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await PrepareLiveAsync(
            temp, "s-a", withGit: true, nullListCost: true);
        await store.SetBranchBudgetAsync("contoso/webapp", "main", 1m);
        WriteReported(temp, "s-a");

        await RunAsync(temp);

        BranchBudgetRow budget = Assert.Single(
            await store.GetBranchBudgetsAsync("contoso/webapp", "main"));
        Assert.Equal(0.0473886m, budget.SpentAiCredits);
    }

    [Fact]
    public async Task RL1_ReimportRelinksToolCallsToTheRequestThatAsked()
    {
        using TempDirectory temp = new();
        await PrepareMisownedToolLinksAsync(temp, "s-links");

        ImportSourceReport report = Copilot(await RunAsync(temp));

        Assert.Equal((0, 2), (report.ToolLinksRecorded, report.ToolLinksRelinked));
        Assert.Equal(
            ["1|1", "1|1"],
            await LedgerSql.RowsAsync(
                Database(temp),
                $"""
                SELECT l.owner_span_row_id = (SELECT id FROM spans WHERE canonical_usage_id = '{Canonical(S1)}'),
                       l.trace_id = (SELECT trace_id FROM spans WHERE canonical_usage_id = '{Canonical(S1)}')
                  FROM span_links AS l
                 WHERE l.owner_span_row_id IS NOT NULL
                 ORDER BY l.span_id;
                """));
        Assert.Equal(
            2L,
            await LedgerSql.ScalarAsync(
                Database(temp), "SELECT COUNT(*) FROM span_links WHERE owner_span_row_id IS NOT NULL;"));
    }

    [Fact]
    public async Task RL2_ARepairedLogIsUnchangedOnTheNextRun()
    {
        using TempDirectory temp = new();
        await PrepareMisownedToolLinksAsync(temp, "s-links");
        await RunAsync(temp);

        ImportSourceReport report = Copilot(await RunAsync(temp));

        Assert.Equal((1, 0, 0), (report.SessionsSkippedUnchanged, report.ToolLinksRecorded, report.ToolLinksRelinked));
    }

    [Fact]
    public async Task RL3_ReimportRemovesALinkWhoseRequestWasOverlapSkipped()
    {
        using TempDirectory temp = new();
        await PrepareSupersededToolLinkAsync(temp, "s-removed", S3);

        ImportSourceReport report = Copilot(await RunAsync(temp));

        Assert.Equal(1, report.ToolLinksRemoved);
        Assert.Equal(0L, await LedgerSql.ScalarAsync(
            Database(temp), "SELECT COUNT(*) FROM span_links WHERE owner_span_row_id IS NOT NULL;"));
    }

    [Fact]
    public async Task RL4_ReimportLeavesALiveLinkWithTheSameSpanIdUntouched()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await PrepareSupersededToolLinkAsync(temp, "s-live-link", S3);
        string spanId = Assert.Single(await LedgerSql.RowsAsync(
            Database(temp), "SELECT span_id FROM span_links WHERE owner_span_row_id IS NOT NULL;"));
        await store.RecordSpanLinksAsync(
        [
            new GenAiSpanLink
            {
                TraceId = new string('f', 32),
                SpanId = spanId,
                OperationName = "execute_tool",
                ConversationId = "s-live-link",
                StartTime = Now,
                ToolName = "bash",
            },
        ]);

        ImportSourceReport report = Copilot(await RunAsync(temp));

        Assert.Equal(1, report.ToolLinksRemoved);
        Assert.Equal(
            1L,
            await LedgerSql.ScalarAsync(
                Database(temp),
                $"SELECT COUNT(*) FROM span_links WHERE owner_span_row_id IS NULL AND span_id = '{spanId}' " +
                $"AND trace_id = '{new string('f', 32)}';"));
    }

    [Fact]
    public async Task RL5_DryRunReportsRelinksAndRemovalsWithoutWriting()
    {
        using TempDirectory temp = new();
        WriteOneToolCall(temp, "s-removes", S3, complete: true);
        await PrepareMisownedToolLinksAsync(temp, "s-relinks");
        SqliteLedgerStore store = await StoreAsync(temp);
        await store.RecordAsync([ImportEntries.Live("s-removes", S3)]);
        await LedgerSql.ExecuteAsync(
            Database(temp),
            "UPDATE import_sessions SET import_revision = 0 WHERE log_id = 's-removes';");
        Assert.Equal(
            ["0", "0"],
            await LedgerSql.RowsAsync(
                Database(temp), "SELECT import_revision FROM import_sessions ORDER BY log_id;"));
        Assert.Equal(
            1L,
            await LedgerSql.ScalarAsync(
                Database(temp),
                "SELECT COUNT(*) FROM span_links WHERE conversation_id = 's-removes' " +
                "AND owner_span_row_id IS NOT NULL;"));
        SqliteConnection.ClearAllPools();
        byte[] before = SHA256.HashData(await FileRetry.ReadAllBytesAsync(Database(temp)));

        ImportSourceReport dry = Copilot(await RunAsync(
            temp, request => request with { DryRun = true }));

        Assert.Equal(before, SHA256.HashData(await FileRetry.ReadAllBytesAsync(Database(temp))));
        Assert.Equal((2, 1), (dry.ToolLinksRelinked, dry.ToolLinksRemoved));
        ImportSourceReport real = Copilot(await RunAsync(temp));
        Assert.Equal(
            (dry.ToolLinksRelinked, dry.ToolLinksRemoved),
            (real.ToolLinksRelinked, real.ToolLinksRemoved));
    }

    [Fact]
    public async Task RL6_ReimportRecordsANewlyCompletedToolCall()
    {
        using TempDirectory temp = new();
        WriteOneToolCall(temp, "s-new-tool", S3, complete: false);
        await RunAsync(temp);
        WriteOneToolCall(temp, "s-new-tool", S3, complete: true);

        ImportSourceReport report = Copilot(await RunAsync(temp));

        Assert.Equal((1, 0), (report.ToolLinksRecorded, report.ToolLinksRelinked));
        Assert.Equal(1L, await LedgerSql.ScalarAsync(
            Database(temp), "SELECT COUNT(*) FROM span_links WHERE owner_span_row_id IS NOT NULL;"));
    }

    private static async Task<SqliteLedgerStore> PrepareReportedImportAsync(
        TempDirectory temp,
        string session,
        string? serviceRequestId = S1)
    {
        SqliteLedgerStore store = await StoreAsync(temp);
        WriteReported(temp, session, serviceRequestId);
        ImportedLedgerEntry entry = await ReadEntryAsync(temp, session, Embedded);
        SessionLogLocation log = Assert.Single(new CopilotSessionLogSource().Discover(CopilotHome(temp)).Logs, item => item.LogId == session);
        SessionLogReadResult read = await new CopilotSessionLogSource().ReadAsync(log, CancellationToken.None);
        await store.RecordImportedAsync([AsRateCard(entry)], Checkpoint(log, read, true, 0));
        return store;
    }

    private static async Task<SqliteLedgerStore> PrepareByokImportAsync(
        TempDirectory temp,
        string session,
        JsonRateCardCalculator calculator,
        SqliteLedgerStore? existing = null)
    {
        SqliteLedgerStore store = existing ?? await StoreAsync(temp);
        new CopilotSessionLogs(session)
            .Usage("u-" + session, null, model: "rr7-local", providerType: "anthropic")
            .Write(CopilotHome(temp));
        SessionLogLocation log = Assert.Single(
            new CopilotSessionLogSource().Discover(CopilotHome(temp)).Logs, item => item.LogId == session);
        SessionLogReadResult read = await new CopilotSessionLogSource().ReadAsync(log, CancellationToken.None);
        ImportedUsageRecord record = Assert.Single(read.Records);
        NormalizedGenAiSpan span = SessionImportMapper.ToSpan(record);
        ImportedLedgerEntry entry = new()
        {
            Source = record.Source,
            RecordKeySha256 = ImportIds.RecordKeySha256(record.Source, record.Kind, record.RecordKey),
            Span = span,
            Calculation = calculator.Calculate(span),
            Git = record.Git,
        };
        await store.RecordImportedAsync([entry], Checkpoint(log, read, true, 0));
        return store;
    }

    private static async Task PrepareMisownedToolLinksAsync(TempDirectory temp, string session)
    {
        Tl1(session).Write(CopilotHome(temp));
        await RunAsync(temp);
        await LedgerSql.ExecuteAsync(
            Database(temp),
            $"""
            UPDATE span_links
               SET owner_span_row_id = (SELECT id FROM spans WHERE canonical_usage_id = '{Canonical(S2)}'),
                   trace_id = (SELECT trace_id FROM spans WHERE canonical_usage_id = '{Canonical(S2)}')
             WHERE owner_span_row_id IS NOT NULL;
            UPDATE import_sessions SET import_revision = 0 WHERE log_id = '{session}';
            """);
    }

    private static async Task<SqliteLedgerStore> PrepareSupersededToolLinkAsync(
        TempDirectory temp,
        string session,
        string serviceRequestId)
    {
        WriteOneToolCall(temp, session, serviceRequestId, complete: true);
        await RunAsync(temp);
        SqliteLedgerStore store = await StoreAsync(temp);
        await store.RecordAsync([ImportEntries.Live(session, serviceRequestId)]);
        await LedgerSql.ExecuteAsync(
            Database(temp),
            $"UPDATE import_sessions SET import_revision = 0 WHERE log_id = '{session}';");
        return store;
    }

    private static CopilotSessionLogs Tl1(string session)
    {
        CopilotSessionLogs log = new CopilotSessionLogs(session).AssistantMessage("api-A");
        string messageA = log.LastId;
        log.Usage("api-A", S1, numToolCalls: 2)
            .AssistantMessage("api-B")
            .Usage("api-B", S2);
        log.ToolStart("t1", "bash", parentId: messageA);
        string startA = log.LastId;
        return log.ToolStart("t2", "bash", parentId: startA)
            .ToolComplete("t1", true, parentId: startA)
            .ToolComplete("t2", true);
    }

    private static void WriteOneToolCall(
        TempDirectory temp,
        string session,
        string serviceRequestId,
        bool complete)
    {
        string apiCallId = "api-" + session;
        CopilotSessionLogs log = new CopilotSessionLogs(session).AssistantMessage(apiCallId);
        string message = log.LastId;
        log.Usage(apiCallId, serviceRequestId, numToolCalls: complete ? 1 : 0);
        if (complete)
        {
            log.ToolStart("t1", "bash", parentId: message).ToolComplete("t1", true);
        }

        log.Write(CopilotHome(temp));
    }

    private static string Canonical(string serviceRequestId) =>
        UsageIdentity.CanonicalUsageId("github", serviceRequestId, BillingChannel.GitHubAiCredits)!;

    private static async Task<SqliteLedgerStore> PrepareLiveAsync(
        TempDirectory temp,
        string conversation,
        SqliteLedgerStore? existing = null,
        bool withGit = false,
        bool nullListCost = false)
    {
        SqliteLedgerStore store = existing ?? await StoreAsync(temp);
        LedgerEntry live = ImportEntries.Live(conversation, S1);
        if (withGit)
        {
            string root = Path.Combine(temp.Path, "repo");
            live = live with
            {
                Git = new GitWorkspaceContext(
                    root,
                    Path.Combine(root, ".git"),
                    "main",
                    new string('a', 40),
                    "https://github.com/contoso/webapp.git",
                    "contoso/webapp"),
            };
        }

        await store.RecordAsync([live]);
        if (nullListCost)
        {
            await LedgerSql.ExecuteAsync(
                Database(temp),
                "UPDATE spans SET list_cost_usd_pico = NULL WHERE ingest_source = 'otlp';");
        }

        return store;
    }

    private static ImportedLedgerEntry AsRateCard(ImportedLedgerEntry entry)
    {
        CreditCalculationResult result = entry.Calculation;
        return entry with
        {
            Calculation = result with
            {
                UsdCost = result.ListUsdCost,
                AiCredits = result.ListUsdCost / 0.01m,
                ChargeSource = ChargeSource.RateCard,
            },
        };
    }

    private static async Task<ImportedLedgerEntry> ReadEntryAsync(
        TempDirectory temp,
        string session,
        JsonRateCardCalculator calculator)
    {
        CopilotSessionLogSource source = new();
        SessionLogLocation log = Assert.Single(
            source.Discover(CopilotHome(temp)).Logs, item => item.LogId == session);
        SessionLogReadResult read = await source.ReadAsync(log, CancellationToken.None);
        ImportedUsageRecord record = Assert.Single(read.Records);
        NormalizedGenAiSpan span = SessionImportMapper.ToSpan(record);
        return new ImportedLedgerEntry
        {
            Source = record.Source,
            RecordKeySha256 = ImportIds.RecordKeySha256(record.Source, record.Kind, record.RecordKey),
            Span = span,
            Calculation = calculator.Calculate(span),
            Git = record.Git,
        };
    }

    private static string WriteReported(
        TempDirectory temp,
        string session,
        string? serviceRequestId = S1) =>
        new CopilotSessionLogs(session)
            .Usage("u-" + session, serviceRequestId, totalNanoAiu: 47_388_600)
            .Write(CopilotHome(temp));

    private static async Task<JsonRateCardCalculator> LocalOverlayAsync(TempDirectory temp)
    {
        string path = Path.Combine(temp.Path, "local-rates.json");
        await File.WriteAllTextAsync(
            path,
            """{ "schemaVersion": 1, "localOffload": { "models": [ { "id": "rr7-local", "aliases": [] } ] } }""");
        return JsonRateCardCalculator.LoadEmbeddedWithOverlay(path);
    }

    private static ImportSessionCheckpoint Checkpoint(
        SessionLogLocation log,
        SessionLogReadResult read,
        bool complete,
        int revision) => new()
    {
        Source = log.Source,
        LogId = log.LogId,
        LogSizeBytes = log.SizeBytes,
        LogLastWriteTime = log.LastWriteTime,
        LogSha256 = read.ContentSha256!,
        Complete = complete,
        Recorded = complete ? 1 : 0,
        Duplicates = 0,
        OverlapSkipped = 0,
        AmbiguousOverlap = 0,
        ImportRevision = revision,
    };

    private static Task<ImportReport> RunAsync(
        TempDirectory temp,
        Func<ImportRequest, ImportRequest>? change) =>
        RunAsync(temp, Embedded, change);

    private static Task<ImportReport> RunAsync(
        TempDirectory temp,
        JsonRateCardCalculator? calculator = null,
        Func<ImportRequest, ImportRequest>? change = null)
    {
        ImportRequest request = new()
        {
            DatabasePath = Database(temp),
            Sources = [new ImportSourceRequest(new CopilotSessionLogSource(), CopilotHome(temp))],
        };
        return new LedgerImporter(calculator ?? Embedded, new ManualTimeProvider(Now))
            .RunAsync(change?.Invoke(request) ?? request);
    }

    private static async Task<SqliteLedgerStore> StoreAsync(TempDirectory temp)
    {
        SqliteLedgerStore store = new(Database(temp));
        await store.InitializeAsync();
        return store;
    }

    private static string CopilotHome(TempDirectory temp) => Path.Combine(temp.Path, "copilot");
    private static string Database(TempDirectory temp) => Path.Combine(temp.Path, "ledger.db");

    private static ImportSourceReport Copilot(ImportReport report) =>
        Assert.Single(report.Sources, source => source.Source == ImportSourceKind.Copilot);
}
