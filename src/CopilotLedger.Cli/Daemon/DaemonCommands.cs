using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Globalization;
using CopilotLedger.Cli.Install;

namespace CopilotLedger.Cli.Daemon;

internal static class DaemonCommands
{
    public static Command Create(CliContext context)
    {
        Command daemon = new("daemon", "Run and manage the local OTLP receiver.");
        daemon.Add(CreateRun(context));
        daemon.Add(CreateStart(context));
        daemon.Add(CreateStop(context));
        return daemon;
    }

    private static Command CreateRun(CliContext context)
    {
        Command command = new("run", "Run the daemon in the foreground.");
        Option<int> port = PortOption();
        Option<string?> logFile = new("--log-file")
        {
            Description = "Append daemon logs to this file.",
        };
        command.Add(port);
        command.Add(logFile);
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
                DaemonHost.RunAsync(
                    context,
                    result.GetValue(port),
                    result.GetValue(logFile),
                    cancellationToken));
        return command;
    }

    private static Command CreateStart(CliContext context)
    {
        Command command = new("start", "Start the daemon as a detached process.");
        Option<int> port = PortOption();
        command.Add(port);
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
                StartAsync(context, result.GetValue(port), cancellationToken));
        return command;
    }

    private static Command CreateStop(CliContext context)
    {
        Command command = new("stop", "Stop the running daemon.");
        command.SetAction(
            (ParseResult _, CancellationToken cancellationToken) =>
                StopAsync(context, cancellationToken));
        return command;
    }

    internal static async Task<int> StartAsync(
        CliContext context,
        int port,
        CancellationToken cancellationToken)
    {
        if (DaemonFiles.IsHeld(context))
        {
            DaemonState? state = DaemonFiles.TryReadState(context);
            context.Out.WriteLine(state is null
                ? "gh-ledger daemon is already running."
                : $"gh-ledger daemon is already running (pid {state.ProcessId}).");
            return 0;
        }

        string? executable = context.Platform.ProcessPath;
        string executableName = GetExecutableName(executable);
        if (!executableName.Equals("gh-ledger", StringComparison.OrdinalIgnoreCase))
        {
            context.Error.WriteLine(
                $"Run this command from the gh-ledger executable (current process: {executable ?? "unknown"}).");
            return 1;
        }

        JobState job;
        string? jobError = null;
        try { job = context.Platform.GetJobState(); }
        catch (Win32Exception ex) { job = default; jobError = ex.Message; }
        if (jobError is not null || DetachedLaunchPolicy.Decide(job) == DetachedLaunch.Blocked)
        {
            context.Error.WriteLine(jobError is null
                ? "gh-ledger daemon not started: this terminal runs in a Windows job that stops the processes it started when it closes, so the daemon would stop with it."
                : $"gh-ledger daemon not started: can't read this terminal's Windows job, so the daemon might stop when the terminal closes ({jobError}).");
            string command = GhExtension.CommandName(context.Platform);
            context.Error.WriteLine(
                context.Platform.GetStartupCommand("CopilotLedger") is not null
                    ? $"Run {command} daemon start from a regular terminal, or sign out and back in to start it from the sign-in item."
                    : $"Run {command} daemon start from a regular terminal.");
            return 1;
        }

        Directory.CreateDirectory(context.DataDirectory);
        DaemonFiles.DeleteIfExists(DaemonFiles.StatePath(context));
        int processId = context.Platform.StartDetached(
            executable!,
            [
                "daemon",
                "run",
                "--port",
                port.ToString(CultureInfo.InvariantCulture),
                "--log-file",
                DaemonFiles.LogPath(context),
            ],
            context.DataDirectory);
        DateTimeOffset deadline = context.TimeProvider.GetUtcNow()
            + context.DaemonStartTimeout;
        while (context.TimeProvider.GetUtcNow() <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DaemonState? state = DaemonFiles.TryReadState(context);
            if (state?.ProcessId == processId)
            {
                context.Out.WriteLine(
                    $"gh-ledger daemon started (pid {processId}) on http://localhost:{port}.");
                return 0;
            }

            if (!context.Platform.IsProcessRunning(processId))
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        context.Error.WriteLine(
            $"gh-ledger daemon did not start; see {DaemonFiles.LogPath(context)}.");
        return 1;
    }

    internal static async Task<int> StopAsync(
        CliContext context,
        CancellationToken cancellationToken)
    {
        if (!DaemonFiles.IsHeld(context))
        {
            context.Out.WriteLine("gh-ledger daemon is not running.");
            return 0;
        }

        int? processId = DaemonFiles.TryReadState(context)?.ProcessId;
        await File.WriteAllBytesAsync(
            DaemonFiles.StopPath(context),
            [],
            cancellationToken);
        DateTimeOffset deadline = context.TimeProvider.GetUtcNow()
            + context.DaemonStopTimeout;
        while (context.TimeProvider.GetUtcNow() <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!DaemonFiles.IsHeld(context))
            {
                context.Out.WriteLine("gh-ledger daemon stopped.");
                return 0;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        int seconds = (int)Math.Ceiling(context.DaemonStopTimeout.TotalSeconds);
        if (processId is int known && context.Platform.IsProcessRunning(known))
        {
            context.Platform.KillProcess(known);
            context.Out.WriteLine(
                $"gh-ledger daemon did not stop within {seconds} seconds; stopped process {known}.");
            return 0;
        }

        context.Error.WriteLine(
            $"gh-ledger daemon did not stop within {seconds} seconds.");
        return 1;
    }

    private static Option<int> PortOption()
    {
        Option<int> port = new("--port")
        {
            Description = "OTLP receiver port.",
            DefaultValueFactory = _ => Otlp.Receiver.OtlpReceiverOptions.DefaultPort,
        };
        port.Validators.Add(result =>
        {
            int value = result.GetValueOrDefault<int>();
            if (value is < 1 or > 65535)
            {
                result.AddError("--port must be from 1 to 65535.");
            }
        });
        return port;
    }

    private static string GetExecutableName(string? path)
    {
        if (path is null)
        {
            return string.Empty;
        }

        int separator = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
        string name = separator >= 0 ? path[(separator + 1)..] : path;
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? name[..^4]
            : name;
    }
}
