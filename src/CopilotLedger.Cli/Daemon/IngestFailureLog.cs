using Microsoft.Data.Sqlite;

namespace CopilotLedger.Cli.Daemon;

// Writes "Warning: could not record spans: <type>: <message>" for the first ingestion failure, then at most once a
// minute, so a ledger that stays unavailable can't flood daemon.log. Only a SqliteException keeps its message: SQLite's
// messages name tables, columns, and constraints but never a bound value, and the ledger binds every value. Any other
// exception is written as "Warning: could not record spans: <type>", because its message can quote the request (a
// JsonException's does) or name a path from it (an IOException's can, such as the span's working directory). After the
// clock steps back, the next failure is written at once. A line that can't be written is dropped.
public sealed class IngestFailureLog(Action<string> write, TimeProvider timeProvider)
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
            write(exception is SqliteException
                ? $"Warning: could not record spans: {exception.GetType().Name}: {exception.Message}"
                : $"Warning: could not record spans: {exception.GetType().Name}");
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
        }
    }
}
