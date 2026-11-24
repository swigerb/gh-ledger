using CopilotLedger.Core.Pricing;

namespace CopilotLedger.Core.Audit;

// The rates an audit reprices with: the billed sections of the active rate card.
public interface IAuditPricing
{
    string RatesVersion { get; }

    // localOffload.baselineModel as its canonical githubAiCredits ID: the default economy model.
    string BaselineModel { get; }

    // The canonical ID of the githubAiCredits model that model names (an ID or alias, normalized as Calculate normalizes
    // models), or null.
    string? FindGitHubModel(string model);

    // The tier that bills a request of channel (GitHubAiCredits or FoundryByok) as Calculate selects it: the section's model
    // for model, else for requestModel; the window covering instant; the first tier whose maxInputTokens holds inputTokens.
    // Null for another channel, a model the section doesn't have, or an instant no window covers.
    RateTier? FindBilledTier(BillingChannel channel, string? model, string? requestModel, DateTimeOffset instant, long inputTokens);

    // The first tier's outputPer1M of each githubAiCredits model with a window covering instant, in the card's order.
    IReadOnlyList<decimal> GitHubOutputRates(DateTimeOffset instant);
}
