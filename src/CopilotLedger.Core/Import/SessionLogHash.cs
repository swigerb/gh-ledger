using System.Security.Cryptography;

namespace CopilotLedger.Core.Import;

// The SHA-256 of a session log's content: the bytes from its start up to the size it had when it was discovered. A log's
// size and last-write time only hint that it's unchanged; this hash decides it.
public static class SessionLogHash
{
    private const int ChunkBytes = 64 * 1024;

    // The lowercase hex SHA-256 of location's first SizeBytes bytes (fewer when it has shrunk since), or null when the log
    // can't be read. A compressed log is never opened, so it has none.
    public static Task<string?> ComputeAsync(SessionLogLocation location, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(location);
        return location.IsCompressed
            ? Task.FromResult<string?>(null)
            : Task.Run(() => Compute(location, cancellationToken), cancellationToken);
    }

    private static string? Compute(SessionLogLocation location, CancellationToken cancellationToken)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] chunk = new byte[ChunkBytes];
        try
        {
            using FileStream stream = new(
                location.Path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                0,
                FileOptions.SequentialScan);
            long position = 0;
            while (position < location.SizeBytes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = stream.Read(chunk, 0, (int)Math.Min(ChunkBytes, location.SizeBytes - position));
                if (read == 0)
                {
                    break;
                }

                hash.AppendData(chunk, 0, read);
                position += read;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
