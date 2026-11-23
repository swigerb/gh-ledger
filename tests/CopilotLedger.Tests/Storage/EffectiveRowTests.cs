using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Import;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Storage;

// One ledger: a live request (L, $0.003) in session-a on main, the imported twin it superseded (T, $3, with a failed tool
// call), and another imported request (E, $0.006, with a failed tool call) in session-b on feature/x. Only L and E count.
public sealed class EffectiveRowTests
{
    private const string S1 = "11111111-1111-4111-8111-111111111111";
    private const string S2 = "22222222-2222-4222-8222-222222222222";
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    public static TheoryData<LedgerGrouping> Groupings => new()
    {
        LedgerGrouping.Repository,
        LedgerGrouping.Branch,
        LedgerGrouping.Model,
        LedgerGrouping.Agent,
        LedgerGrouping.ChannelModel,
    };

    [Theory]
    [MemberData(nameof(Groupings))]
    public async Task ER1_RollupsCountOnlyEffectiveRows(LedgerGrouping grouping)
    {
        using TempDirectory temp = new();
        Ledger ledger = await SeedAsync(temp);

        IReadOnlyList<LedgerRollupRow> rows = await ledger.Store.GetRollupAsync(grouping, LedgerFilter.All);

        Assert.Equal(2, rows.Sum(row => row.SpanCount));
        Assert.Equal(3_000, rows.Sum(row => row.InputTokens));
        Assert.Equal(0.009m, rows.Sum(row => row.AiCreditsUsd));
        Assert.Equal(0.9m, rows.Sum(row => row.AiCredits));
        Assert.Equal(0.009m, rows.Sum(row => row.ApiEquivalentUsd));
    }

    [Fact]
    public async Task ER2_PromptChainsAndCountsLeaveOutSupersededRows()
    {
        using TempDirectory temp = new();
        Ledger ledger = await SeedAsync(temp);

        IReadOnlyList<PromptChainRow> chains = await ledger.Store.GetTopPromptChainsAsync(LedgerFilter.All, 10);

        Assert.Equal([ledger.Other.Span.TraceId, ledger.Live.Span.TraceId], chains.Select(chain => chain.TraceId));
        Assert.Equal([0.006m, 0.003m], chains.Select(chain => chain.AiCreditsUsd));
        Assert.All(chains, chain => Assert.Equal(["claude-sonnet-5.5"], chain.Models));
        Assert.Equal(2, await ledger.Store.GetPromptCountAsync(LedgerFilter.All));
    }

    [Fact]
    public async Task ER3_WhatIfPricingLeavesOutSupersededRows()
    {
        using TempDirectory temp = new();
        Ledger ledger = await SeedAsync(temp);
        JsonRateCardCalculator whatIf = JsonRateCardCalculator.LoadEmbedded();

        LedgerRollupRow total = Assert.Single(
            await ledger.Store.GetRollupAsync(LedgerGrouping.Repository, LedgerFilter.All, whatIf));
        IReadOnlyList<PromptChainRow> chains = await ledger.Store.GetTopPromptChainsAsync(LedgerFilter.All, 10, whatIf);

        Assert.Equal(0.009m, total.ApiEquivalentUsd);
        Assert.Equal(2, total.ApiEquivalentEstimatedSpanCount);
        Assert.Equal([0.006m, 0.003m], chains.Select(chain => chain.ApiEquivalentUsd));
    }

    [Fact]
    public async Task ER4_CacheEfficiencyLeavesOutSupersededRows()
    {
        using TempDirectory temp = new();
        Ledger ledger = await SeedAsync(temp);

        CacheEfficiencyRow row = Assert.Single(
            await ledger.Store.GetCacheEfficiencyAsync(LedgerGrouping.Repository, LedgerFilter.All));

        Assert.Equal(2, row.SpanCount);
        Assert.Equal(3_000, row.InputTokens);
        Assert.Equal(2_000, row.MaxInputTokens);
    }

    [Fact]
    public async Task ER5_TurnsLeaveOutSupersededRows()
    {
        using TempDirectory temp = new();
        Ledger ledger = await SeedAsync(temp);

        TurnTokenRow turn = Assert.Single(await ledger.Store.GetTurnBreakdownAsync(
            TurnSelector.ForConversation("session-a"),
            LedgerFilter.All));

        Assert.Equal(ledger.Live.Span.SpanId, turn.SpanId);
        Assert.Equal(1_000, turn.InputTokens);
    }

    [Fact]
    public async Task ER6_BloatLeavesOutSupersededRows()
    {
        using TempDirectory temp = new();
        Ledger ledger = await SeedAsync(temp);

        ContextBloatReport report = await ledger.Store.GetContextBloatAsync(LedgerFilter.All, 10);

        Assert.Equal(2, report.SpansWithoutAgent);
        Assert.Equal(
            [ledger.Other.Span.TraceId, ledger.Live.Span.TraceId],
            report.ByEstimatedUncachedInput.Select(chain => chain.TraceId));
        Assert.Equal([2_000, 1_000], report.ByEstimatedUncachedInput.Select(chain => chain.EstimatedUncachedInputTokens));
    }

    [Fact]
    public async Task ER7_BudgetsCountOnlyEffectiveSpend()
    {
        using TempDirectory temp = new();
        Ledger ledger = await SeedAsync(temp);
        await ledger.Store.SetBranchBudgetAsync("contoso/webapp", "main", 1m);

        BranchBudgetRow budget = Assert.Single(await ledger.Store.GetBranchBudgetsAsync());
        bool alerted = await ledger.Store.MarkBranchBudgetAlertedAsync("contoso/webapp", "main");

        Assert.Equal(0.3m, budget.SpentAiCredits);
        Assert.False(budget.Exceeded);
        Assert.False(alerted);
        Assert.Empty(await ledger.Store.GetUnnotifiedBudgetAlertsAsync(DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public async Task ER8_TraceActivityLeavesOutSupersededRows()
    {
        using TempDirectory temp = new();
        Ledger ledger = await SeedAsync(temp);

        TraceActivity twin = await ledger.Store.GetTraceActivityAsync(ledger.Twin.Span.TraceId);
        TraceActivity live = await ledger.Store.GetTraceActivityAsync(ledger.Live.Span.TraceId);

        Assert.Equal(0, twin.SpanCount);
        Assert.Empty(twin.Costs);
        Assert.Empty(twin.ToolCalls);
        Assert.Empty(twin.Commits);
        Assert.Equal(1, live.SpanCount);
        Assert.Equal([0.003m], live.Costs.Select(cost => cost.CostUsd));
        Assert.Equal([Sha], live.Commits.Select(commit => commit.CommitSha));
    }

    [Fact]
    public async Task ER9_FailedToolCallsOfSupersededRowsDontCount()
    {
        using TempDirectory temp = new();
        Ledger ledger = await SeedAsync(temp);

        IReadOnlyDictionary<string, long> counts = await ledger.Store.GetFailedToolCallCountsAsync(
            [ledger.Twin.Span.TraceId, ledger.Other.Span.TraceId]);
        TraceActivity other = await ledger.Store.GetTraceActivityAsync(ledger.Other.Span.TraceId);

        Assert.Equal(ledger.Other.Span.TraceId, Assert.Single(counts).Key);
        Assert.Equal(1, counts[ledger.Other.Span.TraceId]);
        Assert.Single(other.ToolCalls);
    }

    [Fact]
    public async Task ER10_TheApiEquivalentBackfillLeavesOutSupersededRows()
    {
        using TempDirectory temp = new();
        Ledger ledger = await SeedAsync(temp);
        await LedgerSql.ExecuteAsync(
            ledger.Path,
            """
            UPDATE spans SET api_equivalent_usd_pico = NULL, api_equivalent_status = NULL, api_rate_card_model_id = NULL,
                             api_rates_version = NULL, api_rate_card_sha256 = NULL;
            """);

        ApiEquivalentBackfill backfill = await ledger.Store.BackfillApiEquivalentAsync(
            JsonRateCardCalculator.LoadEmbedded(),
            dryRun: true);

        Assert.Equal(2, backfill.SpanCount);
        Assert.Equal(0.009m, backfill.PricedUsd);
    }

    private static async Task<Ledger> SeedAsync(TempDirectory temp)
    {
        string path = Path.Combine(temp.Path, "ledger.db");
        string root = Path.Combine(temp.Path, "webapp");
        SqliteLedgerStore store = new(path);
        await store.InitializeAsync();
        ImportedLedgerEntry twin = Imported("usage-t", "session-a", S1, 1_000_000, 100_000, root, "main");
        twin = twin with { ToolLinks = [ImportEntries.ToolLink(twin, "toolu-t", 1)] };
        ImportedLedgerEntry other = Imported("usage-e", "session-b", S2, 2_000, 200, root, "feature/x");
        other = other with { ToolLinks = [ImportEntries.ToolLink(other, "toolu-e", 1)] };
        await store.RecordImportedAsync([twin, other], null);
        LedgerEntry live = ImportEntries.Live("session-a", S1);
        NormalizedGenAiSpan span = live.Span with { RequestModel = "claude-sonnet-5.5" };
        live = new LedgerEntry(
            span,
            ImportEntries.Calculator.Calculate(span),
            new GitWorkspaceContext(root, Path.Combine(root, ".git"), "main", Sha, null, "contoso/webapp"));
        await store.RecordAsync([live]);
        Assert.Equal(
            ["import:copilot|superseded", "import:copilot|effective", "otlp|effective"],
            await LedgerSql.RowsAsync(path, "SELECT ingest_source, accounting_state FROM spans ORDER BY id;"));
        return new Ledger(store, path, live, twin, other);
    }

    private static ImportedLedgerEntry Imported(
        string key,
        string conversation,
        string serviceRequestId,
        long input,
        long output,
        string root,
        string branch)
    {
        ImportedLedgerEntry entry = ImportEntries.Copilot(key, conversation, serviceRequestId);
        NormalizedGenAiSpan span = entry.Span with
        {
            RequestModel = "claude-sonnet-5.5",
            InputTokens = input,
            OutputTokens = output,
        };
        return entry with
        {
            Span = span,
            Calculation = ImportEntries.Calculator.Calculate(span),
            Git = new ImportedGitContext
            {
                RepositoryRoot = root,
                RepositorySlug = "contoso/webapp",
                Branch = branch,
                HeadCommitSha = Sha,
            },
        };
    }

    private sealed record Ledger(
        SqliteLedgerStore Store,
        string Path,
        LedgerEntry Live,
        ImportedLedgerEntry Twin,
        ImportedLedgerEntry Other);
}
