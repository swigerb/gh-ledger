namespace CopilotLedger.Storage;

// GetContextBloatAsync's rankings. Each list holds at most the limit given, and ties go to the lower trace ID.
public sealed record ContextBloatReport
{
    public required IReadOnlyList<BloatChainRow> ByUncachedInput { get; init; }            // chains whose spans all reported cache usage, by ReportedUncachedInputTokens
    public required IReadOnlyList<BloatChainRow> ByEstimatedUncachedInput { get; init; }   // the other chains, by EstimatedUncachedInputTokens
    public required IReadOnlyList<InputGrowthRow> ByInputGrowth { get; init; }             // each chain's largest sequence growth above 0
    public required long SpansWithoutAgent { get; init; }                                  // spans left out of growth: no known invoke_agent ancestor
}
