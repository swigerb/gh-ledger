using CopilotLedger.Core.Pricing;

namespace CopilotLedger.Core.Audit;

// premium_model_trivial_turn: many short, tool-free GitHub AI credits requests to a model in the top quartile of output
// rates, per agent, model, and local day. Routed requests are left out; others are candidates: a short answer can need a strong one.
internal sealed class PremiumModelTrivialTurnRule : IAuditRule
{
    private const decimal RoutedChargeRatio = 0.95m;   // charged at least 5% below list: Auto (10%) or Fusion (30%) routed it

    public string Id => AuditRuleIds.PremiumModelTrivialTurn;

    public IEnumerable<AuditFinding> Evaluate(AuditInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Pricing is not IAuditPricing pricing)
        {
            yield break;
        }

        AuditThresholds thresholds = input.Thresholds;
        List<Candidate> candidates = [];
        foreach (AuditSpan span in AuditMath.ByStart(input.Spans))
        {
            if (span.Channel != BillingChannel.GitHubAiCredits ||
                span.OutputTokens > thresholds.TrivialOutputTokens ||
                span.ToolCallCount != 0)
            {
                continue;
            }

            if (span.ListCostUsd is decimal list && list > 0m && span.CostUsd <= list * RoutedChargeRatio)
            {
                continue;
            }

            RateTier? tier = pricing.FindBilledTier(BillingChannel.GitHubAiCredits, span.Model, span.RequestModel, span.StartTime, 0);
            decimal? topQuartile = TopQuartile(pricing.GitHubOutputRates(span.StartTime));
            if (tier is null || topQuartile is null || tier.OutputPer1M < topQuartile.Value)
            {
                continue;
            }

            DateOnly day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(span.StartTime, input.TimeZone).DateTime);
            candidates.Add(new Candidate(span, tier.OutputPer1M, topQuartile.Value, day));
        }

        foreach (IGrouping<(string, string, int?, string, string, DateOnly), Candidate> group in candidates
            .GroupBy(candidate => (candidate.Span.Repository, candidate.Span.Branch, candidate.Span.PullRequestNumber,
                AuditMath.Agent(candidate.Span.AgentName), candidate.Span.Model, candidate.Day))
            .OrderBy(group => group.Key.Item1, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Item2, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Item3 ?? 0)
            .ThenBy(group => group.Key.Item4, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Item5, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Item6))
        {
            List<Candidate> requests = [.. group];
            if (requests.Count < thresholds.TrivialRequestsPerDay)
            {
                continue;
            }

            AuditSpan first = requests[0].Span;
            List<AuditSpan> spans = [.. requests.Select(candidate => candidate.Span)];
            yield return new AuditFinding
            {
                RuleId = Id,
                Severity = AuditMath.Severity(requests.Count, thresholds.TrivialRequestsPerDay),
                Scope = new AuditScope
                {
                    Repository = first.Repository,
                    Branch = first.Branch,
                    PullRequestNumber = first.PullRequestNumber,
                    Agent = AuditMath.Agent(first.AgentName),
                    Model = first.Model,
                },
                WindowStart = first.StartTime,
                WindowEnd = AuditMath.LatestEnd(spans),
                Metrics =
                [
                    new AuditMetric("requests", requests.Count, AuditUnits.Requests),
                    new AuditMetric("outputTokens", spans.Sum(span => span.OutputTokens), AuditUnits.Tokens),
                    new AuditMetric("modelOutputRatePer1M", requests[0].OutputRate, AuditUnits.UsdPer1MTokens),
                    new AuditMetric("topQuartileOutputRatePer1M", requests[0].TopQuartile, AuditUnits.UsdPer1MTokens),
                ],
                EstimatedImpactUsd = Impact(spans, pricing, input.EconomyModel),
                RecommendationKey = Id,
            };
        }
    }

    // The nearest-rank 75th percentile of rates (the ceil(0.75 n)-th smallest), or null with fewer than 4 rates.
    private static decimal? TopQuartile(IReadOnlyList<decimal> rates)
    {
        if (rates.Count < 4)
        {
            return null;
        }

        decimal[] sorted = [.. rates.Order()];
        return sorted[(3 * sorted.Length + 3) / 4 - 1];
    }

    // What the requests cost above the economy model's price for the same tokens, at the active rates. Null without an
    // economy model, when a request has no known billed price (BilledKnown), as a recorded sum would be, or when either
    // model has no tier for a request.
    private static decimal? Impact(List<AuditSpan> spans, IAuditPricing pricing, string? economyModel)
    {
        if (economyModel is null)
        {
            return null;
        }

        decimal sum = 0m;
        foreach (AuditSpan span in spans)
        {
            if (!span.BilledKnown)
            {
                return null;
            }

            RateTier? actual = pricing.FindBilledTier(
                BillingChannel.GitHubAiCredits, span.Model, span.RequestModel, span.StartTime, span.InputTokens);
            RateTier? economy = pricing.FindBilledTier(
                BillingChannel.GitHubAiCredits, economyModel, null, span.StartTime, span.InputTokens);
            if (actual is null || economy is null)
            {
                return null;
            }

            sum += Math.Max(0m, AuditMath.Cost(actual, span) - AuditMath.Cost(economy, span));
        }

        return sum;
    }

    private sealed record Candidate(AuditSpan Span, decimal OutputRate, decimal TopQuartile, DateOnly Day);
}
