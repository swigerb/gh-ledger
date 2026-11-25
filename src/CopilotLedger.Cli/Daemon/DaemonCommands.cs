using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Globalization;
using CopilotLedger.Cli.Install;

namespace CopilotLedger.Cli.Daemon;

// What daemon start decides before it starts anything.
internal enum DaemonStartOutcome
{
    AlreadyRunning,   // the daemon holds its lock
    NotExecutable,    // this process isn't the gh-ledger executable
    Blocked,          // the terminal's Windows job would stop the daemon, or can't be read
    Start,
}

internal sealed record DaemonStartDecision
{
    public required DaemonStartOutcome Outcome { get; init; }
    public int? ProcessId { get; init; }      // AlreadyRunning: daemon.json's processId; null when it can't be read
    public string? Executable { get; init; }  // NotExecutable: the process's path; null when unknown
    public string? JobError { get; init; }    // Blocked: why the job can't be read; null when the job blocks
    public bool SignInItem { get; init; }     // Blocked: a sign-in item exists, or signInItemPlanned
}

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

    // Decides daemon start without side effects: DaemonFiles.IsHeld opens and closes the lock file only when it exists, and
    // daemon.json, the process path, the job, and the sign-in item are only read. signInItemPlanned: install's dry run would
    // have registered the Windows sign-in item before starting the daemon.
    internal static DaemonStartDecision DecideStart(
        CliContext context,
        bool signInItemPlanned = false)
    {
        if (DaemonFiles.IsHeld(context))
        {
            return new DaemonStartDecision
            {
                Outcome = DaemonStartOutcome.AlreadyRunning,
                ProcessId = DaemonFiles.TryReadState(context)?.ProcessId,
            };
        }

        string? executable = context.Platform.ProcessPath;
        if (!GetExecutableName(executable).Equals(
                "gh-ledger",
                StringComparison.OrdinalIgnoreCase))
        {
            return new DaemonStartDecision
            {
                Outcome = DaemonStartOutcome.NotExecutable,
                Executable = executable,
            };
        }

        JobState job;
        string? jobError = null;
        try { job = context.Platform.GetJobState(); }
        catch (Win32Exception ex) { job = default; jobError = ex.Message; }
        if (jobError is not null ||
            DetachedLaunchPolicy.Decide(job) == DetachedLaunch.Blocked)
        {
            return new DaemonStartDecision
            {
                Outcome = DaemonStartOutcome.Blocked,
                JobError = jobError,
                SignInItem = signInItemPlanned ||
                    context.Platform.GetStartupCommand("CopilotLedger") is not null,
            };
        }

        return new DaemonStartDecision { Outcome = DaemonStartOutcome.Start };
    }

    // Writes what daemon start says for a decision other than Start, and returns its exit code: AlreadyRunning to standard
    // output, 0; NotExecutable and Blocked to standard error, 1. With dryRun, Blocked's first line starts "Wouldn't start the
    // gh-ledger daemon: " instead of "gh-ledger daemon not started: ". Throws ArgumentException for Start.
    internal static int WriteStartDecision(
        CliContext context,
        DaemonStartDecision decision,
        bool dryRun)
    {
        if (decision.Outcome == DaemonStartOutcome.AlreadyRunning)
        {
            context.Out.WriteLine(decision.ProcessId is int processId
                ? $"gh-ledger daemon is already running (pid {processId})."
                : "gh-ledger daemon is already running.");
            return 0;
        }

        if (decision.Outcome == DaemonStartOutcome.NotExecutable)
        {
            context.Error.WriteLine(
                $"Run this command from the gh-ledger executable (current process: {decision.Executable ?? "unknown"}).");
            return 1;
        }

        if (decision.Outcome == DaemonStartOutcome.Blocked)
        {
            string reason = decision.JobError is null
                ? "this terminal runs in a Windows job that stops the processes it started when it closes, so the daemon would stop with it."
                : $"can't read this terminal's Windows job, so the daemon might stop when the terminal closes ({decision.JobError}).";
            context.Error.WriteLine(
                (dryRun
                    ? "Wouldn't start the gh-ledger daemon: "
                    : "gh-ledger daemon not started: ") + reason);
            string command = GhExtension.CommandName(context.Platform);
            context.Error.WriteLine(decision.SignInItem
                ? $"Run {command} daemon start from a regular terminal, or sign out and back in to start it from the sign-in item."
                : $"Run {command} daemon start from a regular terminal.");
            return 1;
        }

        throw new ArgumentException("Start has nothing to report.", nameof(decision));
    }

    internal static async Task<int> StartAsync(
        CliContext context,
        int port,
        CancellationToken cancellationToken)
    {
        DaemonStartDecision decision = DecideStart(context);
        if (decision.Outcome != DaemonStartOutcome.Start)
        {
            return WriteStartDecision(context, decision, dryRun: false);
        }

        string executable = context.Platform.ProcessPath!;
        Directory.CreateDirectory(context.DataDirectory);
        DaemonFiles.DeleteIfExists(DaemonFiles.StatePath(context));
        int processId = context.Platform.StartDetached(
            executable,
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
