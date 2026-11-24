using CopilotLedger.Cli;

namespace CopilotLedger.Tests.TestSupport;

public sealed record DetachedStart(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory);

public sealed class FakeCliPlatform : ICliPlatform
{
    private string? _homeDirectory;

    public bool IsWindows { get; set; }
    public bool IsMacOS { get; set; }

    // Throws until a test sets it, so install can't write a profile or sign-in item outside the test's TempDirectory.
    public string HomeDirectory
    {
        get => _homeDirectory
            ?? throw new InvalidOperationException(
                "Set FakeCliPlatform.HomeDirectory to a directory under the test's TempDirectory.");
        set => _homeDirectory = value;
    }

    public string? ProcessPath { get; set; } = "/opt/gh-ledger/gh-ledger";
    public string? ShellPath { get; set; }
    public Dictionary<string, string> ProcessVariables { get; } =
        new(StringComparer.Ordinal);
    public Dictionary<string, string> UserVariables { get; } =
        new(StringComparer.Ordinal);
    public List<(string Name, string? Value)> UserVariableWrites { get; } = [];
    public Dictionary<string, string> StartupCommands { get; } = new(StringComparer.Ordinal);
    public List<(string Name, string? Command)> StartupCommandWrites { get; } = [];
    public Exception? StartupCommandException { get; set; }
    public Dictionary<string, string> PathExecutables { get; } =
        new(StringComparer.Ordinal);
    public Dictionary<string, TimeZoneInfo> TimeZones { get; } = new(StringComparer.Ordinal);
    public HashSet<string> ExistingFiles { get; } = new(StringComparer.Ordinal);
    public List<(string FileName, IReadOnlyList<string> Arguments)> ProcessRuns { get; } = [];
    public Func<string, IReadOnlyList<string>, CancellationToken, Task<ProcessRunResult>>? RunHandler { get; set; }
    public List<DetachedStart> DetachedStarts { get; } = [];
    public Func<string, IReadOnlyList<string>, string, int>? StartDetachedHandler { get; set; }
    public HashSet<int> RunningProcesses { get; } = [];
    public List<int> KilledProcesses { get; } = [];

    public string? GetEnvironmentVariable(string name) =>
        ProcessVariables.GetValueOrDefault(name);

    public string? GetUserEnvironmentVariable(string name) =>
        UserVariables.GetValueOrDefault(name);

    public void SetUserEnvironmentVariable(string name, string? value)
    {
        UserVariableWrites.Add((name, value));
        if (value is null)
        {
            UserVariables.Remove(name);
        }
        else
        {
            UserVariables[name] = value;
        }
    }

    public string? GetStartupCommand(string name) =>
        StartupCommands.GetValueOrDefault(name);

    public void SetStartupCommand(string name, string? command)
    {
        if (StartupCommandException is not null)
        {
            throw StartupCommandException;
        }

        StartupCommandWrites.Add((name, command));
        if (command is null)
        {
            StartupCommands.Remove(name);
        }
        else
        {
            StartupCommands[name] = command;
        }
    }

    public bool FileExists(string path) => ExistingFiles.Contains(path);

    public string? FindOnPath(string fileName) =>
        PathExecutables.GetValueOrDefault(fileName);

    public TimeZoneInfo? FindTimeZone(string id) =>
        TimeZones.GetValueOrDefault(id);

    public Task<ProcessRunResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        lock (ProcessRuns)
        {
            ProcessRuns.Add((fileName, arguments));
        }

        return RunHandler?.Invoke(fileName, arguments, cancellationToken)
            ?? Task.FromResult(new ProcessRunResult(0, string.Empty, string.Empty));
    }

    public int StartDetached(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        DetachedStarts.Add(new DetachedStart(fileName, arguments, workingDirectory));
        return StartDetachedHandler?.Invoke(fileName, arguments, workingDirectory)
            ?? Environment.ProcessId;
    }

    public bool IsProcessRunning(int processId) =>
        processId == Environment.ProcessId || RunningProcesses.Contains(processId);

    public void KillProcess(int processId) => KilledProcesses.Add(processId);
}
