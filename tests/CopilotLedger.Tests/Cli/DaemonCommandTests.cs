using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Cli;

public sealed class DaemonCommandTests
{
    [Fact]
    public async Task DM1_RunningDaemonIsExclusiveAndStopsByMarker()
    {
        using var temp = new TempDirectory();
        await using var harness = new CliHarness(temp.Path);
        await harness.StartInProcessDaemonAsync();
        string statePath = Path.Combine(temp.Path, "data", "daemon.json");
        using JsonDocument state = JsonDocument.Parse(await FileRetry.ReadAllTextAsync(statePath));
        JsonElement root = state.RootElement;
        Assert.Equal(Environment.ProcessId, root.GetProperty("processId").GetInt32());
        Assert.Equal("http://localhost:4318", root.GetProperty("endpoint").GetString());
        Assert.Equal("0.4.1", root.GetProperty("version").GetString());
        Assert.Equal(
            Path.Combine(temp.Path, "data", "ledger.db"),
            root.GetProperty("databasePath").GetString());

        CliRun start = await harness.RunAsync("daemon", "start");
        Assert.Equal(0, start.ExitCode);
        Assert.Equal(
            $"gh-ledger daemon is already running (pid {Environment.ProcessId}).\n",
            start.Out);
        CliRun duplicate = await harness.RunAsync("daemon", "run");
        Assert.Equal(1, duplicate.ExitCode);
        Assert.Contains("gh-ledger daemon is already running.", duplicate.Out);
        Assert.True(File.Exists(statePath));
        using JsonDocument stateAfterDuplicate = JsonDocument.Parse(
            await FileRetry.ReadAllTextAsync(statePath));
        Assert.Equal(
            Environment.ProcessId,
            stateAfterDuplicate.RootElement.GetProperty("processId").GetInt32());

        CliRun stop = await harness.RunAsync("daemon", "stop");
        Assert.Equal(0, stop.ExitCode);
        Assert.Equal("gh-ledger daemon stopped.\n", stop.Out);
        CliRun daemon = await harness.InProcessDaemon!;
        Assert.Equal(0, daemon.ExitCode);
        Assert.False(File.Exists(statePath));
        Assert.False(File.Exists(Path.Combine(temp.Path, "data", "daemon.stop")));
        CliRun stoppedAgain = await harness.RunAsync("daemon", "stop");
        Assert.Equal(0, stoppedAgain.ExitCode);
        Assert.Equal("gh-ledger daemon is not running.\n", stoppedAgain.Out);
    }

    [Fact]
    public async Task DM2_StartLaunchesTheDetachedCommand()
    {
        using var temp = new TempDirectory();
        await using var harness = new CliHarness(temp.Path);
        CliRun run = await harness.RunAsync("daemon", "start");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            $"gh-ledger daemon started (pid {Environment.ProcessId}) on http://localhost:4318.\n",
            run.Out);
        DetachedStart detached = Assert.Single(harness.Platform.DetachedStarts);
        Assert.Equal("/opt/gh-ledger/gh-ledger", detached.FileName);
        Assert.Equal(
            [
                "daemon",
                "run",
                "--port",
                "4318",
                "--log-file",
                Path.Combine(temp.Path, "data", "daemon.log"),
            ],
            detached.Arguments);
        Assert.Equal(Path.Combine(temp.Path, "data"), detached.WorkingDirectory);
    }

    [Fact]
    public async Task DM3_StartFailsWhenDetachedProcessExits()
    {
        using var temp = new TempDirectory();
        FakeCliPlatform platform = new()
        {
            StartDetachedHandler = (_, _, _) => 999999,
        };
        await using var harness = new CliHarness(temp.Path, platform);
        CliRun run = await harness.RunAsync("daemon", "start");

        Assert.Equal(1, run.ExitCode);
        Assert.Equal(
            $"gh-ledger daemon did not start; see {Path.Combine(temp.Path, "data", "daemon.log")}.\n",
            run.Error);
    }

    public static TheoryData<string, int> ProcessPaths => new()
    {
        { "/usr/bin/dotnet", 1 },
        { @"C:\tools\GH-LEDGER.EXE", 0 },
        { "/opt/bin/gh-ledger", 0 },
    };

    [Theory]
    [MemberData(nameof(ProcessPaths))]
    public async Task DM4_StartRequiresTheGhLedgerExecutable(
        string processPath,
        int expectedExit)
    {
        using var temp = new TempDirectory();
        FakeCliPlatform platform = new() { ProcessPath = processPath };
        await using var harness = new CliHarness(temp.Path, platform);
        CliRun run = await harness.RunAsync("daemon", "start");

        Assert.Equal(expectedExit, run.ExitCode);
        if (expectedExit == 1)
        {
            Assert.Equal(
                $"Run this command from the gh-ledger executable (current process: {processPath}).\n",
                run.Error);
        }
    }

    [Fact]
    public async Task DM5_InvalidOverlayDoesNotLeaveDaemonStateOrLock()
    {
        using var temp = new TempDirectory();
        await using var harness = new CliHarness(temp.Path);
        string data = Path.Combine(temp.Path, "data");
        Directory.CreateDirectory(data);
        string malformed = "{ \"schemaVersion\": 1,\n  \"foundryByok\": { \"models\": [ }\n}\n";
        await File.WriteAllTextAsync(Path.Combine(data, "rates.json"), malformed);

        CliRun run = await harness.RunAsync("daemon", "run");
        Assert.Equal(1, run.ExitCode);
        Assert.Contains(
            $"Invalid rates overlay: {Path.Combine(data, "rates.json")}: line 2, column 32: invalid JSON",
            run.Out);
        Assert.DoesNotContain("gh-ledger daemon stopped", run.Out);
        Assert.False(File.Exists(Path.Combine(data, "daemon.json")));
        CliRun stop = await harness.RunAsync("daemon", "stop");
        Assert.Equal(0, stop.ExitCode);
        Assert.Equal("gh-ledger daemon is not running.\n", stop.Out);
    }

    [Fact]
    public async Task DM6_LockedStopFileIsDeletedOnceReleased()
    {
        using var temp = new TempDirectory();
        await using var harness = new CliHarness(temp.Path);
        await harness.StartInProcessDaemonAsync();
        string stopPath = Path.Combine(temp.Path, "data", "daemon.stop");
        string statePath = Path.Combine(temp.Path, "data", "daemon.json");

        // Held without delete sharing, the stop file can't be deleted on Windows until it's released.
        using (FileStream held = new(stopPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }

        CliRun daemon = await harness.InProcessDaemon!.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, daemon.ExitCode);
        Assert.False(File.Exists(stopPath));
        Assert.False(File.Exists(statePath));
    }

    [Fact]
    public async Task DM7_IngestFailuresAreLoggedAtMostOnceAMinute()
    {
        using var temp = new TempDirectory();
        ManualTimeProvider clock = new(
            DateTimeOffset.Parse("2026-11-21T15:00:00Z", CultureInfo.InvariantCulture));
        await using var harness = new CliHarness(temp.Path, timeProvider: clock);
        await harness.StartInProcessDaemonAsync();

        // Without the spans table, every request fails in RecordAsync, as it would with a broken or full ledger.
        await using (SqliteConnection connection = new(
            $"Data Source={Path.Combine(temp.Path, "data", "ledger.db")}"))
        {
            await connection.OpenAsync();
            await using SqliteCommand drop = connection.CreateCommand();
            drop.CommandText = "DROP TABLE spans;";
            await drop.ExecuteNonQueryAsync();
        }

        using HttpClient client = harness.CreateDaemonClient();
        HttpStatusCode first = await PostTraceAsync(client);
        HttpStatusCode second = await PostTraceAsync(client);
        clock.Advance(TimeSpan.FromMinutes(1));
        HttpStatusCode third = await PostTraceAsync(client);

        // After the clock steps back, the next failure is logged at once.
        clock.Advance(TimeSpan.FromMinutes(-2));
        HttpStatusCode fourth = await PostTraceAsync(client);
        CliRun stop = await harness.RunAsync("daemon", "stop");
        CliRun daemon = await harness.InProcessDaemon!;

        Assert.Equal(
            [
                HttpStatusCode.ServiceUnavailable,
                HttpStatusCode.ServiceUnavailable,
                HttpStatusCode.ServiceUnavailable,
                HttpStatusCode.ServiceUnavailable,
            ],
            [first, second, third, fourth]);
        Assert.Equal(0, stop.ExitCode);
        Assert.Equal(0, daemon.ExitCode);
        Assert.Equal(
            [
                "2026-11-21T15:00:00.0000000+00:00 Warning: could not record spans: SqliteException: SQLite Error 1: 'no such table: spans'.",
                "2026-11-21T15:01:00.0000000+00:00 Warning: could not record spans: SqliteException: SQLite Error 1: 'no such table: spans'.",
                "2026-11-21T14:59:00.0000000+00:00 Warning: could not record spans: SqliteException: SQLite Error 1: 'no such table: spans'.",
            ],
            harness.DaemonOutput.Split('\n').Where(
                line => line.Contains(" Warning: could not record spans: ", StringComparison.Ordinal)));

        // The other lines are "daemon listening", "daemon stopped", and the empty string after the last line break.
        Assert.Equal(6, harness.DaemonOutput.Split('\n').Length);
    }

    private static async Task<HttpStatusCode> PostTraceAsync(HttpClient client)
    {
        using ByteArrayContent content = new(Encoding.UTF8.GetBytes(OtlpFixtures.CopilotChatTraceJson));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using HttpResponseMessage response = await client.PostAsync("/v1/traces", content);
        return response.StatusCode;
    }
}
