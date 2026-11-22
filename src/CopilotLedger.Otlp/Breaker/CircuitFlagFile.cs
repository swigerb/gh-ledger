using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CopilotLedger.Core.Breaker;
using CopilotLedger.Storage;

namespace CopilotLedger.Otlp.Breaker;

public static class CircuitFlagFile
{
    public const string DirectoryName = ".copilot-ledger";
    public const string FileName = "CIRCUIT_OPEN";

    private static readonly UTF8Encoding Utf8 = new(false);

    public static string GetPath(string repositoryRoot) =>                     // Path.Combine(repositoryRoot, DirectoryName, FileName)
        Path.Combine(repositoryRoot, DirectoryName, FileName);

    public static string Render(string repositoryRoot, IReadOnlyList<BreakerTrip> openTrips)   // the file's text; openTrips is non-empty
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(
            stream,
            new JsonWriterOptions
            {
                Indented = true,
                NewLine = "\n",
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("repositoryRoot", repositoryRoot);
            writer.WriteStartArray("openTrips");
            foreach (BreakerTrip trip in openTrips)
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", trip.Id);
                writer.WriteString("rule", BreakerRuleNames.ToName(trip.Rule));
                writer.WriteString("repository", trip.Repository);
                writer.WriteString("branch", trip.Branch);
                if (trip.ConversationId is null)
                {
                    writer.WriteNull("conversationId");
                }
                else
                {
                    writer.WriteString("conversationId", trip.ConversationId);
                }

                writer.WriteString("traceId", trip.TraceId);
                writer.WriteString(
                    "trippedAt",
                    trip.TrippedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                writer.WriteString("detail", trip.Detail);
                writer.WriteString(
                    "resetCommand",
                    $"gh-ledger breaker reset --id {trip.Id.ToString(CultureInfo.InvariantCulture)}");
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    // Makes the flag file match the ledger. Throws IOException or UnauthorizedAccessException from the file system, and
    // whatever the store throws, including OperationCanceledException. Whatever it throws, it first tries to delete the
    // temporary file it was writing. Does nothing when repositoryRoot isn't an existing directory.
    public static async Task SyncAsync(
        SqliteLedgerStore store,
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(repositoryRoot))
        {
            return;
        }

        string path = GetPath(repositoryRoot);
        string? applied = null;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            BreakerSettings settings = await store.GetBreakerSettingsAsync(cancellationToken);
            IReadOnlyList<BreakerTrip> trips = settings.FlagFile
                ? await store.GetOpenBreakerTripsAsync(
                    new BreakerTripFilter { RepositoryRoot = repositoryRoot },
                    cancellationToken)
                : [];
            string? desired = trips.Count == 0 ? null : Render(repositoryRoot, trips);
            if (attempt > 0 && string.Equals(applied, desired, StringComparison.Ordinal))
            {
                return;
            }

            if (desired is null)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            else
            {
                string directory = Path.Combine(repositoryRoot, DirectoryName);
                Directory.CreateDirectory(directory);
                string gitignore = Path.Combine(directory, ".gitignore");
                if (!File.Exists(gitignore))
                {
                    await File.WriteAllTextAsync(gitignore, "*\n", Utf8, cancellationToken);
                }

                string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    await File.WriteAllTextAsync(temp, desired, Utf8, cancellationToken);
                    File.Move(temp, path, overwrite: true);
                }
                catch
                {
                    try
                    {
                        File.Delete(temp);
                    }
                    catch (Exception cleanup) when (
                        cleanup is IOException or UnauthorizedAccessException)
                    {
                    }

                    throw;
                }
            }

            applied = desired;
        }
    }
}
