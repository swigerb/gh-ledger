using CopilotLedger.Core.Audit;
using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Otlp.Attribution;
using CopilotLedger.Otlp.Ingestion;
using CopilotLedger.Storage;
using CopilotLedger.Storage.Import;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Storage;

// The audit queries and the snapshot's GetAuditInputAsync.
public sealed class AuditQueryTests
{
    [Fact]
    public async Task AQ1_SpansMapEveryColumn_AndCountEachLiveRequestsToolCalls()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await AuditSeeds.CreateAsync(temp.Path);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        IReadOnlyList<AuditSpan> spans = await snapshot.GetAuditSpansAsync(LedgerFilter.All);

        Assert.Equal(29, spans.Count);
        Assert.Equal(
            new AuditSpan
            {
                TraceId = AuditSeeds.R,
                SpanId = "52c0000000000001",
                ParentSpanId = "52a0000000000000",
                ConversationId = "conv-r",
                StartTime = AuditSeeds.At("15:00:00"),
                EndTime = AuditSeeds.At("15:00:02"),
                AgentInvocationId = "52a0000000000000",
                Model = "claude-opus-5.5",
                RequestModel = "claude-opus-5.5",
                InputTokens = 20_000,
                CacheReadInputTokens = 16_000,
                CacheWriteInputTokens = 0,
                OutputTokens = 400,
                CacheReadState = CacheCounterState.Reported,
                CacheWriteState = CacheCounterState.Absent,
                Channel = BillingChannel.GitHubAiCredits,
                PricingStatus = PricingStatus.Priced,
                CostUsd = 0.0272m,
                AiCredits = 2.72m,
                ApiEquivalentUsd = 0.0272m,
                ToolCallCount = 1,
                Repository = "swigerb/copilot-ledger",
                Branch = "feature/ledger",
                PullRequestNumber = 42,
                IngestSource = "otlp",
            },
            spans.Single(span => span.SpanId == "52c0000000000001"));
        Assert.Equal(
            [
                .. Enumerable.Range(1, 12).Select(index => $"50c{index:D13} 50a0000000000000 0 (none) main"),
                "52c0000000000001 52a0000000000000 1 (none) feature/ledger",
                "52c0000000000002 52a0000000000001 1 explore feature/ledger",
                "52c0000000000003 52a0000000000001 1 explore feature/ledger",
                "52c0000000000004 52a0000000000001 1 explore feature/ledger",
                "52c0000000000005 52a0000000000001 1 explore feature/ledger",
                "52c0000000000006 52a0000000000001 1 explore feature/ledger",
                "52c0000000000007 52a0000000000000 0 (none) feature/ledger",
                "42c0000000000001 - - (none) feature/ledger",
                .. Enumerable.Range(1, 6).Select(index => $"4dc{index:D13} 4da0000000000000 0 (none) feature/ledger"),
            ],
            spans
                .Where(span => span.IngestSource == "otlp")
                .Select(span => $"{span.SpanId} {span.AgentInvocationId ?? "-"} {span.ToolCallCount?.ToString() ?? "-"} {span.AgentName ?? "(none)"} {span.Branch}"));
    }

    [Fact]
    public async Task AQ2_IdentityAndToolCounts_FollowCopilotsAgentTree_AndWaitForTheAgentSpan()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"));
        await store.InitializeAsync();
        LedgerIngestor ingestor = new(
            store,
            JsonRateCardCalculator.LoadEmbedded(),
            new CachingGitContextResolver(new FileSystemGitContextResolver(ceilingDirectory: temp.Path)),
            new CopilotSessionWorkspaceLocator(Path.Combine(temp.Path, "home")));

        await ingestor.IngestAsync(CopilotTraces.TraceBA());
        string partial = Describe(await store.GetAuditSpansAsync(LedgerFilter.All));
        await ingestor.IngestAsync(CopilotTraces.TraceBB());
        string complete = Describe(await store.GetAuditSpansAsync(LedgerFilter.All));
        IReadOnlyList<AuditToolCall> calls = await store.GetAuditToolCallsAsync(LedgerFilter.All);

        Assert.Equal("00f067aa0ba902b8 - - | 00f067aa0ba902bb 00f067aa0ba902ba 1 | 00f067aa0ba902bd 00f067aa0ba902ba 0", partial);
        Assert.Equal(
            "00f067aa0ba902b8 00f067aa0ba902b7 1 | 00f067aa0ba902bb 00f067aa0ba902ba 1 | 00f067aa0ba902bd 00f067aa0ba902ba 0 | 00f067aa0ba902be 00f067aa0ba902b7 0",
            complete);
        Assert.Equal(
            ["00f067aa0ba902b9 00f067aa0ba902b7 task unknown", "00f067aa0ba902bc 00f067aa0ba902ba powershell unknown"],
            calls.Select(call => $"{call.SpanId} {call.AgentInvocationId} {call.ToolName} {AuditToolOutcomes.ToName(call.Outcome)}"));

        static string Describe(IReadOnlyList<AuditSpan> spans) =>
            string.Join(" | ", spans.Select(span => $"{span.SpanId} {span.AgentInvocationId ?? "-"} {span.ToolCallCount?.ToString() ?? "-"}"));
    }

    [Fact]
    public async Task AQ3_ToolCalls_TakeTheirChainsScopeTheirInvocationAndTheirOutcome()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await AuditSeeds.CreateAsync(temp.Path);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        IReadOnlyList<AuditToolCall> calls = await snapshot.GetAuditToolCallsAsync(LedgerFilter.All);

        string imported = ImportIds.ToolSpanId(ImportSourceKind.Copilot, "conv-i", "call-1");
        Assert.Equal(
            [
                $"{imported} main bash success (none) (none) - imported",
                "52t0000000000000 52a0000000000000 task success swigerb/copilot-ledger feature/ledger 42 live",
                "52t0000000000001 52a0000000000001 bash failure swigerb/copilot-ledger feature/ledger 42 live",
                "52t0000000000002 52a0000000000001 bash failure swigerb/copilot-ledger feature/ledger 42 live",
                "52t0000000000003 52a0000000000001 bash failure swigerb/copilot-ledger feature/ledger 42 live",
                "52t0000000000004 52a0000000000001 bash failure swigerb/copilot-ledger feature/ledger 42 live",
                "52t0000000000005 52a0000000000001 bash success swigerb/copilot-ledger feature/ledger 42 live",
            ],
            calls.Select(call =>
                $"{call.SpanId} {call.AgentInvocationId} {call.ToolName} {AuditToolOutcomes.ToName(call.Outcome)} {call.Repository} " +
                $"{call.Branch} {call.PullRequestNumber?.ToString() ?? "-"} {(call.Imported ? "imported" : "live")}"));
        AuditToolCall bash = calls[2];
        Assert.Equal(("explore", "conv-r", "52a0000000000001", 1L, 2), (bash.AgentName, bash.ConversationId, bash.ParentSpanId, bash.ExitCode, bash.StatusCode));
    }

    [Fact]
    public async Task AQ4_ImportedRequests_HaveTheMainAgentOrNoIdentity_AndTheirLoggedToolCounts()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await AuditSeeds.CreateAsync(temp.Path);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        AuditInput input = await snapshot.GetAuditInputAsync(LedgerFilter.All, AuditSeeds.GeneratedAt);

        Assert.Equal(
            [
                "import:codex - - (none) Unpriced OtherProvider 0",
                "import:copilot main 1 (none) GitHubAiCredits Priced 0.00075",
                "import:copilot - 0 explore GitHubAiCredits Priced 0.00075",
            ],
            input.Spans
                .Where(span => span.IngestSource != "otlp")
                .OrderBy(span => span.IngestSource, StringComparer.Ordinal)
                .ThenBy(span => span.AgentName ?? "", StringComparer.Ordinal)
                .Select(span =>
                    $"{span.IngestSource} {span.AgentInvocationId ?? "-"} {span.ToolCallCount?.ToString() ?? "-"} {span.AgentName ?? "(none)"} " +
                    $"{span.Channel} {span.PricingStatus} {AuditFixtures.Number(span.CostUsd)}"));
        AuditSummary summary = AuditAnalyzer.Summarize(input);
        Assert.Equal(
            [new AuditSourceCount("import:codex", 1), new AuditSourceCount("import:copilot", 2), new AuditSourceCount("otlp", 26)],
            summary.SpansBySource);
        Assert.Equal((3L, 2L, 1L, 0L), (summary.SpansWithoutAgentIdentity, summary.SpansWithUnknownToolCount, summary.UnpricedSpans, summary.ToolCallsWithoutAgentIdentity));
    }

    [Fact]
    public async Task AQ5_ToolCalls_AreTheEffectiveLinksOfSelectedChainsInTheWindow()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        IReadOnlyList<AuditToolCall> all = await snapshot.GetAuditToolCallsAsync(LedgerFilter.All);
        IReadOnlyList<AuditToolCall> window = await snapshot.GetAuditToolCallsAsync(new LedgerFilter
        {
            From = new DateTimeOffset(2026, 11, 21, 14, 0, 35, TimeSpan.Zero),
            To = new DateTimeOffset(2026, 11, 21, 16, 0, 0, TimeSpan.Zero),
        });
        IReadOnlyList<AuditToolCall> main = await snapshot.GetAuditToolCallsAsync(
            new LedgerFilter { Repository = "swigerb/copilot-ledger", Branch = "main" });

        Assert.Equal(
            [ImportIds.ToolSpanId(ImportSourceKind.Copilot, "conv-i", "call-1"), "7100000000000001", "7100000000000002", "7100000000000003"],
            all.Select(call => call.SpanId));
        Assert.Equal(["7100000000000002", "7100000000000003"], window.Select(call => call.SpanId));
        Assert.Empty(main);
        Assert.All(all.Skip(1), call => Assert.Null(call.AgentInvocationId));
        Assert.Equal(("main", true), (all[0].AgentInvocationId, all[0].Imported));
    }

    // The earliest span link the ledger keeps, I1's tool call at 11-20 12:00, is later than the retention's start, so tool
    // history starts there, and the request of 12 days ago started before it. Codex's X1 has no tool-call count.
    [Fact]
    public async Task AQ6_TheInputsCoverage_IsSprint9sCoverageAtTheToolHistoryCutoff()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await AuditSeeds.CreateAsync(temp.Path);
        await ExportSeeds.SupersedeUnmatchedAsync(store);
        await store.RecordImportedAsync([ImportEntries.Copilot("k7", "conv-w", null) with { ToolCallCount = 3 }], null);
        await store.RecordAsync([ImportEntries.Live("conv-old", null, AuditSeeds.GeneratedAt.AddDays(-12))]);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        AuditInput input = await snapshot.GetAuditInputAsync(
            new LedgerFilter { From = AuditSeeds.GeneratedAt.AddDays(-30), To = AuditSeeds.GeneratedAt },
            AuditSeeds.GeneratedAt);

        Assert.Equal(
            new AuditCoverage
            {
                ToolHistoryFrom = ImportEntries.Start,
                ToolHistoryStart = AuditToolHistoryStart.EarliestLink,
                ToolHistoryRetentionDays = 7,
                SpansBeforeToolHistory = 1,
                ImportedToolCallsWithoutDetail = 3,
                ImportedRequestsWithoutToolData = 1,
                SupersededMatchedImportSpans = 0,
                SupersededUnmatchedImportSpans = 1,
                LedgerAmbiguousImportRecords = 0,
                LedgerIncompleteImportLogs = 0,
            },
            input.Coverage);
        Assert.True(input.Coverage.InsufficientToolHistory);
        Assert.Equal(32, input.Spans.Count);
        Assert.Equal(BreakerSettings.Default, input.Breaker);
        Assert.Null(input.Pricing);
    }

    [Fact]
    public async Task AQ7_TheSnapshotReadsTheStoredBreakerSettings()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await AuditSeeds.CreateAsync(temp.Path);
        BreakerSettings settings = new() { VelocityCredits = 80m, VelocityWindowMinutes = 10, ChurnFailures = 4 };
        await store.UpdateBreakerSettingsAsync(settings);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        Assert.Equal(settings, await snapshot.GetBreakerSettingsAsync());
        Assert.Equal(settings, (await snapshot.GetAuditInputAsync(LedgerFilter.All, AuditSeeds.GeneratedAt)).Breaker);
        Assert.Equal(settings, await store.GetBreakerSettingsAsync());
    }

    [Fact]
    public async Task AQ8_ThirtyDaysAfterPruning_TheTokenRulesRunAndMissingToolHistoryIsUnknownNotZero()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"), new ManualTimeProvider(AuditSeeds.GeneratedAt));
        await store.InitializeAsync();
        DateTimeOffset tenDaysAgo = new(2026, 11, 11, 15, 0, 0, TimeSpan.Zero);
        DateTimeOffset twentyDaysAgo = new(2026, 11, 1, 15, 0, 0, TimeSpan.Zero);

        // O1, ten days old: a context that grows from 10,000 to 40,000 input tokens in 10 requests, and 8 view calls.
        AuditSeeds.LiveTrace growth = new("01010101010101010101010101010101", "conv-o1", null);
        string growthAgent = growth.Agent(0, tenDaysAgo);
        for (int k = 0; k < 10; k++)
        {
            growth.Request(1 + k, growthAgent, tenDaysAgo.AddMinutes(k), "claude-sonnet-5.5", k == 9 ? 40_000 : 10_000 + 3_000 * k, 8_000, 2_000);
            if (k < 8)
            {
                growth.Tool(k, growthAgent, tenDaysAgo.AddMinutes(k).AddSeconds(30), "view", null, 1);
            }
        }

        // O2, twenty days old: 5 cache misses, a request without cache counters, then 5 more misses.
        AuditSeeds.LiveTrace misses = new("02020202020202020202020202020202", "conv-o2", null);
        string missAgent = misses.Agent(0, twentyDaysAgo);
        for (int k = 0; k < 11; k++)
        {
            misses.Request(1 + k, missAgent, twentyDaysAgo.AddMinutes(k), "mai-code-2", 40_000, 0, 1_500, reportZeroCache: k != 5);
        }

        // O3, ten days old: 10 short, tool-free claude-opus-5.5 requests.
        AuditSeeds.LiveTrace trivial = new("03030303030303030303030303030303", "conv-o3", null);
        string trivialAgent = trivial.Agent(0, tenDaysAgo.AddHours(1));
        for (int k = 0; k < 10; k++)
        {
            trivial.Request(1 + k, trivialAgent, tenDaysAgo.AddHours(1).AddMinutes(2 * k), "claude-opus-5.5", 30_000, 24_000, 120);
        }

        await growth.RecordAsync(store);
        await misses.RecordAsync(store);
        await trivial.RecordAsync(store);
        await store.PruneSpanLinksAsync(AuditSeeds.GeneratedAt - LedgerImporter.ToolLinkRetention);
        AuditInput input;
        await using (LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync())
        {
            input = await snapshot.GetAuditInputAsync(
                new LedgerFilter { From = new DateTimeOffset(2026, 10, 23, 5, 0, 0, TimeSpan.Zero), To = AuditSeeds.GeneratedAt },
                AuditSeeds.GeneratedAt);
        }

        IReadOnlyList<AuditFinding> findings = AuditAnalyzer.Run(input with
        {
            Pricing = AuditFixtures.Pricing,
            EconomyModel = "mai-code-2",
            TimeZone = ManualTimeProvider.UtcMinus5,
        });

        Assert.Equal(31, input.Spans.Count);
        Assert.Empty(input.ToolCalls);
        Assert.All(input.Spans, span => Assert.NotNull(span.AgentInvocationId));
        Assert.All(input.Spans, span => Assert.Null(span.ToolCallCount));
        Assert.True(input.Coverage.InsufficientToolHistory);
        Assert.Equal(31, input.Coverage.SpansBeforeToolHistory);
        Assert.Equal(31, AuditAnalyzer.Summarize(input).SpansWithUnknownToolCount);
        Assert.Equal(1, AuditAnalyzer.Summarize(input).CacheAbsentSpans);
        Assert.Equal(
            ["cache_miss_streak 5", "cache_miss_streak 5", "context_growth 10"],
            findings
                .Select(finding => $"{finding.RuleId} {AuditFixtures.Number(finding.Metrics[0].Value)}")
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AQ9_TheStoresAuditMethods_ReadWhatTheSnapshotReads()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await AuditSeeds.CreateAsync(temp.Path);
        LedgerFilter filter = new() { Repository = "swigerb/copilot-ledger", Branch = "feature/ledger" };
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        IReadOnlyList<AuditSpan> spans = await store.GetAuditSpansAsync(filter);
        IReadOnlyList<AuditToolCall> calls = await store.GetAuditToolCallsAsync(filter);

        Assert.Equal(14, spans.Count);
        Assert.Equal(await snapshot.GetAuditSpansAsync(filter), spans);
        Assert.Equal(6, calls.Count);
        Assert.Equal(await snapshot.GetAuditToolCallsAsync(filter), calls);
    }

    [Fact]
    public async Task AQ10_ASnapshotDoesntSeeRequestsRecordedAfterItBegan()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await AuditSeeds.CreateAsync(temp.Path);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        await store.RecordAsync([ImportEntries.Live("conv-late", null, AuditSeeds.At("19:00:00"))]);

        Assert.Equal(29, (await snapshot.GetAuditSpansAsync(LedgerFilter.All)).Count);
        Assert.Equal(30, (await store.GetAuditSpansAsync(LedgerFilter.All)).Count);
    }

    // As export's coverage, tool history starts at the retention's start, at the earliest span link the ledger keeps when
    // that's later, or at the generation time when it keeps no link: the daemon deletes links by its own clock, so one older
    // than the retention may still be there.
    [Theory]
    [InlineData(null, 0, AuditToolHistoryStart.NoLinks, 2)]
    [InlineData(192, 168, AuditToolHistoryStart.Retention, 0)]
    [InlineData(48, 48, AuditToolHistoryStart.EarliestLink, 1)]
    public async Task AQ11_ToolHistory_StartsWhereExportsDoes(
        int? linkHoursAgo,
        int fromHoursAgo,
        AuditToolHistoryStart expected,
        int spansBefore)
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"), new ManualTimeProvider(AuditSeeds.GeneratedAt));
        await store.InitializeAsync();
        AuditSeeds.LiveTrace requests = new("0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a", "conv-t", null);
        requests.Request(1, null, AuditSeeds.GeneratedAt.AddDays(-3), "claude-sonnet-5.5", 10_000, 8_000, 300);
        requests.Request(2, null, AuditSeeds.GeneratedAt.AddDays(-1), "claude-sonnet-5.5", 10_000, 8_000, 300);
        await requests.RecordAsync(store);
        if (linkHoursAgo is int hours)
        {
            AuditSeeds.LiveTrace tools = new("0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b", "conv-u", null);
            DateTimeOffset start = AuditSeeds.GeneratedAt.AddHours(-hours);
            tools.Tool(0, tools.Agent(0, start), start, "view", null, 1);
            await tools.RecordAsync(store);
        }

        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();
        AuditCoverage coverage = (await snapshot.GetAuditInputAsync(LedgerFilter.All, AuditSeeds.GeneratedAt)).Coverage;

        Assert.Equal(
            (AuditSeeds.GeneratedAt.AddHours(-fromHoursAgo), expected, spansBefore),
            (coverage.ToolHistoryFrom, coverage.ToolHistoryStart, (int)coverage.SpansBeforeToolHistory));
        Assert.Equal(spansBefore > 0, coverage.InsufficientToolHistory);
    }

    // Tool history isn't complete while the rules can't see tool calls (Sprint 9's counts): an imported request that reported 4
    // tool calls but kept 3 links lacks the detail of 1, and an imported request without a tool-call count, as every Codex
    // request and a Copilot request whose log gave none, may be missing all of its tool calls. Copilot's import records a
    // compaction with 0 tool calls, which is data: the request is known to have none. A link of 8 days ago keeps the tool
    // history at the retention.
    [Theory]
    [InlineData("partial detail", 1, 0, 4L)]
    [InlineData("codex", 0, 1, null)]
    [InlineData("copilot without a count", 0, 1, null)]
    [InlineData("compaction", 0, 0, 0L)]
    public async Task AQ12_ToolHistory_IsInsufficientWithoutEveryToolCallsDetail(
        string name,
        long withoutDetail,
        long withoutToolData,
        long? toolCount)
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"), new ManualTimeProvider(AuditSeeds.GeneratedAt));
        await store.InitializeAsync();
        AuditSeeds.LiveTrace old = new("0c0c0c0c0c0c0c0c0c0c0c0c0c0c0c0c", "conv-o", null);
        DateTimeOffset week = AuditSeeds.GeneratedAt.AddDays(-8);
        old.Tool(0, old.Agent(0, week), week, "view", null, 1);
        await old.RecordAsync(store);
        ImportedLedgerEntry entry = ImportEntries.Copilot("k1", "conv-p", null);
        ImportedLedgerEntry imported = name switch
        {
            "codex" => AuditSeeds.Codex("x1", "conv-x"),
            "copilot without a count" => entry,
            "compaction" => ImportEntries.Copilot("k2", "conv-p", null, ImportedRecordKind.Compaction) with { ToolCallCount = 0 },
            _ => entry with
            {
                ToolCallCount = 4,
                ToolLinks =
                [
                    ImportEntries.ToolLink(entry, "call-1", 0),
                    ImportEntries.ToolLink(entry, "call-2", 0),
                    ImportEntries.ToolLink(entry, "call-3", 1),
                ],
            },
        };
        await store.RecordImportedAsync([imported], null);

        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();
        AuditInput input = await snapshot.GetAuditInputAsync(
            new LedgerFilter { From = AuditSeeds.GeneratedAt.AddDays(-30), To = AuditSeeds.GeneratedAt },
            AuditSeeds.GeneratedAt);
        AuditCoverage coverage = input.Coverage;

        Assert.Equal(
            (AuditToolHistoryStart.Retention, withoutDetail, withoutToolData, 0L, withoutDetail + withoutToolData > 0, toolCount),
            (coverage.ToolHistoryStart, coverage.ImportedToolCallsWithoutDetail, coverage.ImportedRequestsWithoutToolData,
                coverage.SpansBeforeToolHistory, coverage.InsufficientToolHistory, Assert.Single(input.Spans).ToolCallCount));
    }

    // The input holds the selected prompt chains' recorded credit_velocity trips, reset or not, read in its snapshot. In
    // ExportSeeds, A's trip counts once reset; C's retry_churn trip is selected but doesn't count; E's trip has no selected
    // request; and the main branch selects none of A's requests.
    [Theory]
    [InlineData(null, 2, ExportSeeds.A)]
    [InlineData("main", 1, null)]
    public async Task AQ13_TheInput_HoldsTheSelectedChainsRecordedVelocityTrips(string? branch, int selectedTrips, string? trace)
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);
        await store.ResetBreakerTripsAsync(BreakerTripFilter.All);

        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();
        LedgerFilter filter = new() { Branch = branch };
        AuditInput input = await snapshot.GetAuditInputAsync(filter, ExportSeeds.GeneratedAt);

        string[] expected = trace is null ? [] : [trace];
        Assert.Equal(expected, input.VelocityTrippedTraces.Order(StringComparer.Ordinal));
        Assert.Equal(selectedTrips, (await snapshot.GetBreakerTripsAsync(filter)).Count);
    }
}
