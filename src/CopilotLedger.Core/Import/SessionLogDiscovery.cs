namespace CopilotLedger.Core.Import;

// What discovery found under a home. A missing home, or a missing store folder in it, is no logs and no unreadable entries;
// a store folder, session folder, or log file that exists but can't be listed or examined is an unreadable entry.
public sealed record SessionLogDiscovery
{
    public required IReadOnlyList<SessionLogLocation> Logs { get; init; }   // ordered by LogId (ordinal)
    public required long UnreadableEntries { get; init; }
}
