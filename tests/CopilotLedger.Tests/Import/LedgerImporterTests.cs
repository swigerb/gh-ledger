using System.Security.Cryptography;
using System.Text.Json.Nodes;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;
using CopilotLedger.Storage.Import;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Import;

public sealed class LedgerImporterTests
{
    private const string S1 = "11111111-1111-4111-8111-111111111111";
    private const string S2 = "22222222-2222-4222-8222-222222222222";
    private const string S3 = "33333333-3333-4333-8333-333333333333";
    private const string S4 = "44444444-4444-4444-8444-444444444444";
    private const string Thread = "019a1b2c-3d4e-7f50-8a6b-7c8d9e0f1a2b";
    private const string Fork = "019a1b2c-3d4e-7f50-8a6b-7c8d9e0f1a2c";

    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task IR1_ASecondRunSkipsUnchangedSessions()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs("s-a").Usage("u1", S1).Usage("u2", S2).Write(CopilotHome(temp));
        new CopilotSessionLogs("s-b").Usage("u3", S3).Write(CopilotHome(temp));

        ImportSourceReport first = Copilot(await RunAsync(temp));
        ImportSourceReport second = Copilot(await RunAsync(temp));

        Assert.Equal((2, 0, 3, 3), (first.SessionsScanned, first.SessionsSkippedUnchanged, first.RecordsParsed, first.Recorded));
        Assert.Equal((2, 2, 0, 0), (second.SessionsScanned, second.SessionsSkippedUnchanged, second.RecordsParsed, second.Recorded));
        Assert.True(first.Complete);
        Assert.Equal(
            first.RecordsParsed,
            first.RecordsOutOfScope + first.Recorded + first.Duplicates + first.OverlapSkipped + first.AmbiguousOverlap
            + first.UnsupportedUsageShapes + first.CostOutOfRange);
        Assert.Equal(3L, await LedgerSql.ScalarAsync(Database(temp), "SELECT COUNT(*) FROM spans;"));
    }

    [Theory]
    [InlineData("append", 1, 2, 3)]
    [InlineData("truncate", 0, 1, 3)]
    [InlineData("replace", 2, 0, 4)]
    [InlineData("rewrite", 0, 2, 2)]
    public async Task IR2_ChangedSessionsConverge(string change, long recorded, long duplicates, long rows)
    {
        using TempDirectory temp = new();
        string path = Log(change == "truncate" ? ["u1", "u2", "u3"] : ["u1", "u2"]).Write(CopilotHome(temp));
        await RunAsync(temp);
        DateTimeOffset written = new(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
        string[] keys = change switch
        {
            "append" => ["u1", "u2", "u3"],
            "truncate" => ["u1"],
            "replace" => ["u3", "u4"],
            _ => ["u1", "u2"],
        };
        Log(keys).Write(CopilotHome(temp));
        File.SetLastWriteTimeUtc(path, written.UtcDateTime.AddMinutes(1));

        ImportSourceReport second = Copilot(await RunAsync(temp));
        ImportSourceReport third = Copilot(await RunAsync(temp));

        Assert.Equal((recorded, duplicates), (second.Recorded, second.Duplicates));
        Assert.Equal(1, third.SessionsSkippedUnchanged);
        Assert.Equal(rows, await LedgerSql.ScalarAsync(Database(temp), "SELECT COUNT(*) FROM spans;"));

        static CopilotSessionLogs Log(string[] keys)
        {
            CopilotSessionLogs log = new("s-a");
            foreach (string key in keys)
            {
                log.Usage(key, $"{key[1]}{key[1]}{key[1]}{key[1]}0000-0000-4000-8000-000000000000");
            }

            return log;
        }
    }

    [Fact]
    public async Task IR3_ARunCancelledAfterItsFirstBatchConvergesWhenRunAgain()
    {
        using TempDirectory temp = new();
        CopilotSessionLogs log = new("s-a");
        for (int index = 1; index <= 5; index++)
        {
            log.Usage($"u{index}", $"{index}{index}{index}{index}0000-0000-4000-8000-000000000000");
        }

        log.Write(CopilotHome(temp));
        using CancellationTokenSource cancel = new();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RunAsync(
            temp,
            request => request with { BatchSize = 2, BatchCommitted = _ => cancel.Cancel() },
            cancel.Token));
        long afterCancel = (long)(await LedgerSql.ScalarAsync(Database(temp), "SELECT COUNT(*) FROM spans;"))!;
        ImportSourceReport rerun = Copilot(await RunAsync(temp, request => request with { BatchSize = 2 }));

        Assert.Equal(2, afterCancel);
        Assert.Equal((3, 2), (rerun.Recorded, rerun.Duplicates));
        Assert.True(rerun.Complete);
        Assert.Equal(5L, await LedgerSql.ScalarAsync(Database(temp), "SELECT COUNT(*) FROM spans;"));
    }

    [Fact]
    public async Task IR4_SinceSkipsOldSessionsAndOldRecords()
    {
        using TempDirectory temp = new();
        string old = new CopilotSessionLogs("s-old", start: new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero))
            .Usage("u1", S1)
            .Write(CopilotHome(temp));
        File.SetLastWriteTimeUtc(old, new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc));
        CopilotSessionLogs mixed = new("s-mixed", start: new DateTimeOffset(2026, 11, 19, 23, 59, 59, TimeSpan.Zero));
        mixed.Usage("u2", S2).Usage("u3", S3).Write(CopilotHome(temp));

        ImportSourceReport report = Copilot(await RunAsync(
            temp,
            request => request with { Since = new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero) }));

        Assert.Equal(1, report.SessionsSkippedOutOfScope);
        Assert.Equal((2, 1, 1), (report.RecordsParsed, report.RecordsOutOfScope, report.Recorded));
        Assert.True(report.Complete);
        Assert.False((await (await StoreAsync(temp)).GetImportSessionAsync(ImportSourceKind.Copilot, "s-mixed"))!.Complete);
    }

    [Fact]
    public async Task IR5_RepoImportsOnlyThatRepositorysRecords()
    {
        using TempDirectory temp = new();
        string root = GitFixture.Create(temp.Path);
        string nested = GitFixture.Create(Path.Combine(root, "vendor"));
        string other = Path.Combine(temp.Path, "other");
        new CopilotSessionLogs("s-root", context: CopilotSessionLogs.Context(root, root, "main", "contoso/webapp"))
            .Usage("u1", S1)
            .Write(CopilotHome(temp));
        new CopilotSessionLogs("s-cwd", context: CopilotSessionLogs.Context(Path.Combine(root, "src")))
            .Usage("u2", S2)
            .Write(CopilotHome(temp));
        new CopilotSessionLogs("s-nested", context: CopilotSessionLogs.Context(Path.Combine(nested, "src")))
            .Usage("u6", "66666666-6666-4666-8666-666666666666")
            .Write(CopilotHome(temp));
        new CopilotSessionLogs("s-gone", context: CopilotSessionLogs.Context(Path.Combine(root, "gone")))
            .Usage("u7", "77777777-7777-4777-8777-777777777777")
            .Write(CopilotHome(temp));
        new CopilotSessionLogs("s-other", context: CopilotSessionLogs.Context(other, other, "main", "contoso/other"))
            .Usage("u3", S3)
            .Write(CopilotHome(temp));
        new CopilotSessionLogs("s-moved", context: CopilotSessionLogs.Context(root, root, "main", "contoso/webapp"))
            .Usage("u4", S4)
            .Resume(CopilotSessionLogs.Context(other, other, "main", "contoso/other"))
            .Usage("u5", "55555555-5555-4555-8555-555555555555")
            .Write(CopilotHome(temp));
        new CodexRollouts(Thread, cwd: Path.Combine(root, "src"))
            .TurnContext("turn-1", "gpt-6.1-sol")
            .Usage("resp_1", "turn-1")
            .Write(CodexHome(temp));
        new CodexRollouts(Fork, cwd: Path.Combine(nested, "src"))
            .TurnContext("turn-1", "gpt-6.1-sol")
            .Usage("resp_2", "turn-1")
            .Write(CodexHome(temp));

        ImportReport report = await RunAsync(temp, request => request with { RepositoryRoot = root });

        ImportSourceReport copilot = Copilot(report);
        ImportSourceReport codex = Assert.Single(report.Sources, source => source.Source == ImportSourceKind.Codex);
        Assert.Equal((7, 4, 3), (copilot.RecordsParsed, copilot.RecordsOutOfScope, copilot.Recorded));
        Assert.Equal((2, 1, 1), (codex.RecordsParsed, codex.RecordsOutOfScope, codex.Recorded));
        Assert.Equal(
            ["null", "contoso/webapp", "contoso/webapp"],
            await LedgerSql.RowsAsync(
                Database(temp),
                "SELECT COALESCE(repo_slug, 'null') FROM spans WHERE ingest_source = 'import:copilot' ORDER BY id;"));
    }

    [Fact]
    public async Task IR6_ActiveSessionsNeedIncludeActive()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs("s-a").Usage("u1", S1).Write(CopilotHome(temp), active: true);

        ImportSourceReport skipped = Copilot(await RunAsync(temp));
        ImportSourceReport included = Copilot(await RunAsync(temp, request => request with { IncludeActive = true }));

        Assert.Equal((1, 0), (skipped.SessionsSkippedActive, skipped.Recorded));
        Assert.True(skipped.Complete);
        Assert.Equal((0, 1), (included.SessionsSkippedActive, included.Recorded));
    }

    [Fact]
    public async Task IR7_RequestsArePricedWithTheCurrentRatesAtTheirStartTime()
    {
        using TempDirectory temp = new();
        (string Session, DateTimeOffset Start, string Model, string Id)[] requests =
        [
            ("s-1", new DateTimeOffset(2026, 11, 30, 12, 0, 0, TimeSpan.Zero), "gemini-4-argon", S1),
            ("s-2", new DateTimeOffset(2026, 12, 2, 12, 0, 0, TimeSpan.Zero), "gemini-4-argon", S2),
            ("s-3", new DateTimeOffset(2027, 5, 16, 12, 0, 0, TimeSpan.Zero), "mai-code-2", S3),
            ("s-4", new DateTimeOffset(2027, 5, 17, 12, 0, 0, TimeSpan.Zero), "mai-code-2", S4),
        ];
        foreach ((string session, DateTimeOffset start, string model, string id) in requests)
        {
            new CopilotSessionLogs(session, start: start).Usage("u-" + session, id, model: model).Write(CopilotHome(temp));
        }

        ImportSourceReport report = Copilot(await RunAsync(temp));

        Assert.Equal(
            [
                "gemini-4-argon|priced|3000000000",
                "gemini-4-argon|no_rate_for_date|0",
                "mai-code-2|priced|750000000",
                "mai-code-2|priced|1500000000",
            ],
            await LedgerSql.RowsAsync(
                Database(temp),
                "SELECT model_id, pricing_status, cost_usd_pico FROM spans ORDER BY start_time_unix_ms;"));
        Assert.Equal(1, report.Unpriced);
        Assert.Equal(0.00525m, report.AiCreditsUsd);
        Assert.Equal(0.525m, report.AiCredits);
    }

    [Fact]
    public async Task IR8_ToolCallsAreImportedOnlyWithinTheLinkRetention()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs("s-old", start: new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero))
            .Usage("u-old", S1, numToolCalls: 3)
            .ToolStart("toolu-old", "bash")
            .ToolComplete("toolu-old", false, exitCode: 1)
            .Write(CopilotHome(temp));
        new CopilotSessionLogs("s-new", start: new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero))
            .Usage("u-new", S2, numToolCalls: 1)
            .ToolStart("toolu-new", "bash")
            .ToolComplete("toolu-new", false, exitCode: 2)
            .Write(CopilotHome(temp));

        ImportSourceReport report = Copilot(await RunAsync(temp));

        Assert.Equal(1, report.ToolLinksRecorded);
        Assert.Equal(
            ["1|bash|2|2"],
            await LedgerSql.RowsAsync(
                Database(temp),
                "SELECT owner_span_row_id, tool_name, tool_exit_code, status_code FROM span_links;"));
        Assert.Equal(
            ["s-new|1", "s-old|3"],
            await LedgerSql.RowsAsync(
                Database(temp),
                "SELECT conversation_id, imported_tool_call_count FROM spans ORDER BY conversation_id;"));
    }

    [Fact]
    public async Task IR9_ADryRunReportsWhatTheImportDoesAndWritesNothing()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        await store.RecordAsync([ImportEntries.Live("s-a", S1), ImportEntries.Live("s-b", null)]);
        SqliteConnection.ClearAllPools();
        new CopilotSessionLogs("s-a").Usage("u1", S1).Usage("u2", S2).Write(CopilotHome(temp));
        new CopilotSessionLogs("s-b").Usage("u3", S3).Write(CopilotHome(temp));
        new CopilotSessionLogs("s-c").Usage("u4", S4).Usage("u4", S4).Write(CopilotHome(temp));
        new CopilotSessionLogs("s-d").Usage("u4", S4).Raw("{oops").Write(CopilotHome(temp));
        byte[] before = SHA256.HashData(await FileRetry.ReadAllBytesAsync(Database(temp)));

        ImportReport dry = await RunAsync(temp, request => request with { DryRun = true });

        Assert.Equal(before, SHA256.HashData(await FileRetry.ReadAllBytesAsync(Database(temp))));
        Assert.False(File.Exists(Database(temp) + "-wal"));
        Assert.False(File.Exists(Database(temp) + "-shm"));
        ImportReport real = await RunAsync(temp);
        ImportSourceReport copilot = Copilot(real);
        Assert.Equal(
            (2, 2, 1, 1, 1),
            (copilot.Recorded, copilot.Duplicates, copilot.OverlapSkipped, copilot.AmbiguousOverlap, copilot.MalformedLines));
        Assert.True(dry.DryRun);
        Assert.Equal(real.Sources, dry.Sources);
        Assert.Equal(real.Diagnostics, dry.Diagnostics);
    }

    [Fact]
    public async Task IR10_ACodexResponseInSessionsAndArchivedSessionsIsChargedOnce()
    {
        using TempDirectory temp = new();
        CodexRollouts rollout = new CodexRollouts(Thread).TurnContext("turn-1", "gpt-6.1-sol").Usage("resp_1", "turn-1");
        rollout.Write(CodexHome(temp));
        rollout.Write(CodexHome(temp), archived: true);

        ImportReport report = await RunAsync(temp);

        ImportSourceReport codex = Assert.Single(report.Sources, source => source.Source == ImportSourceKind.Codex);
        Assert.Equal((2, 1, 1), (codex.SessionsScanned, codex.Recorded, codex.Duplicates));
        Assert.Equal(
            ["import:codex|unpriced|other_provider|priced"],
            await LedgerSql.RowsAsync(
                Database(temp),
                "SELECT ingest_source, billing_channel, pricing_status, api_equivalent_status FROM spans;"));
    }

    [Fact]
    public async Task IR11_ARecordWhoseCostIsOutOfRangeIsSkippedAndReported()
    {
        using TempDirectory temp = new();
        string overlay = Path.Combine(temp.Path, "rates.json");
        await File.WriteAllTextAsync(
            overlay,
            """
            {
              "schemaVersion": 2,
              "ratesVersion": "ir11",
              "apiEquivalent": {
                "models": [
                  { "id": "ir11-huge", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1000000, "outputPer1M": 1000000 } ] } ] }
                ]
              }
            }
            """);
        new CopilotSessionLogs("s-a")
            .Usage("u1", S1, model: "ir11-huge", input: 10_000_000)
            .Usage("u2", S2)
            .Write(CopilotHome(temp));

        ImportReport report = await RunAsync(temp, calculator: JsonRateCardCalculator.LoadEmbeddedWithOverlay(overlay));

        ImportSourceReport copilot = Copilot(report);
        Assert.Equal((1, 1), (copilot.CostOutOfRange, copilot.Recorded));
        Assert.False(copilot.Complete);
        ImportDiagnostic diagnostic = Assert.Single(report.Diagnostics);
        Assert.Equal(ImportDiagnosticCode.CostOutOfRange, diagnostic.Code);
        Assert.Equal("s-a", diagnostic.LogId);
        Assert.Equal(1, diagnostic.Count);
    }

    [Fact]
    public async Task IR12_IncompleteSessionsAreReadAgainAndUnsupportedOnesGetNoCheckpoint()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs("s-new", "1.0.120").Usage("u1", S1).Write(CopilotHome(temp));
        new CopilotSessionLogs("s-bad").Usage("u2", S2).Raw("{oops").Write(CopilotHome(temp));

        ImportSourceReport first = Copilot(await RunAsync(temp));
        ImportSourceReport second = Copilot(await RunAsync(temp));

        Assert.Equal((1, 1, 1), (first.SessionsUnsupported, first.MalformedLines, first.Recorded));
        Assert.False(first.Complete);
        Assert.Equal((0, 1, 1), (second.SessionsSkippedUnchanged, second.Duplicates, second.SessionsUnsupported));
        Assert.Null(await (await StoreAsync(temp)).GetImportSessionAsync(ImportSourceKind.Copilot, "s-new"));
    }

    [Fact]
    public async Task IR13_ASessionWithoutARecordedBranchKeepsNone()
    {
        using TempDirectory temp = new();
        string repo = GitFixture.Create(temp.Path);
        new CopilotSessionLogs("s-a", context: new JsonObject { ["cwd"] = repo, ["gitRoot"] = repo })
            .Usage("u1", S1)
            .Write(CopilotHome(temp));

        await RunAsync(temp, request => request with { RepositoryRoot = repo });

        Assert.Equal(
            [$"{repo}|null|null"],
            await LedgerSql.RowsAsync(
                Database(temp),
                "SELECT repo_root, COALESCE(branch_name, 'null'), COALESCE(head_commit_sha, 'null') FROM spans;"));
    }

    [Fact]
    public async Task IR14_OldSessionsAreCountedAsAggregateOnly()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs("s-old", "1.0.98").Usage("u1", S1).Write(CopilotHome(temp));
        new CopilotSessionLogs("s-new").Usage("u2", S2).Write(CopilotHome(temp));

        ImportReport report = await RunAsync(temp);

        ImportSourceReport copilot = Copilot(report);
        Assert.Equal((1, 1, 1), (copilot.SessionsUnsupported, copilot.SessionsAggregateOnly, copilot.Recorded));
        Assert.False(report.Complete);
        ImportDiagnostic diagnostic = Assert.Single(report.Diagnostics);
        Assert.Equal(ImportDiagnosticCode.AggregateOnlySession, diagnostic.Code);
        Assert.Equal("1.0.98", diagnostic.Version);
    }

    // Copilot 1.0.114 records the dated response model claude-haiku-4-5-20251001 for claude-haiku-4.5. Both models reach the
    // card as reported, so the record is priced exactly as its live span is.
    [Fact]
    public async Task IR15_ARecordIsPricedAsItsLiveSpanIs()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs("s-a", "1.0.114")
            .Usage(
                "u1",
                S1,
                model: "claude-haiku-4.5",
                responseModel: "claude-haiku-4-5-20251001",
                input: 100_000,
                output: 10_000,
                cacheRead: 40_000,
                cacheWrite: 20_000)
            .Write(CopilotHome(temp));
        CreditCalculationResult live = JsonRateCardCalculator.LoadEmbedded().Calculate(TestSpans.Chat(
            startTime: new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero),
            providerName: "github",
            requestModel: "claude-haiku-4.5",
            responseModel: "claude-haiku-4-5-20251001",
            serverAddress: "api.business.githubcopilot.com",
            inputTokens: 100_000,
            outputTokens: 10_000,
            cacheReadInputTokens: 40_000,
            cacheWriteInputTokens: 20_000));

        await RunAsync(temp);

        Assert.Equal(
            ("claude-haiku-4.5", PricingStatus.Priced, 0.119m, 11.9m, 0.119m),
            (live.ModelId, live.Status, live.UsdCost, live.AiCredits, live.ApiEquivalent.UsdCost));
        Assert.Equal(
            ["claude-haiku-4.5|claude-haiku-4.5|claude-haiku-4-5-20251001|github_ai_credits|priced|119000000000|priced|119000000000"],
            await LedgerSql.RowsAsync(
                Database(temp),
                """
                SELECT model_id, request_model, response_model, billing_channel, pricing_status, cost_usd_pico,
                       api_equivalent_status, api_equivalent_usd_pico
                FROM spans;
                """));
    }

    // Size and last-write time only hint that a log is unchanged: a log rewritten with the same size and its old last-write
    // time is read again, because its SHA-256 differs.
    [Fact]
    public async Task IR16_ASameSizeRewriteWithItsOldLastWriteTimeIsReadAgain()
    {
        using TempDirectory temp = new();
        string path = new CopilotSessionLogs("s-a").Usage("u1", S1).Write(CopilotHome(temp));
        byte[] bytes = await FileRetry.ReadAllBytesAsync(path);
        ImportSourceReport first = Copilot(await RunAsync(temp));
        DateTime written = File.GetLastWriteTimeUtc(path);
        ImportSessionCheckpoint? checkpoint =
            await (await StoreAsync(temp)).GetImportSessionAsync(ImportSourceKind.Copilot, "s-a");
        new CopilotSessionLogs("s-a").Usage("u2", S2).Write(CopilotHome(temp));
        File.SetLastWriteTimeUtc(path, written);

        ImportSourceReport second = Copilot(await RunAsync(temp));
        ImportSourceReport third = Copilot(await RunAsync(temp));

        Assert.Equal(1, first.Recorded);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), checkpoint!.LogSha256);
        Assert.Equal(bytes.Length, new FileInfo(path).Length);
        Assert.Equal(written, File.GetLastWriteTimeUtc(path));
        Assert.Equal((0, 1), (second.SessionsSkippedUnchanged, second.Recorded));
        Assert.Equal((1, 0), (third.SessionsSkippedUnchanged, third.Recorded));
        Assert.Equal(2L, await LedgerSql.ScalarAsync(Database(temp), "SELECT COUNT(*) FROM spans;"));
    }

    // A store that exists but can't be listed makes its coverage incomplete; a missing one only has no logs.
    [Fact]
    public async Task IR17_AStoreThatCantBeListedIsReported()
    {
        using TempDirectory temp = new();
        Directory.CreateDirectory(CopilotHome(temp));
        await File.WriteAllTextAsync(Path.Combine(CopilotHome(temp), "session-state"), "not a folder");

        ImportReport report = await RunAsync(temp);

        ImportSourceReport copilot = Copilot(report);
        ImportSourceReport codex = Assert.Single(report.Sources, source => source.Source == ImportSourceKind.Codex);
        Assert.Equal((1, 0), (copilot.UnreadableStoreEntries, copilot.SessionsScanned));
        Assert.False(copilot.Complete);
        Assert.Equal((0, true), (codex.UnreadableStoreEntries, codex.Complete));
        Assert.Equal(
            new ImportDiagnostic
            {
                Source = ImportSourceKind.Copilot,
                LogId = "",
                Code = ImportDiagnosticCode.UnreadableStore,
                Count = 1,
            },
            Assert.Single(report.Diagnostics));
    }

    // A tool still running when its request was imported gets its link from the run that reads its completion; a dry run
    // counts it the same way. Another session's copy of the request adds no link to the request's row.
    [Fact]
    public async Task IR18_AToolThatEndsAfterItsRequestWasImportedGetsItsLink()
    {
        using TempDirectory temp = new();
        CopilotSessionLogs log = new CopilotSessionLogs("s-a").Usage("u1", S1, numToolCalls: 1).ToolStart("toolu-1", "bash");
        log.Write(CopilotHome(temp), active: true);
        ImportSourceReport first = Copilot(await RunAsync(temp, request => request with { IncludeActive = true }));
        log.ToolComplete("toolu-1", false, exitCode: 3).Write(CopilotHome(temp), active: true);
        SqliteConnection.ClearAllPools();

        ImportSourceReport dry = Copilot(await RunAsync(temp, request => request with { IncludeActive = true, DryRun = true }));
        ImportSourceReport second = Copilot(await RunAsync(temp, request => request with { IncludeActive = true }));
        ImportSourceReport third = Copilot(await RunAsync(temp, request => request with { IncludeActive = true }));
        new CopilotSessionLogs("s-copy")
            .Usage("u1", S1, numToolCalls: 1)
            .ToolStart("toolu-1", "bash")
            .ToolComplete("toolu-1", false, exitCode: 3)
            .Write(CopilotHome(temp));
        ImportSourceReport copy = Copilot(await RunAsync(temp, request => request with { IncludeActive = true }));

        Assert.Equal((1, 0), (first.Recorded, first.ToolLinksRecorded));
        Assert.Equal((0, 1, 1), (second.Recorded, second.Duplicates, second.ToolLinksRecorded));
        Assert.Equal(second, dry);
        Assert.Equal((1, 0), (third.SessionsSkippedUnchanged, third.ToolLinksRecorded));
        Assert.Equal((1, 0), (copy.Duplicates, copy.ToolLinksRecorded));
        Assert.True(second.Complete);
        Assert.Equal(
            ["1|bash|3|2"],
            await LedgerSql.RowsAsync(
                Database(temp),
                "SELECT owner_span_row_id, tool_name, tool_exit_code, status_code FROM span_links;"));
    }

    // Imported rows that a live span superseded without sharing their service request ID limit coverage on every run whose
    // scope holds them, and a dry run reports them the same way. Rows superseded by their own call's live span don't.
    [Fact]
    public async Task IR19_RowsSupersededWithoutAMatchAreReported()
    {
        using TempDirectory temp = new();
        string root = Path.Combine(temp.Path, "webapp");
        new CopilotSessionLogs("s-a", context: CopilotSessionLogs.Context(root, root, "main", "contoso/webapp"))
            .Usage("u1", S1)
            .Usage("u2", S2)
            .Write(CopilotHome(temp));
        new CopilotSessionLogs("s-b").Usage("u3", S3).Write(CopilotHome(temp));
        await RunAsync(temp);
        SqliteLedgerStore store = await StoreAsync(temp);
        await store.RecordAsync([ImportEntries.Live("s-a", null), ImportEntries.Live("s-b", S3)]);

        ImportReport report = await RunAsync(temp);
        SqliteConnection.ClearAllPools();
        ImportReport dry = await RunAsync(temp, request => request with { DryRun = true });
        ImportReport repo = await RunAsync(temp, request => request with { RepositoryRoot = root });
        ImportReport otherRepo = await RunAsync(
            temp,
            request => request with { RepositoryRoot = Path.Combine(temp.Path, "other") });
        ImportReport since = await RunAsync(
            temp,
            request => request with { Since = new DateTimeOffset(2026, 11, 21, 0, 0, 0, TimeSpan.Zero) });

        ImportSourceReport copilot = Copilot(report);
        Assert.Equal((2, 2), (copilot.SessionsSkippedUnchanged, copilot.SupersededUnmatched));
        Assert.False(copilot.Complete);
        Assert.Equal(
            new ImportDiagnostic
            {
                Source = ImportSourceKind.Copilot,
                LogId = "s-a",
                Code = ImportDiagnosticCode.SupersededUnmatched,
                Count = 2,
            },
            Assert.Single(report.Diagnostics));
        Assert.Equal(report.Sources, dry.Sources);
        Assert.Equal(report.Diagnostics, dry.Diagnostics);
        Assert.Equal(2, Copilot(repo).SupersededUnmatched);
        Assert.Equal((0, true), (Copilot(otherRepo).SupersededUnmatched, Copilot(otherRepo).Complete));
        Assert.Equal((0, true), (Copilot(since).SupersededUnmatched, Copilot(since).Complete));
        Assert.Equal(
            ["s-a|superseded", "s-a|superseded", "s-b|superseded"],
            await LedgerSql.RowsAsync(
                Database(temp),
                "SELECT conversation_id, accounting_state FROM spans WHERE ingest_source = 'import:copilot' ORDER BY id;"));
    }

    private static string CopilotHome(TempDirectory temp) => Path.Combine(temp.Path, "copilot");

    private static string CodexHome(TempDirectory temp) => Path.Combine(temp.Path, "codex");

    private static string Database(TempDirectory temp) => Path.Combine(temp.Path, "ledger.db");

    private static async Task<SqliteLedgerStore> StoreAsync(TempDirectory temp)
    {
        SqliteLedgerStore store = new(Database(temp));
        await store.InitializeAsync();
        return store;
    }

    private static ImportSourceReport Copilot(ImportReport report) =>
        Assert.Single(report.Sources, source => source.Source == ImportSourceKind.Copilot);

    private static Task<ImportReport> RunAsync(
        TempDirectory temp,
        Func<ImportRequest, ImportRequest>? change = null,
        CancellationToken cancellationToken = default,
        JsonRateCardCalculator? calculator = null)
    {
        ImportRequest request = new()
        {
            DatabasePath = Database(temp),
            Sources =
            [
                new ImportSourceRequest(new CopilotSessionLogSource(), CopilotHome(temp)),
                new ImportSourceRequest(new CodexSessionLogSource(), CodexHome(temp)),
            ],
        };
        return new LedgerImporter(calculator ?? JsonRateCardCalculator.LoadEmbedded(), new ManualTimeProvider(Now))
            .RunAsync(change is null ? request : change(request), cancellationToken);
    }
}
