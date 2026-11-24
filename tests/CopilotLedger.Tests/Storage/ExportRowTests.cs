using System.Globalization;
using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Storage;

// gh ledger export's row queries, on ExportSeeds.
public sealed class ExportRowTests
{
    private static readonly LedgerFilter InRepository = new() { Repository = "swigerb/copilot-ledger" };

    [Fact]
    public async Task XR1_PagesReturnEachEffectiveSpanOnceInOrder()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        List<int> pages = [];
        List<ExportSpanRow> rows = [];
        ExportCursor? after = null;
        IReadOnlyList<ExportSpanRow> page;
        while ((page = await snapshot.GetSpansAsync(LedgerFilter.All, after, 3)).Count > 0)
        {
            pages.Add(page.Count);
            rows.AddRange(page);
            after = new ExportCursor(page[^1].StartTime.ToUnixTimeMilliseconds(), page[^1].TraceId, page[^1].SpanId);
        }

        Assert.Equal([3, 3, 2], pages);
        Assert.Equal(
            [
                ImportEntries.Copilot("k1", "conv-i", null).Span.SpanId, "f100000000000001", "a100000000000001",
                "a100000000000002", "b100000000000001", "c100000000000001", "c100000000000002", "d100000000000001",
            ],
            rows.Select(row => row.SpanId));
        Assert.Equal(
            ["import:copilot", "otlp", "otlp", "otlp", "otlp", "otlp", "otlp", "otlp"],
            rows.Select(row => row.IngestSource));
        Assert.Equal(
            new ExportSpanRow
            {
                TraceId = ExportSeeds.A,
                SpanId = "a100000000000001",
                OperationName = "chat",
                ConversationId = "conv-a",
                RequestModel = "claude-opus-5.5",
                ModelId = "claude-opus-5.5",
                StartTime = new DateTimeOffset(2026, 11, 21, 14, 0, 0, TimeSpan.Zero),
                EndTime = new DateTimeOffset(2026, 11, 21, 14, 0, 2, TimeSpan.Zero),
                InputTokens = 100_000,
                OutputTokens = 10_000,
                CacheReadInputTokens = 40_000,
                CacheWriteInputTokens = 0,
                CacheReadState = CacheCounterState.Reported,
                CacheWriteState = CacheCounterState.Absent,
                BillingChannel = BillingChannel.GitHubAiCredits,
                PricingStatus = "priced",
                RatesVersion = "2026-11-23",
                CostUsd = 0.448m,
                SavedUsd = 0m,
                ApiEquivalentUsd = 0.448m,
                ApiEquivalentStatus = "priced",
                ApiRatesVersion = "2026-11-23",
                ApiRateCardSha256 = JsonRateCardCalculator.LoadEmbedded().ApiRateCardSha256,
                RepositorySlug = "swigerb/copilot-ledger",
                RepositoryRoot = Path.Combine(temp.Path, "repo"),
                BranchName = "feature/ledger",
                HeadCommitSha = GitFixture.Sha1,
                PullRequestNumber = 42,
                IngestSource = "otlp",
            },
            rows[2]);
    }

    [Fact]
    public async Task XR2_TheLatestSpansComeNewestFirstWithinTheLimits()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        Assert.Equal(
            ["d100000000000001", "c100000000000002", "c100000000000001"],
            (await snapshot.GetLatestSpansAsync(LedgerFilter.All, 3)).Select(row => row.SpanId));
        Assert.Empty(await snapshot.GetLatestSpansAsync(LedgerFilter.All, 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => snapshot.GetLatestSpansAsync(LedgerFilter.All, 100_001));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => snapshot.GetLatestToolCallsAsync(LedgerFilter.All, -1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => snapshot.GetSpansAsync(LedgerFilter.All, null, 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => snapshot.GetToolCallsAsync(LedgerFilter.All, null, 10_001));
    }

    [Fact]
    public async Task XR3_ToolCallsAreTheEffectiveLinksOfSelectedChains()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();
        string imported = ImportEntries.ToolLink(ImportEntries.Copilot("k1", "conv-i", null), "call-1", 0).SpanId;

        IReadOnlyList<ExportToolCallRow> first = await snapshot.GetToolCallsAsync(LedgerFilter.All, null, 3);
        ExportCursor after = new(first[^1].StartTime.ToUnixTimeMilliseconds(), first[^1].TraceId, first[^1].SpanId);
        IReadOnlyList<ExportToolCallRow> rows = [.. first, .. await snapshot.GetToolCallsAsync(LedgerFilter.All, after, 3)];

        Assert.Equal([imported, "7100000000000001", "7100000000000002", "7100000000000003"], rows.Select(row => row.SpanId));
        Assert.Equal(["import:copilot", "otlp", "otlp", "otlp"], rows.Select(row => row.IngestSource));
        Assert.Equal(
            new ExportToolCallRow
            {
                TraceId = ExportSeeds.B,
                SpanId = "7100000000000003",
                ConversationId = "conv-b",
                ToolName = "view",
                StartTime = new DateTimeOffset(2026, 11, 21, 15, 0, 10, TimeSpan.Zero),
                StatusCode = 2,
                IngestSource = "otlp",
            },
            rows[3]);
        Assert.Equal([0L, 0L, 1L, null], rows.Select(row => row.ToolExitCode));
        Assert.Equal(4, await snapshot.CountToolCallsAsync(LedgerFilter.All));
        Assert.Equal(
            ["7100000000000003", "7100000000000002"],
            (await snapshot.GetLatestToolCallsAsync(LedgerFilter.All, 2)).Select(row => row.SpanId));
        Assert.Equal(
            ["7100000000000001", "7100000000000002", "7100000000000003"],
            (await snapshot.GetToolCallsAsync(InRepository, null, 10)).Select(row => row.SpanId));
        Assert.Equal(3, await snapshot.CountToolCallsAsync(InRepository));
    }

    [Fact]
    public async Task XR4_CoverageCountsSourcesSupersededRowsImportsRetentionAndCache()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        DateTimeOffset toolHistoryFrom = new(2026, 11, 21, 0, 0, 0, TimeSpan.Zero);
        string i1 = ImportEntries.Copilot("k1", "conv-i", null).Span.TraceId;

        await using (LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync())
        {
            ExportCoverageRow all = await snapshot.GetCoverageAsync(LedgerFilter.All, toolHistoryFrom);
            Assert.Equal([new ExportSourceCount("import:copilot", 1), new ExportSourceCount("otlp", 7)], all.SpansBySource);
            Assert.Equal(
                (1L, 0L, 0L, 0L, 2L, 0L),
                (all.SupersededMatchedImportSpans, all.SupersededUnmatchedImportSpans, all.LedgerAmbiguousImportRecords,
                    all.LedgerIncompleteImportLogs, all.SpansBeforeToolHistory, all.ImportedToolCallsWithoutDetail));
            Assert.Equal((1L, 7L, 0L), (all.CacheReportedSpans, all.CacheAbsentSpans, all.CacheLegacyUnknownSpans));
            ExportCoverageRow repository = await snapshot.GetCoverageAsync(InRepository, toolHistoryFrom);
            Assert.Equal([new ExportSourceCount("otlp", 5)], repository.SpansBySource);
            Assert.Equal((0L, 0L), (repository.SupersededMatchedImportSpans, repository.SpansBeforeToolHistory));
        }

        await store.RecordImportedAsync(
            [],
            new ImportSessionCheckpoint
            {
                Source = ImportSourceKind.Copilot,
                LogId = "session-log-1",
                LogSizeBytes = 10,
                LogLastWriteTime = ExportSeeds.GeneratedAt,
                LogSha256 = new string('a', 64),
                Complete = false,
                Recorded = 0,
                Duplicates = 0,
                OverlapSkipped = 0,
                AmbiguousOverlap = 3,
            });
        await LedgerSql.ExecuteAsync(
            store.DatabasePath,
            "UPDATE spans SET cache_read_state = 'legacy_unknown', cache_write_state = 'legacy_unknown' WHERE span_id = @p0;",
            "d100000000000001");
        await LedgerSql.ExecuteAsync(store.DatabasePath, "UPDATE spans SET imported_tool_call_count = 3 WHERE trace_id = @p0;", i1);
        await LedgerSql.ExecuteAsync(store.DatabasePath, "DELETE FROM span_links WHERE trace_id = @p0;", i1);

        await using LedgerExportSnapshot later = await store.OpenExportSnapshotAsync();
        ExportCoverageRow coverage = await later.GetCoverageAsync(LedgerFilter.All, toolHistoryFrom);
        Assert.Equal((3L, 1L), (coverage.LedgerAmbiguousImportRecords, coverage.LedgerIncompleteImportLogs));
        Assert.Equal((1L, 6L, 1L), (coverage.CacheReportedSpans, coverage.CacheAbsentSpans, coverage.CacheLegacyUnknownSpans));
        Assert.Equal(3, coverage.ImportedToolCallsWithoutDetail);
        ExportCoverageRow scoped = await later.GetCoverageAsync(InRepository, toolHistoryFrom);
        Assert.Equal(
            (3L, 1L, 0L),
            (scoped.LedgerAmbiguousImportRecords, scoped.LedgerIncompleteImportLogs, scoped.ImportedToolCallsWithoutDetail));
    }

    [Fact]
    public async Task XR10_UnmatchedSupersessionsCountByTheirOwnColumns()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        DateTimeOffset toolHistoryFrom = new(2026, 11, 21, 0, 0, 0, TimeSpan.Zero);

        ImportedLedgerEntry imported = await ExportSeeds.SupersedeUnmatchedAsync(store);

        Assert.Equal(
            ["superseded|" + ExportSeeds.U],
            await LedgerSql.RowsAsync(
                store.DatabasePath,
                "SELECT imported.accounting_state, live.trace_id FROM spans AS imported JOIN spans AS live ON live.id = imported.superseded_by WHERE imported.trace_id = @p0;",
                imported.Span.TraceId));
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();
        ExportCoverageRow all = await snapshot.GetCoverageAsync(LedgerFilter.All, toolHistoryFrom);
        Assert.Equal((1L, 1L), (all.SupersededMatchedImportSpans, all.SupersededUnmatchedImportSpans));
        List<long> scoped = [];
        foreach (LedgerFilter filter in new[]
                 {
                     LedgerFilter.All with { ConversationId = "conv-u" },
                     LedgerFilter.All with { ConversationId = "conv-a" },
                     LedgerFilter.All with { From = toolHistoryFrom },
                     InRepository,
                 })
        {
            scoped.Add((await snapshot.GetCoverageAsync(filter, toolHistoryFrom)).SupersededUnmatchedImportSpans);
        }

        Assert.Equal([1L, 0L, 0L, 0L], scoped);
    }

    [Fact]
    public async Task XR11_ImportedToolCallsWithoutDetailAreWhatEachRequestLacks()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        DateTimeOffset toolHistoryFrom = new(2026, 11, 14, 0, 0, 0, TimeSpan.Zero);
        string i1 = ImportEntries.Copilot("k1", "conv-i", null).Span.TraceId;
        List<long> withoutDetail = [];

        // I1 owns the link of one tool call: it lacks none of 1, and 2 of 3.
        foreach (int count in new[] { 1, 3 })
        {
            await LedgerSql.ExecuteAsync(
                store.DatabasePath,
                "UPDATE spans SET imported_tool_call_count = @p0 WHERE trace_id = @p1;",
                count,
                i1);
            await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();
            withoutDetail.Add((await snapshot.GetCoverageAsync(LedgerFilter.All, toolHistoryFrom)).ImportedToolCallsWithoutDetail);
        }

        Assert.Equal([0L, 2L], withoutDetail);
    }

    [Fact]
    public async Task XR12_TheEarliestLinkIsTheWholeLedgers()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore empty = new(Path.Combine(temp.Path, "empty.db"), new ManualTimeProvider(ExportSeeds.GeneratedAt));
        await empty.InitializeAsync();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        List<DateTimeOffset?> earliest = [];

        foreach (SqliteLedgerStore ledger in new[] { empty, store })
        {
            await using LedgerExportSnapshot snapshot = await ledger.OpenExportSnapshotAsync();
            earliest.Add(await snapshot.GetEarliestLinkStartAsync());
        }

        // Once I1's, I2's, T1, and T2 are deleted, the earliest link is T5, A's invoke_agent link at 14:00:50, though the
        // earliest tool call is T3's: the daemon deletes both kinds by the same rule.
        await store.PruneSpanLinksAsync(new DateTimeOffset(2026, 11, 21, 14, 0, 45, TimeSpan.Zero));
        await using (LedgerExportSnapshot pruned = await store.OpenExportSnapshotAsync())
        {
            earliest.Add(await pruned.GetEarliestLinkStartAsync());
        }

        Assert.Equal([null, ImportEntries.Start, new DateTimeOffset(2026, 11, 21, 14, 0, 50, TimeSpan.Zero)], earliest);
    }

    // Pages seek past rows that start in the same millisecond, from the period's first, and skip or repeat none.
    [Fact]
    public async Task XR13_PagesSplitRowsThatStartTogether()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "ledger.db"), new ManualTimeProvider(ExportSeeds.GeneratedAt));
        await store.InitializeAsync();
        DateTimeOffset start = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);
        string[] traces = [.. "12345".Select(digit => new string(digit, 32))];
        await store.RecordAsync(
        [
            .. traces.SelectMany(trace => new[] { Request(trace, 2, start), Request(trace, 1, start) }),
            Request(traces[0], 3, start.AddMilliseconds(-1)),
            Request(traces[4], 3, start.AddSeconds(1)),
        ]);
        await store.RecordSpanLinksAsync(
        [
            .. traces.Take(3).SelectMany(trace => new[] { ToolCall(trace, 2, start.AddSeconds(30)), ToolCall(trace, 1, start.AddSeconds(30)) }),
            ToolCall(new string('9', 32), 1, start.AddSeconds(30)),
        ]);
        LedgerFilter filter = LedgerFilter.All with { From = start, To = start.AddHours(1) };
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        List<string> spans = [];
        ExportCursor? after = null;
        IReadOnlyList<ExportSpanRow> page;
        while ((page = await snapshot.GetSpansAsync(filter, after, 3)).Count > 0)
        {
            spans.AddRange(page.Select(row => $"{row.TraceId[0]}{row.SpanId[^1]}"));
            after = new ExportCursor(page[^1].StartTime.ToUnixTimeMilliseconds(), page[^1].TraceId, page[^1].SpanId);
        }

        List<string> toolCalls = [];
        after = null;
        IReadOnlyList<ExportToolCallRow> calls;
        while ((calls = await snapshot.GetToolCallsAsync(filter, after, 4)).Count > 0)
        {
            toolCalls.AddRange(calls.Select(row => $"{row.TraceId[0]}{row.SpanId[^1]}"));
            after = new ExportCursor(calls[^1].StartTime.ToUnixTimeMilliseconds(), calls[^1].TraceId, calls[^1].SpanId);
        }

        Assert.Equal(["11", "12", "21", "22", "31", "32", "41", "42", "51", "52", "53"], spans);
        Assert.Equal(["11", "12", "21", "22", "31", "32"], toolCalls);
    }

    [Fact]
    public async Task XR5_ProvenanceListsTheRateCardsThatPricedTheSelection()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        ExportRatesProvenance provenance = await snapshot.GetRatesProvenanceAsync(LedgerFilter.All);

        Assert.Equal(["2026-11-23", "test-fixture"], provenance.RatesVersions);
        Assert.Equal(
            [
                new ExportApiRateCard("2026-11-23", JsonRateCardCalculator.LoadEmbedded().ApiRateCardSha256),
                new ExportApiRateCard("test-fixture", JsonRateCardCalculator.Parse(RateFixtures.ByokTestRatesJson).ApiRateCardSha256),
            ],
            provenance.ApiRateCards);
        Assert.Equal(
            ["2026-11-23"],
            (await snapshot.GetRatesProvenanceAsync(new LedgerFilter { Branch = "main" })).RatesVersions);
    }

    [Fact]
    public async Task XR6_BreakerTripsAreTheSelectedChainsNewestFirst()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        IReadOnlyList<BreakerTrip> trips = await snapshot.GetBreakerTripsAsync(LedgerFilter.All);

        Assert.Equal([ExportSeeds.C, ExportSeeds.A], trips.Select(trip => trip.TraceId));
        Assert.Equal([BreakerRule.RetryChurn, BreakerRule.CreditVelocity], trips.Select(trip => trip.Rule));
        Assert.Equal([ExportSeeds.GeneratedAt, null], trips.Select(trip => trip.ResetAt));
        Assert.Equal(
            [ExportSeeds.A],
            (await snapshot.GetBreakerTripsAsync(new LedgerFilter { Branch = "feature/ledger" })).Select(trip => trip.TraceId));
    }

    [Fact]
    public async Task XR7_BudgetsAreTheSelectedKeysAsBudgetListShowsThem()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        BranchBudgetRow budget = Assert.Single(await snapshot.GetBudgetsAsync(LedgerFilter.All));

        Assert.Equal(
            ("swigerb/copilot-ledger", "feature/ledger", 50m, 91.3m, true),
            (budget.Repository, budget.Branch, budget.BudgetAiCredits, budget.SpentAiCredits, budget.Exceeded));
        Assert.Equal(Assert.Single(await store.GetBranchBudgetsAsync("swigerb/copilot-ledger", "feature/ledger")), budget);
        Assert.Empty(await snapshot.GetBudgetsAsync(new LedgerFilter { Branch = "main" }));
    }

    [Fact]
    public async Task XR8_RootsAreTheSelectedRepositoriesWithoutASlug()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        Assert.Equal([ExportSeeds.LocalRoot(temp.Path)], await snapshot.GetRepositoryRootsAsync(LedgerFilter.All));
        Assert.Empty(await snapshot.GetRepositoryRootsAsync(InRepository));
    }

    [Theory]
    [InlineData("repository", 5, 3, 2)]
    [InlineData("branch", 3, 0, 1)]
    [InlineData("pull request", 2, 2, 1)]
    [InlineData("conversation", 2, 0, 1)]
    public async Task XR9_EachFilterSelectsSpansToolCallsAndTripsTogether(string by, int spans, int toolCalls, int trips)
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();
        LedgerFilter filter = by switch
        {
            "repository" => InRepository,
            "branch" => new LedgerFilter { Branch = "main" },
            "pull request" => new LedgerFilter { PullRequestNumber = 42 },
            _ => new LedgerFilter { ConversationId = "conv-c" },
        };

        Assert.Equal(spans, (await snapshot.GetSpansAsync(filter, null, 100)).Count);
        Assert.Equal(toolCalls, (await snapshot.GetToolCallsAsync(filter, null, 100)).Count);
        Assert.Equal(trips, (await snapshot.GetBreakerTripsAsync(filter)).Count);
    }

    // A claude-haiku-4.5 request, the index-th of trace, at start.
    private static LedgerEntry Request(string trace, int index, DateTimeOffset start)
    {
        NormalizedGenAiSpan span = TestSpans.Chat(
            startTime: start,
            requestModel: "claude-haiku-4.5",
            conversationId: "conv-" + trace[0],
            inputTokens: 1_000,
            outputTokens: 100) with
        {
            TraceId = trace,
            SpanId = ExportSeeds.SpanId(trace, index),
        };
        return new LedgerEntry(span, JsonRateCardCalculator.LoadEmbedded().Calculate(span), null);
    }

    // A bash call, the index-th of trace, at start.
    private static GenAiSpanLink ToolCall(string trace, int index, DateTimeOffset start) => new()
    {
        TraceId = trace,
        SpanId = "7" + trace[0] + "0000000000000" + index.ToString(CultureInfo.InvariantCulture),
        OperationName = "execute_tool",
        ConversationId = "conv-" + trace[0],
        StartTime = start,
        ToolName = "bash",
        ToolExitCode = 0,
    };
}
