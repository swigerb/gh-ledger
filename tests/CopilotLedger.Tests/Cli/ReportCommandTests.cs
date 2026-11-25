using System.Text.Json;
using CopilotLedger.Cli;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

public sealed class ReportCommandTests
{
    private static ManualTimeProvider Time => new(
        DateTimeOffset.Parse("2026-11-21T15:00:00Z"),
        ManualTimeProvider.UtcMinus5);

    [Fact]
    public async Task RP1_StatusTextIsExact()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);

        CliRun run = await harness.RunAsync("status");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(StatusText(temp.Path), run.Out);
    }

    [Fact]
    public async Task RP2_StatusJsonIncludesDaemonAndExactPeriods()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(
            temp.Path,
            platform => platform.IsWindows = true);
        harness.Platform.UserVariables["COPILOT_OTEL_ENABLED"] = "true";
        await harness.StartInProcessDaemonAsync();

        CliRun run = await harness.RunAsync("status", "--json");

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("+00:00\"", run.Out);
        Assert.DoesNotContain("\\u002B", run.Out);
        using JsonDocument json = JsonDocument.Parse(run.Out);
        JsonElement root = json.RootElement;
        JsonElement daemon = root.GetProperty("daemon");
        Assert.True(daemon.GetProperty("running").GetBoolean());
        Assert.Equal(Environment.ProcessId, daemon.GetProperty("processId").GetInt32());
        Assert.Equal(
            "http://localhost:4318",
            daemon.GetProperty("endpoint").GetString());
        Assert.True(root.GetProperty("copilotOtelEnabled").GetBoolean());
        Assert.Empty(root.GetProperty("warnings").EnumerateArray());
        Assert.Equal("2026-11-23", root.GetProperty("ratesVersion").GetString());
        JsonElement[] periods = root.GetProperty("periods").EnumerateArray().ToArray();
        Assert.Equal(3, periods.Length);
        AssertPeriod(periods[0], "today", "2026-11-21T05:00:00.0000000+00:00", 3, 342m, 3.42m, 0m, 0.23m);
        Assert.Equal(0, periods[0].GetProperty("unpricedSpans").GetInt64());
        AssertPeriod(periods[1], "last 7 days", "2026-11-15T05:00:00.0000000+00:00", 4, 342m, 3.42m, 0.315m, 0.23m);
        AssertPeriod(periods[2], "last 30 days", "2026-10-23T05:00:00.0000000+00:00", 4, 342m, 3.42m, 0.315m, 0.23m);
    }

    public static TheoryData<string, string, string, bool> Warnings => new()
    {
        { "OTEL_EXPORTER_OTLP_ENDPOINT", "http://127.0.0.1:4318/", "", true },
        { "OTEL_EXPORTER_OTLP_ENDPOINT", "https://collector.contoso.com:4318", "OTEL_EXPORTER_OTLP_ENDPOINT is https://collector.contoso.com:4318; Copilot exports there instead of to gh-ledger at http://localhost:4318.", false },
        { "COPILOT_OTEL_FILE_EXPORTER_PATH", "/tmp/x.jsonl", "COPILOT_OTEL_FILE_EXPORTER_PATH is set; Copilot writes telemetry to that file instead of exporting it to gh-ledger.", false },
        { "COPILOT_OTEL_EXPORTER_TYPE", "file", "COPILOT_OTEL_EXPORTER_TYPE is file; remove it or set it to otlp-http.", false },
        { "COPILOT_OTEL_EXPORTER_TYPE", "OTLP-HTTP", "", false },
        { "OTEL_EXPORTER_OTLP_TRACES_CLIENT_KEY", "/k.pem", "OTEL_EXPORTER_OTLP_TRACES_CLIENT_KEY is set; Copilot turns off export to http:// endpoints while TLS settings are set.", false },
        { "OTEL_EXPORTER_OTLP_PROTOCOL", "grpc", "OTEL_EXPORTER_OTLP_PROTOCOL is grpc; gh-ledger accepts only http/protobuf and http/json.", false },
        { "OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf", "", false },
    };

    [Theory]
    [MemberData(nameof(Warnings))]
    public async Task RP3_StatusJsonWarnsForConflictingProcessVariables(
        string name,
        string value,
        string warning,
        bool enabled)
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        harness.Platform.ProcessVariables[name] = value;

        CliRun run = await harness.RunAsync("status", "--json");

        Assert.Equal(0, run.ExitCode);
        using JsonDocument json = JsonDocument.Parse(run.Out);
        string[] actualWarnings = json.RootElement.GetProperty("warnings")
            .EnumerateArray().Select(item => item.GetString()!).ToArray();
        Assert.Equal(
            warning.Length == 0 ? [] : [warning],
            actualWarnings);
        Assert.Equal(
            enabled,
            json.RootElement.GetProperty("copilotOtelEnabled").GetBoolean());
    }

    [Fact]
    public async Task RP4_BranchTextIsExact()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);

        CliRun run = await harness.RunInAsync(
            Path.Combine(temp.Path, "repo"),
            "branch");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            Normalize("""
            Branches in swigerb/copilot-ledger (last 30 days)

            Branch          Spans  Unpriced  AI credits  GitHub USD  BYOK USD  API equiv USD  Local saved
            main                1         0      286.00       $2.86     $0.00          $2.86        $0.00
            feature/ledger      2         0       56.00       $0.56     $0.00       Unpriced        $0.23

            """),
            run.Out);
    }

    [Fact]
    public async Task RP5_BranchAllRepositoriesJsonHasExactOrdering()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);

        CliRun run = await harness.RunAsync("branch", "--all-repos", "--json");

        Assert.Equal(0, run.ExitCode);
        using JsonDocument json = JsonDocument.Parse(run.Out);
        Assert.Equal("last 30 days", json.RootElement.GetProperty("period").GetString());
        Assert.Equal(
            "2026-10-23T05:00:00.0000000+00:00",
            json.RootElement.GetProperty("from").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("repository").ValueKind);
        JsonElement[] rows = json.RootElement.GetProperty("rows").EnumerateArray().ToArray();
        Assert.Equal(3, rows.Length);
        AssertRollup(rows[0], "swigerb/copilot-ledger", "main", 1, 286m, 2.86m);
        AssertRollup(rows[1], "swigerb/copilot-ledger", "feature/ledger", 2, 56m, 0.56m, 0.23m);
        Assert.Equal("contoso/api", rows[2].GetProperty("repository").GetString());
        Assert.Equal("main", rows[2].GetProperty("branch").GetString());
        Assert.Equal(1, rows[2].GetProperty("spans").GetInt64());
        Assert.Equal(0.315m, rows[2].GetProperty("foundryByokUsd").GetDecimal());
    }

    [Fact]
    public async Task RP6_AgentsTextIsExact()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);

        CliRun run = await harness.RunAsync("agents", "--since", "all");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            Normalize("""
            Agents in all repositories (all time)

            Agent             Spans  Unpriced  AI credits  GitHub USD  BYOK USD  API equiv USD  Local saved
            ledger-architect      1         0      286.00       $2.86     $0.00          $2.86        $0.00
            ledger-builder        3         1       56.00       $0.56     $0.00       Unpriced        $0.23
            (none)                1         0        0.00       $0.00     $0.32       Unpriced        $0.00

            """),
            run.Out);
    }

    [Fact]
    public async Task RP7_TopPromptsTextIsExact()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);

        CliRun run = await harness.RunAsync(
            "top-prompts",
            "--since",
            "all",
            "--limit",
            "2");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            Normalize("""
            Top 2 prompts by cost in all repositories (all time)

            #  Started           Repository              Agents            Models                   Spans  AI credits  GitHub USD  BYOK USD  API equiv USD  Local saved  Trace
            1  2026-11-21 09:00  swigerb/copilot-ledger  ledger-architect  claude-opus-5.5              1      286.00       $2.86     $0.00          $2.86        $0.00  11111111111111111111111111111111
            2  2026-11-21 08:00  swigerb/copilot-ledger  ledger-builder    mai-code-2, qwen3.8-27b      2       56.00       $0.56     $0.00       Unpriced        $0.23  22222222222222222222222222222222

            """),
            run.Out);
    }

    [Fact]
    public async Task RP8_TopPromptsJsonHasAllChainFields()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);

        CliRun run = await harness.RunAsync("top-prompts", "--since", "all", "--json");

        Assert.Equal(0, run.ExitCode);
        using JsonDocument json = JsonDocument.Parse(run.Out);
        JsonElement[] rows = json.RootElement.GetProperty("rows").EnumerateArray().ToArray();
        Assert.Equal(4, rows.Length);
        Assert.Equal([1, 2, 3, 4], rows.Select(row => row.GetProperty("rank").GetInt32()));
        AssertPromptChain(
            rows[0],
            new string('1', 32),
            "conv-1",
            "2026-11-21T14:00:00.0000000+00:00",
            "2026-11-21T14:00:02.0000000+00:00",
            "swigerb/copilot-ledger",
            "main",
            ["ledger-architect"],
            ["claude-opus-5.5"],
            1, 0, 1_200_000, 50_000, 800_000, 100_000,
            286m, 2.86m, 0m, 0m);
        AssertPromptChain(
            rows[1],
            new string('2', 32),
            "conv-2",
            "2026-11-21T13:00:00.0000000+00:00",
            "2026-11-21T13:05:02.0000000+00:00",
            "swigerb/copilot-ledger",
            "feature/ledger",
            ["ledger-builder"],
            ["mai-code-2", "qwen3.8-27b"],
            2, 0, 1_400_000, 112_000, 400_000, 0,
            56m, 0.56m, 0m, 0.23m);
        AssertPromptChain(
            rows[2],
            new string('3', 32),
            null,
            "2026-11-18T12:00:00.0000000+00:00",
            "2026-11-18T12:00:02.0000000+00:00",
            "contoso/api",
            "main",
            [],
            ["gpt-6.1-sol"],
            1, 0, 100_000, 10_000, 0, 0,
            0m, 0m, 0.315m, 0m);
        AssertPromptChain(
            rows[3],
            new string('4', 32),
            "conv-4",
            "2026-10-01T12:00:00.0000000+00:00",
            "2026-10-01T12:00:02.0000000+00:00",
            "(none)",
            "(none)",
            ["ledger-builder"],
            ["gpt-unknown"],
            1, 1, 1_000, 100, 0, 0,
            0m, 0m, 0m, 0m);
    }

    public static TheoryData<string, string, string?> ValidPeriods => new()
    {
        { "today", "today", "2026-11-21T05:00:00.0000000+00:00" },
        { "TODAY", "today", "2026-11-21T05:00:00.0000000+00:00" },
        { "1d", "today", "2026-11-21T05:00:00.0000000+00:00" },
        { "7d", "last 7 days", "2026-11-15T05:00:00.0000000+00:00" },
        { "30d", "last 30 days", "2026-10-23T05:00:00.0000000+00:00" },
        { "all", "all time", null },
        { "2026-11-01", "since 2026-11-01", "2026-11-01T05:00:00.0000000+00:00" },
    };

    [Theory]
    [MemberData(nameof(ValidPeriods))]
    public async Task RP9_AgentsAcceptExactPeriodValues(
        string value,
        string expectedLabel,
        string? expectedFrom)
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);

        CliRun run = await harness.RunAsync("agents", "--since", value, "--json");

        Assert.Equal(0, run.ExitCode);
        using JsonDocument json = JsonDocument.Parse(run.Out);
        Assert.Equal(expectedLabel, json.RootElement.GetProperty("period").GetString());
        Assert.Equal(
            expectedFrom,
            json.RootElement.GetProperty("from").ValueKind == JsonValueKind.Null
                ? null
                : json.RootElement.GetProperty("from").GetString());
        if (expectedLabel == "today")
        {
            JsonElement[] rows = json.RootElement.GetProperty("rows").EnumerateArray().ToArray();
            Assert.Equal(2, rows.Length);
            Assert.Equal("ledger-architect", rows[0].GetProperty("agent").GetString());
            Assert.Equal(1, rows[0].GetProperty("spans").GetInt64());
            Assert.Equal(286m, rows[0].GetProperty("aiCredits").GetDecimal());
            Assert.Equal("ledger-builder", rows[1].GetProperty("agent").GetString());
            Assert.Equal(2, rows[1].GetProperty("spans").GetInt64());
            Assert.Equal(56m, rows[1].GetProperty("aiCredits").GetDecimal());
            Assert.Equal(0.23m, rows[1].GetProperty("localSavedUsd").GetDecimal());
        }
    }

    public static TheoryData<string> InvalidPeriods => new()
    {
        "0d",
        "3651d",
        "7x",
        "2026-13-01",
        "yesterday",
    };

    [Theory]
    [MemberData(nameof(InvalidPeriods))]
    public async Task RP9_RejectsInvalidPeriodAndOtherReportOptions(string value)
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);

        CliRun run = await harness.RunAsync("agents", "--since", value, "--json");

        Assert.Equal(1, run.ExitCode);
        Assert.Contains(
            $"Invalid --since value {value}. Use today, all, <N>d (1-3650), or yyyy-MM-dd.",
            run.Error);
        CliRun branch = await harness.RunAsync(
            "branch",
            "--repo",
            "a/b",
            "--all-repos");
        Assert.Equal(1, branch.ExitCode);
        Assert.Contains("Use either --repo or --all-repos, not both.", branch.Error);
        CliRun limit = await harness.RunAsync("top-prompts", "--limit", "0");
        Assert.Equal(1, limit.ExitCode);
        Assert.Contains("--limit must be from 1 to 100.", limit.Error);
    }

    [Fact]
    public async Task RP10_InvalidOverlayDoesNotFailStatus()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        string overlay = Path.Combine(temp.Path, "data", "rates.json");
        string malformed = "{ \"schemaVersion\": 1,\n  \"foundryByok\": { \"models\": [ }\n}\n";
        await File.WriteAllTextAsync(overlay, malformed);
        string expectedError = $"{overlay}: line 2, column 32: invalid JSON";

        CliRun text = await harness.RunAsync("status");
        CliRun jsonRun = await harness.RunAsync("status", "--json");

        Assert.Equal(0, text.ExitCode);
        Assert.Equal(
            StatusText(temp.Path).Replace(
                "Rates:   2026-11-23",
                $"Rates:   invalid overlay: {expectedError}",
                StringComparison.Ordinal),
            text.Out);
        Assert.Equal(0, jsonRun.ExitCode);
        using JsonDocument json = JsonDocument.Parse(jsonRun.Out);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("ratesVersion").ValueKind);
        Assert.Equal(expectedError, json.RootElement.GetProperty("ratesError").GetString());
        JsonElement[] periods = json.RootElement.GetProperty("periods")
            .EnumerateArray().ToArray();
        Assert.Equal(3, periods.Length);
        AssertPeriod(periods[0], "today", "2026-11-21T05:00:00.0000000+00:00", 3, 342m, 3.42m, 0m, 0.23m);
        AssertPeriod(periods[1], "last 7 days", "2026-11-15T05:00:00.0000000+00:00", 4, 342m, 3.42m, 0.315m, 0.23m);
        AssertPeriod(periods[2], "last 30 days", "2026-10-23T05:00:00.0000000+00:00", 4, 342m, 3.42m, 0.315m, 0.23m);
    }

    [Fact]
    public async Task RP11_TitleCountsRowsShown()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);

        CliRun run = await harness.RunAsync("top-prompts", "--since", "all");

        Assert.Equal(0, run.ExitCode);
        Assert.StartsWith(
            "Top 4 prompts by cost in all repositories (all time)\n",
            run.Out);
    }

    [Fact]
    public async Task RP12_StatusAsGhExtensionNamesGhLedgerCommands()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(
            temp.Path,
            platform =>
            {
                platform.ProcessPath = "/home/me/.local/share/gh/extensions/gh-ledger/gh-ledger";
                platform.ExistingFiles.Add("/home/me/.local/share/gh/extensions/gh-ledger/manifest.yml");
            });

        CliRun text = await harness.RunAsync("status");
        CliRun json = await harness.RunAsync("status", "--json");

        Assert.Equal(0, text.ExitCode);
        Assert.Equal(
            StatusText(temp.Path)
                .Replace($"gh-ledger {GhLedgerCli.Version}\n", $"gh-ledger {GhLedgerCli.Version} (GitHub CLI extension)\n", StringComparison.Ordinal)
                .Replace("start it with: gh-ledger daemon start", "start it with: gh ledger daemon start", StringComparison.Ordinal)
                .Replace("run: gh-ledger install", "run: gh ledger install", StringComparison.Ordinal),
            text.Out);
        Assert.Equal(0, json.ExitCode);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        Assert.True(document.RootElement.GetProperty("ghExtension").GetBoolean());
        Assert.Empty(document.RootElement.GetProperty("warnings").EnumerateArray());
    }

    [Fact]
    public async Task RP13_StatusWarnsWhenInstalledFromAnotherExecutable()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        string manifest = Path.Combine(temp.Path, "data", "install.json");
        await File.WriteAllTextAsync(
            manifest,
            """
            {"schemaVersion":1,"installedAt":"2026-11-20T00:00:00+00:00","executablePath":"/opt/old/gh-ledger","windowsUserVariableSet":false,"windowsUserVariablePreviousValue":null,"profileFiles":[],"ghAliasSet":false}
            """);
        await FileRetry.ReadAllTextAsync(manifest);
        const string Warning =
            "gh-ledger was installed from /opt/old/gh-ledger; run gh-ledger install to use this copy instead.";

        CliRun text = await harness.RunAsync("status");
        CliRun json = await harness.RunAsync("status", "--json");

        Assert.Equal(0, text.ExitCode);
        Assert.Equal(
            StatusText(temp.Path).Replace(
                "run: gh-ledger install\n",
                $"run: gh-ledger install\nWarning: {Warning}\n",
                StringComparison.Ordinal),
            text.Out);
        Assert.Equal(0, json.ExitCode);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        Assert.False(document.RootElement.GetProperty("ghExtension").GetBoolean());
        Assert.Equal(
            [Warning],
            document.RootElement.GetProperty("warnings").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public async Task RP14_StatusJsonHasEachPeriodsCacheCoverage()
    {
        using TempDirectory temp = new();
        using TempDirectory empty = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        await using CliHarness emptyHarness = new(empty.Path, new FakeCliPlatform(), Time);

        CliRun run = await harness.RunAsync("status", "--json");
        CliRun emptyRun = await emptyHarness.RunAsync("status", "--json");

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("\"cacheHitRatio\": 0.5454545454545454545454545455\n", run.Out);
        using JsonDocument json = JsonDocument.Parse(run.Out);
        JsonElement[] periods = json.RootElement.GetProperty("periods").EnumerateArray().ToArray();
        AssertCache(periods[0], 2, 1, 2_200_000, 1_200_000, 0.5454545454545454545454545455m);
        AssertCache(periods[1], 2, 2, 2_200_000, 1_200_000, 0.5454545454545454545454545455m);
        AssertCache(periods[2], 2, 2, 2_200_000, 1_200_000, 0.5454545454545454545454545455m);
        Assert.Equal("cache", periods[0].EnumerateObject().Last().Name);
        Assert.Equal(0, emptyRun.ExitCode);
        using JsonDocument emptyJson = JsonDocument.Parse(emptyRun.Out);
        foreach (JsonElement period in emptyJson.RootElement.GetProperty("periods").EnumerateArray())
        {
            AssertCache(period, 0, 0, 0, 0, null);
        }
    }

    [Fact]
    public async Task RP15_BranchAndAgentRowsEndWithCacheCoverage()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);

        CliRun branch = await harness.RunAsync("branch", "--all-repos", "--json");
        CliRun agents = await harness.RunAsync("agents", "--since", "all", "--json");

        Assert.Equal(0, branch.ExitCode);
        using JsonDocument branchJson = JsonDocument.Parse(branch.Out);
        JsonElement[] branchRows = branchJson.RootElement.GetProperty("rows").EnumerateArray().ToArray();
        Assert.Equal(3, branchRows.Length);
        AssertCoverage(branchRows[0], 1, 0, 0.6666666666666666666666666667m);
        AssertCoverage(branchRows[1], 1, 1, 0.4m);
        AssertCoverage(branchRows[2], 0, 1, null);
        Assert.Equal(0, agents.ExitCode);
        using JsonDocument agentJson = JsonDocument.Parse(agents.Out);
        JsonElement[] agentRows = agentJson.RootElement.GetProperty("rows").EnumerateArray().ToArray();
        Assert.Equal(["ledger-architect", "ledger-builder", "(none)"], agentRows.Select(row => row.GetProperty("agent").GetString()));
        AssertCoverage(agentRows[0], 1, 0, 0.6666666666666666666666666667m);
        AssertCoverage(agentRows[1], 1, 2, 0.4m);
        AssertCoverage(agentRows[2], 0, 1, null);
    }

    private static async Task<CliHarness> SeededHarnessAsync(
        string root,
        Action<FakeCliPlatform>? configure = null)
    {
        CreateGitFixture(Path.Combine(root, "repo"));
        FakeCliPlatform platform = new();
        configure?.Invoke(platform);
        CliHarness harness = new(root, platform, Time);
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

    private static void CreateGitFixture(string root)
    {
        string git = Path.Combine(root, ".git");
        Directory.CreateDirectory(git);
        File.WriteAllText(Path.Combine(git, "HEAD"), "ref: refs/heads/main\n");
        File.WriteAllText(
            Path.Combine(git, "config"),
            "[remote \"origin\"]\n\turl = https://github.com/swigerb/copilot-ledger.git\n");
    }

    private static void AssertPeriod(
        JsonElement item,
        string label,
        string from,
        long spans,
        decimal credits,
        decimal githubUsd,
        decimal byokUsd,
        decimal saved)
    {
        Assert.Equal(label, item.GetProperty("label").GetString());
        Assert.Equal(from, item.GetProperty("from").GetString());
        Assert.Equal(spans, item.GetProperty("spans").GetInt64());
        Assert.Equal(credits, item.GetProperty("aiCredits").GetDecimal());
        Assert.Equal(githubUsd, item.GetProperty("aiCreditsUsd").GetDecimal());
        Assert.Equal(byokUsd, item.GetProperty("foundryByokUsd").GetDecimal());
        Assert.Equal(saved, item.GetProperty("localSavedUsd").GetDecimal());
    }

    private static string Normalize(string value) => value.Replace("\r\n", "\n");

    private static void AssertCache(
        JsonElement period,
        long reported,
        long unreported,
        long reportedInput,
        long reportedCacheRead,
        decimal? hitRatio)
    {
        JsonElement cache = period.GetProperty("cache");
        Assert.Equal(
            ["cacheReportedSpans", "cacheUnreportedSpans", "reportedInputTokens", "reportedCacheReadInputTokens", "cacheHitRatio"],
            cache.EnumerateObject().Select(property => property.Name));
        Assert.Equal(reported, cache.GetProperty("cacheReportedSpans").GetInt64());
        Assert.Equal(unreported, cache.GetProperty("cacheUnreportedSpans").GetInt64());
        Assert.Equal(reportedInput, cache.GetProperty("reportedInputTokens").GetInt64());
        Assert.Equal(reportedCacheRead, cache.GetProperty("reportedCacheReadInputTokens").GetInt64());
        JsonElement ratio = cache.GetProperty("cacheHitRatio");
        Assert.Equal(hitRatio, ratio.ValueKind == JsonValueKind.Null ? null : ratio.GetDecimal());
    }

    private static void AssertCoverage(
        JsonElement row,
        long reported,
        long unreported,
        decimal? hitRatio)
    {
        Assert.Equal(
            ["apiEquivalentEstimatedSpans", "cacheReportedSpans", "cacheUnreportedSpans", "cacheHitRatio"],
            row.EnumerateObject().Select(property => property.Name).TakeLast(4));
        Assert.Equal(reported, row.GetProperty("cacheReportedSpans").GetInt64());
        Assert.Equal(unreported, row.GetProperty("cacheUnreportedSpans").GetInt64());
        JsonElement ratio = row.GetProperty("cacheHitRatio");
        Assert.Equal(hitRatio, ratio.ValueKind == JsonValueKind.Null ? null : ratio.GetDecimal());
    }

    private static void AssertRollup(
        JsonElement row,
        string repository,
        string branch,
        long spans,
        decimal credits,
        decimal githubUsd,
        decimal saved = 0m)
    {
        Assert.Equal(repository, row.GetProperty("repository").GetString());
        Assert.Equal(branch, row.GetProperty("branch").GetString());
        Assert.Equal(spans, row.GetProperty("spans").GetInt64());
        Assert.Equal(credits, row.GetProperty("aiCredits").GetDecimal());
        Assert.Equal(githubUsd, row.GetProperty("aiCreditsUsd").GetDecimal());
        Assert.Equal(saved, row.GetProperty("localSavedUsd").GetDecimal());
    }

    private static void AssertPromptChain(
        JsonElement row,
        string traceId,
        string? conversationId,
        string startTime,
        string endTime,
        string repository,
        string branch,
        IReadOnlyList<string> agents,
        IReadOnlyList<string> models,
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
        Assert.Equal(traceId, row.GetProperty("traceId").GetString());
        Assert.Equal(
            conversationId,
            row.GetProperty("conversationId").ValueKind == JsonValueKind.Null
                ? null
                : row.GetProperty("conversationId").GetString());
        Assert.Equal(startTime, row.GetProperty("startTime").GetString());
        Assert.Equal(endTime, row.GetProperty("endTime").GetString());
        Assert.Equal(repository, row.GetProperty("repository").GetString());
        Assert.Equal(branch, row.GetProperty("branch").GetString());
        Assert.Equal(
            agents,
            row.GetProperty("agents").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(
            models,
            row.GetProperty("models").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(spans, row.GetProperty("spans").GetInt64());
        Assert.Equal(unpriced, row.GetProperty("unpricedSpans").GetInt64());
        Assert.Equal(input, row.GetProperty("inputTokens").GetInt64());
        Assert.Equal(output, row.GetProperty("outputTokens").GetInt64());
        Assert.Equal(cacheRead, row.GetProperty("cacheReadInputTokens").GetInt64());
        Assert.Equal(cacheWrite, row.GetProperty("cacheWriteInputTokens").GetInt64());
        Assert.Equal(credits, row.GetProperty("aiCredits").GetDecimal());
        Assert.Equal(githubUsd, row.GetProperty("aiCreditsUsd").GetDecimal());
        Assert.Equal(byokUsd, row.GetProperty("foundryByokUsd").GetDecimal());
        Assert.Equal(savedUsd, row.GetProperty("localSavedUsd").GetDecimal());
    }

    private static string StatusText(string root) =>
        Normalize($"""
        gh-ledger 0.7.0
        Daemon:  not running; start it with: gh-ledger daemon start
        Ledger:  {Path.Combine(root, "data", "ledger.db")}
        Rates:   2026-11-23
        Copilot: OTel export not enabled; run: gh-ledger install

        Period        Spans  Unpriced  AI credits  GitHub USD  BYOK USD  API equiv USD  Local saved
        Today             3         0      342.00       $3.42     $0.00         $2.86*        $0.23
        Last 7 days       4         0      342.00       $3.42     $0.32         $2.86*        $0.23
        Last 30 days      4         0      342.00       $3.42     $0.32         $2.86*        $0.23

        Period        GitHub USD  At API prices  GitHub/API  Local saved  Local at API prices
        Today              $3.42         $2.86*         n/a        $0.23             Unpriced
        Last 7 days        $3.42         $2.86*         n/a        $0.23             Unpriced
        Last 30 days       $3.42         $2.86*         n/a        $0.23             Unpriced

        3 of 4 spans in the last 30 days had no API-equivalent price. API-equivalent amounts leave them out; * marks a partial amount.

        Period        Cache hit  Cache reported
        Today             54.5%  2 of 3 spans
        Last 7 days       54.5%  2 of 4 spans
        Last 30 days      54.5%  2 of 4 spans

        """);
}
