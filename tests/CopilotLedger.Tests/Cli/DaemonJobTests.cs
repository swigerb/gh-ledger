using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using CopilotLedger.Cli;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

public sealed class DaemonJobTests
{
    private const string Refusal =
        "gh-ledger daemon not started: this terminal runs in a Windows job that stops the processes it started when it closes, so the daemon would stop with it.";
    private const string RegularTerminal =
        "Run gh-ledger daemon start from a regular terminal.";
    private const string SignInItem =
        "Run gh-ledger daemon start from a regular terminal, or sign out and back in to start it from the sign-in item.";
    private const string DaemonWarning =
        " Warning: this daemon runs in a Windows job that stops its processes when the job closes, so it stops when the program that started it closes. Run gh-ledger daemon start from a regular terminal.";

    [Fact]
    public async Task DJ1_Start_Refuses_InAKillOnCloseJob()
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = new() { JobState = new(true, 0x2000) };
        await using CliHarness harness = new(temp.Path, platform);

        CliRun run = await harness.RunAsync("daemon", "start");

        Assert.Equal(1, run.ExitCode);
        Assert.Empty(run.Out);
        Assert.Equal(Refusal + "\n" + RegularTerminal + "\n", run.Error);
        Assert.Empty(platform.DetachedStarts);
        Assert.False(File.Exists(Path.Combine(temp.Path, "data", "daemon.json")));
    }

    [Fact]
    public async Task DJ2_Start_Refusal_MentionsTheSignInItem()
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = new() { JobState = new(true, 0x2000) };
        platform.StartupCommands["CopilotLedger"] = "x";
        await using CliHarness harness = new(temp.Path, platform);

        CliRun run = await harness.RunAsync("daemon", "start");

        Assert.Equal(1, run.ExitCode);
        Assert.Equal(SignInItem, run.Error.Split('\n')[1]);
    }

    [Fact]
    public async Task DJ3_Start_BreaksAway_WhenTheJobAllowsIt()
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = new() { JobState = new(true, 0x2800) };
        await using CliHarness harness = new(temp.Path, platform);

        CliRun run = await harness.RunAsync("daemon", "start");

        Assert.Equal(0, run.ExitCode);
        Assert.StartsWith("gh-ledger daemon started (pid ", run.Out);
        Assert.Empty(run.Error);
        CliRun stop = await harness.RunAsync("daemon", "stop");
        Assert.Equal(0, stop.ExitCode);
        Assert.Equal(0, (await harness.InProcessDaemon!).ExitCode);
    }

    [Fact]
    public async Task DJ4_Start_Refuses_WhenTheJobCantBeRead()
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = new()
        {
            JobStateException = new Win32Exception(5, "Access is denied."),
        };
        await using CliHarness harness = new(temp.Path, platform);

        CliRun run = await harness.RunAsync("daemon", "start");

        Assert.Equal(1, run.ExitCode);
        Assert.Equal(
            "gh-ledger daemon not started: can't read this terminal's Windows job, so the daemon might stop when the terminal closes (Access is denied.).\n"
            + RegularTerminal + "\n",
            run.Error);
        Assert.Empty(platform.DetachedStarts);
    }

    [Fact]
    public async Task DJ5_Install_InAKillOnCloseJob_EndsWithTheMessage()
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = new()
        {
            IsWindows = true,
            ProcessPath = @"C:\tools\gh-ledger.exe",
            PathExecutables =
            {
                ["gh"] = @"C:\gh\gh.exe",
                ["git"] = @"C:\git\cmd\git.exe",
            },
            JobState = new(true, 0x2000),
        };
        await using CliHarness harness = new(temp.Path, platform);

        CliRun run = await harness.RunAsync("install");

        Assert.Equal(1, run.ExitCode);
        Assert.Equal(Refusal + "\n" + SignInItem + "\n", run.Error);
        Assert.EndsWith(
            "gh-ledger is installed. Restart the GitHub Copilot app, VS Code, and open terminals so they pick up COPILOT_OTEL_ENABLED.\n",
            run.Out);
        Assert.Empty(platform.DetachedStarts);
        Assert.True(platform.StartupCommands.ContainsKey("CopilotLedger"));
    }

    [Fact]
    public async Task DJ6_DaemonRun_WarnsWhenItsJobKillsOnClose()
    {
        using (TempDirectory temp = new())
        {
            FakeCliPlatform platform = new() { JobState = new(true, 0x2800) };
            await using CliHarness harness = new(temp.Path, platform);
            await harness.StartInProcessDaemonAsync();
            Assert.Equal(0, (await harness.RunAsync("daemon", "stop")).ExitCode);
            CliRun daemon = await harness.InProcessDaemon!;

            Assert.Equal(0, daemon.ExitCode);
            Assert.Single(
                harness.DaemonOutput.Split('\n'),
                line => line.EndsWith(DaemonWarning, StringComparison.Ordinal));
        }

        using (TempDirectory temp = new())
        {
            await using CliHarness harness = new(temp.Path);
            await harness.StartInProcessDaemonAsync();
            Assert.Equal(0, (await harness.RunAsync("daemon", "stop")).ExitCode);
            await harness.InProcessDaemon!;

            Assert.DoesNotContain("Windows job", harness.DaemonOutput);
        }
    }

    [Theory]
    [InlineData(0x2000u, false)]
    [InlineData(0x2800u, true)]
    [InlineData(0x3000u, true)]
    public async Task DJ7_RealJob_TheDaemonSurvivesOrStartNeverRan(
        uint flags,
        bool expectStarted)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using TempDirectory temp = new();
        string exe = Path.Combine(AppContext.BaseDirectory, "gh-ledger.exe");
        int port;
        TcpListener listener = new(IPAddress.Loopback, 0);
        try
        {
            listener.Start();
            port = ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }

        Dictionary<string, string> environment = Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(
                entry => (string)entry.Key,
                entry => (string)entry.Value!,
                StringComparer.Ordinal);
        string dataDirectory = Path.Combine(temp.Path, "data");
        environment["COPILOT_LEDGER_HOME"] = dataDirectory;
        string outputPath = Path.Combine(temp.Path, "out.txt");
        int? daemonPid = null;
        try
        {
            int exitCode = WindowsJobHarness.RunInJobThenCloseJob(
                exe,
                ["daemon", "start", "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture)],
                environment,
                flags,
                outputPath,
                TimeSpan.FromSeconds(60));
            string output = await File.ReadAllTextAsync(outputPath);

            if (!expectStarted)
            {
                Assert.Equal(1, exitCode);
                string[] lines = output.Replace("\r\n", "\n").Split('\n');
                Assert.Equal(Refusal, lines[0]);
                Assert.StartsWith(
                    "Run gh-ledger daemon start from a regular terminal",
                    lines[1]);
                Assert.False(File.Exists(Path.Combine(dataDirectory, "daemon.json")));
                return;
            }

            Assert.Equal(0, exitCode);
            Assert.StartsWith("gh-ledger daemon started (pid ", output);
            const string prefix = "gh-ledger daemon started (pid ";
            int pidEnd = output.IndexOf(')', prefix.Length);
            daemonPid = int.Parse(
                output[prefix.Length..pidEnd],
                System.Globalization.CultureInfo.InvariantCulture);
            await Task.Delay(TimeSpan.FromSeconds(1));
            Process process = Process.GetProcessById(daemonPid.Value);
            try
            {
                Assert.False(process.HasExited);
            }
            finally
            {
                process.Dispose();
            }

            using JsonDocument state = JsonDocument.Parse(
                await File.ReadAllTextAsync(Path.Combine(dataDirectory, "daemon.json")));
            Assert.Equal(
                daemonPid.Value,
                state.RootElement.GetProperty("processId").GetInt32());
        }
        finally
        {
            ProcessStartInfo stopInfo = new()
            {
                FileName = exe,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            stopInfo.ArgumentList.Add("daemon");
            stopInfo.ArgumentList.Add("stop");
            stopInfo.Environment["COPILOT_LEDGER_HOME"] = dataDirectory;
            using (Process? stop = Process.Start(stopInfo))
            {
                if (stop is not null)
                {
                    Task<string> output = stop.StandardOutput.ReadToEndAsync();
                    Task<string> error = stop.StandardError.ReadToEndAsync();
                    using CancellationTokenSource stopTimeout = new(TimeSpan.FromSeconds(30));
                    try
                    {
                        await stop.WaitForExitAsync(stopTimeout.Token);
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    await Task.WhenAll(output, error);
                }
            }

            if (daemonPid is int pid)
            {
                try
                {
                    using Process process = Process.GetProcessById(pid);
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
                catch (ArgumentException)
                {
                }
            }
        }
    }
}
