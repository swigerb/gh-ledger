using System.Globalization;
using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;

namespace CopilotLedger.Otlp.Breaker;

public static class BreakerMessages
{
    public const string TripTitle = "gh-ledger circuit breaker tripped";
    public const string BudgetTitle = "gh-ledger branch budget exceeded";
    public const string DefaultCommand = "gh-ledger";   // how alerts name gh-ledger unless the caller knows better; the daemon passes GhExtension.CommandName

    public static string Credits(decimal aiCredits) =>                          // decimal.Round(aiCredits, 2, MidpointRounding.AwayFromZero).ToString("N2", CultureInfo.InvariantCulture)
        decimal.Round(aiCredits, 2, MidpointRounding.AwayFromZero)
            .ToString("N2", CultureInfo.InvariantCulture);

    public static string Minutes(int minutes) =>                                // "1 minute", otherwise "<n> minutes" (invariant culture)
        minutes == 1
            ? "1 minute"
            : $"{minutes.ToString(CultureInfo.InvariantCulture)} minutes";

    public static string VelocityDetail(decimal windowCostUsd, int windowMinutes) => // "<Credits(windowCostUsd / 0.01)> AI credits in <Minutes(windowMinutes)>"
        $"{Credits(windowCostUsd / AiCreditMath.UsdPerAiCredit)} AI credits in {Minutes(windowMinutes)}";

    public static string ChurnDetail(int failureCount) =>                       // "<n> failed tool calls in a row"
        $"{failureCount.ToString(CultureInfo.InvariantCulture)} failed tool calls in a row";

    public static string RulesSummary(BreakerSettings settings) =>              // "more than <Credits(VelocityCredits)> AI credits in <Minutes(VelocityWindowMinutes)> on one prompt; <ChurnFailures> failed tool calls in a row on one prompt"
        $"more than {Credits(settings.VelocityCredits)} AI credits in {Minutes(settings.VelocityWindowMinutes)} on one prompt; {ChurnDetail(settings.ChurnFailures)} on one prompt";

    public static BreakerAlert ForTrip(
        long tripId,
        string repository,
        string branch,
        string detail,
        bool showNotification,
        string command = DefaultCommand) => new()
    {
        Kind = BreakerAlertKind.Trip,
        TripId = tripId,
        Repository = repository,
        Branch = branch,
        Title = TripTitle,
        Body = $"{repository} @ {branch}: {detail} on one prompt. Run: {command} breaker status",
        ShowNotification = showNotification,
    };

    public static BreakerAlert ForBudget(
        BranchBudgetRow budget,
        bool showNotification,
        string command = DefaultCommand) => new()
    {
        Kind = BreakerAlertKind.Budget,
        TripId = null,
        Repository = budget.Repository,
        Branch = budget.Branch,
        Title = BudgetTitle,
        Body = $"{budget.Repository} @ {budget.Branch}: {Credits(budget.SpentAiCredits)} of {Credits(budget.BudgetAiCredits)} AI credits spent. Run: {command} budget list",
        ShowNotification = showNotification,
    };
}
