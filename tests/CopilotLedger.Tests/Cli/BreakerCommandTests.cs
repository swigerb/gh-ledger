using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using CopilotLedger.Core.Breaker;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

public sealed class BreakerCommandTests
{
    private static readonly DateTimeOffset T0 =
        DateTimeOffset.Parse("2026-11-21T10:00:00Z", CultureInfo.InvariantCulture);
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-11-21T12:00:00Z", CultureInfo.InvariantCulture);
    private const string Rules =
        "Rules: more than 150.00 AI credits in 5 minutes on one prompt; 5 failed tool calls in a row on one prompt";

    [Fact]
    public async Task BC1_Closed()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(
            temp.Path,
            timeProvider: new ManualTimeProvider(Now, ManualTimeProvider.UtcMinus5));

        CliRun text = await harness.RunAsync("breaker", "status");
        Assert.Equal(0, text.ExitCode);
        Assert.Equal(
            """
            Circuit breaker: closed
            Rules: more than 150.00 AI credits in 5 minutes on one prompt; 5 failed tool calls in a row on one prompt
            Flag file: off. Notifications: on.

            """.ReplaceLineEndings("\n"),
            text.Out);

        CliRun json = await harness.RunAsync("breaker", "status", "--json");
        Assert.Equal(0, json.ExitCode);
        AssertJsonEqual(
            """{"state":"closed","settings":{"velocityCredits":150,"velocityWindowMinutes":5,"churnFailures":5,"flagFile":false,"notifications":true},"openTrips":[]}""",
            json.Out);
    }

    [Fact]
    public async Task BC2_Open()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(
            temp.Path,
            timeProvider: new ManualTimeProvider(Now, ManualTimeProvider.UtcMinus5));
        SqliteLedgerStore store = await StoreAsync(harness);
        Assert.Equal(1L, await store.RecordBreakerTripAsync(VA()));
        Assert.Equal(2L, await store.RecordBreakerTripAsync(CB()));
        Assert.True(await store.MarkBreakerTripNotifiedAsync(1));

        CliRun text = await harness.RunAsync("breaker", "status");
        Assert.Equal(0, text.ExitCode);
        Assert.Equal(
            """
            Circuit breaker: open (2 trips)
            Rules: more than 150.00 AI credits in 5 minutes on one prompt; 5 failed tool calls in a row on one prompt
            Flag file: off. Notifications: on.

            Trip 2 at 2026-11-21 05:05: swigerb/copilot-ledger @ main: 5 failed tool calls in a row on one prompt
              rule retry_churn, trace 22222222222222222222222222222222, session conv-2
            Trip 1 at 2026-11-21 05:03: swigerb/copilot-ledger @ feature/ledger: 180.00 AI credits in 5 minutes on one prompt
              rule credit_velocity, trace 11111111111111111111111111111111, session (none)
            Reset a trip with: gh-ledger breaker reset --id <id>

            """.ReplaceLineEndings("\n"),
            text.Out);

        CliRun json = await harness.RunAsync("breaker", "status", "--json");
        AssertJsonEqual(
            """
            {
              "state": "open",
              "settings": { "velocityCredits": 150, "velocityWindowMinutes": 5, "churnFailures": 5, "flagFile": false, "notifications": true },
              "openTrips": [
                { "id": 2, "rule": "retry_churn", "repository": "swigerb/copilot-ledger", "branch": "main", "repositoryRoot": null, "conversationId": "conv-2", "traceId": "22222222222222222222222222222222", "trippedAt": "2026-11-21T10:05:00.0000000+00:00", "detail": "5 failed tool calls in a row", "windowAiCredits": null, "windowMinutes": null, "failureCount": 5, "recordedAt": "2026-11-21T12:00:00.0000000+00:00", "notifiedAt": null },
                { "id": 1, "rule": "credit_velocity", "repository": "swigerb/copilot-ledger", "branch": "feature/ledger", "repositoryRoot": null, "conversationId": null, "traceId": "11111111111111111111111111111111", "trippedAt": "2026-11-21T10:03:00.0000000+00:00", "detail": "180.00 AI credits in 5 minutes", "windowAiCredits": 180, "windowMinutes": 5, "failureCount": null, "recordedAt": "2026-11-21T12:00:00.0000000+00:00", "notifiedAt": "2026-11-21T12:00:00.0000000+00:00" }
              ]
            }
            """,
            json.Out);
    }

    [Fact]
    public async Task BC3_Reset()
    {
        using TempDirectory temp = new();
        GitFixture.Create(temp.Path);
        await using CliHarness harness = new(
            temp.Path,
            timeProvider: new ManualTimeProvider(Now, ManualTimeProvider.UtcMinus5));
        SqliteLedgerStore store = await StoreAsync(harness);
        await store.RecordBreakerTripAsync(VA());
        await store.RecordBreakerTripAsync(CB());
        await store.RecordBreakerTripAsync(CB() with
        {
            TraceId = new string('3', 32),
            Repository = "contoso/api",
        });
        await store.RecordBreakerTripAsync(CB() with
        {
            TraceId = new string('4', 32),
        });

        CliRun idAndAll = await harness.RunAsync(
            "breaker", "reset", "--id", "2", "--all");
        Assert.Equal(1, idAndAll.ExitCode);
        Assert.Contains(
            "Use only one of --id, --all, or --repo with --branch.",
            idAndAll.Error);
        CliRun allAndScope = await harness.RunAsync(
            "breaker", "reset", "--all", "--repo", "x", "--branch", "y");
        Assert.Equal(1, allAndScope.ExitCode);
        Assert.Contains(
            "Use only one of --id, --all, or --repo with --branch.",
            allAndScope.Error);
        CliRun incompleteScope = await harness.RunAsync(
            "breaker", "reset", "--repo", "swigerb/copilot-ledger");
        Assert.Equal(1, incompleteScope.ExitCode);
        Assert.Contains(
            "--repo and --branch must be used together.",
            incompleteScope.Error);
        CliRun outside = await harness.RunAsync("breaker", "reset");
        Assert.Equal(1, outside.ExitCode);
        Assert.Equal(
            "Not in a Git repository. Pass --repo and --branch, --id, or --all.\n",
            outside.Error);

        string repo = Path.Combine(temp.Path, "repo");
        CliRun current = await harness.RunInAsync(repo, "breaker", "reset");
        Assert.Equal(0, current.ExitCode);
        Assert.Equal("Reset 1 trip.\n", current.Out);
        Assert.Equal(
            "No open trips to reset.\n",
            (await harness.RunInAsync(repo, "breaker", "reset")).Out);
        Assert.Equal(
            "Reset 2 trips.\n",
            (await harness.RunAsync(
                "breaker",
                "reset",
                "--repo",
                "swigerb/copilot-ledger",
                "--branch",
                "main")).Out);
        Assert.Equal(
            "Reset 1 trip.\n",
            (await harness.RunAsync("breaker", "reset", "--id", "3")).Out);
        Assert.Equal(
            "No open trips to reset.\n",
            (await harness.RunAsync("breaker", "reset", "--id", "3")).Out);
        Assert.Equal(
            "No open trips to reset.\n",
            (await harness.RunAsync("breaker", "reset", "--all")).Out);
        Assert.Empty(
            await store.GetOpenBreakerTripsAsync(BreakerTripFilter.All));
    }

    [Fact]
    public async Task BC4_Config()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(
            temp.Path,
            timeProvider: new ManualTimeProvider(Now, ManualTimeProvider.UtcMinus5));
        SqliteLedgerStore store = await StoreAsync(harness);

        CliRun configured = await harness.RunAsync("breaker", "config");
        Assert.Equal(0, configured.ExitCode);
        Assert.Equal(
            $"{Rules}\nFlag file: off. Notifications: on.\n",
            configured.Out);
        CliRun updated = await harness.RunAsync(
            "breaker",
            "config",
            "--velocity-credits",
            "12.5",
            "--velocity-minutes",
            "1",
            "--churn-failures",
            "2",
            "--notifications",
            "off");
        Assert.Equal(0, updated.ExitCode);
        Assert.Equal(
            """
            Updated the circuit breaker settings.
            Rules: more than 12.50 AI credits in 1 minute on one prompt; 2 failed tool calls in a row on one prompt
            Flag file: off. Notifications: off.

            """.ReplaceLineEndings("\n"),
            updated.Out);
        Assert.Equal(
            BreakerSettings.Default with
            {
                VelocityCredits = 12.5m,
                VelocityWindowMinutes = 1,
                ChurnFailures = 2,
                Notifications = false,
            },
            await store.GetBreakerSettingsAsync());
    }

    public static TheoryData<string[], string> InvalidConfig => new()
    {
        { ["--velocity-credits", "0.99"], "--velocity-credits must be a number from 1 to 1000000 with at most 2 decimal places." },
        { ["--velocity-credits", "1000000.01"], "--velocity-credits must be a number from 1 to 1000000 with at most 2 decimal places." },
        { ["--velocity-credits", "1.001"], "--velocity-credits must be a number from 1 to 1000000 with at most 2 decimal places." },
        { ["--velocity-credits", "1e2"], "--velocity-credits must be a number from 1 to 1000000 with at most 2 decimal places." },
        { ["--velocity-credits", "1,5"], "--velocity-credits must be a number from 1 to 1000000 with at most 2 decimal places." },
        { ["--velocity-minutes", "0"], "--velocity-minutes must be from 1 to 1440." },
        { ["--velocity-minutes", "1441"], "--velocity-minutes must be from 1 to 1440." },
        { ["--churn-failures", "1"], "--churn-failures must be from 2 to 100." },
        { ["--churn-failures", "101"], "--churn-failures must be from 2 to 100." },
        { ["--flag-file", "yes"], "--flag-file must be on or off." },
        { ["--notifications", "ON"], "--notifications must be on or off." },
    };

    [Theory]
    [MemberData(nameof(InvalidConfig))]
    public async Task BC4_InvalidConfig(string[] arguments, string message)
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(
            temp.Path,
            timeProvider: new ManualTimeProvider(Now, ManualTimeProvider.UtcMinus5));
        SqliteLedgerStore store = await StoreAsync(harness);

        CliRun run = await harness.RunAsync(
            ["breaker", "config", .. arguments]);
        Assert.Equal(1, run.ExitCode);
        Assert.Contains(message, run.Error);
        Assert.Equal(BreakerSettings.Default, await store.GetBreakerSettingsAsync());
    }

    [Fact]
    public async Task BC5_FlagFileFollowsSetting()
    {
        using TempDirectory temp = new();
        string repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        await using CliHarness harness = new(
            temp.Path,
            timeProvider: new ManualTimeProvider(Now, ManualTimeProvider.UtcMinus5));
        SqliteLedgerStore store = await StoreAsync(harness);
        await store.RecordBreakerTripAsync(VA() with { RepositoryRoot = repo });
        string flag = Path.Combine(repo, ".copilot-ledger", "CIRCUIT_OPEN");
        string expected = FlagJson(repo);

        CliRun enabled = await harness.RunAsync(
            "breaker", "config", "--flag-file", "on");
        Assert.Equal(0, enabled.ExitCode);
        Assert.Equal(
            """
            Updated the circuit breaker settings.
            Rules: more than 150.00 AI credits in 5 minutes on one prompt; 5 failed tool calls in a row on one prompt
            Flag file: on. Notifications: on.

            """.ReplaceLineEndings("\n"),
            enabled.Out);
        Assert.Equal(expected, await FileRetry.ReadAllTextAsync(flag));

        File.Delete(flag);
        Assert.Equal(
            0,
            (await harness.RunAsync(
                "breaker", "config", "--flag-file", "on")).ExitCode);
        Assert.Equal(expected, await FileRetry.ReadAllTextAsync(flag));

        Assert.Equal(
            0,
            (await harness.RunAsync(
                "breaker", "config", "--flag-file", "off")).ExitCode);
        Assert.False(File.Exists(flag));
    }

    [Fact]
    public async Task BC6_Budgets()
    {
        using TempDirectory temp = new();
        string repo = GitFixture.Create(temp.Path);
        await using CliHarness harness = new(
            temp.Path,
            timeProvider: new ManualTimeProvider(Now, ManualTimeProvider.UtcMinus5));

        CliRun empty = await harness.RunAsync("budget", "list");
        Assert.Equal(0, empty.ExitCode);
        Assert.Equal("No branch budgets are set.\n", empty.Out);
        SqliteLedgerStore store = await StoreAsync(harness);
        await LedgerSeeds.SeedChainsAsync(
            store,
            repo,
            Path.Combine(temp.Path, "api"));

        CliRun exactBudget = await harness.RunInAsync(
            repo, "budget", "set", "56");
        Assert.Equal(0, exactBudget.ExitCode);
        Assert.Equal(
            "Budget for swigerb/copilot-ledger @ feature/ledger: 56.00 AI credits. Spent so far: 56.00.\n",
            exactBudget.Out);
        CliRun exceededBudget = await harness.RunInAsync(
            repo, "budget", "set", "50");
        Assert.Equal(0, exceededBudget.ExitCode);
        Assert.Equal(
            """
            Budget for swigerb/copilot-ledger @ feature/ledger: 50.00 AI credits. Spent so far: 56.00.
            This branch is already over its budget.

            """.ReplaceLineEndings("\n"),
            exceededBudget.Out);
        Assert.Equal(
            "Budget for contoso/api @ main: 40.50 AI credits. Spent so far: 31.50.\n",
            (await harness.RunAsync(
                "budget",
                "set",
                "40.5",
                "--repo",
                "contoso/api",
                "--branch",
                "main")).Out);
        Assert.Equal(
            """
            contoso/api @ main: 31.50 of 40.50 AI credits
            swigerb/copilot-ledger @ feature/ledger: 56.00 of 50.00 AI credits (over budget)

            """.ReplaceLineEndings("\n"),
            (await harness.RunAsync("budget", "list")).Out);
        AssertJsonEqual(
            """{"budgets":[{"repository":"contoso/api","branch":"main","budgetAiCredits":40.5,"spentAiCredits":31.5,"exceeded":false,"setAt":"2026-11-21T12:00:00.0000000+00:00","alertedAt":null},{"repository":"swigerb/copilot-ledger","branch":"feature/ledger","budgetAiCredits":50,"spentAiCredits":56,"exceeded":true,"setAt":"2026-11-21T12:00:00.0000000+00:00","alertedAt":null}]}""",
            (await harness.RunAsync("budget", "list", "--json")).Out);
        Assert.Equal(
            "Cleared the budget for contoso/api @ main.\n",
            (await harness.RunAsync(
                "budget",
                "clear",
                "--repo",
                "contoso/api",
                "--branch",
                "main")).Out);
        Assert.Equal(
            "No budget is set for contoso/api @ main.\n",
            (await harness.RunAsync(
                "budget",
                "clear",
                "--repo",
                "contoso/api",
                "--branch",
                "main")).Out);

        foreach (string invalid in new[] { "0", "abc", "1000000.01", "0.001" })
        {
            CliRun run = await harness.RunInAsync(
                repo, "budget", "set", invalid);
            Assert.Equal(1, run.ExitCode);
            Assert.Contains(
                "Credits must be a number greater than 0 and at most 1000000 with at most 2 decimal places.",
                run.Error);
        }

        CliRun outside = await harness.RunAsync("budget", "set", "5");
        Assert.Equal(1, outside.ExitCode);
        Assert.Equal(
            "Not in a Git repository. Pass --repo and --branch.\n",
            outside.Error);
        CliRun incomplete = await harness.RunAsync(
            "budget", "clear", "--branch", "main");
        Assert.Equal(1, incomplete.ExitCode);
        Assert.Contains(
            "--repo and --branch must be used together.",
            incomplete.Error);
    }

    private static NewBreakerTrip VA() => new()
    {
        Rule = BreakerRule.CreditVelocity,
        TraceId = new string('1', 32),
        Repository = "swigerb/copilot-ledger",
        Branch = "feature/ledger",
        TrippedAt = T0.AddSeconds(180),
        Detail = "180.00 AI credits in 5 minutes",
        WindowCostUsd = 1.80m,
        WindowMinutes = 5,
    };

    private static NewBreakerTrip CB() => new()
    {
        Rule = BreakerRule.RetryChurn,
        TraceId = new string('2', 32),
        ConversationId = "conv-2",
        Repository = "swigerb/copilot-ledger",
        Branch = "main",
        TrippedAt = T0.AddSeconds(300),
        Detail = "5 failed tool calls in a row",
        FailureCount = 5,
    };

    private static async Task<SqliteLedgerStore> StoreAsync(CliHarness harness)
    {
        SqliteLedgerStore store = new(
            harness.Context.DatabasePath,
            harness.TimeProvider);
        await store.InitializeAsync();
        return store;
    }

    private static string FlagJson(string repo)
    {
        string root = JsonSerializer.Serialize(
            repo,
            new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
        return $$"""
            {
              "schemaVersion": 1,
              "repositoryRoot": {{root}},
              "openTrips": [
                {
                  "id": 1,
                  "rule": "credit_velocity",
                  "repository": "swigerb/copilot-ledger",
                  "branch": "feature/ledger",
                  "conversationId": null,
                  "traceId": "11111111111111111111111111111111",
                  "trippedAt": "2026-11-21T10:03:00.0000000+00:00",
                  "detail": "180.00 AI credits in 5 minutes",
                  "resetCommand": "gh-ledger breaker reset --id 1"
                }
              ]
            }

            """.ReplaceLineEndings("\n");
    }

    private static void AssertJsonEqual(string expected, string actual) =>
        Assert.True(
            JsonNode.DeepEquals(
                JsonNode.Parse(actual),
                JsonNode.Parse(expected)));
}
