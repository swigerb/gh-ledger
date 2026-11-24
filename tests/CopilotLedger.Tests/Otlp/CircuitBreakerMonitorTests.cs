using System.Text.Encodings.Web;
using System.Text.Json;
using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Otlp.Attribution;
using CopilotLedger.Otlp.Breaker;
using CopilotLedger.Otlp.Decoding;
using CopilotLedger.Otlp.Ingestion;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using static CopilotLedger.Tests.TestSupport.BreakerTraces;
using static CopilotLedger.Tests.TestSupport.OtlpRequests;

namespace CopilotLedger.Tests.Otlp;

public sealed class CircuitBreakerMonitorTests
{
    private static readonly DateTimeOffset T0 =
        DateTimeOffset.FromUnixTimeMilliseconds(1795255200000);
    private static readonly DateTimeOffset NOW =
        new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task BM1_RunATripsRule2OnceOnRequest6()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(temp);

        for (int request = 1; request <= 5; request++)
        {
            await harness.Ingestor.IngestAsync(TraceHRequest(request));
            Assert.Empty(harness.Sink.Alerts);
            Assert.Empty(await harness.Store.GetOpenBreakerTripsAsync(BreakerTripFilter.All));
        }

        IngestResult sixth = await harness.Ingestor.IngestAsync(TraceHRequest(6));
        AssertResult(sixth, 4, 0, 2, 2);
        AssertTripH(Assert.Single(
            await harness.Store.GetOpenBreakerTripsAsync(BreakerTripFilter.All)),
            harness.Repo);
        BreakerAlert alert = Assert.Single(harness.Sink.Alerts);
        Assert.Equal(BreakerAlertKind.Trip, alert.Kind);
        Assert.Equal(1, alert.TripId);
        Assert.Equal("swigerb/copilot-ledger", alert.Repository);
        Assert.Equal("feature/ledger", alert.Branch);
        Assert.Equal("gh-ledger circuit breaker tripped", alert.Title);
        Assert.Equal(
            "swigerb/copilot-ledger @ feature/ledger: 5 failed tool calls in a row on one prompt. Run: gh-ledger breaker status",
            alert.Body);
        Assert.True(alert.ShowNotification);

        IngestResult seventh = await harness.Ingestor.IngestAsync(TraceHRequest(7));
        AssertResult(seventh, 6, 0, 3, 3);
        Assert.Single(harness.Sink.Alerts);
        Assert.Single(await harness.Store.GetOpenBreakerTripsAsync(BreakerTripFilter.All));
        IngestResult duplicate = await harness.Ingestor.IngestAsync(TraceHRequest(6));
        Assert.Equal(0, duplicate.RecordedSpans);
        Assert.Single(harness.Sink.Alerts);

        LedgerRollupRow model = Assert.Single(await harness.Store.GetRollupAsync(
            LedgerGrouping.Model,
            LedgerFilter.All));
        Assert.Equal("mai-code-2-flash", model.Model);
        Assert.Equal(14, model.SpanCount);
        Assert.Equal(0.372181m, model.AiCredits);
        Assert.Equal(0.00372181m, model.AiCreditsUsd);
        LedgerRollupRow branch = Assert.Single(await harness.Store.GetRollupAsync(
            LedgerGrouping.Branch,
            LedgerFilter.All));
        Assert.Equal("swigerb/copilot-ledger", branch.Repository);
        Assert.Equal("feature/ledger", branch.Branch);
        Assert.False(Directory.Exists(Path.Combine(harness.Repo, ".copilot-ledger")));
        Assert.Empty(harness.Sink.Warnings);
    }

    [Fact]
    public async Task BM2_NewCommitResetsTheCount()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(temp);
        for (int request = 1; request <= 4; request++)
        {
            await harness.Ingestor.IngestAsync(TraceHRequest(request));
        }

        File.WriteAllText(
            Path.Combine(harness.Repo, ".git", "refs", "heads", "feature", "ledger"),
            GitFixture.Sha2 + "\n");
        for (int request = 5; request <= 7; request++)
        {
            await harness.Ingestor.IngestAsync(TraceHRequest(request));
        }

        Assert.Empty(harness.Sink.Alerts);
        Assert.Empty(await harness.Store.GetOpenBreakerTripsAsync(BreakerTripFilter.All));
        await using SqliteConnection connection = await OpenAsync(harness.DatabasePath);
        Assert.Equal(
            7L,
            await ScalarAsync(
                connection,
                $"SELECT COUNT(*) FROM spans WHERE head_commit_sha = '{GitFixture.Sha2}';"));
        Assert.Equal(
            7L,
            await ScalarAsync(
                connection,
                $"SELECT COUNT(*) FROM spans WHERE head_commit_sha = '{GitFixture.Sha1}';"));
    }

    [Fact]
    public async Task BM3_HigherThreshold()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(
            temp,
            BreakerSettings.Default with { ChurnFailures = 6 });
        for (int request = 1; request <= 7; request++)
        {
            await harness.Ingestor.IngestAsync(TraceHRequest(request));
        }

        Assert.Empty(harness.Sink.Alerts);
        Assert.Empty(await harness.Store.GetOpenBreakerTripsAsync(BreakerTripFilter.All));
    }

    [Fact]
    public async Task BM4_BudgetThenVelocity()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(temp);
        await harness.Store.SetBranchBudgetAsync(
            "swigerb/copilot-ledger",
            "feature/ledger",
            100m);

        await harness.Ingestor.IngestAsync(TraceVRequest(harness.Repo, 1));
        Assert.Empty(harness.Sink.Alerts);
        await harness.Ingestor.IngestAsync(TraceVRequest(harness.Repo, 2));
        BreakerAlert budget = Assert.Single(harness.Sink.Alerts);
        AssertBudgetAlert(
            budget,
            "swigerb/copilot-ledger @ feature/ledger: 120.00 of 100.00 AI credits spent. Run: gh-ledger budget list");
        BranchBudgetRow budgetRow = Assert.Single(
            await harness.Store.GetBranchBudgetsAsync(
                "swigerb/copilot-ledger",
                "feature/ledger"));
        Assert.Equal(NOW, budgetRow.AlertedAt);

        await harness.Ingestor.IngestAsync(TraceVRequest(harness.Repo, 3));
        Assert.Equal(2, harness.Sink.Alerts.Count);
        BreakerAlert trip = harness.Sink.Alerts[1];
        AssertTripAlert(
            trip,
            1,
            "swigerb/copilot-ledger @ feature/ledger: 180.00 AI credits in 5 minutes on one prompt. Run: gh-ledger breaker status");
        BreakerTrip open = Assert.Single(
            await harness.Store.GetOpenBreakerTripsAsync(BreakerTripFilter.All));
        AssertVelocityTrip(open, harness.Repo, T0.AddSeconds(180), 1.80m);

        await harness.Ingestor.IngestAsync(TraceVRequest(harness.Repo, 3));
        Assert.Equal(2, harness.Sink.Alerts.Count);
    }

    [Fact]
    public async Task BM5_WindowSlides()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(temp);
        await harness.Ingestor.IngestAsync(TraceWRequest(harness.Repo));

        Assert.Empty(harness.Sink.Alerts);
        Assert.Empty(await harness.Store.GetOpenBreakerTripsAsync(BreakerTripFilter.All));
    }

    [Fact]
    public async Task BM6_SettingsChangeTheTrip()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(
            temp,
            BreakerSettings.Default with
            {
                VelocityCredits = 100m,
                Notifications = false,
            });
        await harness.Ingestor.IngestAsync(TraceVRequest(harness.Repo, 1, 2));

        BreakerAlert alert = Assert.Single(harness.Sink.Alerts);
        Assert.Equal(BreakerAlertKind.Trip, alert.Kind);
        Assert.Equal(
            "swigerb/copilot-ledger @ feature/ledger: 120.00 AI credits in 5 minutes on one prompt. Run: gh-ledger breaker status",
            alert.Body);
        Assert.False(alert.ShowNotification);
        BreakerTrip trip = Assert.Single(
            await harness.Store.GetOpenBreakerTripsAsync(BreakerTripFilter.All));
        Assert.Equal(T0.AddSeconds(120), trip.TrippedAt);
        Assert.Equal(1.20m, trip.WindowCostUsd);
    }

    [Fact]
    public async Task BM7_FlagFile()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(
            temp,
            BreakerSettings.Default with { FlagFile = true });
        for (int request = 1; request <= 6; request++)
        {
            await harness.Ingestor.IngestAsync(TraceHRequest(request));
        }

        string flag = Path.Combine(harness.Repo, ".copilot-ledger", "CIRCUIT_OPEN");
        string root = JsonSerializer.Serialize(
            harness.Repo,
            new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
        string expected = $$"""
            {
              "schemaVersion": 1,
              "repositoryRoot": {{root}},
              "openTrips": [
                {
                  "id": 1,
                  "rule": "retry_churn",
                  "repository": "swigerb/copilot-ledger",
                  "branch": "feature/ledger",
                  "conversationId": "982f73ad-18f4-49ef-a1f9-70748fa013fb",
                  "traceId": "0a000000000000000000000000000001",
                  "trippedAt": "2026-11-21T00:17:17.2600000+00:00",
                  "detail": "5 failed tool calls in a row",
                  "resetCommand": "gh-ledger breaker reset --id 1"
                }
              ]
            }

            """;
        Assert.Equal(
            expected.ReplaceLineEndings("\n"),
            await FileRetry.ReadAllTextAsync(flag));
        Assert.Equal(
            "*\n",
            await FileRetry.ReadAllTextAsync(
                Path.Combine(harness.Repo, ".copilot-ledger", ".gitignore")));

        await harness.Store.ResetBreakerTripsAsync(BreakerTripFilter.All);
        await CircuitFlagFile.SyncAsync(harness.Store, harness.Repo);
        Assert.False(File.Exists(flag));
        Assert.True(File.Exists(Path.Combine(harness.Repo, ".copilot-ledger", ".gitignore")));
        string missing = Path.Combine(temp.Path, "missing");
        await CircuitFlagFile.SyncAsync(harness.Store, missing);
        Assert.False(Directory.Exists(missing));
        Assert.Empty(harness.Sink.Warnings);
    }

    [Fact]
    public async Task BM8_FlagFileCannotBeWritten()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(
            temp,
            BreakerSettings.Default with { FlagFile = true });
        string directory = Path.Combine(harness.Repo, ".copilot-ledger");
        File.WriteAllText(directory, "");
        for (int request = 1; request <= 6; request++)
        {
            await harness.Ingestor.IngestAsync(TraceHRequest(request));
        }

        AssertTripH(Assert.Single(
            await harness.Store.GetOpenBreakerTripsAsync(BreakerTripFilter.All)),
            harness.Repo);
        Assert.Equal(
            BreakerAlertKind.Trip,
            Assert.Single(harness.Sink.Alerts).Kind);
        string warning = Assert.Single(harness.Sink.Warnings);
        Assert.StartsWith(
            $"could not update {Path.Combine(directory, "CIRCUIT_OPEN")}: ",
            warning);
    }

    [Fact]
    public async Task BM9_NoMonitor()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(temp, monitor: false);
        for (int request = 1; request <= 7; request++)
        {
            await harness.Ingestor.IngestAsync(TraceHRequest(request));
        }

        Assert.Empty(await harness.Store.GetOpenBreakerTripsAsync(BreakerTripFilter.All));
        await using SqliteConnection connection = await OpenAsync(harness.DatabasePath);
        Assert.Equal(
            0L,
            await ScalarAsync(connection, "SELECT COUNT(*) FROM breaker_trips;"));
    }

    [Fact]
    public async Task BM10_TripWithoutRepository()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(
            temp,
            BreakerSettings.Default with { FlagFile = true, ChurnFailures = 2 });
        string traceId = "0d000000000000000000000000000001";
        await harness.Ingestor.IngestAsync(Request(
            CopilotResource,
            Span(
                traceId,
                "0d000000000000a1",
                name: "execute_tool powershell",
                startNs: 1795255201000000000,
                endNs: 1795255202000000000,
                attributes:
                [
                    Str("gen_ai.operation.name", "execute_tool"),
                    Str("gen_ai.tool.name", "powershell"),
                    Int("process.exit.code", 1),
                ]),
            Span(
                traceId,
                "0d000000000000a2",
                name: "execute_tool powershell",
                startNs: 1795255202000000000,
                endNs: 1795255203000000000,
                attributes:
                [
                    Str("gen_ai.operation.name", "execute_tool"),
                    Str("gen_ai.tool.name", "powershell"),
                    Int("process.exit.code", 1),
                ])));

        BreakerTrip trip = Assert.Single(
            await harness.Store.GetOpenBreakerTripsAsync(BreakerTripFilter.All));
        Assert.Equal("(none)", trip.Repository);
        Assert.Equal("(none)", trip.Branch);
        Assert.Null(trip.RepositoryRoot);
        Assert.Null(trip.ConversationId);
        Assert.Equal(T0.AddSeconds(2), trip.TrippedAt);
        Assert.Equal("2 failed tool calls in a row", trip.Detail);
        Assert.Equal(2, trip.FailureCount);
        BreakerAlert alert = Assert.Single(harness.Sink.Alerts);
        Assert.Equal(
            "(none) @ (none): 2 failed tool calls in a row on one prompt. Run: gh-ledger breaker status",
            alert.Body);
        Assert.Empty(harness.Sink.Warnings);
        Assert.Empty(Directory.GetDirectories(
            temp.Path,
            ".copilot-ledger",
            SearchOption.AllDirectories));
    }

    [Fact]
    public async Task BM11_SpendExactlyAtBudgetDoesNotAlert()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(temp);
        await harness.Store.SetBranchBudgetAsync(
            "swigerb/copilot-ledger",
            "feature/ledger",
            120m);

        await harness.Ingestor.IngestAsync(TraceVRequest(harness.Repo, 1));
        Assert.Empty(harness.Sink.Alerts);
        await harness.Ingestor.IngestAsync(TraceVRequest(harness.Repo, 2));
        Assert.Empty(harness.Sink.Alerts);
        Assert.Empty(await harness.Store.GetOpenBreakerTripsAsync(BreakerTripFilter.All));
        BranchBudgetRow atBudget = Assert.Single(
            await harness.Store.GetBranchBudgetsAsync(
                "swigerb/copilot-ledger",
                "feature/ledger"));
        Assert.False(atBudget.Exceeded);
        Assert.Null(atBudget.AlertedAt);

        await harness.Ingestor.IngestAsync(TraceVRequest(harness.Repo, 3));
        Assert.Collection(
            harness.Sink.Alerts,
            alert => AssertTripAlert(
                alert,
                1,
                "swigerb/copilot-ledger @ feature/ledger: 180.00 AI credits in 5 minutes on one prompt. Run: gh-ledger breaker status"),
            alert => AssertBudgetAlert(
                alert,
                "swigerb/copilot-ledger @ feature/ledger: 180.00 of 120.00 AI credits spent. Run: gh-ledger budget list"));
        BranchBudgetRow exceeded = Assert.Single(
            await harness.Store.GetBranchBudgetsAsync(
                "swigerb/copilot-ledger",
                "feature/ledger"));
        Assert.Equal(NOW, exceeded.AlertedAt);
    }

    [Fact]
    public async Task BM12_CancellationAfterATripStillWritesTheFlagFile()
    {
        using TempDirectory temp = new();
        Harness harness = await Harness.CreateAsync(
            temp,
            BreakerSettings.Default with { FlagFile = true },
            monitor: false);
        for (int request = 1; request <= 6; request++)
        {
            await harness.Ingestor.IngestAsync(TraceHRequest(request));
        }

        // The sink cancels the token as the trip is published, as a client that disconnects right then would.
        using CancellationTokenSource aborted = new();
        CancelingAlertSink sink = new(aborted);
        CircuitBreakerMonitor breaker = new(harness.Store, sink);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => breaker.EvaluateAsync([TraceH], aborted.Token));

        IReadOnlyList<BreakerTrip> trips =
            await harness.Store.GetOpenBreakerTripsAsync(BreakerTripFilter.All);
        AssertTripH(Assert.Single(trips), harness.Repo);
        Assert.Equal(BreakerAlertKind.Trip, Assert.Single(sink.Alerts).Kind);
        Assert.Empty(sink.Warnings);
        string directory = Path.Combine(harness.Repo, ".copilot-ledger");
        Assert.Equal(
            CircuitFlagFile.Render(harness.Repo, trips),
            await FileRetry.ReadAllTextAsync(Path.Combine(directory, "CIRCUIT_OPEN")));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    private static void AssertTripH(BreakerTrip trip, string repo)
    {
        Assert.Equal(1, trip.Id);
        Assert.Equal(BreakerRule.RetryChurn, trip.Rule);
        Assert.Equal(TraceH, trip.TraceId);
        Assert.Equal(ConversationH, trip.ConversationId);
        Assert.Equal("swigerb/copilot-ledger", trip.Repository);
        Assert.Equal("feature/ledger", trip.Branch);
        Assert.Equal(repo, trip.RepositoryRoot);
        Assert.Equal(
            DateTimeOffset.FromUnixTimeMilliseconds(1795220237260),
            trip.TrippedAt);
        Assert.Equal("5 failed tool calls in a row", trip.Detail);
        Assert.Null(trip.WindowCostUsd);
        Assert.Null(trip.WindowMinutes);
        Assert.Equal(5, trip.FailureCount);
        Assert.Equal(NOW, trip.RecordedAt);
        Assert.Null(trip.NotifiedAt);
        Assert.Null(trip.ResetAt);
    }

    private static void AssertVelocityTrip(
        BreakerTrip trip,
        string repo,
        DateTimeOffset trippedAt,
        decimal windowCostUsd)
    {
        Assert.Equal(1, trip.Id);
        Assert.Equal(BreakerRule.CreditVelocity, trip.Rule);
        Assert.Equal(TraceV, trip.TraceId);
        Assert.Equal("conv-v", trip.ConversationId);
        Assert.Equal("swigerb/copilot-ledger", trip.Repository);
        Assert.Equal("feature/ledger", trip.Branch);
        Assert.Equal(repo, trip.RepositoryRoot);
        Assert.Equal(trippedAt, trip.TrippedAt);
        Assert.Equal("180.00 AI credits in 5 minutes", trip.Detail);
        Assert.Equal(windowCostUsd, trip.WindowCostUsd);
        Assert.Equal(5, trip.WindowMinutes);
        Assert.Null(trip.FailureCount);
        Assert.Equal(NOW, trip.RecordedAt);
    }

    private static void AssertTripAlert(BreakerAlert alert, long id, string body)
    {
        Assert.Equal(BreakerAlertKind.Trip, alert.Kind);
        Assert.Equal(id, alert.TripId);
        Assert.Equal("swigerb/copilot-ledger", alert.Repository);
        Assert.Equal("feature/ledger", alert.Branch);
        Assert.Equal("gh-ledger circuit breaker tripped", alert.Title);
        Assert.Equal(body, alert.Body);
        Assert.True(alert.ShowNotification);
    }

    private static void AssertBudgetAlert(BreakerAlert alert, string body)
    {
        Assert.Equal(BreakerAlertKind.Budget, alert.Kind);
        Assert.Null(alert.TripId);
        Assert.Equal("swigerb/copilot-ledger", alert.Repository);
        Assert.Equal("feature/ledger", alert.Branch);
        Assert.Equal("gh-ledger branch budget exceeded", alert.Title);
        Assert.Equal(body, alert.Body);
        Assert.True(alert.ShowNotification);
    }

    private static void AssertResult(
        IngestResult result,
        int received,
        int rejected,
        int inference,
        int recorded)
    {
        Assert.Equal(received, result.ReceivedSpans);
        Assert.Equal(rejected, result.RejectedSpans);
        Assert.Equal(inference, result.InferenceSpans);
        Assert.Equal(recorded, result.RecordedSpans);
    }

    private static async Task<SqliteConnection> OpenAsync(string path)
    {
        SqliteConnection connection = new($"Data Source={path}");
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<long> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private sealed class Harness
    {
        private Harness(
            string databasePath,
            SqliteLedgerStore store,
            LedgerIngestor ingestor,
            RecordingAlertSink sink,
            string repo)
        {
            DatabasePath = databasePath;
            Store = store;
            Ingestor = ingestor;
            Sink = sink;
            Repo = repo;
        }

        public string DatabasePath { get; }
        public SqliteLedgerStore Store { get; }
        public LedgerIngestor Ingestor { get; }
        public RecordingAlertSink Sink { get; }
        public string Repo { get; }

        public static async Task<Harness> CreateAsync(
            TempDirectory temp,
            BreakerSettings? settings = null,
            bool monitor = true)
        {
            string databasePath = Path.Combine(temp.Path, "data", "ledger.db");
            SqliteLedgerStore store = new(databasePath, new ManualTimeProvider(NOW));
            await store.InitializeAsync();
            if (settings is not null)
            {
                await store.UpdateBreakerSettingsAsync(settings);
            }

            string repo = GitFixture.Create(temp.Path);
            string home = Path.Combine(temp.Path, "copilot-home");
            string workspace = Path.Combine(
                home,
                "session-state",
                ConversationH,
                "workspace.yaml");
            Directory.CreateDirectory(Path.GetDirectoryName(workspace)!);
            File.WriteAllText(workspace, $"cwd: {repo}\n");
            RecordingAlertSink sink = new();
            CircuitBreakerMonitor breaker = new(store, sink);
            IGitContextResolver resolver =
                new FileSystemGitContextResolver(ceilingDirectory: temp.Path);
            LedgerIngestor ingestor = monitor
                ? new LedgerIngestor(
                    store,
                    JsonRateCardCalculator.LoadEmbedded(),
                    resolver,
                    new CopilotSessionWorkspaceLocator(home),
                    breaker)
                : new LedgerIngestor(
                    store,
                    JsonRateCardCalculator.LoadEmbedded(),
                    resolver,
                    new CopilotSessionWorkspaceLocator(home));
            return new Harness(databasePath, store, ingestor, sink, repo);
        }
    }

    // Records alerts and warnings, and cancels source when an alert is published.
    private sealed class CancelingAlertSink(CancellationTokenSource source) : IBreakerAlertSink
    {
        public List<BreakerAlert> Alerts { get; } = [];
        public List<string> Warnings { get; } = [];

        public void Publish(BreakerAlert alert)
        {
            Alerts.Add(alert);
            source.Cancel();
        }

        public void Warn(string message) => Warnings.Add(message);
    }
}
