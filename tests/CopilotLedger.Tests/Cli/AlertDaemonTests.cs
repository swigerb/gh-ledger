using System.ComponentModel;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using CopilotLedger.Cli;
using CopilotLedger.Core.Breaker;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

public sealed class AlertDaemonTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-11-21T12:00:00Z", CultureInfo.InvariantCulture);
    private const string AlertA =
        "ALERT gh-ledger circuit breaker tripped: swigerb/copilot-ledger @ feature/a: 5 failed tool calls in a row on one prompt. Run: gh-ledger breaker status";

    [Fact]
    public async Task AD1_EndToEndOnWindows()
    {
        using TempDirectory temp = new();
        string repo = GitFixture.Create(temp.Path);
        FakeCliPlatform platform = Windows();
        await using CliHarness harness = new(temp.Path, platform);

        Assert.Equal(
            0,
            (await harness.RunAsync(
                "breaker", "config", "--flag-file", "on")).ExitCode);
        await harness.StartInProcessDaemonAsync();
        using HttpClient client = harness.CreateDaemonClient();
        using StringContent content = new(
            BreakerTraces.TraceVJson(repo),
            new MediaTypeHeaderValue("application/json"));
        using HttpResponseMessage response =
            await client.PostAsync("/v1/traces", content);
        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal("{}", await response.Content.ReadAsStringAsync());

        string flag = Path.Combine(repo, ".copilot-ledger", "CIRCUIT_OPEN");
        Assert.True(File.Exists(flag));
        SqliteLedgerStore store = new(
            harness.Context.DatabasePath,
            TimeProvider.System);
        await store.InitializeAsync();
        await WaitUntilAsync(async () =>
        {
            IReadOnlyList<BreakerTrip> trips =
                await store.GetOpenBreakerTripsAsync(BreakerTripFilter.All);
            return trips.Count == 1 && trips[0].NotifiedAt is not null;
        });
        CliRun status = await harness.RunAsync("breaker", "status");
        Assert.Equal(0, status.ExitCode);
        Assert.StartsWith(
            "Circuit breaker: open (1 trip)\n",
            status.Out);
        await StopAsync(harness);

        var process = Assert.Single(platform.ProcessRuns);
        Assert.Equal(
            @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
            process.FileName);
        Assert.Equal(
            [
                "-NoProfile",
                "-NonInteractive",
                "-Command",
                Script(Path.Combine(temp.Path, "data", "alert-toast.xml")),
            ],
            process.Arguments);
        Assert.Equal(
            "<toast><visual><binding template=\"ToastGeneric\"><text>gh-ledger circuit breaker tripped</text><text>swigerb/copilot-ledger @ feature/ledger: 180.00 AI credits in 5 minutes on one prompt. Run: gh-ledger breaker status</text></binding></visual></toast>",
            await FileRetry.ReadAllTextAsync(
                Path.Combine(temp.Path, "data", "alert-toast.xml")));
        Assert.Contains(
            "ALERT gh-ledger circuit breaker tripped: swigerb/copilot-ledger @ feature/ledger: 180.00 AI credits in 5 minutes on one prompt. Run: gh-ledger breaker status",
            harness.DaemonOutput);

        CliRun reset = await harness.RunInAsync(repo, "breaker", "reset");
        Assert.Equal("Reset 1 trip.\n", reset.Out);
        Assert.False(File.Exists(flag));
        Assert.True(
            File.Exists(Path.Combine(repo, ".copilot-ledger", ".gitignore")));
        CliRun closed = await harness.RunAsync("breaker", "status");
        Assert.Equal(0, closed.ExitCode);
        Assert.StartsWith(
            "Circuit breaker: closed\n",
            closed.Out);
    }

    [Fact]
    public async Task AD2_RestartResendsRecentAlerts()
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = new() { IsMacOS = true };
        ManualTimeProvider clock = new(Now);
        await using CliHarness harness = new(temp.Path, platform, clock);
        string repo = Path.Combine(temp.Path, "repo");
        string api = Path.Combine(temp.Path, "api");

        SqliteLedgerStore recent = await StoreAsync(temp.Path, Now.AddMinutes(-30));
        await LedgerSeeds.SeedChainsAsync(recent, repo, api);
        await RecordTripAsync(recent, '1', "feature/a", Now.AddMinutes(-30));
        SqliteLedgerStore old = await StoreAsync(temp.Path, Now.AddHours(-2));
        await RecordTripAsync(old, '2', "feature/b", Now.AddHours(-2));
        SqliteLedgerStore notified = await StoreAsync(
            temp.Path,
            Now.AddMinutes(-15));
        await RecordTripAsync(notified, '3', "feature/c", Now.AddMinutes(-15));
        Assert.True(await notified.MarkBreakerTripNotifiedAsync(3));
        SqliteLedgerStore reset = await StoreAsync(
            temp.Path,
            Now.AddMinutes(-10));
        await RecordTripAsync(reset, '4', "feature/d", Now.AddMinutes(-10));
        await reset.ResetBreakerTripsAsync(new BreakerTripFilter { Id = 4 });
        SqliteLedgerStore budget = await StoreAsync(
            temp.Path,
            Now.AddMinutes(-20));
        await budget.SetBranchBudgetAsync(
            "swigerb/copilot-ledger",
            "feature/ledger",
            50m);
        Assert.True(
            await budget.MarkBranchBudgetAlertedAsync(
                "swigerb/copilot-ledger",
                "feature/ledger"));

        await harness.StartInProcessDaemonAsync();
        SqliteLedgerStore atNow = await StoreAsync(temp.Path, Now);
        await WaitUntilAsync(async () =>
        {
            BreakerTrip? trip = (await atNow.GetOpenBreakerTripsAsync(
                new BreakerTripFilter { Id = 1 })).SingleOrDefault();
            BranchBudgetRow row = Assert.Single(
                await atNow.GetBranchBudgetsAsync(
                    "swigerb/copilot-ledger",
                    "feature/ledger"));
            return trip?.NotifiedAt is not null && row.NotifiedAt is not null;
        });
        await StopAsync(harness);

        Assert.Collection(
            platform.ProcessRuns,
            trip =>
            {
                Assert.Equal("/usr/bin/osascript", trip.FileName);
                Assert.Equal(
                    [
                        "-e",
                        "on run argv",
                        "-e",
                        "display notification (item 2 of argv) with title (item 1 of argv)",
                        "-e",
                        "end run",
                        "gh-ledger circuit breaker tripped",
                        "swigerb/copilot-ledger @ feature/a: 5 failed tool calls in a row on one prompt. Run: gh-ledger breaker status",
                    ],
                    trip.Arguments);
            },
            budgetRun =>
            {
                Assert.Equal("/usr/bin/osascript", budgetRun.FileName);
                Assert.Equal(
                    [
                        "-e",
                        "on run argv",
                        "-e",
                        "display notification (item 2 of argv) with title (item 1 of argv)",
                        "-e",
                        "end run",
                        "gh-ledger branch budget exceeded",
                        "swigerb/copilot-ledger @ feature/ledger: 56.00 of 50.00 AI credits spent. Run: gh-ledger budget list",
                    ],
                    budgetRun.Arguments);
            });
        Assert.Equal(
            Now,
            Assert.Single(
                await atNow.GetOpenBreakerTripsAsync(
                    new BreakerTripFilter { Id = 1 })).NotifiedAt);
        Assert.Null(
            Assert.Single(
                await atNow.GetOpenBreakerTripsAsync(
                    new BreakerTripFilter { Id = 2 })).NotifiedAt);
        Assert.Equal(
            Now,
            Assert.Single(
                await atNow.GetBranchBudgetsAsync(
                    "swigerb/copilot-ledger",
                    "feature/ledger")).NotifiedAt);
        Assert.Contains(AlertA, harness.DaemonOutput);
        Assert.Contains(
            "ALERT gh-ledger branch budget exceeded: swigerb/copilot-ledger @ feature/ledger: 56.00 of 50.00 AI credits spent. Run: gh-ledger budget list",
            harness.DaemonOutput);
        Assert.DoesNotContain("feature/b", harness.DaemonOutput);
        Assert.DoesNotContain("feature/c", harness.DaemonOutput);
        Assert.DoesNotContain("feature/d", harness.DaemonOutput);
    }

    public static TheoryData<string> NotificationCases => new()
    {
        "linux-notify-send",
        "linux-missing",
        "notifications-off",
        "windows-exit-code",
        "windows-throws",
        "windows-quotes",
    };

    [Theory]
    [MemberData(nameof(NotificationCases))]
    public async Task AD3_NotificationOutcomes(string caseName)
    {
        using TempDirectory temp = new();
        string harnessRoot = caseName == "windows-quotes"
            ? Path.Combine(temp.Path, "it's O\u2019Brien")
            : temp.Path;
        FakeCliPlatform platform = caseName switch
        {
            "linux-notify-send" => new FakeCliPlatform
            {
                PathExecutables =
                {
                    ["notify-send"] = "/usr/bin/notify-send",
                },
            },
            "linux-missing" => new FakeCliPlatform(),
            "notifications-off" => Windows(),
            "windows-exit-code" => Windows(
                (_, _, _) =>
                    Task.FromResult(new ProcessRunResult(1, string.Empty, "boom\n"))),
            "windows-throws" => Windows(
                (_, _, _) => throw new Win32Exception(
                    2,
                    "The system cannot find the file specified.")),
            "windows-quotes" => Windows(),
            _ => throw new ArgumentOutOfRangeException(nameof(caseName)),
        };
        await using CliHarness harness = new(
            harnessRoot,
            platform,
            new ManualTimeProvider(Now));
        SqliteLedgerStore store = await StoreAsync(harnessRoot, Now);
        await RecordTripAsync(store, '1', "feature/a", Now);
        if (caseName == "notifications-off")
        {
            Assert.Equal(
                0,
                (await harness.RunAsync(
                    "breaker",
                    "config",
                    "--notifications",
                    "off")).ExitCode);
        }

        await harness.StartInProcessDaemonAsync();
        await WaitUntilAsync(async () =>
            Assert.Single(
                await store.GetOpenBreakerTripsAsync(
                    new BreakerTripFilter { Id = 1 })).NotifiedAt is not null);
        BreakerTrip notified = Assert.Single(
            await store.GetOpenBreakerTripsAsync(
                new BreakerTripFilter { Id = 1 }));
        Assert.Equal(Now, notified.NotifiedAt);
        await StopAsync(harness);
        Assert.Contains(AlertA, harness.DaemonOutput);

        switch (caseName)
        {
            case "linux-notify-send":
                var linux = Assert.Single(platform.ProcessRuns);
                Assert.Equal("/usr/bin/notify-send", linux.FileName);
                Assert.Equal(
                    [
                        "--app-name=CopilotLedger",
                        "gh-ledger circuit breaker tripped",
                        "swigerb/copilot-ledger @ feature/a: 5 failed tool calls in a row on one prompt. Run: gh-ledger breaker status",
                    ],
                    linux.Arguments);
                break;
            case "linux-missing":
                Assert.Empty(platform.ProcessRuns);
                Assert.Contains(
                    "Notification skipped: notify-send was not found.",
                    harness.DaemonOutput);
                break;
            case "notifications-off":
                Assert.Empty(platform.ProcessRuns);
                break;
            case "windows-exit-code":
                Assert.Contains(
                    @"Notification failed: C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe exited with code 1: boom",
                    harness.DaemonOutput);
                break;
            case "windows-throws":
                Assert.Contains(
                    "Notification failed: The system cannot find the file specified.",
                    harness.DaemonOutput);
                break;
            case "windows-quotes":
                var windows = Assert.Single(platform.ProcessRuns);
                Assert.Equal(
                    @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
                    windows.FileName);
                string xmlPath = Path.Combine(
                    harnessRoot,
                    "data",
                    "alert-toast.xml");
                Assert.Equal(
                    ["-NoProfile", "-NonInteractive", "-Command", Script(xmlPath)],
                    windows.Arguments);
                Assert.Contains("it''s O\u2019\u2019Brien", Script(xmlPath));
                break;
        }
    }

    private static FakeCliPlatform Windows(
        Func<string, IReadOnlyList<string>, CancellationToken, Task<ProcessRunResult>>?
            runHandler = null)
    {
        FakeCliPlatform platform = new()
        {
            IsWindows = true,
            ProcessPath = @"C:\tools\gh-ledger.exe",
            ProcessVariables =
            {
                ["SystemRoot"] = @"C:\Windows",
            },
            RunHandler = runHandler,
        };
        return platform;
    }

    private static async Task<SqliteLedgerStore> StoreAsync(
        string root,
        DateTimeOffset now)
    {
        SqliteLedgerStore store = new(
            Path.Combine(root, "data", "ledger.db"),
            new ManualTimeProvider(now));
        await store.InitializeAsync();
        return store;
    }

    private static async Task RecordTripAsync(
        SqliteLedgerStore store,
        char id,
        string branch,
        DateTimeOffset trippedAt) =>
        Assert.Equal(
            id - '0',
            await store.RecordBreakerTripAsync(new NewBreakerTrip
            {
                Rule = BreakerRule.RetryChurn,
                TraceId = new string(id, 32),
                Repository = "swigerb/copilot-ledger",
                Branch = branch,
                TrippedAt = trippedAt,
                Detail = "5 failed tool calls in a row",
                FailureCount = 5,
            }));

    private static async Task StopAsync(CliHarness harness)
    {
        Assert.Equal(
            0,
            (await harness.RunAsync("daemon", "stop")).ExitCode);
        Assert.Equal(0, (await harness.InProcessDaemon!).ExitCode);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        if (!await condition())
        {
            throw new TimeoutException();
        }
    }

    private static string Script(string path)
    {
        string escapedPath = path
            .Replace("'", "''", StringComparison.Ordinal)
            .Replace("\u2018", "\u2018\u2018", StringComparison.Ordinal)
            .Replace("\u2019", "\u2019\u2019", StringComparison.Ordinal)
            .Replace("\u201A", "\u201A\u201A", StringComparison.Ordinal)
            .Replace("\u201B", "\u201B\u201B", StringComparison.Ordinal);
        return
            """$ErrorActionPreference='Stop'; [void][Windows.UI.Notifications.ToastNotificationManager,Windows.UI.Notifications,ContentType=WindowsRuntime]; [void][Windows.Data.Xml.Dom.XmlDocument,Windows.Data.Xml.Dom,ContentType=WindowsRuntime]; $x=New-Object Windows.Data.Xml.Dom.XmlDocument; $x.LoadXml([IO.File]::ReadAllText('XMLPATH')); $t=[Windows.UI.Notifications.ToastNotification]::new($x); $t.Tag='copilot-ledger'; $t.Group='alerts'; [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}\WindowsPowerShell\v1.0\powershell.exe').Show($t)"""
                .Replace("XMLPATH", escapedPath, StringComparison.Ordinal);
    }
}
