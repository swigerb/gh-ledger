using CopilotLedger.Cli.Rates;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Receipts;

internal sealed record PullRequestReceipt
{
    public required string Repository { get; init; }                       // GitWorkspaceContext.RepositorySlug
    public required string Branch { get; init; }                           // GitWorkspaceContext.BranchName
    public required string Commit { get; init; }                           // GitWorkspaceContext.HeadCommitSha
    public required DateTimeOffset GeneratedAt { get; init; }              // context.TimeProvider.GetUtcNow()
    public required LedgerRollupRow Totals { get; init; }                  // the branch's Branch rollup row; all zero without spans
    public required IReadOnlyList<LedgerRollupRow> Models { get; init; }   // ChannelModel rollup rows, in GetRollupAsync order
    public required long Prompts { get; init; }                            // GetPromptCountAsync
    public required decimal MaxPromptCredits { get; init; }                // costliest prompt chain: (AiCreditsUsd + FoundryByokUsd) / 0.01m; 0 without spans
    public required long BreakerTrips { get; init; }                       // CountBreakerTripsAsync
    public WhatIfPricing? Pricing { get; init; }                           // --pricing; null for the recorded API-equivalent results
}
