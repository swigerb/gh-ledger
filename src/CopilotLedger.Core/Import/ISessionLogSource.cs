namespace CopilotLedger.Core.Import;

// One local session store's logs: where they are, and what each one recorded, as metadata only.
public interface ISessionLogSource
{
    ImportSourceKind Kind { get; }

    // The logs under home, ordered by LogId (ordinal), and how many entries couldn't be listed or examined. A missing home
    // gives none of either.
    SessionLogDiscovery Discover(string home);

    // Reads at most location.SizeBytes bytes of the log. Throws only ArgumentException (a location of another source) and
    // OperationCanceledException; a log that can't be read has Status Unreadable. A Read result has the SHA-256 of the bytes
    // it parsed.
    Task<SessionLogReadResult> ReadAsync(SessionLogLocation location, CancellationToken cancellationToken);
}
