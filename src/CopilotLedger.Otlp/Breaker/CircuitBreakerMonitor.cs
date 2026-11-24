using CopilotLedger.Core.Breaker;
using CopilotLedger.Storage;

namespace CopilotLedger.Otlp.Breaker;

public sealed class CircuitBreakerMonitor
{
    private readonly SqliteLedgerStore _store;
    private readonly IBreakerAlertSink _sink;
    private readonly string _command;

    // Throws ArgumentNullException for null arguments. The caller initializes the store.
    public CircuitBreakerMonitor(
        SqliteLedgerStore store,
        IBreakerAlertSink sink,
        string command = BreakerMessages.DefaultCommand)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(sink);
        _store = store;
        _sink = sink;
        _command = command;
    }

    // Evaluates both rules for each trace and the budgets of the branches those traces touched. Store exceptions propagate.
    // Flag-file IOException and UnauthorizedAccessException are reported to sink.Warn and not rethrown.
    public async Task EvaluateAsync(
        IEnumerable<string> traceIds,
        CancellationToken cancellationToken = default)
    {
        string[] orderedTraceIds = traceIds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(traceId => traceId, StringComparer.Ordinal)
            .ToArray();
        if (orderedTraceIds.Length == 0)
        {
            return;
        }

        BreakerSettings settings = await _store.GetBreakerSettingsAsync(cancellationToken);
        HashSet<BudgetKey> budgetKeys = [];
        foreach (string traceId in orderedTraceIds)
        {
            TraceActivity activity = await _store.GetTraceActivityAsync(traceId, cancellationToken);
            if (activity.SpanCount > 0)
            {
                budgetKeys.Add(new BudgetKey(activity.Repository, activity.Branch));
            }

            VelocityTrip? velocity = BreakerRules.FindVelocityTrip(
                activity.Costs,
                settings.VelocityThresholdUsd,
                settings.VelocityWindow);
            if (velocity is not null)
            {
                await RecordTripAsync(
                    settings,
                    new NewBreakerTrip
                    {
                        Rule = BreakerRule.CreditVelocity,
                        TraceId = activity.TraceId,
                        ConversationId = activity.ConversationId,
                        Repository = activity.Repository,
                        Branch = activity.Branch,
                        RepositoryRoot = activity.RepositoryRoot,
                        TrippedAt = velocity.TrippedAt,
                        Detail = BreakerMessages.VelocityDetail(
                            velocity.WindowCostUsd,
                            settings.VelocityWindowMinutes),
                        WindowCostUsd = velocity.WindowCostUsd,
                        WindowMinutes = settings.VelocityWindowMinutes,
                    },
                    cancellationToken);
            }

            ChurnTrip? churn = BreakerRules.FindChurnTrip(
                activity.ToolCalls,
                activity.Commits,
                settings.ChurnFailures);
            if (churn is not null)
            {
                await RecordTripAsync(
                    settings,
                    new NewBreakerTrip
                    {
                        Rule = BreakerRule.RetryChurn,
                        TraceId = activity.TraceId,
                        ConversationId = activity.ConversationId,
                        Repository = activity.Repository,
                        Branch = activity.Branch,
                        RepositoryRoot = activity.RepositoryRoot,
                        TrippedAt = churn.TrippedAt,
                        Detail = BreakerMessages.ChurnDetail(churn.FailureCount),
                        FailureCount = churn.FailureCount,
                    },
                    cancellationToken);
            }
        }

        foreach (BudgetKey key in budgetKeys
            .OrderBy(key => key.Repository, StringComparer.Ordinal)
            .ThenBy(key => key.Branch, StringComparer.Ordinal))
        {
            IReadOnlyList<BranchBudgetRow> budgets =
                await _store.GetBranchBudgetsAsync(
                    key.Repository,
                    key.Branch,
                    cancellationToken);
            if (budgets.Count != 1
                || !budgets[0].Exceeded
                || budgets[0].AlertedAt is not null)
            {
                continue;
            }

            if (!await _store.MarkBranchBudgetAlertedAsync(
                key.Repository,
                key.Branch,
                cancellationToken))
            {
                continue;
            }

            IReadOnlyList<BranchBudgetRow> reread =
                await _store.GetBranchBudgetsAsync(
                    key.Repository,
                    key.Branch,
                    cancellationToken);
            if (reread.Count != 1
                || !reread[0].Exceeded
                || reread[0].AlertedAt is null)
            {
                continue;
            }

            _sink.Publish(BreakerMessages.ForBudget(
                reread[0],
                settings.Notifications,
                _command));
        }
    }

    private async Task RecordTripAsync(
        BreakerSettings settings,
        NewBreakerTrip trip,
        CancellationToken cancellationToken)
    {
        long? id = await _store.RecordBreakerTripAsync(trip, cancellationToken);
        if (id is null)
        {
            return;
        }

        _sink.Publish(BreakerMessages.ForTrip(
            id.Value,
            trip.Repository,
            trip.Branch,
            trip.Detail,
            settings.Notifications,
            _command));
        if (!settings.FlagFile || trip.RepositoryRoot is null)
        {
            return;
        }

        try
        {
            // The trip is committed and published, so an aborted request mustn't leave its flag file unwritten.
            await CircuitFlagFile.SyncAsync(
                _store,
                trip.RepositoryRoot,
                CancellationToken.None);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            _sink.Warn(
                $"could not update {CircuitFlagFile.GetPath(trip.RepositoryRoot)}: {ex.Message}");
        }
    }

    private sealed record BudgetKey(string Repository, string Branch);
}
