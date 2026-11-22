namespace CopilotLedger.Cli.Daemon;

// Writes "Warning: could not record spans: <type>: <message>" for the first ingestion failure, then at most once a
// minute, so a ledger that stays unavailable can't flood daemon.log. After the clock steps back, the next failure is
// written at once. A line that can't be written is dropped.
internal sealed class IngestFailureLog(Action<string> write, TimeProvider timeProvider)
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly Lock _gate = new();
    private DateTimeOffset? _lastWritten;

    public void Report(Exception exception)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        lock (_gate)
        {
            if (_lastWritten is DateTimeOffset last && now >= last && now - last < Interval)
            {
                return;
            }

            _lastWritten = now;
        }

        try
        {
            write($"Warning: could not record spans: {exception.GetType().Name}: {exception.Message}");
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
        }
    }
}
