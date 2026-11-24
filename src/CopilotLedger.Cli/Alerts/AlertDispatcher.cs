using System.Threading.Channels;
using CopilotLedger.Cli.Install;
using CopilotLedger.Otlp.Breaker;
using CopilotLedger.Storage;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Cli.Alerts;

internal sealed class AlertDispatcher : IBreakerAlertSink
{
    private readonly CliContext _context;
    private readonly SqliteLedgerStore _store;
    private readonly Action<string> _log;
    private readonly string _command;
    private readonly Channel<BreakerAlert> _alerts = Channel.CreateUnbounded<BreakerAlert>(
        new UnboundedChannelOptions { SingleReader = true });

    // log writes one daemon log line. Throws ArgumentNullException for null arguments.
    public AlertDispatcher(CliContext context, SqliteLedgerStore store, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(log);
        _context = context;
        _store = store;
        _log = log;
        _command = GhExtension.CommandName(context.Platform);
    }

    public string Command => _command;

    public void Publish(BreakerAlert alert) =>           // Channel.CreateUnbounded<BreakerAlert>(new UnboundedChannelOptions { SingleReader = true }); TryWrite
        _alerts.Writer.TryWrite(alert);

    public void Warn(string message)                     // log("Warning: " + message), catching only IOException (the line is dropped), so it never throws into the monitor
    {
        try
        {
            _log("Warning: " + message);
        }
        catch (IOException)
        {
        }
    }

    // Publishes, oldest first, ForTrip(trip.Id, trip.Repository, trip.Branch, trip.Detail, notifications) for each trip from
    // GetUnnotifiedBreakerTripsAsync(now - 1 hour), then ForBudget(row, notifications) for each row from GetUnnotifiedBudgetAlertsAsync(now - 1 hour),
    // where now is context.TimeProvider.GetUtcNow() and notifications is GetBreakerSettingsAsync().Notifications.
    public async Task EnqueuePendingAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = _context.TimeProvider.GetUtcNow();
        bool notifications = (await _store.GetBreakerSettingsAsync(cancellationToken))
            .Notifications;
        foreach (BreakerTrip trip in
            await _store.GetUnnotifiedBreakerTripsAsync(
                now - TimeSpan.FromHours(1),
                cancellationToken))
        {
            Publish(BreakerMessages.ForTrip(
                trip.Id,
                trip.Repository,
                trip.Branch,
                trip.Detail,
                notifications,
                _command));
        }

        foreach (BranchBudgetRow budget in
            await _store.GetUnnotifiedBudgetAlertsAsync(
                now - TimeSpan.FromHours(1),
                cancellationToken))
        {
            Publish(BreakerMessages.ForBudget(budget, notifications, _command));
        }
    }

    // Handles alerts until Complete() has been called and the channel is empty. Cancellation stops it (OperationCanceledException).
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await foreach (BreakerAlert alert in _alerts.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                _log($"ALERT {alert.Title}: {alert.Body}");
                if (alert.ShowNotification)
                {
                    await DesktopNotifier.ShowAsync(
                        _context,
                        alert.Title,
                        alert.Body,
                        _log,
                        cancellationToken);
                }

                try
                {
                    if (alert.Kind == BreakerAlertKind.Trip)
                    {
                        await _store.MarkBreakerTripNotifiedAsync(
                            alert.TripId!.Value,
                            cancellationToken);
                    }
                    else
                    {
                        await _store.MarkBranchBudgetNotifiedAsync(
                            alert.Repository,
                            alert.Branch,
                            cancellationToken);
                    }
                }
                catch (SqliteException ex)
                {
                    _log($"Warning: could not record the notification: {ex.Message}");
                }
            }
            catch (IOException)
            {
            }
        }
    }

    public void Complete() =>                            // Writer.TryComplete()
        _alerts.Writer.TryComplete();
}
