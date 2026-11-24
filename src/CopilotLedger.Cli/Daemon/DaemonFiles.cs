using System.Text.Json;
using System.Text.Json.Serialization;

namespace CopilotLedger.Cli.Daemon;

internal sealed record DaemonState
{
    // The import rules this build's daemon applies to the live spans it records (UsageAuthority). gh ledger import needs
    // them: a daemon whose daemon.json lacks importRules (an earlier build) could record a request twice.
    public const int CurrentImportRules = 1;

    public required int SchemaVersion { get; init; }
    public required int ProcessId { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required string Endpoint { get; init; }
    public required string Version { get; init; }
    public required string DatabasePath { get; init; }
    public int ImportRules { get; init; }            // 0 when daemon.json has no importRules
}

internal static class DaemonFiles
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static string LockPath(CliContext context) =>
        Path.Combine(context.DataDirectory, "daemon.lock");

    public static string StatePath(CliContext context) =>
        Path.Combine(context.DataDirectory, "daemon.json");

    public static string StopPath(CliContext context) =>
        Path.Combine(context.DataDirectory, "daemon.stop");

    public static string LogPath(CliContext context) =>
        Path.Combine(context.DataDirectory, "daemon.log");

    public static FileStream AcquireLock(CliContext context) =>
        new(
            LockPath(context),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);

    public static bool IsHeld(CliContext context)
    {
        string lockPath = LockPath(context);
        if (!File.Exists(lockPath))
        {
            return false;
        }

        try
        {
            using FileStream lockStream = AcquireLock(context);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    public static async Task WriteStateAsync(
        CliContext context,
        DaemonState state,
        CancellationToken cancellationToken)
    {
        string statePath = StatePath(context);
        string temporaryPath = statePath + ".tmp";
        await using FileStream stream = new(
            temporaryPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);
        await JsonSerializer.SerializeAsync(stream, state, SerializerOptions, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        stream.Dispose();
        File.Move(temporaryPath, statePath, overwrite: true);
    }

    public static DaemonState? TryReadState(CliContext context)
    {
        try
        {
            string json = File.ReadAllText(StatePath(context));
            return JsonSerializer.Deserialize<DaemonState>(json, SerializerOptions);
        }
        catch (Exception ex) when (
            ex is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException)
        {
            return null;
        }
    }

    // Deletes the file. When Windows reports it in use (for example while antivirus scans a file that was
    // just written), retries every 25 ms, up to 200 attempts (5 seconds), then gives up silently.
    public static void DeleteIfExists(string path)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }
            catch (Exception ex) when (
                (ex is IOException or UnauthorizedAccessException) && attempt < DeleteAttempts)
            {
                Thread.Sleep(DeleteRetryDelay);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return;
            }
        }
    }

    private const int DeleteAttempts = 200;

    private static readonly TimeSpan DeleteRetryDelay = TimeSpan.FromMilliseconds(25);
}
