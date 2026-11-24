using System.Text.Json;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Storage;

// gh ledger export's read snapshot: the reports' queries in one read transaction, and the conversation filter.
public sealed class ExportSnapshotTests
{
    [Theory]
    [InlineData("rollup")]
    [InlineData("cache")]
    [InlineData("chains")]
    [InlineData("count")]
    [InlineData("turns")]
    public async Task XS1_TheConversationFilterLimitsEveryQuery(string query)
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp);
        LedgerFilter conversation = new() { ConversationId = "conv-c" };
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        switch (query)
        {
            case "rollup":
                foreach (IReadOnlyList<LedgerRollupRow> rows in new[]
                         {
                             await store.GetRollupAsync(LedgerGrouping.Model, conversation),
                             await snapshot.GetRollupAsync(LedgerGrouping.Model, conversation),
                         })
                {
                    LedgerRollupRow row = Assert.Single(rows);
                    Assert.Equal(("claude-opus-5.5", 2L, 50_000L), (row.Model, row.SpanCount, row.InputTokens));
                }

                break;
            case "cache":
                foreach (IReadOnlyList<CacheEfficiencyRow> rows in new[]
                         {
                             await store.GetCacheEfficiencyAsync(LedgerGrouping.Model, conversation),
                             await snapshot.GetCacheEfficiencyAsync(LedgerGrouping.Model, conversation),
                         })
                {
                    CacheEfficiencyRow row = Assert.Single(rows);
                    Assert.Equal(("claude-opus-5.5", 2L, 50_000L), (row.Model, row.SpanCount, row.InputTokens));
                }

                break;
            case "chains":
                Assert.Equal(TurnSeeds.B3, Assert.Single(await store.GetTopPromptChainsAsync(conversation, 10)).TraceId);
                Assert.Equal(TurnSeeds.B3, Assert.Single(await snapshot.GetTopPromptChainsAsync(conversation, 10)).TraceId);
                break;
            case "count":
                Assert.Equal(1, await store.GetPromptCountAsync(conversation));
                Assert.Equal(1, await snapshot.GetPromptCountAsync(conversation));
                break;
            default:
                Assert.Empty(await store.GetTurnBreakdownAsync(TurnSelector.ForTrace(TurnSeeds.B1), conversation));
                Assert.Empty(await snapshot.GetTurnBreakdownAsync(TurnSelector.ForTrace(TurnSeeds.B1), conversation));
                Assert.Equal(2, (await store.GetTurnBreakdownAsync(TurnSelector.ForTrace(TurnSeeds.B3), conversation)).Count);
                Assert.Equal(2, (await snapshot.GetTurnBreakdownAsync(TurnSelector.ForTrace(TurnSeeds.B3), conversation)).Count);
                break;
        }
    }

    [Fact]
    public async Task XS2_ASnapshotDoesntSeeRequestsRecordedAfterItStarted()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        Assert.Equal(1, await store.RecordAsync([Entry()]));

        Assert.Equal(4, await snapshot.GetPromptCountAsync(LedgerFilter.All));
        Assert.Equal(10, (await snapshot.GetRollupAsync(LedgerGrouping.Model, LedgerFilter.All)).Sum(row => row.SpanCount));
        Assert.Equal(4, (await snapshot.GetTopPromptChainsAsync(LedgerFilter.All, 10)).Count);
        Assert.Equal(5, await store.GetPromptCountAsync(LedgerFilter.All));
        await using LedgerExportSnapshot later = await store.OpenExportSnapshotAsync();
        Assert.Equal(5, await later.GetPromptCountAsync(LedgerFilter.All));
    }

    [Fact]
    public async Task XS3_TheSnapshotAnswersAsTheStoreDoes()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp);
        await using LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync();

        foreach (LedgerFilter filter in new[] { LedgerFilter.All, new LedgerFilter { Repository = "swigerb/copilot-ledger" } })
        {
            foreach (LedgerGrouping grouping in new[]
                     {
                         LedgerGrouping.Repository, LedgerGrouping.Branch, LedgerGrouping.Model, LedgerGrouping.Agent,
                         LedgerGrouping.ChannelModel,
                     })
            {
                IReadOnlyList<LedgerRollupRow> rows = await store.GetRollupAsync(grouping, filter);
                Assert.NotEmpty(rows);
                Assert.Equal(Json(rows), Json(await snapshot.GetRollupAsync(grouping, filter)));
            }

            foreach (LedgerGrouping grouping in new[]
                     {
                         LedgerGrouping.Repository, LedgerGrouping.Branch, LedgerGrouping.Model, LedgerGrouping.Agent,
                         LedgerGrouping.PullRequest,
                     })
            {
                IReadOnlyList<CacheEfficiencyRow> rows = await store.GetCacheEfficiencyAsync(grouping, filter);
                Assert.NotEmpty(rows);
                Assert.Equal(Json(rows), Json(await snapshot.GetCacheEfficiencyAsync(grouping, filter)));
            }

            Assert.Equal(
                Json(await store.GetTopPromptChainsAsync(filter, 10)),
                Json(await snapshot.GetTopPromptChainsAsync(filter, 10)));
            Assert.Equal(await store.GetPromptCountAsync(filter), await snapshot.GetPromptCountAsync(filter));
            Assert.Equal(
                Json(await store.GetTurnBreakdownAsync(TurnSelector.ForConversation("conv-b"), filter)),
                Json(await snapshot.GetTurnBreakdownAsync(TurnSelector.ForConversation("conv-b"), filter)));
        }
    }

    [Fact]
    public async Task XS4_ASnapshotNeedsAnInitializedLedger()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "ledger.db"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.OpenExportSnapshotAsync());
        await store.InitializeAsync();
        await using (LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync())
        {
            Assert.Equal(0, await snapshot.GetPromptCountAsync(LedgerFilter.All));
        }

        Assert.Equal(1, await store.RecordAsync([Entry()]));
        Assert.Equal(1, await store.GetPromptCountAsync(LedgerFilter.All));
    }

    private static async Task<SqliteLedgerStore> SeededStoreAsync(TempDirectory temp)
    {
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "ledger.db"));
        await store.InitializeAsync();
        await TurnSeeds.SeedAsync(store, temp.Path);
        return store;
    }

    // One request in a new prompt chain and conversation.
    private static LedgerEntry Entry()
    {
        NormalizedGenAiSpan span = TestSpans.Chat(
            startTime: new DateTimeOffset(2026, 11, 21, 13, 0, 0, TimeSpan.Zero),
            requestModel: "claude-opus-5.5",
            conversationId: "conv-d",
            inputTokens: 1_000,
            outputTokens: 100) with
        {
            TraceId = "b4b4b4b4b4b4b4b4b4b4b4b4b4b4b4b4",
            SpanId = "f400000000000001",
        };
        return new LedgerEntry(span, JsonRateCardCalculator.LoadEmbedded().Calculate(span), null);
    }

    private static string Json<T>(T value) => JsonSerializer.Serialize(value);
}
