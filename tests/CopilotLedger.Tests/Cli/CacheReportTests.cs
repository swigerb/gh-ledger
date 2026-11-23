using System.Globalization;
using System.Text.Json;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Otlp.Attribution;
using CopilotLedger.Otlp.Decoding;
using CopilotLedger.Otlp.Ingestion;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using static CopilotLedger.Tests.TestSupport.OtlpRequests;

namespace CopilotLedger.Tests.Cli;

public sealed class CacheReportTests
{
    private static ManualTimeProvider Time => new(
        DateTimeOffset.Parse("2026-11-21T15:00:00Z", CultureInfo.InvariantCulture),
        ManualTimeProvider.UtcMinus5);

    public static TheoryData<string, string> Groupings => new()
    {
        {
            "status --cache",
            """
            Cache efficiency by model in all repositories (last 30 days)

            Model            Spans  Cache reported      Input  Cache read  Cache write  Uncached   Output  Hit %
            claude-opus-5.5      1               1  1,200,000     800,000      100,000   300,000   50,000  66.7%
            mai-code-2           1               1  1,000,000     400,000            0   600,000  100,000  40.0%
            qwen3.8-27b          1               0    400,000         n/a          n/a       n/a   12,000    n/a
            gpt-6.1-sol          1               0    100,000         n/a          n/a       n/a   10,000    n/a

            2 of 4 spans reported no cache counters. Hit % and the cache columns leave them out; * marks a partial total.

            """
        },
        {
            "status --cache --by branch",
            """
            Cache efficiency by branch in all repositories (last 30 days)

            Repository              Branch          Spans  Cache reported      Input  Cache read  Cache write  Uncached   Output  Hit %
            swigerb/copilot-ledger  feature/ledger      2               1  1,400,000    400,000*           0*  600,000*  112,000  40.0%
            swigerb/copilot-ledger  main                1               1  1,200,000     800,000      100,000   300,000   50,000  66.7%
            contoso/api             main                1               0    100,000         n/a          n/a       n/a   10,000    n/a

            2 of 4 spans reported no cache counters. Hit % and the cache columns leave them out; * marks a partial total.

            """
        },
        {
            "status --cache --by pr --since all",
            """
            Cache efficiency by pull request in all repositories (all time)

            Repository              Pull request            Spans  Cache reported      Input  Cache read  Cache write  Uncached   Output  Hit %
            swigerb/copilot-ledger  (no PR) feature/ledger      2               1  1,400,000    400,000*           0*  600,000*  112,000  40.0%
            swigerb/copilot-ledger  (no PR) main                1               1  1,200,000     800,000      100,000   300,000   50,000  66.7%
            contoso/api             (no PR) main                1               0    100,000         n/a          n/a       n/a   10,000    n/a
            (none)                  (no PR) (none)              1               0      1,000         n/a          n/a       n/a      100    n/a

            3 of 5 spans reported no cache counters. Hit % and the cache columns leave them out; * marks a partial total.

            """
        },
        {
            "status --cache --by agent --repo swigerb/copilot-ledger",
            """
            Cache efficiency by agent in swigerb/copilot-ledger (last 30 days)

            Agent             Spans  Cache reported      Input  Cache read  Cache write  Uncached   Output  Hit %
            ledger-builder        2               1  1,400,000    400,000*           0*  600,000*  112,000  40.0%
            ledger-architect      1               1  1,200,000     800,000      100,000   300,000   50,000  66.7%

            1 of 3 spans reported no cache counters. Hit % and the cache columns leave them out; * marks a partial total.

            """
        },
        {
            "status --cache --by pr --repo swigerb/copilot-ledger --branch main",
            """
            Cache efficiency by pull request in swigerb/copilot-ledger, branch main (last 30 days)

            Pull request  Spans  Cache reported      Input  Cache read  Cache write  Uncached  Output  Hit %
            (no PR) main      1               1  1,200,000     800,000      100,000   300,000  50,000  66.7%

            """
        },
    };

    [Theory]
    [MemberData(nameof(Groupings))]
    public async Task CR1_CacheTablesGroupByModelBranchPullRequestAndAgent(string command, string expected)
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);

        CliRun run = await harness.RunAsync(command.Split(' '));

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(string.Empty, run.Error);
        Assert.Equal(expected.ReplaceLineEndings("\n"), run.Out);
    }

    [Fact]
    public async Task CR2_CacheJsonHasExactRowsAndNullsForUnreportedGroups()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);

        CliRun run = await harness.RunAsync("status", "--cache", "--json");
        CliRun pullRequests = await harness.RunAsync("status", "--cache", "--by", "pr", "--pr", "42", "--json");

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("\"cacheHitRatio\": 0.6666666666666666666666666667\n", run.Out);
        using JsonDocument json = JsonDocument.Parse(run.Out);
        JsonElement root = json.RootElement;
        Assert.Equal(
            ["period", "from", "by", "repository", "branch", "pullRequest", "rows"],
            root.EnumerateObject().Select(property => property.Name));
        Assert.Equal("last 30 days", root.GetProperty("period").GetString());
        Assert.Equal("model", root.GetProperty("by").GetString());
        JsonElement[] rows = root.GetProperty("rows").EnumerateArray().ToArray();
        Assert.Equal(
            ["model", "spans", "cacheReportedSpans", "cacheUnreportedSpans", "inputTokens", "outputTokens", "maxInputTokens", "averageInputTokensPerTurn", "reportedInputTokens", "reportedCacheReadInputTokens", "reportedCacheWriteInputTokens", "uncachedInputTokens", "estimatedUncachedInputTokens", "cacheHitRatio"],
            rows[0].EnumerateObject().Select(property => property.Name));
        Assert.Equal(["claude-opus-5.5", "mai-code-2", "qwen3.8-27b", "gpt-6.1-sol"], rows.Select(row => row.GetProperty("model").GetString()));
        Assert.Equal(1_200_000, rows[0].GetProperty("maxInputTokens").GetInt64());
        Assert.Equal(1_200_000m, rows[0].GetProperty("averageInputTokensPerTurn").GetDecimal());
        Assert.Equal(800_000, rows[0].GetProperty("reportedCacheReadInputTokens").GetInt64());
        Assert.Equal(100_000, rows[0].GetProperty("reportedCacheWriteInputTokens").GetInt64());
        Assert.Equal(300_000, rows[0].GetProperty("uncachedInputTokens").GetInt64());
        Assert.Equal(0.6666666666666666666666666667m, rows[0].GetProperty("cacheHitRatio").GetDecimal());
        Assert.Equal(0.4m, rows[1].GetProperty("cacheHitRatio").GetDecimal());
        foreach (JsonElement unreported in rows[2..])
        {
            Assert.Equal(0, unreported.GetProperty("cacheReportedSpans").GetInt64());
            Assert.Equal(1, unreported.GetProperty("cacheUnreportedSpans").GetInt64());
            Assert.Equal(JsonValueKind.Null, unreported.GetProperty("uncachedInputTokens").ValueKind);
            Assert.Equal(JsonValueKind.Null, unreported.GetProperty("cacheHitRatio").ValueKind);
        }

        Assert.Equal(400_000, rows[2].GetProperty("estimatedUncachedInputTokens").GetInt64());
        Assert.Equal(0, pullRequests.ExitCode);
        using JsonDocument pullRequestJson = JsonDocument.Parse(pullRequests.Out);
        Assert.Equal(42, pullRequestJson.RootElement.GetProperty("pullRequest").GetInt32());
        Assert.Empty(pullRequestJson.RootElement.GetProperty("rows").EnumerateArray());
    }

    [Fact]
    public async Task CR3_PullRequestCheckout_GroupsSpansByItsNumber()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path, new FakeCliPlatform(), Time);
        string pullRequestRepo = GitFixture.Create(Path.Combine(temp.Path, "pr"), pullRequest: 42);
        string plainRepo = GitFixture.Create(Path.Combine(temp.Path, "plain"));
        SqliteLedgerStore store = new(harness.Context.DatabasePath, harness.TimeProvider);
        await store.InitializeAsync();
        LedgerIngestor ingestor = new(
            store,
            JsonRateCardCalculator.LoadEmbedded(),
            new FileSystemGitContextResolver(ceilingDirectory: temp.Path),
            new CopilotSessionWorkspaceLocator(Path.Combine(temp.Path, "copilot-home")));
        await ingestor.IngestAsync(ChatIn(pullRequestRepo, "c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1", 10_000, 8_000));
        await ingestor.IngestAsync(ChatIn(pullRequestRepo, "c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2", 20_000, 0));
        await ingestor.IngestAsync(ChatIn(plainRepo, "c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3", 5_000, 1_000));

        CliRun run = await harness.RunAsync("status", "--cache", "--by", "pr");
        CliRun filtered = await harness.RunAsync("status", "--cache", "--pr", "42", "--json");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            """
            Cache efficiency by pull request in all repositories (last 30 days)

            Repository              Pull request            Spans  Cache reported   Input  Cache read  Cache write  Uncached  Output  Hit %
            swigerb/copilot-ledger  #42                         2               2  30,000       8,000            0    22,000      20  26.7%
            swigerb/copilot-ledger  (no PR) feature/ledger      1               1   5,000       1,000            0     4,000      10  20.0%

            """.ReplaceLineEndings("\n"),
            run.Out);
        Assert.Equal(0, filtered.ExitCode);
        using JsonDocument json = JsonDocument.Parse(filtered.Out);
        JsonElement row = Assert.Single(json.RootElement.GetProperty("rows").EnumerateArray());
        Assert.Equal("mai-code-2", row.GetProperty("model").GetString());
        Assert.Equal(2, row.GetProperty("spans").GetInt64());
        Assert.Equal(0.2666666666666666666666666667m, row.GetProperty("cacheHitRatio").GetDecimal());
    }

    public static TheoryData<string, string> InvalidOptions => new()
    {
        { "status --by model", "--by requires --cache." },
        { "status --json --repo a/b", "--repo requires --cache." },
        { "status --branch main", "--branch requires --cache." },
        { "status --pr 4", "--pr requires --cache." },
        { "status --since 7d", "--since requires --cache." },
        { "status --cache --by foo", "--by must be model, branch, pr, or agent." },
        { "status --cache --pr 0", "--pr must be a positive integer." },
        { "status --cache --since yesterday", "Invalid --since value yesterday. Use today, all, <N>d (1-3650), or yyyy-MM-dd." },
    };

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public async Task CR4_BreakdownOptionsAreValidated(string command, string message)
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path, new FakeCliPlatform(), Time);

        CliRun run = await harness.RunAsync(command.Split(' '));

        Assert.Equal(1, run.ExitCode);
        Assert.Contains(message, run.Error);
    }

    [Fact]
    public async Task CR5_EmptyLedger_ReportsNoSpans()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path, new FakeCliPlatform(), Time);

        CliRun text = await harness.RunAsync("status", "--cache", "--repo", "contoso/webapp");
        CliRun json = await harness.RunAsync("status", "--cache", "--json");

        Assert.Equal(0, text.ExitCode);
        Assert.Equal("No spans recorded in contoso/webapp (last 30 days).\n", text.Out);
        Assert.Equal(0, json.ExitCode);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        Assert.Empty(document.RootElement.GetProperty("rows").EnumerateArray());
    }

    [Fact]
    public async Task CR6_OutputIsTheSameUnderGermanCulture()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        string[][] commands =
        [
            ["status"],
            ["status", "--json"],
            ["status", "--cache"],
            ["status", "--cache", "--json"],
            ["branch", "--all-repos", "--json"],
        ];
        List<string> invariant = [];
        List<string> german = [];
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            foreach (string[] command in commands)
            {
                invariant.Add((await harness.RunAsync(command)).Out);
            }

            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            CultureInfo.CurrentUICulture = new CultureInfo("de-DE");
            foreach (string[] command in commands)
            {
                german.Add((await harness.RunAsync(command)).Out);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }

        Assert.Equal(invariant, german);
        Assert.Contains("  66.7%\n", german[2]);
        Assert.Contains("1,200,000", german[2]);
        Assert.Contains("\"cacheHitRatio\": 0.6666666666666666666666666667", german[3]);
        Assert.Contains("54.5%", german[0]);
    }

    private static async Task<CliHarness> SeededHarnessAsync(string root)
    {
        string git = Path.Combine(root, "repo", ".git");
        Directory.CreateDirectory(git);
        File.WriteAllText(Path.Combine(git, "HEAD"), "ref: refs/heads/main\n");
        File.WriteAllText(
            Path.Combine(git, "config"),
            "[remote \"origin\"]\n\turl = https://github.com/swigerb/copilot-ledger.git\n");
        CliHarness harness = new(root, new FakeCliPlatform(), Time);
        SqliteLedgerStore store = new(
            Path.Combine(root, "data", "ledger.db"),
            harness.TimeProvider);
        await store.InitializeAsync();
        await LedgerSeeds.SeedChainsAsync(
            store,
            Path.Combine(root, "repo"),
            Path.Combine(root, "api"));
        return harness;
    }

    // One mai-code-2 chat span at 2026-11-20T18:33:24.707Z in repo with a cache read, or, when cacheRead is 0, a cache write of
    // 0, so the span reports its cache usage either way.
    private static OtlpTraceRequest ChatIn(string repo, string traceId, long input, long cacheRead)
    {
        List<OtlpAttribute> attributes =
        [
            Str("gen_ai.operation.name", "chat"),
            Str("gen_ai.request.model", "mai-code-2"),
            Int("gen_ai.usage.input_tokens", input),
            Int("gen_ai.usage.output_tokens", 10),
        ];
        if (cacheRead > 0)
        {
            attributes.Add(Int("gen_ai.usage.cache_read.input_tokens", cacheRead));
        }
        else
        {
            attributes.Add(Int("gen_ai.usage.cache_write.input_tokens", 0));
        }

        return Request(
            [.. CopilotResource, Str("process.working_directory", repo)],
            Span(traceId: traceId, spanId: traceId[..16], attributes: [.. attributes]));
    }
}
