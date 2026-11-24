namespace CopilotLedger.Cli;

public sealed record ProcessRunResult(int ExitCode, string StandardOutput, string StandardError);

// What gh-ledger needs from the operating system beyond files in its data directory. Tests use a fake.
public interface ICliPlatform
{
    bool IsWindows { get; }
    bool IsMacOS { get; }
    string HomeDirectory { get; }
    string? ProcessPath { get; }                                           // Environment.ProcessPath
    string? ShellPath { get; }                                             // the SHELL environment variable
    string? GetEnvironmentVariable(string name);                           // current process
    string? GetUserEnvironmentVariable(string name);                       // Windows user scope; null on other platforms
    void SetUserEnvironmentVariable(string name, string? value);           // Windows user scope; null deletes; other platforms throw PlatformNotSupportedException
    string? GetStartupCommand(string name);                                // Windows: the HKCU\Software\Microsoft\Windows\CurrentVersion\Run value; null when absent and on other platforms
    void SetStartupCommand(string name, string? command);                  // Windows: sets that value (REG_SZ), or deletes it when command is null; other platforms throw PlatformNotSupportedException
    bool FileExists(string path);
    string? FindOnPath(string fileName);                                   // full path of an executable on PATH, or null
    TimeZoneInfo? FindTimeZone(string id);                                 // a time zone by IANA ID (on Windows also by Windows ID), or null
    Task<ProcessRunResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
    int StartDetached(string fileName, IReadOnlyList<string> arguments, string workingDirectory);   // returns the new process id; its standard input, output, and error are the null device; on Windows it inherits no other handle
    bool IsProcessRunning(int processId);
    void KillProcess(int processId);
}
