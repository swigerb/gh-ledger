using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;

namespace CopilotLedger.Cli;

public sealed class SystemCliPlatform : ICliPlatform
{
    public bool IsWindows => OperatingSystem.IsWindows();
    public bool IsMacOS => OperatingSystem.IsMacOS();
    public string HomeDirectory =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public string? ProcessPath => Environment.ProcessPath;
    public string? ShellPath => Environment.GetEnvironmentVariable("SHELL");

    public string? GetEnvironmentVariable(string name) =>
        Environment.GetEnvironmentVariable(name);

    public string? GetUserEnvironmentVariable(string name) =>
        IsWindows ? Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User) : null;

    public void SetUserEnvironmentVariable(string name, string? value)
    {
        if (!IsWindows)
        {
            throw new PlatformNotSupportedException();
        }

        Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.User);
    }

    public string? GetStartupCommand(string name)
    {
        if (OperatingSystem.IsWindows())
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run",
                writable: false);
            return key?.GetValue(name) as string;
        }

        return null;
    }

    public void SetStartupCommand(string name, string? command)
    {
        if (OperatingSystem.IsWindows())
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run",
                writable: true);
            if (command is null)
            {
                key.DeleteValue(name, throwOnMissingValue: false);
            }
            else
            {
                key.SetValue(name, command, RegistryValueKind.String);
            }

            return;
        }

        throw new PlatformNotSupportedException();
    }

    public bool FileExists(string path) => File.Exists(path);

    public string? FindOnPath(string fileName)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (path is null)
        {
            return null;
        }

        string candidateName = IsWindows
            && Path.GetExtension(fileName).Length == 0
            ? fileName + ".exe"
            : fileName;
        foreach (string entry in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(entry, candidateName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    // gh-ledger runs with invariant globalization, which turns off .NET's IANA-to-Windows mapping on Windows, so an IANA ID that
    // Windows doesn't know is mapped through Windows' own ICU.
    public TimeZoneInfo? FindTimeZone(string id)
    {
        if (id.Length is 0 or > 128 || id.Trim().Length != id.Length)
        {
            return null;
        }

        try
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out TimeZoneInfo? zone))
            {
                return zone;
            }

            return OperatingSystem.IsWindows()
                && WindowsTimeZoneId(id) is string windowsId
                && TimeZoneInfo.TryFindSystemTimeZoneById(windowsId, out zone)
                    ? zone
                    : null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }

    // The Windows time zone ID that Windows' ICU (icu.dll, Windows 10 1903 and later) maps an IANA ID to; null when it maps to
    // none or icu.dll isn't there.
    [SupportedOSPlatform("windows")]
    public static string? WindowsTimeZoneId(string ianaId)
    {
        char[] buffer = new char[128];
        int status = 0;
        try
        {
            int length = GetWindowsTimeZoneId(ianaId, ianaId.Length, buffer, buffer.Length, ref status);
            return status > 0 || length is <= 0 or > 128 ? null : new string(buffer, 0, length);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    public async Task<ProcessRunResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using Process process = new();
        process.StartInfo.FileName = fileName;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
        process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new ProcessRunResult(process.ExitCode, await output, await error);
    }

    public int StartDetached(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsDetachedProcess.Start(fileName, arguments, workingDirectory);
        }

        using Process process = new() { StartInfo = UnixDetachedStartInfo(fileName, arguments, workingDirectory) };
        process.Start();
        return process.Id;
    }

    // /bin/sh first checks that fileName can be executed: if not, it says so on the caller's standard error and exits
    // with 126, because a failed exec would report to the null device. Otherwise exec keeps the process id, and the
    // redirections replace the inherited standard handles before the daemon runs. printf, not echo: dash's echo
    // interprets backslashes in the path.
    public static ProcessStartInfo UnixDetachedStartInfo(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        ProcessStartInfo startInfo = new("/bin/sh")
        {
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(
            """[ -x "$0" ] || { printf 'gh-ledger: cannot execute %s\n' "$0" >&2; exit 126; }; exec "$0" "$@" </dev/null >/dev/null 2>&1""");
        startInfo.ArgumentList.Add(fileName);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    public bool IsProcessRunning(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    public void KillProcess(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            process.Kill();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
        }
    }

    // ICU's int32_t ucal_getWindowsTimeZoneID(const UChar* id, int32_t len, UChar* winid, int32_t winidCapacity, UErrorCode* status):
    // the result's length, 0 for an unknown or unmappable ID; a status above 0 is an error.
    [DllImport("icu.dll", EntryPoint = "ucal_getWindowsTimeZoneID", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetWindowsTimeZoneId(string id, int length, [Out] char[] windowsId, int capacity, ref int status);
}
