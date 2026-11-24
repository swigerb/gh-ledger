using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CopilotLedger.Cli.Daemon;
using CopilotLedger.Core.Git;
using CopilotLedger.Mcp;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CopilotLedger.Tests.Cli;

public sealed class ImportCommandTests
{
    private const string S1 = "11111111-1111-4111-8111-111111111111";
    private const string S2 = "22222222-2222-4222-8222-222222222222";
    private const string S3 = "33333333-3333-4333-8333-333333333333";
    private const string S4 = "44444444-4444-4444-8444-444444444444";
    private const string S5 = "55555555-5555-4555-8555-555555555555";
    private const string Thread = "019a1b2c-3d4e-7f50-8a6b-7c8d9e0f1a2b";

    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    public static TheoryData<string, string> InvalidOptions => new()
    {
        { "import", "Use --repo <path> or --all." },
        { "import|--repo|.|--all", "Use either --repo or --all, not both." },
        { "import|--repo| ", "--repo can't be blank." },
        { "import|--all|--source|cursor", "--source must be copilot or codex." },
        {
            "import|--all|--source|claude",
            "Claude Code import isn't supported: Claude Code's session-log format is internal and undocumented, so gh-ledger doesn't read it."
        },
        { "import|--all|--since|yesterday", "Invalid --since value yesterday. Use today, all, <N>d (1-3650), or yyyy-MM-dd." },
    };

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public async Task IC1_OptionsAreValidated(string command, string message)
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);

        CliRun run = await harness.RunAsync(command.Split('|'));

        Assert.Equal(1, run.ExitCode);
        Assert.Contains(message + "\n", run.Error);
        Assert.False(File.Exists(harness.Context.DatabasePath));
    }

    [Fact]
    public async Task IC2_RepoMustBeInAGitRepository()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        string work = Path.Combine(temp.Path, "work");
        Directory.CreateDirectory(work);

        CliRun run = await harness.RunInAsync(work, "import", "--repo", ".");

        Assert.Equal(1, run.ExitCode);
        Assert.Equal("", run.Out);
        Assert.Equal($"{work} isn't in a Git repository.\n", run.Error);
        Assert.False(File.Exists(harness.Context.DatabasePath));
    }

    [Fact]
    public async Task IC3_TheTableAndTotalsAreExact()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        string repo = GitFixture.Create(temp.Path);
        string other = Path.Combine(temp.Path, "other");
        JsonObject context = CopilotSessionLogs.Context(repo, repo, "feature/import", "swigerb/copilot-ledger", GitFixture.Sha2);
        new CopilotSessionLogs("s-a", context: context)
            .Usage("u1", S1, input: 1_000, output: 100)
            .Usage("u2", S2, input: 2_000, output: 200, cacheRead: 500)
            .Compaction(S3)
            .Write(harness.Context.CopilotHome);
        new CopilotSessionLogs("s-b", context: context.DeepClone().AsObject())
            .Usage("u3", S4)
            .Write(harness.Context.CopilotHome, active: true);
        new CopilotSessionLogs("s-c", context: CopilotSessionLogs.Context(other, other, "main", "contoso/other", GitFixture.Sha1))
            .Usage("u4", S5)
            .Write(harness.Context.CopilotHome);

        CliRun first = await harness.RunInAsync(repo, "import", "--repo", ".");
        CliRun second = await harness.RunInAsync(repo, "import", "--repo", ".");
        CliRun active = await harness.RunInAsync(repo, "import", "--repo", ".", "--include-active");

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(
            Normalize("""
            Import from local session logs for swigerb/copilot-ledger (all time)

                                       Copilot
            Sessions scanned                 3
            Skipped, unchanged               0
            Skipped, active                  1
            Skipped, out of scope            0
            Unsupported sessions             0
            Records parsed                   4
            Out of scope                     1
            Recorded                         3
            Duplicate                        0
            Overlap-skipped                  0
            Ambiguous-overlap                0
            Superseded, unmatched            0
            Malformed lines                  0
            Unsupported usage shapes         0
            Cost out of range                0
            Unpriced                         0
            Coverage                  complete

            Recorded 3 requests: 12.26 AI credits ($0.12), API equivalent $0.12.

            """),
            first.Out);
        Assert.Equal("", first.Error);
        Assert.Equal(0, second.ExitCode);
        Assert.Contains("Skipped, unchanged               1\n", second.Out);
        Assert.EndsWith("\nNothing new to record.\n", second.Out);
        Assert.Equal(0, active.ExitCode);
        Assert.EndsWith("\nRecorded 1 request: 0.15 AI credits ($0.00), API equivalent $0.00.\n", active.Out);
        Assert.Equal(
            [
                "s-a|feature/import|1000|1500000000",
                "s-a|feature/import|2000|2550000000",
                "s-a|feature/import|112223|118555450000",
                "s-b|feature/import|1000|1500000000",
            ],
            await LedgerSql.RowsAsync(
                harness.Context.DatabasePath,
                """
                SELECT conversation_id, branch_name, input_tokens, cost_usd_pico FROM spans
                ORDER BY conversation_id, start_time_unix_ms;
                """));
    }

    [Fact]
    public async Task IC4_TheJsonIsExact()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        new CopilotSessionLogs("s-a").Usage("u1", S1).Write(harness.Context.CopilotHome);
        new CodexRollouts(Thread).TurnContext("turn-1", "gpt-6.1-sol").Usage("resp_1", "turn-1").Write(harness.Context.CodexHome);

        CliRun run = await harness.RunAsync("import", "--all", "--source", "codex", "--source", "copilot", "--json");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("", run.Error);
        Assert.Equal(
            Normalize("""
            {
              "dryRun": false,
              "period": "all time",
              "from": null,
              "repository": null,
              "repositoryRoot": null,
              "coverage": "complete",
              "sources": [
                {
                  "source": "copilot",
                  "coverage": "complete",
                  "sessionsScanned": 1,
                  "unreadableStoreEntries": 0,
                  "sessionsSkippedUnchanged": 0,
                  "sessionsSkippedActive": 0,
                  "sessionsSkippedOutOfScope": 0,
                  "sessionsUnsupported": 0,
                  "sessionsAggregateOnly": 0,
                  "recordsParsed": 1,
                  "recordsOutOfScope": 0,
                  "recorded": 1,
                  "duplicate": 0,
                  "overlapSkipped": 0,
                  "ambiguousOverlap": 0,
                  "supersededUnmatched": 0,
                  "malformedLines": 0,
                  "unsupportedUsageShapes": 0,
                  "costOutOfRange": 0,
                  "journalPredecessors": 0,
                  "skippedEvents": 0,
                  "unpriced": 0,
                  "toolLinksRecorded": 0,
                  "aiCredits": 0.15,
                  "aiCreditsUsd": 0.0015,
                  "foundryByokUsd": 0,
                  "localSavedUsd": 0,
                  "apiEquivalentUsd": 0.0015,
                  "apiEquivalentUnpricedSpans": 0,
                  "apiEquivalentEstimatedSpans": 0
                },
                {
                  "source": "codex",
                  "coverage": "complete",
                  "sessionsScanned": 1,
                  "unreadableStoreEntries": 0,
                  "sessionsSkippedUnchanged": 0,
                  "sessionsSkippedActive": 0,
                  "sessionsSkippedOutOfScope": 0,
                  "sessionsUnsupported": 0,
                  "sessionsAggregateOnly": 0,
                  "recordsParsed": 1,
                  "recordsOutOfScope": 0,
                  "recorded": 1,
                  "duplicate": 0,
                  "overlapSkipped": 0,
                  "ambiguousOverlap": 0,
                  "supersededUnmatched": 0,
                  "malformedLines": 0,
                  "unsupportedUsageShapes": 0,
                  "costOutOfRange": 0,
                  "journalPredecessors": 0,
                  "skippedEvents": 0,
                  "unpriced": 1,
                  "toolLinksRecorded": 0,
                  "aiCredits": 0,
                  "aiCreditsUsd": 0,
                  "foundryByokUsd": 0,
                  "localSavedUsd": 0,
                  "apiEquivalentUsd": 0.003,
                  "apiEquivalentUnpricedSpans": 0,
                  "apiEquivalentEstimatedSpans": 0
                }
              ],
              "totals": {
                "recorded": 2,
                "aiCredits": 0.15,
                "aiCreditsUsd": 0.0015,
                "foundryByokUsd": 0,
                "localSavedUsd": 0,
                "apiEquivalentUsd": 0.0045,
                "apiEquivalentUnpricedSpans": 0,
                "apiEquivalentEstimatedSpans": 0
              },
              "diagnostics": []
            }

            """),
            run.Out);
    }

    [Fact]
    public async Task IC5_AnIncompleteImportExitsWith2AndCappedContentFreeWarnings()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        List<string> expected = [];
        CopilotSessionLogs compaction = new CopilotSessionLogs("s-00")
            .Usage("u-00", Srid(0))
            .Compaction(Srid(100), change: data => data["compactionTokensUsed"]!["requestCount"] = 2);
        compaction.Write(harness.Context.CopilotHome);
        expected.Add(
            $"Warning: Copilot session s-00, line 3 (byte {Offset(compaction, 2)}): 1 compaction record skipped: unsupported data.compactionTokensUsed.requestCount");
        for (int index = 1; index <= 21; index++)
        {
            string id = string.Create(CultureInfo.InvariantCulture, $"s-{index:00}");
            CopilotSessionLogs logs = new CopilotSessionLogs(id)
                .Usage("u-" + id, Srid(index))
                .Raw("{\"type\":\"user.message\",\"data\":{\"content\":\"" + CopilotSessionLogs.Canary + "\"");
            logs.Write(harness.Context.CopilotHome);
            expected.Add($"Warning: Copilot session {id}, line 3 (byte {Offset(logs, 2)}): 1 malformed line skipped");
        }

        CliRun text = await harness.RunAsync("import", "--all");
        CliRun json = await harness.RunAsync("import", "--all", "--json");

        Assert.Equal(2, text.ExitCode);
        Assert.Contains("Records parsed                    23\n", text.Out);
        Assert.Contains("Recorded                          22\n", text.Out);
        Assert.Contains("Malformed lines                   21\n", text.Out);
        Assert.Contains("Unsupported usage shapes           1\n", text.Out);
        Assert.Contains("Coverage                  incomplete\n", text.Out);
        Assert.EndsWith(
            "\nRecorded 22 requests: 3.30 AI credits ($0.03), API equivalent $0.03.\nCoverage is incomplete; see the warnings.\n",
            text.Out);
        Assert.Equal(
            string.Concat(expected.Take(20).Select(line => line + "\n")) +
            "Warning: 2 more warnings; run with --json to see them all.\n",
            text.Error);
        Assert.Equal(2, json.ExitCode);
        Assert.Equal("", json.Error);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        Assert.Equal("incomplete", document.RootElement.GetProperty("coverage").GetString());
        JsonElement[] diagnostics = [.. document.RootElement.GetProperty("diagnostics").EnumerateArray()];
        Assert.Equal(22, diagnostics.Length);
        Assert.Equal(
            """{"source":"copilot","log":"s-00","code":"unsupportedUsageShape","event":"compactionComplete","field":"data.compactionTokensUsed.requestCount","version":null,"count":1,"line":3,"byteOffset":""" +
            Offset(compaction, 2).ToString(CultureInfo.InvariantCulture) + "}",
            JsonSerializer.Serialize(diagnostics[0]));
        Assert.Equal(22, document.RootElement.GetProperty("sources")[0].GetProperty("duplicate").GetInt64());
        Assert.DoesNotContain(CopilotSessionLogs.Canary, text.Out + text.Error + json.Out);
    }

    [Fact]
    public async Task IC6_ADryRunPrintsWhatTheImportDoesAndLeavesTheLedgerAsItWas()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        SqliteLedgerStore store = new(harness.Context.DatabasePath, harness.TimeProvider);
        await store.InitializeAsync();
        await store.RecordAsync([ImportEntries.Live("s-a", S1)]);
        SqliteConnection.ClearAllPools();
        new CopilotSessionLogs("s-a")
            .Usage("u1", S1, input: 100_000, output: 10_000)
            .Usage("u2", S2, input: 100_000, output: 10_000)
            .Write(harness.Context.CopilotHome);
        new CopilotSessionLogs("s-b").Usage("u3", S3, input: 100_000, output: 10_000).Write(harness.Context.CopilotHome);
        byte[] before = SHA256.HashData(await FileRetry.ReadAllBytesAsync(harness.Context.DatabasePath));

        CliRun dry = await harness.RunAsync("import", "--all", "--dry-run");

        Assert.Equal(before, SHA256.HashData(await FileRetry.ReadAllBytesAsync(harness.Context.DatabasePath)));
        Assert.False(File.Exists(harness.Context.DatabasePath + "-wal"));
        Assert.False(File.Exists(harness.Context.DatabasePath + "-shm"));
        CliRun real = await harness.RunAsync("import", "--all");
        string table = Normalize("""

                                       Copilot
            Sessions scanned                 2
            Skipped, unchanged               0
            Skipped, active                  0
            Skipped, out of scope            0
            Unsupported sessions             0
            Records parsed                   3
            Out of scope                     0
            Recorded                         2
            Duplicate                        0
            Overlap-skipped                  1
            Ambiguous-overlap                0
            Superseded, unmatched            0
            Malformed lines                  0
            Unsupported usage shapes         0
            Cost out of range                0
            Unpriced                         0
            Coverage                  complete


            """);
        Assert.Equal(0, dry.ExitCode);
        Assert.Equal(
            "Import from local session logs (all time, dry run)\n" + table +
            "Importing would record 2 requests: 30.00 AI credits ($0.30), API equivalent $0.30.\nDry run: nothing was written.\n",
            dry.Out);
        Assert.Equal(0, real.ExitCode);
        Assert.Equal(
            "Import from local session logs (all time)\n" + table +
            "Recorded 2 requests: 30.00 AI credits ($0.30), API equivalent $0.30.\n",
            real.Out);
        Assert.Equal(3L, await LedgerSql.ScalarAsync(harness.Context.DatabasePath, "SELECT COUNT(*) FROM spans;"));
    }

    [Fact]
    public async Task IC7_ImportedSpendAppearsInTheReportsWithItsRecordedBranch()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        string repo = await ImportBranchAsync(temp, harness);

        CliRun branch = await harness.RunInAsync(repo, "branch", "--since", "all", "--json");
        CliRun agents = await harness.RunInAsync(repo, "agents", "--since", "all", "--json");
        CliRun prompts = await harness.RunInAsync(repo, "top-prompts", "--since", "all", "--json");

        using JsonDocument branchJson = JsonDocument.Parse(branch.Out);
        JsonElement row = Assert.Single(branchJson.RootElement.GetProperty("rows").EnumerateArray());
        Assert.Equal(
            ("swigerb/copilot-ledger", "feature/import", 2L, 22.5m, 0.225m),
            (row.GetProperty("repository").GetString(), row.GetProperty("branch").GetString(),
             row.GetProperty("spans").GetInt64(), row.GetProperty("aiCredits").GetDecimal(),
             row.GetProperty("apiEquivalentUsd").GetDecimal()));
        using JsonDocument agentJson = JsonDocument.Parse(agents.Out);
        Assert.Equal(
            ["(none)|1|15", "explore|1|7.5"],
            agentJson.RootElement.GetProperty("rows").EnumerateArray()
                .Select(item => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{item.GetProperty("agent").GetString()}|{item.GetProperty("spans").GetInt64()}|{item.GetProperty("aiCredits").GetDecimal()}"))
                .Order(StringComparer.Ordinal));
        using JsonDocument promptJson = JsonDocument.Parse(prompts.Out);
        JsonElement prompt = Assert.Single(promptJson.RootElement.GetProperty("rows").EnumerateArray());
        Assert.Equal(
            ("s-a", "feature/import", 2L, 22.5m, "explore"),
            (prompt.GetProperty("conversationId").GetString(), prompt.GetProperty("branch").GetString(),
             prompt.GetProperty("spans").GetInt64(), prompt.GetProperty("aiCredits").GetDecimal(),
             Assert.Single(prompt.GetProperty("agents").EnumerateArray()).GetString()));
    }

    [Fact]
    public async Task IC8_ImportedSpendAppearsInTheMcpBranchLedger()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        string repo = await ImportBranchAsync(temp, harness);
        await using McpTestHarness mcp = await McpTestHarness.StartAsync(new LedgerMcpContext
        {
            DatabasePath = harness.Context.DatabasePath,
            WorkingDirectory = repo,
            GetEnvironmentVariable = _ => null,
            GitResolver = new FileSystemGitContextResolver(ceilingDirectory: temp.Path),
            TimeProvider = harness.TimeProvider,
            Version = "9.9.9",
        });

        CallToolResult result = await mcp.Client.CallToolAsync(
            "get_branch_ledger",
            new Dictionary<string, object?> { ["repository"] = "swigerb/copilot-ledger", ["branch"] = "feature/import" });

        Assert.NotEqual(true, result.IsError);
        using JsonDocument json = JsonDocument.Parse(Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
        Assert.Equal(
            (2L, 22.5m, 0.225m, 0.225m, "claude-haiku-4.5"),
            (json.RootElement.GetProperty("spans").GetInt64(), json.RootElement.GetProperty("aiCredits").GetDecimal(),
             json.RootElement.GetProperty("aiCreditsUsd").GetDecimal(), json.RootElement.GetProperty("apiEquivalentUsd").GetDecimal(),
             json.RootElement.GetProperty("topModel").GetString()));
    }

    [Fact]
    public void IC9_TheIngestFailureLogWritesOnlyTheTypeOfOtherExceptions()
    {
        List<string> lines = [];
        ManualTimeProvider clock = new(Now);
        IngestFailureLog log = new(lines.Add, clock);

        log.Report(new JsonException(CopilotSessionLogs.Canary));
        clock.Advance(TimeSpan.FromMinutes(1));
        log.Report(new SqliteException("SQLite Error 5: 'database is locked'.", 5));
        clock.Advance(TimeSpan.FromMinutes(1));
        log.Report(new IOException("There is not enough space on the disk."));
        clock.Advance(TimeSpan.FromMinutes(1));
        log.Report(new ArgumentException(CopilotSessionLogs.Canary));

        Assert.Equal(
            [
                "Warning: could not record spans: JsonException",
                "Warning: could not record spans: SqliteException: SQLite Error 5: 'database is locked'.",
                "Warning: could not record spans: IOException: There is not enough space on the disk.",
                "Warning: could not record spans: ArgumentException",
            ],
            lines);
    }

    [Fact]
    public async Task IC10_ADaemonOfAnotherVersionIsReported()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        string data = harness.Context.DataDirectory;
        Directory.CreateDirectory(data);
        await File.WriteAllTextAsync(
            Path.Combine(data, "daemon.json"),
            """{"schemaVersion":1,"processId":4242,"startedAt":"2026-11-23T11:00:00+00:00","endpoint":"http://localhost:4318","version":"0.4.0","databasePath":"ledger.db"}""");
        CliRun run;
        await using (FileStream held = new(Path.Combine(data, "daemon.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            run = await harness.RunAsync("import", "--all");
        }

        CliRun released = await harness.RunAsync("import", "--all");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            "Warning: the daemon runs gh-ledger 0.4.0; restart it so live spans follow this version's import rules.\n",
            run.Error);
        Assert.Equal("", released.Error);
    }

    [Fact]
    public async Task IC11_SessionsFromBeforeCopilot1_0_103AloneSuggestSince()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        string old = new CopilotSessionLogs("s-old", "1.0.98", start: new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero))
            .Usage("u1", S1)
            .Write(harness.Context.CopilotHome);
        string recent = new CopilotSessionLogs("s-new").Usage("u2", S2).Write(harness.Context.CopilotHome);
        File.SetLastWriteTimeUtc(old, new DateTime(2026, 10, 1, 12, 1, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(recent, new DateTime(2026, 11, 20, 12, 1, 0, DateTimeKind.Utc));

        CliRun all = await harness.RunAsync("import", "--all");
        CliRun since = await harness.RunAsync("import", "--all", "--since", "2026-11-01");

        Assert.Equal(2, all.ExitCode);
        Assert.Contains("Unsupported sessions               1\n", all.Out);
        Assert.EndsWith(
            "\nRecorded 1 request: 0.15 AI credits ($0.00), API equivalent $0.00.\nCoverage is incomplete only because 1 session predates Copilot 1.0.103; use --since to leave it out.\n",
            all.Out);
        Assert.Equal(
            "Warning: Copilot session s-old, line 1 (byte 0): Copilot before 1.0.103 logs only session totals (this session: 1.0.98); 1.0.103 to 1.0.114 are supported; skipped\n",
            all.Error);
        Assert.Equal(0, since.ExitCode);
        Assert.Contains("Skipped, out of scope            1\n", since.Out);
        Assert.EndsWith("\nNothing new to record.\n", since.Out);
        Assert.Equal("", since.Error);
    }

    [Fact]
    public async Task IC12_ADryRunRefusesALedgerWithOneCompanionFile()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        await new SqliteLedgerStore(harness.Context.DatabasePath, harness.TimeProvider).InitializeAsync();
        SqliteConnection.ClearAllPools();
        await File.WriteAllBytesAsync(harness.Context.DatabasePath + "-wal", []);
        new CopilotSessionLogs("s-a").Usage("u1", S1).Write(harness.Context.CopilotHome);

        CliRun run = await harness.RunAsync("import", "--all", "--dry-run");

        Assert.Equal(1, run.ExitCode);
        Assert.Equal("", run.Out);
        Assert.Equal(
            "Dry run stopped: the ledger has a -wal or -shm file without the other, and reading it could create the missing one. Open the ledger once with another gh-ledger command, such as status, and try again.\n",
            run.Error);
        Assert.False(File.Exists(harness.Context.DatabasePath + "-shm"));
        Assert.Equal(0, new FileInfo(harness.Context.DatabasePath + "-wal").Length);
    }

    [Fact]
    public async Task IC13_AStoreThatCantBeListedIsAWarning()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        Directory.CreateDirectory(harness.Context.CopilotHome);
        await File.WriteAllTextAsync(Path.Combine(harness.Context.CopilotHome, "session-state"), "not a folder");

        CliRun text = await harness.RunAsync("import", "--all");
        CliRun json = await harness.RunAsync("import", "--all", "--json");

        Assert.Equal(2, text.ExitCode);
        Assert.Contains("Sessions scanned                   0\n", text.Out);
        Assert.Contains("Coverage                  incomplete\n", text.Out);
        Assert.EndsWith("\nNothing new to record.\nCoverage is incomplete; see the warnings.\n", text.Out);
        Assert.Equal(
            "Warning: Copilot session logs: 1 folder or file couldn't be listed; any logs there weren't imported\n",
            text.Error);
        Assert.Equal(2, json.ExitCode);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        Assert.Equal(1, document.RootElement.GetProperty("sources")[0].GetProperty("unreadableStoreEntries").GetInt64());
        Assert.Equal(
            """{"source":"copilot","log":"","code":"unreadableStore","event":"unknown","field":null,"version":null,"count":1,"line":null,"byteOffset":null}""",
            JsonSerializer.Serialize(Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray())));
    }

    [Fact]
    public async Task IC14_RequestsSupersededWithoutAMatchAreAWarning()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        new CopilotSessionLogs("s-a").Usage("u1", S1).Usage("u2", S2).Write(harness.Context.CopilotHome);
        await harness.RunAsync("import", "--all");
        SqliteLedgerStore store = new(harness.Context.DatabasePath, harness.TimeProvider);
        await store.InitializeAsync();
        await store.RecordAsync([ImportEntries.Live("s-a", null)]);

        CliRun text = await harness.RunAsync("import", "--all");
        CliRun json = await harness.RunAsync("import", "--all", "--json");

        Assert.Equal(2, text.ExitCode);
        Assert.Contains("Superseded, unmatched              2\n", text.Out);
        Assert.EndsWith("\nNothing new to record.\nCoverage is incomplete; see the warnings.\n", text.Out);
        Assert.Equal(
            "Warning: Copilot session s-a: 2 imported requests superseded by live spans that can't be matched to them; requests the daemon missed may not count\n",
            text.Error);
        Assert.Equal(2, json.ExitCode);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        Assert.Equal(2, document.RootElement.GetProperty("sources")[0].GetProperty("supersededUnmatched").GetInt64());
        Assert.Equal(
            """{"source":"copilot","log":"s-a","code":"supersededUnmatched","event":"unknown","field":null,"version":null,"count":2,"line":null,"byteOffset":null}""",
            JsonSerializer.Serialize(Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray())));
    }

    private static CliHarness Harness(TempDirectory temp) => new(temp.Path, new FakeCliPlatform(), new ManualTimeProvider(Now));

    private static string Normalize(string value) => value.Replace("\r\n", "\n");

    private static string Srid(int index) =>
        string.Create(CultureInfo.InvariantCulture, $"5e000000-0000-4000-8000-{index:x12}");

    // The byte offset of the line after the first count lines.
    private static long Offset(CopilotSessionLogs logs, int count) =>
        logs.Lines.Take(count).Sum(line => Encoding.UTF8.GetByteCount(line) + 1L);

    // Imports one session of the repository at <temp>/repo, recorded on branch feature/import: a request of the main agent
    // (15 AI credits) and one of its explore sub-agent (7.5 AI credits) in one prompt. Returns the repository's path.
    private static async Task<string> ImportBranchAsync(TempDirectory temp, CliHarness harness)
    {
        string repo = GitFixture.Create(temp.Path);
        new CopilotSessionLogs(
                "s-a",
                context: CopilotSessionLogs.Context(repo, repo, "feature/import", "swigerb/copilot-ledger", GitFixture.Sha2))
            .Usage("u1", S1, input: 100_000, output: 10_000)
            .SubagentStarted("agent-1", "explore", "toolu-1")
            .Usage("u2", S2, input: 50_000, output: 5_000, agentId: "agent-1")
            .Write(harness.Context.CopilotHome);
        CliRun run = await harness.RunInAsync(repo, "import", "--repo", ".");
        Assert.Equal(0, run.ExitCode);
        return repo;
    }
}
