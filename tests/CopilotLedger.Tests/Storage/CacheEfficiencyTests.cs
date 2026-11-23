using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Storage;

public sealed class CacheEfficiencyTests
{
    private static readonly JsonRateCardCalculator Calculator = JsonRateCardCalculator.LoadEmbedded();

    public static TheoryData<int, decimal> HitRatios => new()
    {
        { 0, 1m },
        { 1, 0m },
        { 2, 0.25m },
        { 3, 0.1666666666666666666666666667m },
        { 4, 0.4929331000134607618791223583m },
    };

    [Theory]
    [MemberData(nameof(HitRatios))]
    public async Task CE1_HitRatio_UsesOnlyTheSpansThatReportedCacheUsage(int caseNumber, decimal expected)
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        LedgerEntry[] entries = caseNumber switch
        {
            // All input read from the cache.
            0 => [Entry("1", input: 1_000, cacheRead: 1_000)],

            // True misses: a cache-read counter of 0, and a span that reported only a cache write.
            1 =>
            [
                Entry("1", input: 1_000, readState: CacheCounterState.Reported),
                Entry("2", input: 500, cacheWrite: 500),
            ],

            // A span without cache counters is left out, not counted as a miss.
            2 => [Entry("1", input: 1_000, cacheRead: 250), Entry("2", input: 3_000)],
            3 =>
            [
                Entry("1", input: 2_000, cacheRead: 500),
                Entry("2", input: 1_000, cacheWrite: 400),
                Entry("3", input: 5_000),
            ],

            // Copilot CLI's explore subagent: its first call wrote the cache and left out cache_read.
            4 => [Entry("1", input: 3_665, cacheWrite: 3_662), Entry("2", input: 3_764, cacheRead: 3_662, cacheWrite: 99)],
            _ => throw new ArgumentOutOfRangeException(nameof(caseNumber)),
        };
        await store.RecordAsync(entries);

        CacheEfficiencyRow row = Assert.Single(
            await store.GetCacheEfficiencyAsync(LedgerGrouping.Model, LedgerFilter.All));

        Assert.Equal(expected, row.CacheHitRatio);
    }

    [Fact]
    public async Task CE2_HitRatio_IsNullWithoutReportedInput()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        Assert.Empty(await store.GetCacheEfficiencyAsync(LedgerGrouping.Model, LedgerFilter.All));

        await store.RecordAsync(
        [
            Entry("1", model: "qwen3.8-27b-cuda-gpu:1", input: 1_000),
            Entry("2", model: "qwen3.8-27b-cuda-gpu:1", input: 3_000),
            Entry("3", model: "mai-code-2", input: 0, readState: CacheCounterState.Reported),
        ]);

        Assert.Collection(
            await store.GetCacheEfficiencyAsync(LedgerGrouping.Model, LedgerFilter.All),
            row =>
            {
                Assert.Equal("qwen3.8-27b", row.Model);
                Assert.Equal(2, row.SpanCount);
                Assert.Equal(0, row.CacheReportedSpanCount);
                Assert.Equal(2, row.CacheUnreportedSpanCount);
                Assert.Null(row.CacheHitRatio);
                Assert.Null(row.UncachedInputTokens);
                Assert.Equal(0, row.ReportedInputTokens);
                Assert.Equal(4_000, row.EstimatedUncachedInputTokens);
            },
            row =>
            {
                Assert.Equal("mai-code-2", row.Model);
                Assert.Equal(1, row.SpanCount);
                Assert.Equal(1, row.CacheReportedSpanCount);
                Assert.Null(row.CacheHitRatio);
                Assert.Equal(0, row.UncachedInputTokens);
            });
    }

    [Fact]
    public async Task CE3_Row_SeparatesObservedAndEstimatedTotals()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        await store.RecordAsync(
        [
            Entry("1", input: 1_000, output: 40, cacheRead: 600, cacheWrite: 100),
            Entry("2", input: 500, output: 20),
            Entry("3", input: 300, output: 10, cacheRead: 300),
        ]);

        CacheEfficiencyRow row = Assert.Single(
            await store.GetCacheEfficiencyAsync(LedgerGrouping.Model, LedgerFilter.All));

        Assert.Equal("mai-code-2", row.Model);
        Assert.Null(row.Repository);
        Assert.Null(row.Branch);
        Assert.Null(row.PullRequestNumber);
        Assert.Null(row.Agent);
        Assert.Equal(3, row.SpanCount);
        Assert.Equal(2, row.CacheReportedSpanCount);
        Assert.Equal(1, row.CacheUnreportedSpanCount);
        Assert.Equal(1_800, row.InputTokens);
        Assert.Equal(70, row.OutputTokens);
        Assert.Equal(1_000, row.MaxInputTokens);
        Assert.Equal(1_300, row.ReportedInputTokens);
        Assert.Equal(900, row.ReportedCacheReadInputTokens);
        Assert.Equal(100, row.ReportedCacheWriteInputTokens);
        Assert.Equal(300, row.ReportedUncachedInputTokens);
        Assert.Equal(300, row.UncachedInputTokens);
        Assert.Equal(800, row.EstimatedUncachedInputTokens);
        Assert.Equal(600m, row.AverageInputTokensPerTurn);
        Assert.Equal(0.6923076923076923076923076923m, row.CacheHitRatio);
    }

    [Fact]
    public async Task CE4_Groupings_KeyRowsAndOrderThemByInput()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);
        GitWorkspaceContext feature = GitFixture.Feature(temp.Path) with { PullRequestNumber = 42 };
        GitWorkspaceContext main = GitFixture.Feature(temp.Path) with { BranchName = "main" };
        GitWorkspaceContext fix = GitFixture.Feature(temp.Path) with { BranchName = "fix/login" };
        await store.RecordAsync(
        [
            Entry("1", model: "claude-opus-5.5", agent: "explore", git: feature, input: 4_000, cacheRead: 3_000),
            Entry("2", model: "mai-code-2", git: feature, input: 2_000, cacheRead: 500),
            Entry("3", model: "mai-code-2", agent: "explore", git: main, input: 1_000),
            Entry("4", model: "claude-opus-5.5", git: fix, input: 1_000, cacheRead: 1_000),
            Entry("5", model: "mai-code-2", input: 500, cacheRead: 100),
        ]);

        Assert.Collection(
            await store.GetCacheEfficiencyAsync(LedgerGrouping.Model, LedgerFilter.All),
            row => AssertRow(row, null, null, null, "claude-opus-5.5", null, 2, 2, 5_000, 0.8m),
            row => AssertRow(row, null, null, null, "mai-code-2", null, 3, 2, 3_500, 0.24m));
        Assert.Collection(
            await store.GetCacheEfficiencyAsync(LedgerGrouping.Agent, LedgerFilter.All),
            row => AssertRow(row, null, null, null, null, "explore", 2, 1, 5_000, 0.75m),
            row => AssertRow(row, null, null, null, null, "(none)", 3, 3, 3_500, 0.4571428571428571428571428571m));
        Assert.Collection(
            await store.GetCacheEfficiencyAsync(LedgerGrouping.Branch, LedgerFilter.All),
            row => AssertRow(row, "swigerb/copilot-ledger", "feature/ledger", null, null, null, 2, 2, 6_000, 0.5833333333333333333333333333m),
            row => AssertRow(row, "swigerb/copilot-ledger", "fix/login", null, null, null, 1, 1, 1_000, 1m),
            row => AssertRow(row, "swigerb/copilot-ledger", "main", null, null, null, 1, 0, 1_000, null),
            row => AssertRow(row, "(none)", "(none)", null, null, null, 1, 1, 500, 0.2m));
        Assert.Collection(
            await store.GetCacheEfficiencyAsync(LedgerGrouping.PullRequest, LedgerFilter.All),
            row => AssertRow(row, "swigerb/copilot-ledger", null, 42, null, null, 2, 2, 6_000, 0.5833333333333333333333333333m),
            row => AssertRow(row, "swigerb/copilot-ledger", "fix/login", null, null, null, 1, 1, 1_000, 1m),
            row => AssertRow(row, "swigerb/copilot-ledger", "main", null, null, null, 1, 0, 1_000, null),
            row => AssertRow(row, "(none)", "(none)", null, null, null, 1, 1, 500, 0.2m));
        Assert.Collection(
            await store.GetCacheEfficiencyAsync(LedgerGrouping.Repository, LedgerFilter.All),
            row => AssertRow(row, "swigerb/copilot-ledger", null, null, null, null, 4, 3, 8_000, 0.6428571428571428571428571429m),
            row => AssertRow(row, "(none)", null, null, null, null, 1, 1, 500, 0.2m));
        Assert.Collection(
            await store.GetCacheEfficiencyAsync(
                LedgerGrouping.Model,
                new LedgerFilter { PullRequestNumber = 42, Agent = "(none)" }),
            row => AssertRow(row, null, null, null, "mai-code-2", null, 1, 1, 2_000, 0.25m));
    }

    [Fact]
    public async Task CE5_UnsupportedGroupings_Throw()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await StoreAsync(temp);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => store.GetCacheEfficiencyAsync(LedgerGrouping.ChannelModel, LedgerFilter.All));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => store.GetRollupAsync(LedgerGrouping.PullRequest, LedgerFilter.All));
    }

    private static async Task<SqliteLedgerStore> StoreAsync(TempDirectory temp)
    {
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "ledger.db"));
        await store.InitializeAsync();
        return store;
    }

    // A chat span in trace 0e0e...0e<id>; a nonzero cache counter is Reported and a zero one is Absent unless a state is given.
    private static LedgerEntry Entry(
        string id,
        string model = "mai-code-2",
        string? agent = null,
        GitWorkspaceContext? git = null,
        long input = 0,
        long output = 0,
        long cacheRead = 0,
        long cacheWrite = 0,
        CacheCounterState? readState = null)
    {
        NormalizedGenAiSpan span = TestSpans.Chat(
            requestModel: model,
            agentName: agent,
            inputTokens: input,
            outputTokens: output,
            cacheReadInputTokens: cacheRead,
            cacheWriteInputTokens: cacheWrite,
            cacheReadState: readState) with
        {
            TraceId = "0e0e0e0e0e0e0e0e0e0e0e0e0e0e0e" + id.PadLeft(2, '0'),
            SpanId = "0e0e0e0e0e0e0e" + id.PadLeft(2, '0'),
        };
        return new LedgerEntry(span, Calculator.Calculate(span), git);
    }

    private static void AssertRow(
        CacheEfficiencyRow row,
        string? repository,
        string? branch,
        int? pullRequest,
        string? model,
        string? agent,
        long spans,
        long reported,
        long input,
        decimal? hitRatio)
    {
        Assert.Equal(repository, row.Repository);
        Assert.Equal(branch, row.Branch);
        Assert.Equal(pullRequest, row.PullRequestNumber);
        Assert.Equal(model, row.Model);
        Assert.Equal(agent, row.Agent);
        Assert.Equal(spans, row.SpanCount);
        Assert.Equal(reported, row.CacheReportedSpanCount);
        Assert.Equal(input, row.InputTokens);
        Assert.Equal(hitRatio, row.CacheHitRatio);
    }
}
