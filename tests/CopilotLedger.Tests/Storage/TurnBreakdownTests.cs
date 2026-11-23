using System.Globalization;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Storage;

public sealed class TurnBreakdownTests
{
    private const string Root = "a100000000000001";
    private const string Explore = "a100000000000002";

    [Fact]
    public async Task TB1_Trace_ListsRequestsInStartOrderWithDeltasPerSequence()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp);

        IReadOnlyList<TurnTokenRow> rows = await store.GetTurnBreakdownAsync(
            TurnSelector.ForTrace(TurnSeeds.B1),
            LedgerFilter.All);

        Assert.Collection(
            rows,
            row => AssertTurn(row, "c100000000000001", "10:00:00", Root, null, "claude-opus-5.5", 10_000, 500, CacheCounterState.Absent, CacheCounterState.Reported, 0, 9_000, 1_000, 0m, null),
            row => AssertTurn(row, "5100000000000001", "10:00:05", Explore, "explore", "mai-code-2", 3_000, 100, CacheCounterState.Absent, CacheCounterState.Absent, null, null, null, null, null),
            row => AssertTurn(row, "5100000000000002", "10:00:10", Explore, "explore", "mai-code-2", 5_000, 100, CacheCounterState.Reported, CacheCounterState.Absent, 2_000, 0, 3_000, 0.4m, 2_000),
            row => AssertTurn(row, "c100000000000002", "10:00:10", Root, null, "claude-opus-5.5", 12_000, 500, CacheCounterState.Reported, CacheCounterState.Reported, 9_000, 2_500, 500, 0.75m, 2_000),
            row => AssertTurn(row, "c100000000000003", "10:00:20", Root, null, "claude-opus-5.5", 11_000, 500, CacheCounterState.Reported, CacheCounterState.Absent, 11_000, 0, 0, 1m, -1_000),
            row => AssertTurn(row, "0100000000000001", "10:00:30", null, null, "claude-opus-5.5", 20_000, 500, CacheCounterState.Absent, CacheCounterState.Absent, null, null, null, null, null));
        TurnTokenRow first = rows[0];
        Assert.Equal(TurnSeeds.B1, first.TraceId);
        Assert.Equal(Root, first.ParentSpanId);
        Assert.Equal("conv-b", first.ConversationId);
        Assert.Equal("swigerb/copilot-ledger", first.Repository);
        Assert.Equal("feature/ledger", first.Branch);
        Assert.Equal(42, first.PullRequestNumber);
        Assert.Equal("f100000000000009", rows[5].ParentSpanId);
    }

    [Fact]
    public async Task TB2_Conversation_KeepsEachPromptsAgentSeparate()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp);

        IReadOnlyList<TurnTokenRow> rows = await store.GetTurnBreakdownAsync(
            TurnSelector.ForConversation("conv-b"),
            LedgerFilter.All);

        Assert.Equal(
            ["c100000000000001", "5100000000000001", "5100000000000002", "c100000000000002", "c100000000000003", "0100000000000001", "d200000000000001"],
            rows.Select(row => row.SpanId));
        TurnTokenRow next = rows[6];
        AssertTurn(next, "d200000000000001", "10:05:00", "a200000000000001", null, "claude-opus-5.5", 15_000, 500, CacheCounterState.Reported, CacheCounterState.Absent, 10_000, 0, 5_000, 0.6666666666666666666666666667m, null);
        Assert.Equal(TurnSeeds.B2, next.TraceId);
        Assert.Empty(await store.GetTurnBreakdownAsync(TurnSelector.ForConversation("conv-none"), LedgerFilter.All));
    }

    [Fact]
    public async Task TB3_Filter_AppliesAfterDeltasAreComputed()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp);

        IReadOnlyList<TurnTokenRow> explore = await store.GetTurnBreakdownAsync(
            TurnSelector.ForTrace(TurnSeeds.B1.ToUpperInvariant()),
            new LedgerFilter { Agent = "explore" });
        IReadOnlyList<TurnTokenRow> later = await store.GetTurnBreakdownAsync(
            TurnSelector.ForTrace(TurnSeeds.B1),
            new LedgerFilter { From = DateTimeOffset.Parse("2026-11-21T10:00:10Z", CultureInfo.InvariantCulture) });

        Assert.Equal([null, 2_000], explore.Select(row => row.InputDeltaFromPreviousTurn));
        Assert.Equal(
            ["5100000000000002", "c100000000000002", "c100000000000003", "0100000000000001"],
            later.Select(row => row.SpanId));
        Assert.Equal([2_000, 2_000, -1_000, null], later.Select(row => row.InputDeltaFromPreviousTurn));
    }

    [Fact]
    public void TB4_Selectors_ValidateTheirIds()
    {
        Assert.Equal(TurnSeeds.B1, TurnSelector.ForTrace(TurnSeeds.B1.ToUpperInvariant()).TraceId);
        Assert.Null(TurnSelector.ForTrace(TurnSeeds.B1).ConversationId);
        Assert.Equal("conv-b", TurnSelector.ForConversation("conv-b").ConversationId);
        Assert.Null(TurnSelector.ForConversation("conv-b").TraceId);
        Assert.Throws<ArgumentException>(() => TurnSelector.ForTrace("b1"));
        Assert.Throws<ArgumentException>(() => TurnSelector.ForTrace(new string('g', 32)));
        Assert.Throws<ArgumentNullException>(() => TurnSelector.ForTrace(null!));
        Assert.Throws<ArgumentException>(() => TurnSelector.ForConversation(" "));
        Assert.Throws<ArgumentNullException>(() => TurnSelector.ForConversation(null!));
    }

    [Fact]
    public async Task CB1_Bloat_RanksChainsByUncachedInputAndGrowth()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp);

        ContextBloatReport report = await store.GetContextBloatAsync(LedgerFilter.All, 10);

        Assert.Collection(
            report.ByUncachedInput,
            row => AssertChain(row, TurnSeeds.B3, "11:00:00", 2, 2, 50_000, 40_000, 40_000, 0.2m),
            row => AssertChain(row, TurnSeeds.B0, "12:00:00", 1, 1, 5_000, 5_000, 5_000, 0m),
            row => AssertChain(row, TurnSeeds.B2, "10:05:00", 1, 1, 15_000, 5_000, 5_000, 0.6666666666666666666666666667m));
        BloatChainRow incomplete = Assert.Single(report.ByEstimatedUncachedInput);
        AssertChain(incomplete, TurnSeeds.B1, "10:00:00", 6, 4, 61_000, 4_500, 27_500, 0.5789473684210526315789473684m);
        Assert.Equal("conv-b", incomplete.ConversationId);
        Assert.Equal("swigerb/copilot-ledger", incomplete.Repository);
        Assert.Equal("feature/ledger", incomplete.Branch);
        Assert.Equal(38_000, incomplete.ReportedInputTokens);
        Assert.Equal(22_000, incomplete.ReportedCacheReadInputTokens);
        Assert.Collection(
            report.ByInputGrowth,
            row => AssertGrowth(row, TurnSeeds.B3, "a300000000000001", null, "11:00:00", 2, 20_000, 30_000, 10_000),
            row => AssertGrowth(row, TurnSeeds.B1, Root, null, "10:00:00", 3, 10_000, 12_000, 2_000));
        Assert.Equal("conv-c", report.ByInputGrowth[0].ConversationId);
        Assert.Equal("claude-opus-5.5", report.ByInputGrowth[1].Model);
        Assert.Equal("swigerb/copilot-ledger", report.ByInputGrowth[1].Repository);
        Assert.Equal("feature/ledger", report.ByInputGrowth[1].Branch);
        Assert.Equal(2, report.SpansWithoutAgent);
    }

    [Fact]
    public async Task CB2_Bloat_HonorsLimitAndFilter()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp);

        ContextBloatReport one = await store.GetContextBloatAsync(LedgerFilter.All, 1);
        ContextBloatReport explore = await store.GetContextBloatAsync(new LedgerFilter { Agent = "explore" }, 10);

        Assert.Equal([TurnSeeds.B3], one.ByUncachedInput.Select(row => row.TraceId));
        Assert.Equal([TurnSeeds.B1], one.ByEstimatedUncachedInput.Select(row => row.TraceId));
        Assert.Equal([TurnSeeds.B3], one.ByInputGrowth.Select(row => row.TraceId));
        Assert.Empty(explore.ByUncachedInput);
        AssertChain(Assert.Single(explore.ByEstimatedUncachedInput), TurnSeeds.B1, "10:00:05", 2, 1, 8_000, 3_000, 6_000, 0.4m);
        AssertGrowth(Assert.Single(explore.ByInputGrowth), TurnSeeds.B1, Explore, "explore", "10:00:05", 2, 3_000, 5_000, 2_000);
        Assert.Equal(0, explore.SpansWithoutAgent);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.GetContextBloatAsync(LedgerFilter.All, 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.GetContextBloatAsync(LedgerFilter.All, 1001));
    }

    private static async Task<SqliteLedgerStore> SeededStoreAsync(TempDirectory temp)
    {
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "ledger.db"));
        await store.InitializeAsync();
        await TurnSeeds.SeedAsync(store, temp.Path);
        return store;
    }

    private static void AssertTurn(
        TurnTokenRow row,
        string spanId,
        string start,
        string? agentSpanId,
        string? agent,
        string model,
        long input,
        long output,
        CacheCounterState readState,
        CacheCounterState writeState,
        long? cacheRead,
        long? cacheWrite,
        long? uncached,
        decimal? hitRatio,
        long? delta)
    {
        Assert.Equal(spanId, row.SpanId);
        Assert.Equal(DateTimeOffset.Parse($"2026-11-21T{start}Z", CultureInfo.InvariantCulture), row.StartTime);
        Assert.Equal(agentSpanId, row.AgentSpanId);
        Assert.Equal(agent, row.Agent);
        Assert.Equal(model, row.Model);
        Assert.Equal(input, row.InputTokens);
        Assert.Equal(output, row.OutputTokens);
        Assert.Equal(readState, row.CacheReadState);
        Assert.Equal(writeState, row.CacheWriteState);
        Assert.Equal(cacheRead, row.CacheReadInputTokens);
        Assert.Equal(cacheWrite, row.CacheWriteInputTokens);
        Assert.Equal(uncached, row.UncachedInputTokens);
        Assert.Equal(hitRatio, row.CacheHitRatio);
        Assert.Equal(delta, row.InputDeltaFromPreviousTurn);
    }

    private static void AssertChain(
        BloatChainRow row,
        string traceId,
        string start,
        long spans,
        long reported,
        long input,
        long reportedUncached,
        long estimatedUncached,
        decimal? hitRatio)
    {
        Assert.Equal(traceId, row.TraceId);
        Assert.Equal(DateTimeOffset.Parse($"2026-11-21T{start}Z", CultureInfo.InvariantCulture), row.StartTime);
        Assert.Equal(spans, row.SpanCount);
        Assert.Equal(reported, row.CacheReportedSpanCount);
        Assert.Equal(input, row.InputTokens);
        Assert.Equal(reportedUncached, row.ReportedUncachedInputTokens);
        Assert.Equal(estimatedUncached, row.EstimatedUncachedInputTokens);
        Assert.Equal(hitRatio, row.CacheHitRatio);
    }

    private static void AssertGrowth(
        InputGrowthRow row,
        string traceId,
        string agentSpanId,
        string? agent,
        string start,
        long turns,
        long first,
        long peak,
        long growth)
    {
        Assert.Equal(traceId, row.TraceId);
        Assert.Equal(agentSpanId, row.AgentSpanId);
        Assert.Equal(agent, row.Agent);
        Assert.Equal(DateTimeOffset.Parse($"2026-11-21T{start}Z", CultureInfo.InvariantCulture), row.StartTime);
        Assert.Equal(turns, row.TurnCount);
        Assert.Equal(first, row.FirstInputTokens);
        Assert.Equal(peak, row.PeakInputTokens);
        Assert.Equal(growth, row.InputGrowthTokens);
    }
}
