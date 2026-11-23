using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Otlp.Attribution;
using CopilotLedger.Otlp.Decoding;
using CopilotLedger.Otlp.Ingestion;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using static CopilotLedger.Tests.TestSupport.OtlpRequests;

namespace CopilotLedger.Tests.Otlp;

public sealed class LedgerIngestorTests
{
    [Fact]
    public async Task IG1_CopilotJsonTrace_RecordsOnce()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(temp);
        OtlpTraceRequest request = OtlpTraceDecoder.DecodeJson(
            OtlpFixtures.Utf8(OtlpFixtures.CopilotChatTraceJson));

        IngestResult first = await harness.Ingestor.IngestAsync(request);
        AssertResult(first, 2, 0, null, 1, 1);
        LedgerRollupRow model = Assert.Single(await harness.Store.GetRollupAsync(
            LedgerGrouping.Model,
            LedgerFilter.All));
        AssertRollup(model, "mai-code-2", 1, 0.69071m, 0.0069071m);
        Assert.Equal(17_194, model.InputTokens);
        Assert.Equal(3_584, model.CacheReadInputTokens);
        Assert.Equal(5, model.OutputTokens);
        LedgerRollupRow agent = Assert.Single(await harness.Store.GetRollupAsync(
            LedgerGrouping.Agent,
            LedgerFilter.All));
        Assert.Equal("(none)", agent.Agent);

        IngestResult duplicate = await harness.Ingestor.IngestAsync(request);
        AssertResult(duplicate, 2, 0, null, 1, 0);
    }

    [Fact]
    public async Task IG2_SubagentTrace_BackfillsAcrossBatches()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(temp);

        IngestResult batchA = await harness.Ingestor.IngestAsync(CopilotTraces.TraceBA());
        AssertResult(batchA, 6, 0, null, 3, 3);
        Assert.Collection(
            await harness.Store.GetRollupAsync(LedgerGrouping.Agent, LedgerFilter.All),
            row =>
            {
                Assert.Equal("(none)", row.Agent);
                Assert.Equal(1, row.SpanCount);
                Assert.Equal(0.13053m, row.AiCredits);
            },
            row =>
            {
                Assert.Equal("explore", row.Agent);
                Assert.Equal(2, row.SpanCount);
                Assert.Equal(0.0531345m, row.AiCredits);
            });

        IngestResult batchB = await harness.Ingestor.IngestAsync(CopilotTraces.TraceBB());
        AssertResult(batchB, 2, 0, null, 1, 1);
        Assert.Collection(
            await harness.Store.GetRollupAsync(LedgerGrouping.Agent, LedgerFilter.All),
            row =>
            {
                Assert.Equal("(none)", row.Agent);
                Assert.Equal(2, row.SpanCount);
                Assert.Equal(34_527, row.InputTokens);
                Assert.Equal(104, row.OutputTokens);
                Assert.Equal(19_456, row.CacheReadInputTokens);
                Assert.Equal(0, row.CacheWriteInputTokens);
                Assert.Equal(0.82819m, row.AiCredits);
                Assert.Equal(0.0082819m, row.AiCreditsUsd);
            },
            row =>
            {
                Assert.Equal("explore", row.Agent);
                Assert.Equal(2, row.SpanCount);
                Assert.Equal(7_429, row.InputTokens);
                Assert.Equal(48, row.OutputTokens);
                Assert.Equal(3_662, row.CacheReadInputTokens);
                Assert.Equal(3_761, row.CacheWriteInputTokens);
                Assert.Equal(0.0531345m, row.AiCredits);
                Assert.Equal(0.000531345m, row.AiCreditsUsd);
            });
        Assert.Collection(
            await harness.Store.GetRollupAsync(LedgerGrouping.Model, LedgerFilter.All),
            row =>
            {
                Assert.Equal("mai-code-2", row.Model);
                Assert.Equal(0.82819m, row.AiCredits);
            },
            row =>
            {
                Assert.Equal("gpt-6-luna", row.Model);
                Assert.Equal(0.0531345m, row.AiCredits);
            });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IG3_SessionOrResourceWorkingDirectory_ResolvesGit(bool session)
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(temp);
        OtlpTraceRequest request;
        if (session)
        {
            string workspace = Path.Combine(harness.Home, "session-state",
                "d9b2d63d-a233-4123-847a-5a1f3c8e6e1f", "workspace.yaml");
            Directory.CreateDirectory(Path.GetDirectoryName(workspace)!);
            File.WriteAllText(workspace, $"cwd: {Path.Combine(harness.Repo, "src")}\nbranch: stale-branch\n");
            request = CopilotTraces.TraceE();
        }
        else
        {
            OtlpTraceRequest original = CopilotTraces.TraceE();
            OtlpSpan span = original.ResourceSpans[0].Spans[0] with
            {
                Attributes = original.ResourceSpans[0].Spans[0].Attributes
                    .Where(attribute => attribute.Key != "gen_ai.conversation.id")
                    .ToArray(),
            };
            request = Request(
                [.. CopilotResource, Str("process.working_directory", harness.Repo)],
                span);
        }

        await harness.Ingestor.IngestAsync(request);
        LedgerRollupRow row = Assert.Single(await harness.Store.GetRollupAsync(
            LedgerGrouping.Branch,
            LedgerFilter.All));
        Assert.Equal("swigerb/copilot-ledger", row.Repository);
        Assert.Equal("feature/ledger", row.Branch);
        Assert.Equal(1, row.SpanCount);
        Assert.Equal(286m, row.AiCredits);
        Assert.Equal(2.86m, row.AiCreditsUsd);
    }

    [Fact]
    public async Task IG4_LateVsCodeGitAttributes_BackfillAndScrubCredentials()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(temp);
        AssertResult(
            await harness.Ingestor.IngestAsync(CopilotTraces.TraceD1()),
            1,
            0,
            null,
            1,
            1);
        LedgerRollupRow before = Assert.Single(await harness.Store.GetRollupAsync(
            LedgerGrouping.Branch,
            LedgerFilter.All));
        Assert.Equal("(none)", before.Repository);
        Assert.Equal("(none)", before.Branch);

        AssertResult(
            await harness.Ingestor.IngestAsync(CopilotTraces.TraceD2()),
            1,
            0,
            null,
            0,
            0);
        LedgerRollupRow after = Assert.Single(await harness.Store.GetRollupAsync(
            LedgerGrouping.Branch,
            LedgerFilter.All));
        Assert.Equal("contoso/webapp", after.Repository);
        Assert.Equal("feature/login", after.Branch);
        Assert.Equal(60m, after.AiCredits);
        Assert.Equal("GitHub Copilot Chat", Assert.Single(
            await harness.Store.GetRollupAsync(LedgerGrouping.Agent, LedgerFilter.All)).Agent);
        await AssertNoCredentialAsync(harness.DatabasePath);
    }

    [Fact]
    public async Task IG5_NormalizerRejection_DoesNotDropValidSpan()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(temp);
        OtlpSpan valid = IgSpan("0000000000000001",
            Str("gen_ai.request.model", "mai-code-2"),
            Int("gen_ai.usage.input_tokens", 1000),
            Int("gen_ai.usage.output_tokens", 100));
        OtlpSpan invalid = valid with
        {
            SpanId = "0000000000000002",
            Attributes =
            [
                .. valid.Attributes.Where(
                    attribute => attribute.Key != "gen_ai.usage.output_tokens"),
                Int("gen_ai.usage.output_tokens", -1),
            ],
        };

        IngestResult result = await harness.Ingestor.IngestAsync(
            Request(CopilotResource, valid, invalid));
        AssertResult(
            result,
            2,
            1,
            "gen_ai.usage.output_tokens is not a non-negative integer",
            1,
            1);
    }

    [Theory]
    [MemberData(nameof(Ig6Cases))]
    public async Task IG6_CostAndNormalizerRejections_ArePerSpan(
        int scenario,
        int received,
        int rejected,
        string? reason,
        int inference,
        int recorded,
        int links)
    {
        using TempDirectory temp = new();
        string overlay = Path.Combine(temp.Path, "ig6-rates.json");
        File.WriteAllText(
            overlay,
            """
            {
              "schemaVersion": 1,
              "ratesVersion": "ig6",
              "githubAiCredits": {
                "models": [
                  { "id": "ig6-huge", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1000000, "outputPer1M": 1000000 } ] } ] }
                ]
              }
            }
            """);
        Harness harness = await Harness.CreateAsync(
            temp,
            JsonRateCardCalculator.LoadEmbeddedWithOverlay(overlay));
        OtlpSpan h = IgSpan("0000000000000001",
            Str("gen_ai.request.model", "ig6-huge"),
            Int("gen_ai.usage.input_tokens", scenario == 3 ? 9_000_000 : 10_000_000));
        OtlpSpan m = IgSpan("0000000000000002",
            Str("gen_ai.request.model", "mai-code-2"),
            Int("gen_ai.usage.input_tokens", 1000),
            Int("gen_ai.usage.output_tokens", 100));
        OtlpSpan n = IgSpan("0000000000000004",
            Str("gen_ai.request.model", "mai-code-2"),
            Int("gen_ai.usage.output_tokens", -1));
        OtlpSpan[] spans = scenario switch
        {
            1 => [h, m],
            2 => [h, m, n],
            3 => [h, m],
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };

        IngestResult result = await harness.Ingestor.IngestAsync(
            Request(CopilotResource, spans));
        AssertResult(result, received, rejected, reason, inference, recorded);
        IReadOnlyList<LedgerRollupRow> models = await harness.Store.GetRollupAsync(
            LedgerGrouping.Model,
            LedgerFilter.All);
        if (scenario == 3)
        {
            Assert.Collection(
                models,
                row => AssertRollup(row, "ig6-huge", 1, 900_000_000m, 9_000_000m),
                row => AssertRollup(row, "mai-code-2", 1, 0.075m, 0.00075m));
        }
        else
        {
            LedgerRollupRow only = Assert.Single(models);
            AssertRollup(only, "mai-code-2", 1, 0.075m, 0.00075m);
        }

        await using SqliteConnection connection = new($"Data Source={harness.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM span_links;";
        Assert.Equal(links, Convert.ToInt32(await command.ExecuteScalarAsync()));
    }

    public static TheoryData<int, int, int, string?, int, int, int> Ig6Cases => new()
    {
        { 1, 2, 1, "cost out of range", 1, 1, 2 },
        { 2, 3, 2, "gen_ai.usage.output_tokens is not a non-negative integer", 1, 1, 2 },
        { 3, 2, 0, null, 2, 2, 2 },
    };

    [Fact]
    public async Task IG7_GitResolutionIOException_RecordsWithoutGit()
    {
        using TempDirectory temp = new();
        ThrowingGitResolver git = new();
        Harness harness = await Harness.CreateAsync(
            temp,
            gitResolver: git);
        OtlpSpan span = IgSpan("0000000000000002",
            Str("gen_ai.request.model", "mai-code-2"),
            Int("gen_ai.usage.input_tokens", 1000),
            Int("gen_ai.usage.output_tokens", 100));

        IngestResult result = await harness.Ingestor.IngestAsync(Request(
            [.. CopilotResource, Str("process.working_directory", temp.Path)],
            span));
        AssertResult(result, 1, 0, null, 1, 1);
        LedgerRollupRow row = Assert.Single(await harness.Store.GetRollupAsync(
            LedgerGrouping.Branch,
            LedgerFilter.All));
        Assert.Equal("(none)", row.Repository);
        Assert.Equal("(none)", row.Branch);
        Assert.Equal(1, row.SpanCount);
        Assert.Equal(0.075m, row.AiCredits);
        Assert.Equal(temp.Path, Assert.Single(git.Calls));
    }

    [Fact]
    public async Task IG8_OverflowingCalculator_RejectsOnlyItsSpan()
    {
        using TempDirectory temp = new();
        OverflowingCalculator calculator = new();
        Harness harness = await Harness.CreateAsync(temp, calculator);
        OtlpSpan overflowing = IgSpan("0000000000000003",
            Str("gen_ai.request.model", "overflow-model"),
            Int("gen_ai.usage.input_tokens", 1));
        OtlpSpan valid = IgSpan("0000000000000002",
            Str("gen_ai.request.model", "mai-code-2"),
            Int("gen_ai.usage.input_tokens", 1000),
            Int("gen_ai.usage.output_tokens", 100));

        IngestResult result = await harness.Ingestor.IngestAsync(
            Request(CopilotResource, overflowing, valid));
        AssertResult(result, 2, 1, "cost out of range", 1, 1);
        AssertRollup(
            Assert.Single(await harness.Store.GetRollupAsync(
                LedgerGrouping.Model,
                LedgerFilter.All)),
            "mai-code-2",
            1,
            0.075m,
            0.00075m);
        await using SqliteConnection connection = new($"Data Source={harness.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM span_links;";
        Assert.Equal(2L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task IG9_CustomAgents_ReconcileBothLiveRuns()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(temp);

        AssertResult(
            await harness.Ingestor.IngestAsync(CopilotTraces.TraceF()),
            2,
            0,
            null,
            1,
            1);
        AssertResult(
            await harness.Ingestor.IngestAsync(CopilotTraces.TraceG()),
            6,
            0,
            null,
            3,
            3);
        Assert.Collection(
            await harness.Store.GetRollupAsync(LedgerGrouping.Agent, LedgerFilter.All),
            row =>
            {
                Assert.Equal("(none)", row.Agent);
                Assert.Equal(2, row.SpanCount);
                Assert.Equal(11_202, row.InputTokens);
                Assert.Equal(47, row.OutputTokens);
                Assert.Equal(0.057185m, row.AiCredits);
                Assert.Equal(0.00057185m, row.AiCreditsUsd);
            },
            row =>
            {
                Assert.Equal("Ledger Probe Display", row.Agent);
                Assert.Equal(2, row.SpanCount);
                Assert.Equal(5_688, row.InputTokens);
                Assert.Equal(22, row.OutputTokens);
                Assert.Equal(0.02899m, row.AiCredits);
                Assert.Equal(0.0002899m, row.AiCreditsUsd);
            });
        LedgerRollupRow model = Assert.Single(await harness.Store.GetRollupAsync(
            LedgerGrouping.Model,
            LedgerFilter.All));
        AssertRollup(model, "mai-code-2-flash", 4, 0.086175m, 0.00086175m);
    }

    [Fact]
    public async Task IG10_PullRequestCheckout_RecordsThePullRequestAndCacheStates()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(temp);
        string repo = GitFixture.Create(Path.Combine(temp.Path, "pr"), pullRequest: 42);

        AssertResult(
            await harness.Ingestor.IngestAsync(
                CopilotTraces.TraceE([Str("process.working_directory", repo)])),
            1,
            0,
            null,
            1,
            1);
        AssertResult(
            await harness.Ingestor.IngestAsync(CopilotTraces.TraceF()),
            2,
            0,
            null,
            1,
            1);

        await using SqliteConnection connection = new($"Data Source={harness.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT span_id, branch_name, pull_request_number, cache_read_state, cache_write_state, agent_span_id FROM spans ORDER BY span_id;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("0f00000000000002", reader.GetString(0));
        Assert.True(reader.IsDBNull(1));
        Assert.True(reader.IsDBNull(2));
        Assert.Equal("absent", reader.GetString(3));
        Assert.Equal("absent", reader.GetString(4));
        Assert.Equal("0f00000000000001", reader.GetString(5));
        Assert.True(await reader.ReadAsync());
        Assert.Equal("e4c5e6a7b8c9d0e1", reader.GetString(0));
        Assert.Equal("feature/ledger", reader.GetString(1));
        Assert.Equal(42L, reader.GetInt64(2));
        Assert.Equal("reported", reader.GetString(3));
        Assert.Equal("reported", reader.GetString(4));
        Assert.True(reader.IsDBNull(5));
        Assert.False(await reader.ReadAsync());
    }

    private static OtlpSpan IgSpan(string spanId, params OtlpAttribute[] attributes) =>
        Span(
            spanId: spanId,
            attributes: [Str("gen_ai.operation.name", "chat"), .. attributes]);

    private static void AssertResult(
        IngestResult result,
        int received,
        int rejected,
        string? reason,
        int inference,
        int recorded)
    {
        Assert.Equal(received, result.ReceivedSpans);
        Assert.Equal(rejected, result.RejectedSpans);
        Assert.Equal(reason, result.FirstRejectionReason);
        Assert.Equal(inference, result.InferenceSpans);
        Assert.Equal(recorded, result.RecordedSpans);
    }

    private static void AssertRollup(
        LedgerRollupRow row,
        string model,
        long spans,
        decimal credits,
        decimal usd)
    {
        Assert.Equal(model, row.Model);
        Assert.Equal(spans, row.SpanCount);
        Assert.Equal(credits, row.AiCredits);
        Assert.Equal(usd, row.AiCreditsUsd);
    }

    private static async Task AssertNoCredentialAsync(string databasePath)
    {
        await using SqliteConnection connection = new($"Data Source={databasePath}");
        await connection.OpenAsync();
        foreach (string table in new[] { "spans", "span_links" })
        {
            await using SqliteCommand info = connection.CreateCommand();
            info.CommandText = $"PRAGMA table_info({table});";
            List<string> columns = [];
            await using (SqliteDataReader reader = await info.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    columns.Add(reader.GetString(1));
                }
            }

            await using SqliteCommand read = connection.CreateCommand();
            read.CommandText = $"SELECT {string.Join(", ", columns)} FROM {table};";
            await using SqliteDataReader values = await read.ExecuteReaderAsync();
            while (await values.ReadAsync())
            {
                for (int index = 0; index < values.FieldCount; index++)
                {
                    if (!values.IsDBNull(index))
                    {
                        Assert.DoesNotContain("s3cr3t", Convert.ToString(values[index])!);
                    }
                }
            }
        }
    }

    private sealed class Harness
    {
        private Harness(
            string databasePath,
            SqliteLedgerStore store,
            LedgerIngestor ingestor,
            string home,
            string repo)
        {
            DatabasePath = databasePath;
            Store = store;
            Ingestor = ingestor;
            Home = home;
            Repo = repo;
        }

        public string DatabasePath { get; }
        public SqliteLedgerStore Store { get; }
        public LedgerIngestor Ingestor { get; }
        public string Home { get; }
        public string Repo { get; }

        public static async Task<Harness> CreateAsync(
            TempDirectory temp,
            ICreditCalculator? calculator = null,
            IGitContextResolver? gitResolver = null)
        {
            string databasePath = Path.Combine(temp.Path, "data", "ledger.db");
            SqliteLedgerStore store = new(databasePath);
            await store.InitializeAsync();
            string repo = CreateGitFixture(temp.Path);
            string home = Path.Combine(temp.Path, "copilot-home");
            IGitContextResolver resolver = gitResolver
                ?? new CachingGitContextResolver(
                    new FileSystemGitContextResolver(ceilingDirectory: temp.Path));
            LedgerIngestor ingestor = new(
                store,
                calculator ?? JsonRateCardCalculator.LoadEmbedded(),
                resolver,
                new CopilotSessionWorkspaceLocator(home));
            return new Harness(databasePath, store, ingestor, home, repo);
        }

        private static string CreateGitFixture(string root)
        {
            string repo = Path.Combine(root, "repo");
            string git = Path.Combine(repo, ".git");
            Directory.CreateDirectory(Path.Combine(git, "refs", "heads", "feature"));
            Directory.CreateDirectory(Path.Combine(repo, "src"));
            File.WriteAllText(Path.Combine(git, "HEAD"), "ref: refs/heads/feature/ledger\n");
            File.WriteAllText(
                Path.Combine(git, "refs", "heads", "feature", "ledger"),
                "0123456789abcdef0123456789abcdef01234567\n");
            File.WriteAllText(
                Path.Combine(git, "config"),
                "[remote \"origin\"]\n\turl = https://github.com/swigerb/copilot-ledger.git\n");
            return repo;
        }
    }

    private sealed class ThrowingGitResolver : IGitContextResolver
    {
        public List<string> Calls { get; } = [];

        public GitWorkspaceContext? Resolve(string workingDirectory)
        {
            Calls.Add(workingDirectory);
            throw new IOException("unavailable");
        }
    }

    private sealed class OverflowingCalculator : ICreditCalculator
    {
        private readonly JsonRateCardCalculator _inner =
            JsonRateCardCalculator.LoadEmbedded();

        public string RatesVersion => _inner.RatesVersion;

        public CreditCalculationResult Calculate(NormalizedGenAiSpan span) =>
            span.RequestModel == "overflow-model"
                ? throw new OverflowException()
                : _inner.Calculate(span);
    }
}
