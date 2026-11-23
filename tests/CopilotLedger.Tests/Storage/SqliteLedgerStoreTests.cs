using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Storage;

public sealed class SqliteLedgerStoreTests
{
    private static readonly DateTimeOffset Cutover =
        new(2026, 11, 21, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task S1_InitializeAsync_CreatesWalDatabaseAndIsIdempotent()
    {
        using TempDirectory temp = new();
        string databasePath = Path.Combine(temp.Path, "nested", "ledger.db");
        SqliteLedgerStore store = new(databasePath);

        await store.InitializeAsync();

        Assert.True(Directory.Exists(Path.GetDirectoryName(databasePath)));
        Assert.True(File.Exists(databasePath));
        await using (SqliteConnection connection = new($"Data Source={databasePath}"))
        {
            await connection.OpenAsync();
            await using (SqliteCommand journalCommand = connection.CreateCommand())
            {
                journalCommand.CommandText = "PRAGMA journal_mode;";
                Assert.Equal("wal", await journalCommand.ExecuteScalarAsync());
            }

            await using (SqliteCommand versionCommand = connection.CreateCommand())
            {
                versionCommand.CommandText = "PRAGMA user_version;";
                Assert.Equal(6L, await versionCommand.ExecuteScalarAsync());
            }
        }

        await store.InitializeAsync();
    }

    [Fact]
    public async Task S2_InitializeAsync_NewerDatabaseVersion_ThrowsInvalidOperationException()
    {
        using TempDirectory temp = new();
        string databasePath = DatabasePath(temp);
        await new SqliteLedgerStore(databasePath).InitializeAsync();

        await using (SqliteConnection connection = new($"Data Source={databasePath}"))
        {
            await connection.OpenAsync();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version = 99;";
            await command.ExecuteNonQueryAsync();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new SqliteLedgerStore(databasePath).InitializeAsync());
    }

    [Fact]
    public async Task S3_RecordAsync_BeforeInitialize_ThrowsInvalidOperationException()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = new(DatabasePath(temp));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.RecordAsync(CreateSeedEntries(temp.Path)));
    }

    [Fact]
    public async Task S4_RecordAsync_InsertsSeedOnceAndSkipsDuplicates()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await InitializedStoreAsync(temp);
        IReadOnlyList<LedgerEntry> entries = CreateSeedEntries(temp.Path);

        Assert.Equal(5, await store.RecordAsync(entries));
        Assert.Equal(0, await store.RecordAsync(entries));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task S5_RecordAsync_InvalidBatch_WritesNothing(bool negativeOutputTokens)
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await InitializedStoreAsync(temp);
        IReadOnlyList<LedgerEntry> seed = CreateSeedEntries(temp.Path);
        LedgerEntry invalidEntry;
        if (negativeOutputTokens)
        {
            NormalizedGenAiSpan span = TestSpans.Chat(
                requestModel: "claude-opus-5.5",
                outputTokens: -1);
            invalidEntry = new LedgerEntry(
                span,
                JsonRateCardCalculator.LoadEmbedded().Calculate(span),
                null);
        }
        else
        {
            NormalizedGenAiSpan span = TestSpans.Chat(
                operationName: "invoke_agent",
                requestModel: "claude-opus-5.5",
                outputTokens: 1);
            invalidEntry = new LedgerEntry(
                span,
                JsonRateCardCalculator.LoadEmbedded().Calculate(span),
                null);
        }

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.RecordAsync([seed[0], invalidEntry]));

        Assert.Empty(await store.GetRollupAsync(
            LedgerGrouping.Repository,
            LedgerFilter.All));
    }

    [Fact]
    public async Task S6_RepositoryRollup_ReturnsExactSeedTotals()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp);
        IReadOnlyList<LedgerRollupRow> rows = await store.GetRollupAsync(
            LedgerGrouping.Repository,
            LedgerFilter.All);

        Assert.Collection(
            rows,
            row => AssertRollup(
                row,
                repository: "swigerb/copilot-ledger",
                spans: 3,
                unpriced: 0,
                input: 2_600_000,
                output: 162_000,
                cacheRead: 1_200_000,
                cacheWrite: 100_000,
                credits: 342m,
                githubUsd: 3.42m,
                byokUsd: 0m,
                savedUsd: 0.23m),
            row => AssertRollup(
                row,
                repository: "contoso/api",
                spans: 1,
                unpriced: 0,
                input: 100_000,
                output: 10_000,
                cacheRead: 0,
                cacheWrite: 0,
                credits: 0m,
                githubUsd: 0m,
                byokUsd: 0.315m,
                savedUsd: 0m),
            row => AssertRollup(
                row,
                repository: "(none)",
                spans: 1,
                unpriced: 1,
                input: 1_000,
                output: 100,
                cacheRead: 0,
                cacheWrite: 0,
                credits: 0m,
                githubUsd: 0m,
                byokUsd: 0m,
                savedUsd: 0m));
    }

    [Fact]
    public async Task S7_BranchRollup_ReturnsExactSeedTotals()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp);
        IReadOnlyList<LedgerRollupRow> rows = await store.GetRollupAsync(
            LedgerGrouping.Branch,
            LedgerFilter.All);

        Assert.Collection(
            rows,
            row => AssertBranch(
                row,
                "swigerb/copilot-ledger",
                "main",
                1,
                286m,
                2.86m,
                0m,
                0m),
            row => AssertBranch(
                row,
                "swigerb/copilot-ledger",
                "feature/ledger",
                2,
                56m,
                0.56m,
                0m,
                0.23m),
            row => AssertBranch(
                row,
                "contoso/api",
                "main",
                1,
                0m,
                0m,
                0.315m,
                0m),
            row => AssertBranch(
                row,
                "(none)",
                "(none)",
                1,
                0m,
                0m,
                0m,
                0m));
    }

    [Fact]
    public async Task S8_ModelRollup_ReturnsExactSeedTotals()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp);
        IReadOnlyList<LedgerRollupRow> rows = await store.GetRollupAsync(
            LedgerGrouping.Model,
            LedgerFilter.All);

        Assert.Collection(
            rows,
            row => AssertModel(
                row, "claude-opus-5.5", 1, 0, 286m, 2.86m, 0m, 0m),
            row => AssertModel(
                row, "mai-code-2", 1, 0, 56m, 0.56m, 0m, 0m),
            row => AssertModel(
                row, "gpt-6.1-sol", 1, 0, 0m, 0m, 0.315m, 0m),
            row => AssertModel(
                row, "qwen3.8-27b", 1, 0, 0m, 0m, 0m, 0.23m),
            row => AssertModel(
                row, "gpt-unknown", 1, 1, 0m, 0m, 0m, 0m));
    }

    [Fact]
    public async Task S9_AgentRollup_ReturnsExactSeedTotals()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp);
        IReadOnlyList<LedgerRollupRow> rows = await store.GetRollupAsync(
            LedgerGrouping.Agent,
            LedgerFilter.All);

        Assert.Collection(
            rows,
            row => AssertAgent(
                row,
                "ledger-architect",
                1,
                0,
                1_200_000,
                50_000,
                286m,
                2.86m,
                0m,
                0m),
            row => AssertAgent(
                row,
                "ledger-builder",
                3,
                1,
                1_401_000,
                112_100,
                56m,
                0.56m,
                0m,
                0.23m),
            row => AssertAgent(
                row,
                "(none)",
                1,
                0,
                100_000,
                10_000,
                0m,
                0m,
                0.315m,
                0m));
    }

    [Fact]
    public async Task S10_Filters_ReturnExpectedRowsAndOrder()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp);

        IReadOnlyList<LedgerRollupRow> repositoryFilter =
            await store.GetRollupAsync(
                LedgerGrouping.Branch,
                new LedgerFilter { Repository = "swigerb/copilot-ledger" });
        Assert.Collection(
            repositoryFilter,
            row =>
            {
                Assert.Equal("main", row.Branch);
                Assert.Equal(2.86m, row.AiCreditsUsd);
            },
            row =>
            {
                Assert.Equal("feature/ledger", row.Branch);
                Assert.Equal(0.56m, row.AiCreditsUsd);
            });

        IReadOnlyList<LedgerRollupRow> branchFilter =
            await store.GetRollupAsync(
                LedgerGrouping.Repository,
                new LedgerFilter { Branch = "main" });
        Assert.Collection(
            branchFilter,
            row =>
            {
                Assert.Equal("swigerb/copilot-ledger", row.Repository);
                Assert.Equal(1, row.SpanCount);
                Assert.Equal(2.86m, row.AiCreditsUsd);
            },
            row =>
            {
                Assert.Equal("contoso/api", row.Repository);
                Assert.Equal(1, row.SpanCount);
                Assert.Equal(0.315m, row.FoundryByokUsd);
            });

        IReadOnlyList<LedgerRollupRow> toFilter =
            await store.GetRollupAsync(
                LedgerGrouping.Repository,
                new LedgerFilter { To = Cutover });
        Assert.Equal(
            ["swigerb/copilot-ledger", "contoso/api"],
            toFilter.Select(row => row.Repository));

        IReadOnlyList<LedgerRollupRow> fromFilter =
            await store.GetRollupAsync(
                LedgerGrouping.Repository,
                new LedgerFilter { From = Cutover });
        LedgerRollupRow only = Assert.Single(fromFilter);
        Assert.Equal("(none)", only.Repository);
        Assert.Equal(1, only.SpanCount);
    }

    [Fact]
    public async Task S11_Rollup_PreservesPicoUsdExactness()
    {
        using TempDirectory v5Temp = new();
        using TempDirectory v12Temp = new();
        JsonRateCardCalculator calculator = JsonRateCardCalculator.LoadEmbedded();

        NormalizedGenAiSpan v5Span = TestSpans.Chat(
            requestModel: "gpt-6.1-sol",
            inputTokens: 272_001,
            outputTokens: 10_000);
        SqliteLedgerStore v5Store = await InitializedStoreAsync(v5Temp);
        await v5Store.RecordAsync(
            [new LedgerEntry(v5Span, calculator.Calculate(v5Span), null)]);
        LedgerRollupRow v5 =
            Assert.Single(await v5Store.GetRollupAsync(
                LedgerGrouping.Model,
                LedgerFilter.All));
        Assert.Equal(1.238004m, v5.AiCreditsUsd);
        Assert.Equal(123.8004m, v5.AiCredits);

        NormalizedGenAiSpan v12Span = TestSpans.Chat(
            requestModel: "mai-code-2",
            inputTokens: 1,
            cacheReadInputTokens: 1);
        SqliteLedgerStore v12Store = await InitializedStoreAsync(v12Temp);
        await v12Store.RecordAsync(
            [new LedgerEntry(v12Span, calculator.Calculate(v12Span), null)]);
        LedgerRollupRow v12 =
            Assert.Single(await v12Store.GetRollupAsync(
                LedgerGrouping.Model,
                LedgerFilter.All));
        Assert.Equal(0.000000025m, v12.AiCreditsUsd);
        Assert.Equal(0.0000025m, v12.AiCredits);
    }

    [Fact]
    public async Task S12_RecordAsync_StoresCacheStatesAndPullRequest()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await InitializedStoreAsync(temp);
        JsonRateCardCalculator calculator = JsonRateCardCalculator.LoadEmbedded();
        NormalizedGenAiSpan reported = TestSpans.Chat(
            requestModel: "claude-opus-5.5",
            inputTokens: 1_000,
            outputTokens: 10,
            cacheReadInputTokens: 600,
            cacheWriteState: CacheCounterState.Reported) with
        {
            TraceId = new string('a', 32),
            SpanId = "aaaaaaaaaaaaaaa1",
        };
        NormalizedGenAiSpan absent = TestSpans.Chat(
            requestModel: "mai-code-2",
            inputTokens: 500,
            outputTokens: 5) with
        {
            TraceId = new string('a', 32),
            SpanId = "aaaaaaaaaaaaaaa2",
        };
        GitWorkspaceContext pullRequest =
            Git(temp.Path, "swigerb/copilot-ledger", "feature/ledger") with { PullRequestNumber = 42 };

        Assert.Equal(2, await store.RecordAsync(
        [
            new LedgerEntry(reported, calculator.Calculate(reported), pullRequest),
            new LedgerEntry(absent, calculator.Calculate(absent), null),
        ]));

        await using SqliteConnection connection = new($"Data Source={DatabasePath(temp)}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT span_id, cache_read_state, cache_write_state, pull_request_number, agent_span_id FROM spans ORDER BY span_id;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("aaaaaaaaaaaaaaa1", reader.GetString(0));
        Assert.Equal("reported", reader.GetString(1));
        Assert.Equal("reported", reader.GetString(2));
        Assert.Equal(42L, reader.GetInt64(3));
        Assert.True(reader.IsDBNull(4));
        Assert.True(await reader.ReadAsync());
        Assert.Equal("aaaaaaaaaaaaaaa2", reader.GetString(0));
        Assert.Equal("absent", reader.GetString(1));
        Assert.Equal("absent", reader.GetString(2));
        Assert.True(reader.IsDBNull(3));
        Assert.True(reader.IsDBNull(4));
        Assert.False(await reader.ReadAsync());
    }

    [Theory]
    [InlineData(0, "Cache counter states must be Reported or Absent, and an Absent counter must be 0. (Parameter 'entries')")]
    [InlineData(1, "Cache counter states must be Reported or Absent, and an Absent counter must be 0. (Parameter 'entries')")]
    [InlineData(2, "Cache counter states must be Reported or Absent, and an Absent counter must be 0. (Parameter 'entries')")]
    [InlineData(3, "Cache counter states must be Reported or Absent, and an Absent counter must be 0. (Parameter 'entries')")]
    [InlineData(4, "Cache counter states must be Reported or Absent, and an Absent counter must be 0. (Parameter 'entries')")]
    [InlineData(5, "Pull request numbers must be positive. (Parameter 'entries')")]
    [InlineData(6, "Pull request numbers must be positive. (Parameter 'entries')")]
    public async Task S13_RecordAsync_RejectsInconsistentCacheStatesAndPullRequests(int caseNumber, string message)
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await InitializedStoreAsync(temp);
        IReadOnlyList<LedgerEntry> seed = CreateSeedEntries(temp.Path);
        NormalizedGenAiSpan span = TestSpans.Chat(
            requestModel: "mai-code-2",
            inputTokens: 1_000,
            outputTokens: 10);
        span = caseNumber switch
        {
            0 => span with { CacheReadState = CacheCounterState.LegacyUnknown },
            1 => span with { CacheWriteState = CacheCounterState.LegacyUnknown },
            2 => span with { CacheReadInputTokens = 5 },
            3 => span with { CacheWriteInputTokens = 5 },
            4 => span with { CacheReadState = (CacheCounterState)7 },
            _ => span,
        };
        GitWorkspaceContext? git = caseNumber switch
        {
            5 => Git(temp.Path, "swigerb/copilot-ledger", "main") with { PullRequestNumber = 0 },
            6 => Git(temp.Path, "swigerb/copilot-ledger", "main") with { PullRequestNumber = -1 },
            _ => null,
        };
        LedgerEntry invalid = new(span, JsonRateCardCalculator.LoadEmbedded().Calculate(span), git);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => store.RecordAsync([seed[0], invalid]));

        Assert.Equal(message, exception.Message);
        Assert.Empty(await store.GetRollupAsync(LedgerGrouping.Repository, LedgerFilter.All));
    }

    [Fact]
    public async Task S14_Rollups_CountCacheCoverage()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await SeededStoreAsync(temp);

        Assert.Collection(
            await store.GetRollupAsync(LedgerGrouping.Model, LedgerFilter.All),
            row => AssertCoverage(row, "claude-opus-5.5", 1, 1, 1_200_000, 800_000, 300_000, 0.6666666666666666666666666667m),
            row => AssertCoverage(row, "mai-code-2", 1, 1, 1_000_000, 400_000, 600_000, 0.4m),
            row => AssertCoverage(row, "gpt-6.1-sol", 1, 0, 0, 0, 0, null),
            row => AssertCoverage(row, "qwen3.8-27b", 1, 0, 0, 0, 0, null),
            row => AssertCoverage(row, "gpt-unknown", 1, 0, 0, 0, 0, null));
        Assert.Collection(
            await store.GetRollupAsync(LedgerGrouping.Repository, LedgerFilter.All),
            row => AssertCoverage(row, "swigerb/copilot-ledger", 3, 2, 2_200_000, 1_200_000, 900_000, 0.5454545454545454545454545455m),
            row => AssertCoverage(row, "contoso/api", 1, 0, 0, 0, 0, null),
            row => AssertCoverage(row, "(none)", 1, 0, 0, 0, 0, null));
    }

    [Fact]
    public async Task S15_Filters_SelectPullRequestsAndAgents()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await InitializedStoreAsync(temp);
        JsonRateCardCalculator calculator = JsonRateCardCalculator.LoadEmbedded();
        GitWorkspaceContext feature =
            Git(temp.Path, "swigerb/copilot-ledger", "feature/ledger") with { PullRequestNumber = 42 };
        GitWorkspaceContext main = Git(temp.Path, "swigerb/copilot-ledger", "main");
        NormalizedGenAiSpan p1 = FilterSpan('7', "claude-opus-5.5", "ledger-builder");
        NormalizedGenAiSpan p2 = FilterSpan('8', "mai-code-2", null);
        NormalizedGenAiSpan p3 = FilterSpan('9', "mai-code-2", "ledger-builder");
        await store.RecordAsync(
        [
            new LedgerEntry(p1, calculator.Calculate(p1), feature),
            new LedgerEntry(p2, calculator.Calculate(p2), feature),
            new LedgerEntry(p3, calculator.Calculate(p3), main),
        ]);

        Assert.Equal(2, Assert.Single(await store.GetRollupAsync(
            LedgerGrouping.Repository,
            new LedgerFilter { PullRequestNumber = 42 })).SpanCount);
        Assert.Empty(await store.GetRollupAsync(
            LedgerGrouping.Repository,
            new LedgerFilter { PullRequestNumber = 7 }));
        Assert.Equal(2, Assert.Single(await store.GetRollupAsync(
            LedgerGrouping.Repository,
            new LedgerFilter { Agent = "ledger-builder" })).SpanCount);
        Assert.Equal(1, Assert.Single(await store.GetRollupAsync(
            LedgerGrouping.Repository,
            new LedgerFilter { Agent = "(none)" })).SpanCount);
        Assert.Equal(1, Assert.Single(await store.GetRollupAsync(
            LedgerGrouping.Repository,
            new LedgerFilter { PullRequestNumber = 42, Agent = "ledger-builder" })).SpanCount);
        Assert.Equal(
            [new string('7', 32), new string('8', 32)],
            (await store.GetTopPromptChainsAsync(new LedgerFilter { PullRequestNumber = 42 }, 10))
                .Select(row => row.TraceId));
        Assert.Equal(
            [new string('7', 32), new string('9', 32)],
            (await store.GetTopPromptChainsAsync(new LedgerFilter { Agent = "ledger-builder" }, 10))
                .Select(row => row.TraceId));
        Assert.Equal(2, await store.GetPromptCountAsync(new LedgerFilter { PullRequestNumber = 42 }));
        Assert.Equal(1, await store.GetPromptCountAsync(new LedgerFilter { Agent = "(none)" }));
    }

    [Fact]
    public async Task S16_RecordAsync_StoresOtherProvider()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await InitializedStoreAsync(temp);
        NormalizedGenAiSpan span = TestSpans.Chat(
            providerName: "openai",
            requestModel: "gpt-6.1-sol",
            inputTokens: 100_000,
            outputTokens: 10_000) with
        {
            BillingSource = BillingSource.Provider,
        };

        Assert.Equal(1, await store.RecordAsync(
            [new LedgerEntry(span, JsonRateCardCalculator.LoadEmbedded().Calculate(span), null)]));

        await using SqliteConnection connection = new($"Data Source={DatabasePath(temp)}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT billing_channel, pricing_status, cost_usd_pico, api_equivalent_status FROM spans;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("unpriced", reader.GetString(0));
        Assert.Equal("other_provider", reader.GetString(1));
        Assert.Equal(0L, reader.GetInt64(2));
        Assert.Equal("priced", reader.GetString(3));
        Assert.False(await reader.ReadAsync());
    }

    private static async Task<SqliteLedgerStore> SeededStoreAsync(TempDirectory temp)
    {
        SqliteLedgerStore store = await InitializedStoreAsync(temp);
        await store.RecordAsync(CreateSeedEntries(temp.Path));
        return store;
    }

    private static async Task<SqliteLedgerStore> InitializedStoreAsync(TempDirectory temp)
    {
        string databasePath = Path.Combine(temp.Path, "nested", "ledger.db");
        SqliteLedgerStore store = new(databasePath);
        await store.InitializeAsync();
        return store;
    }

    private static string DatabasePath(TempDirectory temp) =>
        Path.Combine(temp.Path, "nested", "ledger.db");

    private static IReadOnlyList<LedgerEntry> CreateSeedEntries(string root)
    {
        JsonRateCardCalculator calculator = JsonRateCardCalculator.LoadEmbedded();
        JsonRateCardCalculator byokCalculator =
            JsonRateCardCalculator.Parse(RateFixtures.ByokTestRatesJson);
        GitWorkspaceContext swigerbMain = Git(
            root,
            "swigerb/copilot-ledger",
            "main");
        GitWorkspaceContext swigerbFeature = Git(
            root,
            "swigerb/copilot-ledger",
            "feature/ledger");
        GitWorkspaceContext contosoMain = Git(root, "contoso/api", "main");

        // E1: claude-opus-5.5, V2 usage.
        NormalizedGenAiSpan e1Span = TestSpans.Chat(
            requestModel: "claude-opus-5.5",
            inputTokens: 1_200_000,
            outputTokens: 50_000,
            cacheReadInputTokens: 800_000,
            cacheWriteInputTokens: 100_000,
            agentName: "ledger-architect");
        LedgerEntry e1 = new(
            e1Span,
            calculator.Calculate(e1Span),
            swigerbMain);
        AssertCalculation(
            e1.Calculation,
            BillingChannel.GitHubAiCredits,
            PricingStatus.Priced,
            2.86m,
            286m,
            0m,
            "claude-opus-5.5");

        // E2: mai-code-2, V8 usage.
        NormalizedGenAiSpan e2Span = TestSpans.Chat(
            requestModel: "mai-code-2",
            inputTokens: 1_000_000,
            outputTokens: 100_000,
            cacheReadInputTokens: 400_000,
            agentName: "ledger-builder");
        LedgerEntry e2 = new(
            e2Span,
            calculator.Calculate(e2Span),
            swigerbFeature);
        AssertCalculation(
            e2.Calculation,
            BillingChannel.GitHubAiCredits,
            PricingStatus.Priced,
            0.56m,
            56m,
            0m,
            "mai-code-2");

        // E3: local Qwen at 127.0.0.1:11434.
        NormalizedGenAiSpan e3Span = TestSpans.Chat(
            requestModel: "qwen3.8-27b-cuda-gpu:1",
            serverAddress: "127.0.0.1",
            serverPort: 11434,
            inputTokens: 400_000,
            outputTokens: 12_000,
            agentName: "ledger-builder");
        LedgerEntry e3 = new(
            e3Span,
            calculator.Calculate(e3Span),
            swigerbFeature);
        AssertCalculation(
            e3.Calculation,
            BillingChannel.Local,
            PricingStatus.LocalOffload,
            0m,
            0m,
            0.23m,
            "qwen3.8-27b");

        // E4: Foundry BYOK gpt-6.1-sol.
        NormalizedGenAiSpan e4Span = TestSpans.Chat(
            requestModel: "gpt-6.1-sol",
            serverAddress: "contoso.openai.azure.com",
            inputTokens: 100_000,
            outputTokens: 10_000);
        LedgerEntry e4 = new(
            e4Span,
            byokCalculator.Calculate(e4Span),
            contosoMain);
        AssertCalculation(
            e4.Calculation,
            BillingChannel.FoundryByok,
            PricingStatus.Priced,
            0.315m,
            0m,
            0m,
            "gpt-6.1-sol");

        // E5: unknown model without Git context on the next day.
        NormalizedGenAiSpan e5Span = TestSpans.Chat(
            startTime: Cutover.AddHours(9),
            requestModel: "gpt-unknown",
            inputTokens: 1_000,
            outputTokens: 100,
            agentName: "ledger-builder");
        LedgerEntry e5 = new(
            e5Span,
            calculator.Calculate(e5Span),
            null);
        AssertCalculation(
            e5.Calculation,
            BillingChannel.GitHubAiCredits,
            PricingStatus.UnknownModel,
            0m,
            0m,
            0m,
            "gpt-unknown");

        // E6: duplicate of E1, including trace_id and span_id.
        return [e1, e2, e3, e4, e5, e1];
    }

    private static GitWorkspaceContext Git(
        string root,
        string slug,
        string branch)
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(root, slug));
        return new GitWorkspaceContext(
            repositoryRoot,
            Path.Combine(repositoryRoot, ".git"),
            branch,
            "0123456789abcdef0123456789abcdef01234567",
            $"https://github.com/{slug}.git",
            slug);
    }

    private static void AssertCalculation(
        CreditCalculationResult calculation,
        BillingChannel channel,
        PricingStatus status,
        decimal usdCost,
        decimal credits,
        decimal savedUsd,
        string model)
    {
        Assert.Equal(channel, calculation.Channel);
        Assert.Equal(status, calculation.Status);
        Assert.Equal(usdCost, calculation.UsdCost);
        Assert.Equal(credits, calculation.AiCredits);
        Assert.Equal(savedUsd, calculation.EstimatedCloudSavedUsd);
        Assert.Equal(model, calculation.ModelId);
    }

    private static void AssertRollup(
        LedgerRollupRow row,
        string repository,
        long spans,
        long unpriced,
        long input,
        long output,
        long cacheRead,
        long cacheWrite,
        decimal credits,
        decimal githubUsd,
        decimal byokUsd,
        decimal savedUsd)
    {
        Assert.Equal(repository, row.Repository);
        Assert.Equal(spans, row.SpanCount);
        Assert.Equal(unpriced, row.UnpricedSpanCount);
        Assert.Equal(input, row.InputTokens);
        Assert.Equal(output, row.OutputTokens);
        Assert.Equal(cacheRead, row.CacheReadInputTokens);
        Assert.Equal(cacheWrite, row.CacheWriteInputTokens);
        Assert.Equal(credits, row.AiCredits);
        Assert.Equal(githubUsd, row.AiCreditsUsd);
        Assert.Equal(byokUsd, row.FoundryByokUsd);
        Assert.Equal(savedUsd, row.LocalSavedUsd);
    }

    private static void AssertBranch(
        LedgerRollupRow row,
        string repository,
        string branch,
        long spans,
        decimal credits,
        decimal githubUsd,
        decimal byokUsd,
        decimal savedUsd)
    {
        Assert.Equal(repository, row.Repository);
        Assert.Equal(branch, row.Branch);
        Assert.Equal(spans, row.SpanCount);
        Assert.Equal(credits, row.AiCredits);
        Assert.Equal(githubUsd, row.AiCreditsUsd);
        Assert.Equal(byokUsd, row.FoundryByokUsd);
        Assert.Equal(savedUsd, row.LocalSavedUsd);
    }

    private static void AssertModel(
        LedgerRollupRow row,
        string model,
        long spans,
        long unpriced,
        decimal credits,
        decimal githubUsd,
        decimal byokUsd,
        decimal savedUsd)
    {
        Assert.Equal(model, row.Model);
        Assert.Equal(spans, row.SpanCount);
        Assert.Equal(unpriced, row.UnpricedSpanCount);
        Assert.Equal(credits, row.AiCredits);
        Assert.Equal(githubUsd, row.AiCreditsUsd);
        Assert.Equal(byokUsd, row.FoundryByokUsd);
        Assert.Equal(savedUsd, row.LocalSavedUsd);
    }

    private static void AssertAgent(
        LedgerRollupRow row,
        string agent,
        long spans,
        long unpriced,
        long input,
        long output,
        decimal credits,
        decimal githubUsd,
        decimal byokUsd,
        decimal savedUsd)
    {
        Assert.Equal(agent, row.Agent);
        Assert.Equal(spans, row.SpanCount);
        Assert.Equal(unpriced, row.UnpricedSpanCount);
        Assert.Equal(input, row.InputTokens);
        Assert.Equal(output, row.OutputTokens);
        Assert.Equal(credits, row.AiCredits);
        Assert.Equal(githubUsd, row.AiCreditsUsd);
        Assert.Equal(byokUsd, row.FoundryByokUsd);
        Assert.Equal(savedUsd, row.LocalSavedUsd);
    }

    // key is the row's Model, or its Repository for a Repository rollup.
    private static void AssertCoverage(
        LedgerRollupRow row,
        string key,
        long spans,
        long reported,
        long reportedInput,
        long reportedCacheRead,
        long reportedUncached,
        decimal? hitRatio)
    {
        Assert.Equal(key, row.Model ?? row.Repository);
        Assert.Equal(spans, row.SpanCount);
        Assert.Equal(reported, row.CacheReportedSpanCount);
        Assert.Equal(spans - reported, row.CacheUnreportedSpanCount);
        Assert.Equal(reportedInput, row.ReportedInputTokens);
        Assert.Equal(reportedCacheRead, row.ReportedCacheReadInputTokens);
        Assert.Equal(reportedUncached, row.ReportedUncachedInputTokens);
        Assert.Equal(hitRatio, row.CacheHitRatio);
    }

    private static NormalizedGenAiSpan FilterSpan(char trace, string model, string? agent) =>
        TestSpans.Chat(
            startTime: Cutover.AddHours(-3),
            requestModel: model,
            agentName: agent,
            inputTokens: 10_000,
            outputTokens: 1_000) with
        {
            TraceId = new string(trace, 32),
            SpanId = new string(trace, 16),
        };
}
